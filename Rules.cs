using System.Text.Json;
using System.Text.Json.Serialization;

namespace EberoNest;

internal sealed class RuleCategory
{
    public string Name { get; set; } = "";
    public string FolderName { get; set; } = "";
    public List<string> Extensions { get; set; } = [];
    public bool IsBuiltIn { get; set; }
}

internal sealed class RulesDocument
{
    public int Version { get; set; } = 1;
    public List<RuleCategory> Categories { get; set; } = [];
}

internal static class RulesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static string DefaultPath => GetCanonicalPath(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    internal static IReadOnlyList<string> LegacyPaths => GetLegacyPaths(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    internal static string GetCanonicalPath(string localAppDataPath) =>
        Path.Combine(localAppDataPath, "EberoNest", "rules.json");

    internal static IReadOnlyList<string> GetLegacyPaths(string localAppDataPath) =>
    [
        Path.Combine(localAppDataPath, "Ebero DropNest", "rules.json"),
        Path.Combine(localAppDataPath, "DownloadsOrganizer", "rules.json")
    ];

    internal static string ResolvePath(string localAppDataPath) =>
        MigrateLegacyRules(GetCanonicalPath(localAppDataPath), GetLegacyPaths(localAppDataPath));

    internal static string MigrateLegacyRules(string destinationPath, string legacyPath) =>
        MigrateLegacyRules(destinationPath, [legacyPath]);

    internal static string MigrateLegacyRules(string destinationPath, IEnumerable<string> legacyPaths)
    {
        if (File.Exists(destinationPath)) return destinationPath;

        string? directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (directory is null) return destinationPath;
        foreach (string legacyPath in legacyPaths)
        {
            if (!File.Exists(legacyPath)) continue;

            string temporaryPath = destinationPath + ".migration-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(directory);
                File.Copy(legacyPath, temporaryPath, overwrite: false);
                try { File.Move(temporaryPath, destinationPath, overwrite: false); }
                catch (IOException) when (File.Exists(destinationPath)) { }
                return File.Exists(destinationPath) ? destinationPath : legacyPath;
            }
            catch (IOException) { return legacyPath; }
            catch (UnauthorizedAccessException) { return legacyPath; }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        return destinationPath;
    }

    internal static List<RuleCategory> DefaultRules() =>
    [
        BuiltIn("Documents", ".pdf .doc .docx .xls .xlsx .ppt .pptx .txt .rtf .odt .csv"),
        BuiltIn("Images", ".jpg .jpeg .png .gif .webp .bmp .svg .ico .heic .heif"),
        BuiltIn("Archives", ".zip .rar .7z .tar .gz .bz2 .xz"),
        BuiltIn("Installers", ".exe .msi .msix .appx"),
        BuiltIn("Music", ".mp3 .wav .flac .aac .ogg .wma .m4a"),
        BuiltIn("Videos", ".mp4 .avi .mkv .mov .wmv .flv .webm"),
        BuiltIn("3D Printing", ".stl .3mf .obj"),
        BuiltIn("CAD", ".dxf .dwg")
    ];

    internal static List<RuleCategory> Load(string? path = null)
    {
        path ??= ResolvePath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        if (!File.Exists(path))
        {
            var defaults = DefaultRules();
            Save(defaults, path);
            return defaults;
        }

        try
        {
            var doc = JsonSerializer.Deserialize<RulesDocument>(File.ReadAllText(path), JsonOptions)
                      ?? throw new InvalidDataException("La configuración está vacía.");
            if (doc.Version != 1)
                throw new InvalidDataException("Versión de configuración no compatible.");
            Validate(doc.Categories);
            return Clone(doc.Categories);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException)
        {
            // Keep malformed user data untouched; defaults are used in memory until the user saves.
            return DefaultRules();
        }
    }

    internal static void Save(IEnumerable<RuleCategory> categories, string? path = null)
    {
        path ??= ResolvePath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var normalized = NormalizeAndValidate(categories);
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("La ruta de reglas no contiene una carpeta.");
        Directory.CreateDirectory(directory);

        if (File.Exists(fullPath))
        {
            byte[] existingBytes = File.ReadAllBytes(fullPath);
            string existingJson = System.Text.Encoding.UTF8.GetString(existingBytes);
            if (!IsValidDocument(existingJson))
            {
                string backupPath = fullPath + ".invalid-" +
                    DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture) +
                    "-" + Guid.NewGuid().ToString("N") + ".bak";
                using var backup = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                backup.Write(existingBytes);
                backup.Flush(flushToDisk: true);
            }
        }

        string temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var temporary = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(temporary, new RulesDocument { Categories = normalized }, JsonOptions);
                temporary.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool IsValidDocument(string json)
    {
        try
        {
            RulesDocument? document = JsonSerializer.Deserialize<RulesDocument>(json, JsonOptions);
            if (document is null || document.Version != 1 || document.Categories is null) return false;
            Validate(document.Categories);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException)
        {
            return false;
        }
    }

    internal static List<RuleCategory> NormalizeAndValidate(IEnumerable<RuleCategory> categories)
    {
        var result = categories.Select(category => new RuleCategory
        {
            Name = (category.Name ?? "").Trim(),
            FolderName = (category.FolderName ?? "").Trim(),
            IsBuiltIn = category.IsBuiltIn,
            Extensions = (category.Extensions ?? []).Select(NormalizeExtension)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList()
        }).ToList();
        Validate(result);
        return result;
    }

    private static void Validate(IReadOnlyCollection<RuleCategory> categories)
    {
        if (categories is null || categories.Count == 0)
            throw new InvalidDataException("Debe existir al menos una categoría.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in categories)
        {
            if (category is null || category.Extensions is null)
                throw new InvalidDataException("La configuración contiene una categoría incompleta.");
            if (string.IsNullOrWhiteSpace(category.Name) || category.Name.Length > 60)
                throw new InvalidDataException("Cada categoría requiere un nombre de hasta 60 caracteres.");
            if (string.IsNullOrWhiteSpace(category.FolderName) || category.FolderName.Length > 80 ||
                category.FolderName is "." or ".." || category.FolderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                category.FolderName.Contains('/') || category.FolderName.Contains('\\') ||
                category.FolderName.EndsWith('.') || category.FolderName.EndsWith(' '))
                throw new InvalidDataException($"El destino de «{category.Name}» debe ser una carpeta dentro de Descargas.");
            if (!names.Add(category.Name) || !folders.Add(category.FolderName))
                throw new InvalidDataException("Los nombres y destinos de las categorías deben ser únicos.");
            if (category.Extensions.Count == 0)
                throw new InvalidDataException($"«{category.Name}» debe tener al menos una extensión.");
            foreach (string extension in category.Extensions)
            {
                if (extension is null || !IsValidExtension(extension) || !extensions.Add(extension))
                    throw new InvalidDataException($"La extensión «{extension}» no es válida o ya pertenece a otra categoría.");
            }
        }
    }

    internal static string NormalizeExtension(string extension)
    {
        string normalized = extension.Trim();
        if (!normalized.StartsWith('.')) normalized = "." + normalized;
        return normalized.ToLowerInvariant();
    }

    private static bool IsValidExtension(string extension) =>
        extension.Length is >= 2 and <= 16 && extension[0] == '.' &&
        extension.AsSpan(1).IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyz0123456789".AsSpan()) < 0;

    private static RuleCategory BuiltIn(string name, string extensions) => new()
    {
        Name = name, FolderName = name, IsBuiltIn = true,
        Extensions = extensions.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
    };

    internal static List<RuleCategory> Clone(IEnumerable<RuleCategory> categories) => categories.Select(c => new RuleCategory
    {
        Name = c.Name, FolderName = c.FolderName, IsBuiltIn = c.IsBuiltIn, Extensions = [.. c.Extensions]
    }).ToList();
}
