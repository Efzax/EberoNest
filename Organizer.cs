using System.Collections.Concurrent;
using System.Threading.Channels;

namespace EberoNest;

internal sealed class Organizer : IAsyncDisposable
{
    private static readonly HashSet<string> TemporaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".crdownload", ".tmp", ".part", ".download"
    };

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(350), TimeSpan.FromMilliseconds(700),
        TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(6),
        TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)
    ];

    private readonly string root;
    private readonly FileSystemWatcher watcher;
    private readonly Channel<QueueItem> queue = Channel.CreateUnbounded<QueueItem>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<string, byte> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource stop = new();
    private RuleCategory[] rules;
    private Task? worker;

    internal Organizer(string rootPath, IEnumerable<RuleCategory> initialRules)
    {
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        rules = RulesStore.NormalizeAndValidate(initialRules).ToArray();
        watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                           NotifyFilters.Size | NotifyFilters.LastWrite,
            InternalBufferSize = 16 * 1024,
            EnableRaisingEvents = false
        };
        watcher.Created += (_, e) => QueueIfRootFile(e.FullPath);
        watcher.Changed += (_, e) => QueueIfRootFile(e.FullPath);
        watcher.Renamed += (_, e) => QueueIfRootFile(e.FullPath);
    }

    internal IReadOnlyList<RuleCategory> Rules => RulesStore.Clone(Volatile.Read(ref rules));

    internal void UpdateRules(IEnumerable<RuleCategory> updated)
    {
        RuleCategory[] safe = RulesStore.NormalizeAndValidate(updated).ToArray();
        Volatile.Write(ref rules, safe);
    }

    internal void Start()
    {
        worker = ProcessQueueAsync(stop.Token);
        watcher.EnableRaisingEvents = true;
    }

    internal IReadOnlyDictionary<string, int> PreviewExistingFiles()
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            if (Directory.Exists(path) || IsReparsePoint(path)) continue;
            RuleCategory? rule = FindRule(Path.GetExtension(path));
            if (rule is null) continue;
            counts[rule.FolderName] = counts.GetValueOrDefault(rule.FolderName) + 1;
        }
        return counts;
    }

    internal async Task<int> OrganizeExistingFilesAsync(CancellationToken cancellationToken = default)
    {
        string[] files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .Where(path => !Directory.Exists(path) && !IsReparsePoint(path) && FindRule(Path.GetExtension(path)) is not null)
            .ToArray();
        int moved = 0;
        foreach (string path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (pending.TryAdd(path, 0))
            {
                if (!queue.Writer.TryWrite(new QueueItem(path, completion)))
                {
                    pending.TryRemove(path, out _);
                    continue;
                }
                if (await completion.Task.WaitAsync(cancellationToken)) moved++;
            }
            else
            {
                // A watcher event already owns this path. Let that same queue finish it.
                for (int wait = 0; wait < 600 && File.Exists(path) && pending.ContainsKey(path); wait++)
                    await Task.Delay(100, cancellationToken);
                if (!File.Exists(path)) moved++;
            }
        }
        return moved;
    }

    private void QueueIfRootFile(string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            if (!string.Equals(Path.GetDirectoryName(fullPath), root, StringComparison.OrdinalIgnoreCase) ||
                Directory.Exists(fullPath) || IsReparsePoint(fullPath) || FindRule(Path.GetExtension(fullPath)) is null)
                return;

            if (pending.TryAdd(fullPath, 0) && !queue.Writer.TryWrite(new QueueItem(fullPath, null)))
                pending.TryRemove(fullPath, out _);
        }
        catch (ArgumentException) { }
        catch (PathTooLongException) { }
        catch (IOException) { }
    }

    private RuleCategory? FindRule(string extension)
    {
        if (TemporaryExtensions.Contains(extension)) return null;
        foreach (RuleCategory rule in Volatile.Read(ref rules))
            if (rule.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) return rule;
        return null;
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (QueueItem item in queue.Reader.ReadAllAsync(cancellationToken))
            {
                bool moved = false;
                try { moved = await ProcessFileWithRetriesAsync(item.Path, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                finally
                {
                    pending.TryRemove(item.Path, out _);
                    item.Completion?.TrySetResult(moved);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            while (queue.Reader.TryRead(out QueueItem? item))
            {
                pending.TryRemove(item.Path, out _);
                item.Completion?.TrySetResult(false);
            }
        }
    }

    private async Task<bool> ProcessFileWithRetriesAsync(string source, CancellationToken cancellationToken)
    {
        RuleCategory? category = FindRule(Path.GetExtension(source));
        if (category is null || !IsRootFile(source) || IsReparsePoint(source)) return false;

        for (int attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(RetryDelays[attempt - 1], cancellationToken);

            if (IsReparsePoint(source) || !await IsStableAndAvailableAsync(source, cancellationToken))
                continue;

            try
            {
                return MoveWithoutOverwrite(source, Path.Combine(root, category.FolderName));
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { return false; }
        }
        return false;
    }

    private bool IsRootFile(string path) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), root, StringComparison.OrdinalIgnoreCase) &&
        !Directory.Exists(path);

    private static async Task<bool> IsStableAndAvailableAsync(string path, CancellationToken cancellationToken)
    {
        long? previousLength = null;
        DateTime? previousWriteTime = null;

        for (int sample = 0; sample < 4; sample++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) return false;
                long length = info.Length;
                DateTime writeTime = info.LastWriteTimeUtc;
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                if (sample > 0 && (length != previousLength || writeTime != previousWriteTime)) return false;
                previousLength = length;
                previousWriteTime = writeTime;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            if (sample < 3) await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken);
        }
        return true;
    }

    private static bool MoveWithoutOverwrite(string source, string destinationDirectory)
    {
        if (IsReparsePoint(source) || IsReparsePoint(destinationDirectory)) return false;
        Directory.CreateDirectory(destinationDirectory);
        if (IsReparsePoint(source) || IsReparsePoint(destinationDirectory)) return false;

        string destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
        if (!File.Exists(destination))
        {
            if (IsReparsePoint(source) || IsReparsePoint(destinationDirectory)) return false;
            File.Move(source, destination);
            return true;
        }
        string name = Path.GetFileNameWithoutExtension(source);
        string extension = Path.GetExtension(source);
        for (int suffix = 1; ; suffix++)
        {
            destination = Path.Combine(destinationDirectory, $"{name} ({suffix}){extension}");
            if (!File.Exists(destination))
            {
                if (IsReparsePoint(source) || IsReparsePoint(destinationDirectory)) return false;
                File.Move(source, destination);
                return true;
            }
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
    public async ValueTask DisposeAsync()
    {
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        queue.Writer.TryComplete();
        stop.Cancel();
        if (worker is not null)
        {
            try { await worker; }
            catch (OperationCanceledException) { }
        }
        stop.Dispose();
    }

    private sealed record QueueItem(string Path, TaskCompletionSource<bool>? Completion);
}
