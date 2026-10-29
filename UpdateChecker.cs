using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EberoNest;

internal readonly record struct SemanticVersion(int Major, int Minor, int Patch) : IComparable<SemanticVersion>
{
    internal static bool TryParseTag(string? tag, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrEmpty(tag) || tag[0] != 'v') return false;
        string[] parts = tag.AsSpan(1).ToString().Split('.');
        if (parts.Length != 3) return false;

        int[] values = new int[3];
        for (int index = 0; index < parts.Length; index++)
        {
            string part = parts[index];
            if (part.Length == 0 || (part.Length > 1 && part[0] == '0') ||
                !int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out values[index]))
                return false;
        }

        version = new SemanticVersion(values[0], values[1], values[2]);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        int major = Major.CompareTo(other.Major);
        if (major != 0) return major;
        int minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    internal string ToTag() => $"v{Major}.{Minor}.{Patch}";
}

internal sealed record OfficialRelease(SemanticVersion Version, string Tag, Uri Url);

internal enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed
}

internal sealed record UpdateCheckResult(UpdateCheckStatus Status, OfficialRelease? LatestRelease);

internal interface ILatestReleaseClient
{
    Task<OfficialRelease> GetLatestStableReleaseAsync(CancellationToken cancellationToken = default);
}

internal interface IAutomaticUpdateCheckState
{
    DateOnly? ReadLastSuccessfulCheckDate();
    void SaveLastSuccessfulCheckDate(DateOnly date);
}

internal interface IUpdateNotificationService
{
    event Action<UpdateCheckResult>? CheckCompleted;
    UpdateCheckResult? LastSuccessfulResult { get; }
    Task CheckAutomaticallyAsync();
    Task<UpdateCheckResult> CheckManuallyAsync();
}

internal sealed class GitHubLatestReleaseClient : ILatestReleaseClient, IDisposable
{
    internal const string Endpoint = "https://api.github.com/repos/Efzax/EberoNest/releases/latest";
    internal const int MaximumResponseBytes = 64 * 1024;

    private readonly HttpClient httpClient;
    private readonly bool ownsHttpClient;
    private readonly SemanticVersion applicationVersion;
    private readonly TimeSpan requestTimeout;

    internal GitHubLatestReleaseClient(SemanticVersion applicationVersion, HttpClient? httpClient = null, TimeSpan? requestTimeout = null)
    {
        this.applicationVersion = applicationVersion;
        this.httpClient = httpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        ownsHttpClient = httpClient is null;
        this.requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(7);
    }

    public async Task<OfficialRelease> GetLatestStableReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("EberoNest", applicationVersion.ToTag()[1..]));

        using HttpResponseMessage response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("GitHub returned a non-success status for the releases request.", null, response.StatusCode);
        if (response.Content.Headers.ContentLength is long contentLength && contentLength > MaximumResponseBytes)
            throw new InvalidDataException("GitHub release response exceeded the allowed size.");

        await using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        while (true)
        {
            int allowedRead = Math.Min(chunk.Length, MaximumResponseBytes + 1 - (int)buffer.Length);
            int read = await stream.ReadAsync(chunk.AsMemory(0, allowedRead), timeout.Token).ConfigureAwait(false);
            if (read == 0) break;
            if (buffer.Length + read > MaximumResponseBytes)
                throw new InvalidDataException("GitHub release response exceeded the allowed size.");
            buffer.Write(chunk, 0, read);
        }

        string json = new UTF8Encoding(false, true).GetString(buffer.ToArray());
        return ParseLatestStableRelease(json)
            ?? throw new InvalidDataException("GitHub did not return a stable release.");
    }

    internal static OfficialRelease? ParseLatestStableRelease(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 12 });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid GitHub release response.");

        if (!root.TryGetProperty("draft", out JsonElement draftElement) ||
            draftElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !root.TryGetProperty("prerelease", out JsonElement prereleaseElement) ||
            prereleaseElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("GitHub release response is missing stable-release flags.");

        bool draft = draftElement.ValueKind == JsonValueKind.True;
        bool prerelease = prereleaseElement.ValueKind == JsonValueKind.True;
        if (draft || prerelease) return null;

        if (!root.TryGetProperty("tag_name", out JsonElement tagElement) || tagElement.ValueKind != JsonValueKind.String ||
            !SemanticVersion.TryParseTag(tagElement.GetString(), out SemanticVersion version))
            throw new InvalidDataException("GitHub release tag is not a stable vX.Y.Z version.");

        if (!root.TryGetProperty("html_url", out JsonElement urlElement) || urlElement.ValueKind != JsonValueKind.String ||
            !OfficialReleaseUrl.TryValidate(urlElement.GetString(), version, out Uri? url) || url is null)
            throw new InvalidDataException("GitHub release URL does not match the official repository and tag.");

        return new OfficialRelease(version, version.ToTag(), url);
    }

    public void Dispose()
    {
        if (ownsHttpClient) httpClient.Dispose();
    }
}

internal static class OfficialReleaseUrl
{
    internal static Uri Create(SemanticVersion version) =>
        new($"https://github.com/Efzax/EberoNest/releases/tag/{version.ToTag()}", UriKind.Absolute);

    internal static bool TryValidate(string? value, SemanticVersion expectedVersion, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? candidate) ||
            !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(candidate.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !candidate.IsDefaultPort || !string.IsNullOrEmpty(candidate.UserInfo) ||
            !string.IsNullOrEmpty(candidate.Query) || !string.IsNullOrEmpty(candidate.Fragment))
            return false;

        string expectedPath = $"/Efzax/EberoNest/releases/tag/{expectedVersion.ToTag()}";
        if (!string.Equals(candidate.AbsolutePath, expectedPath, StringComparison.OrdinalIgnoreCase))
            return false;

        uri = candidate;
        return true;
    }

    internal static bool TryValidate(Uri? uri, SemanticVersion expectedVersion) =>
        uri is not null && TryValidate(uri.AbsoluteUri, expectedVersion, out _);
}

internal sealed class FileAutomaticUpdateCheckState : IAutomaticUpdateCheckState
{
    private readonly string statePath;

    internal FileAutomaticUpdateCheckState(string? statePath = null)
    {
        this.statePath = statePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EberoNest", "last-update-check.txt");
    }

    public DateOnly? ReadLastSuccessfulCheckDate()
    {
        try
        {
            if (!File.Exists(statePath)) return null;
            string content = File.ReadAllText(statePath, Encoding.ASCII).Trim();
            return DateOnly.TryParseExact(content, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly date) ? date : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public void SaveLastSuccessfulCheckDate(DateOnly date)
    {
        string? directory = Path.GetDirectoryName(statePath);
        if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Update state path has no parent folder.");
        Directory.CreateDirectory(directory);
        string temporaryPath = statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Encoding.ASCII);
            File.Move(temporaryPath, statePath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

internal sealed class UpdateNotificationService : IUpdateNotificationService, IDisposable
{
    private readonly SemanticVersion currentVersion;
    private readonly ILatestReleaseClient releaseClient;
    private readonly IAutomaticUpdateCheckState state;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object resultLock = new();
    private UpdateCheckResult? lastSuccessfulResult;

    internal UpdateNotificationService(SemanticVersion currentVersion, ILatestReleaseClient releaseClient, IAutomaticUpdateCheckState state)
    {
        this.currentVersion = currentVersion;
        this.releaseClient = releaseClient;
        this.state = state;
    }

    public event Action<UpdateCheckResult>? CheckCompleted;

    public UpdateCheckResult? LastSuccessfulResult
    {
        get { lock (resultLock) return lastSuccessfulResult; }
    }

    public async Task CheckAutomaticallyAsync()
    {
        try
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);
            if (state.ReadLastSuccessfulCheckDate() == today) return;
            UpdateCheckResult result = await QueryAsync(lifetime.Token).ConfigureAwait(false);
            try { state.SaveLastSuccessfulCheckDate(today); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) { }
            Publish(result);
        }
        catch (Exception)
        {
            // Automatic update checks are isolated from application startup and organization.
        }
    }

    public async Task<UpdateCheckResult> CheckManuallyAsync()
    {
        try
        {
            UpdateCheckResult result = await QueryAsync(lifetime.Token).ConfigureAwait(false);
            Publish(result);
            return result;
        }
        catch (Exception)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed, null);
        }
    }

    private async Task<UpdateCheckResult> QueryAsync(CancellationToken cancellationToken)
    {
        OfficialRelease release = await releaseClient.GetLatestStableReleaseAsync(cancellationToken).ConfigureAwait(false);
        UpdateCheckStatus status = release.Version.CompareTo(currentVersion) > 0
            ? UpdateCheckStatus.UpdateAvailable
            : UpdateCheckStatus.UpToDate;
        return new UpdateCheckResult(status, release);
    }

    private void Publish(UpdateCheckResult result)
    {
        lock (resultLock) lastSuccessfulResult = result;
        Action<UpdateCheckResult>? handlers = CheckCompleted;
        if (handlers is null) return;
        foreach (Action<UpdateCheckResult> handler in handlers.GetInvocationList().Cast<Action<UpdateCheckResult>>())
        {
            try { handler(result); }
            catch (Exception) { }
        }
    }

    public void Dispose()
    {
        lifetime.Cancel();
        lifetime.Dispose();
        if (releaseClient is IDisposable disposable) disposable.Dispose();
    }
}

internal sealed class OfficialReleaseBrowser : IReleaseBrowser
{
    public bool TryOpen(OfficialRelease release)
    {
        if (!OfficialReleaseUrl.TryValidate(release.Url, release.Version)) return false;
        try
        {
            Process.Start(new ProcessStartInfo(release.Url.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
        {
            return false;
        }
    }
}

internal interface IReleaseBrowser
{
    bool TryOpen(OfficialRelease release);
}
