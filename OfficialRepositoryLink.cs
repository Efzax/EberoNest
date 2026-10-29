using System.Diagnostics;

namespace EberoNest;

internal static class OfficialRepositoryLink
{
    internal const string Url = "https://github.com/Efzax/EberoNest";

    internal static bool TryValidate(string? value, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? candidate) ||
            !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(candidate.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !candidate.IsDefaultPort || !string.IsNullOrEmpty(candidate.UserInfo) ||
            !string.IsNullOrEmpty(candidate.Query) || !string.IsNullOrEmpty(candidate.Fragment) ||
            !string.Equals(candidate.AbsolutePath.TrimEnd('/'), "/Efzax/EberoNest", StringComparison.OrdinalIgnoreCase))
            return false;
        uri = candidate;
        return true;
    }

    internal static bool TryOpen(string? value)
    {
        if (!TryValidate(value, out Uri? uri) || uri is null) return false;
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
        {
            return false;
        }
    }
}
