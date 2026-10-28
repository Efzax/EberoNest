using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace EberoNest;

internal sealed record StartupEntry(string TargetPath, string Arguments);

internal interface IStartupEntryStore
{
    bool Exists(string shortcutFileName);
    StartupEntry? Read(string shortcutFileName);
    void Write(string shortcutFileName, StartupEntry entry);
    void Delete(string shortcutFileName);
}

internal interface IStartupManager
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}

internal sealed class StartupManager : IStartupManager
{
    internal const string StartupArgument = "--background";
    internal const string ShortcutFileName = "EberoNest.lnk";
    internal const string LegacyDropNestShortcutFileName = "Ebero DropNest.lnk";
    internal const string LegacyDownloadsOrganizerShortcutFileName = "DownloadsOrganizer.lnk";

    private readonly IStartupEntryStore store;
    private readonly string currentExecutablePath;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> legacyTargets;

    internal StartupManager(IStartupEntryStore store, string currentExecutablePath, string? localAppDataPath = null)
    {
        this.store = store;
        this.currentExecutablePath = Path.GetFullPath(currentExecutablePath);
        string localAppData = localAppDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string currentDirectory = Path.GetDirectoryName(this.currentExecutablePath)
            ?? throw new ArgumentException("La ruta del ejecutable actual no contiene una carpeta.", nameof(currentExecutablePath));
        legacyTargets = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [LegacyDropNestShortcutFileName] =
            [
                Path.Combine(localAppData, "Programs", "Ebero DropNest", "EberoDropNest.exe"),
                Path.Combine(currentDirectory, "EberoDropNest.exe")
            ],
            [LegacyDownloadsOrganizerShortcutFileName] =
            [Path.Combine(localAppData, "Programs", "DownloadsOrganizer", "DownloadsOrganizer.exe")]
        };
    }

    // The mutex and activation event retain their historical names to interoperate with installed older builds.
    internal static string GetCurrentExecutablePath()
    {
        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                using System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
                executablePath = process.MainModule?.FileName;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
            {
                throw new InvalidOperationException("No se pudo determinar la ruta del ejecutable actual de EberoNest.", ex);
            }
        }

        if (string.IsNullOrWhiteSpace(executablePath))
            throw new InvalidOperationException("Windows no informó la ruta del ejecutable actual de EberoNest.");
        return Path.GetFullPath(executablePath);
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                if (!File.Exists(currentExecutablePath) || !store.Exists(ShortcutFileName)) return false;
                return IsValidEntry(store.Read(ShortcutFileName));
            }
            catch (Exception ex) when (IsStartupStorageError(ex))
            {
                return false;
            }
        }
    }

    public void Enable()
    {
        if (!File.Exists(currentExecutablePath))
            throw new FileNotFoundException("No se encontró el ejecutable actual de EberoNest.", currentExecutablePath);

        if (!IsEnabled)
        {
            if (store.Exists(ShortcutFileName))
            {
                StartupEntry? existing = store.Read(ShortcutFileName);
                if (!IsRecognizedOwnedEntry(existing))
                    throw new InvalidOperationException(
                        $"No se modificó «{ShortcutFileName}» porque la entrada existente no se puede identificar como EberoNest.");
            }

            store.Write(ShortcutFileName, new StartupEntry(currentExecutablePath, StartupArgument));
            if (!IsEnabled)
                throw new InvalidOperationException("Windows no confirmó el inicio automático de EberoNest.");
        }

        RemoveRecognizedLegacyEntries();
    }

    public void Disable()
    {
        if (!store.Exists(ShortcutFileName)) return;

        StartupEntry? existing = store.Read(ShortcutFileName);
        if (!IsValidEntry(existing) && !IsCurrentPayloadEntry(existing))
            throw new InvalidOperationException(
                $"No se eliminó «{ShortcutFileName}» porque no apunta a esta instancia de EberoNest con {StartupArgument}.");

        store.Delete(ShortcutFileName);
    }

    private void RemoveRecognizedLegacyEntries()
    {
        foreach (string shortcutName in legacyTargets.Keys)
        {
            if (!store.Exists(shortcutName)) continue;
            if (IsRecognizedLegacyEntry(shortcutName, store.Read(shortcutName)))
                store.Delete(shortcutName);
        }
    }

    private bool IsRecognizedLegacyEntry(string shortcutName, StartupEntry? entry)
    {
        if (entry is null || !legacyTargets.TryGetValue(shortcutName, out IReadOnlyList<string>? expectedTargets) ||
            !expectedTargets.Any(target => SamePath(entry.TargetPath, target)))
            return false;

        // The oldest DownloadsOrganizer installer created its canonical link without arguments.
        if (string.Equals(shortcutName, LegacyDownloadsOrganizerShortcutFileName, StringComparison.OrdinalIgnoreCase))
            return entry.Arguments.Length == 0 || HasBackgroundArgument(entry.Arguments);

        return HasBackgroundArgument(entry.Arguments);
    }

    private bool IsRecognizedOwnedEntry(StartupEntry? entry) =>
        entry is not null && HasBackgroundArgument(entry.Arguments) &&
        (SamePath(entry.TargetPath, currentExecutablePath) || IsCurrentPayloadCopy(entry.TargetPath) ||
         legacyTargets.Keys.Any(name => IsRecognizedLegacyEntry(name, entry)));

    private bool IsValidEntry(StartupEntry? entry) =>
        entry is not null && SamePath(entry.TargetPath, currentExecutablePath) && HasBackgroundArgument(entry.Arguments);

    private bool IsCurrentPayloadEntry(StartupEntry? entry) =>
        entry is not null && HasBackgroundArgument(entry.Arguments) && IsCurrentPayloadCopy(entry.TargetPath);

    private bool IsCurrentPayloadCopy(string targetPath)
    {
        if (!string.Equals(Path.GetFileName(targetPath), "EberoNest.exe", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(targetPath) || !File.Exists(currentExecutablePath))
            return false;

        try
        {
            using FileStream currentStream = File.OpenRead(currentExecutablePath);
            using FileStream targetStream = File.OpenRead(targetPath);
            byte[] currentHash = SHA256.HashData(currentStream);
            byte[] targetHash = SHA256.HashData(targetStream);
            return CryptographicOperations.FixedTimeEquals(currentHash, targetHash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                                   NotSupportedException or CryptographicException)
        {
            return false;
        }
    }

    private static bool HasBackgroundArgument(string arguments) =>
        string.Equals(arguments, StartupArgument, StringComparison.Ordinal);

    private static bool SamePath(string first, string second)
    {
        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsStartupStorageError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or COMException or
            InvalidOperationException or ArgumentException or TypeLoadException;
}

internal sealed class WindowsStartupEntryStore : IStartupEntryStore
{
    private readonly string startupDirectory;

    internal WindowsStartupEntryStore(string? startupDirectory = null)
    {
        this.startupDirectory = startupDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (string.IsNullOrWhiteSpace(this.startupDirectory))
            throw new InvalidOperationException("Windows no informó la carpeta Inicio del usuario.");
    }

    public bool Exists(string shortcutFileName) => File.Exists(GetShortcutPath(shortcutFileName));

    public StartupEntry? Read(string shortcutFileName)
    {
        string shortcutPath = GetShortcutPath(shortcutFileName);
        if (!File.Exists(shortcutPath)) return null;

        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = CreateShell();
            dynamic shellDispatch = shell;
            shortcut = shellDispatch.CreateShortcut(shortcutPath);
            dynamic link = shortcut;
            return new StartupEntry(Convert.ToString(link.TargetPath) ?? "", Convert.ToString(link.Arguments) ?? "");
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    public void Write(string shortcutFileName, StartupEntry entry)
    {
        string shortcutPath = GetShortcutPath(shortcutFileName);
        string? workingDirectory = Path.GetDirectoryName(entry.TargetPath);
        Directory.CreateDirectory(startupDirectory);
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = CreateShell();
            dynamic shellDispatch = shell;
            shortcut = shellDispatch.CreateShortcut(shortcutPath);
            dynamic link = shortcut;
            link.TargetPath = entry.TargetPath;
            link.WorkingDirectory = workingDirectory ?? "";
            link.Arguments = entry.Arguments;
            link.Description = "EberoNest permanece en la bandeja del sistema";
            link.Save();
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    public void Delete(string shortcutFileName)
    {
        string shortcutPath = GetShortcutPath(shortcutFileName);
        if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
    }

    private string GetShortcutPath(string shortcutFileName)
    {
        if (Path.GetFileName(shortcutFileName) != shortcutFileName || !shortcutFileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Nombre de acceso directo no válido.", nameof(shortcutFileName));
        return Path.Combine(startupDirectory, shortcutFileName);
    }

    private static object CreateShell()
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows Script Host no está disponible para configurar el inicio automático.");
        return Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("No se pudo iniciar Windows Script Host.");
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is null || !Marshal.IsComObject(value)) return;
        try { Marshal.FinalReleaseComObject(value); }
        catch (COMException) { }
    }
}

