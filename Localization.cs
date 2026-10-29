using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace EberoNest;

internal enum AppLanguage
{
    Spanish,
    English
}

internal interface ILanguagePreferenceStore
{
    string? Load();
    void Save(AppLanguage language);
}

internal sealed class FileLanguagePreferenceStore(string? path = null) : ILanguagePreferenceStore
{
    private readonly string filePath = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EberoNest", "language.txt");

    public string? Load()
    {
        try { return File.Exists(filePath) ? File.ReadAllText(filePath, Encoding.ASCII).Trim() : null; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    public void Save(AppLanguage language)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Language preference path has no parent folder.");
        Directory.CreateDirectory(directory);
        string temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, language == AppLanguage.Spanish ? "es" : "en", Encoding.ASCII);
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

internal static class AppLocalization
{
    private sealed record LocalizedValue(string Key, object?[] Arguments);
    private static readonly ConditionalWeakTable<Control, LocalizedValue> BoundControls = new();
    private static ILanguagePreferenceStore preferenceStore = new FileLanguagePreferenceStore();

    internal static AppLanguage Current { get; private set; } = AppLanguage.English;
    internal static event EventHandler? LanguageChanged;

    internal static void Initialize(ILanguagePreferenceStore? store = null, CultureInfo? windowsUiCulture = null)
    {
        preferenceStore = store ?? new FileLanguagePreferenceStore();
        string? stored = preferenceStore.Load();
        Current = stored?.Trim().ToLowerInvariant() switch
        {
            "es" => AppLanguage.Spanish,
            "en" => AppLanguage.English,
            _ => (windowsUiCulture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.Spanish
                : AppLanguage.English
        };
    }

    internal static void SetLanguage(AppLanguage language)
    {
        if (Current == language) return;
        preferenceStore.Save(language);
        Current = language;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    internal static string Get(string key, params object?[] arguments)
    {
        return Format(Current, key, arguments);
    }

    private static string Format(AppLanguage language, string key, object?[] arguments)
    {
        string template = language == AppLanguage.Spanish ? SpanishText(key) : EnglishText(key);
        return arguments.Length == 0 ? template : string.Format(CultureInfo.CurrentCulture, template, arguments);
    }

    internal static string Get(AppLanguage language, string key, params object?[] arguments)
        => Format(language, key, arguments);

    internal static void Bind(Control control, string key, params object?[] arguments)
    {
        BoundControls.Remove(control);
        BoundControls.Add(control, new LocalizedValue(key, arguments));
        control.Text = Get(key, arguments);
    }

    internal static void Apply(Control root)
    {
        if (BoundControls.TryGetValue(root, out LocalizedValue? value))
            root.Text = Get(value.Key, value.Arguments);
        foreach (Control child in root.Controls) Apply(child);
    }

    internal static string LocalizeRuleError(Exception exception)
    {
        if (exception is not InvalidDataException) return Get("rules.save.failure.body");
        string message = exception.Message;
        if (message.StartsWith("El destino de «", StringComparison.Ordinal))
        {
            int end = message.IndexOf('»');
            string name = end > 15 ? message[15..end] : string.Empty;
            return Get("rules.error.destination", name);
        }
        if (message.StartsWith("«", StringComparison.Ordinal) && message.Contains("debe tener al menos una extensión", StringComparison.Ordinal))
        {
            int end = message.IndexOf('»');
            string name = end > 1 ? message[1..end] : string.Empty;
            return Get("rules.error.extensions", name);
        }
        if (message.Contains("extensión «", StringComparison.Ordinal))
        {
            int start = message.IndexOf('«') + 1;
            int end = message.IndexOf('»', start);
            string extension = end > start ? message[start..end] : string.Empty;
            return Get("rules.error.extension", extension);
        }

        return message switch
        {
            "Debe existir al menos una categoría." => Get("rules.error.atLeastOne"),
            "La configuración contiene una categoría incompleta." => Get("rules.error.incomplete"),
            "Cada categoría requiere un nombre de hasta 60 caracteres." => Get("rules.error.name"),
            "Los nombres y destinos de las categorías deben ser únicos." => Get("rules.error.unique"),
            "La configuración está vacía." => Get("rules.error.empty"),
            _ => Get("rules.save.failure.body")
        };
    }

    internal static string TranslateKnownOperationalMessage(string message)
    {
        if (Current == AppLanguage.Spanish) return message;
        return message switch
        {
            "No se encontró el ejecutable actual de EberoNest." => Get("startup.error.missingExe"),
            "No se encontró la carpeta de Descargas." => Get("downloads.missing"),
            "No se pudo determinar la ruta del ejecutable actual de EberoNest." => Get("startup.error.path"),
            "Windows no informó la ruta del ejecutable actual de EberoNest." => Get("startup.error.path"),
            "Windows no confirmó el inicio automático de EberoNest." => Get("startup.error.confirm"),
            "Windows Script Host no está disponible para configurar el inicio automático." => Get("startup.error.host"),
            "No se pudo iniciar Windows Script Host." => Get("startup.error.host"),
            _ => message
        };
    }

    private static string SpanishText(string key) => key switch
    {
        "app.name" => "EberoNest",
        "app.tagline" => "Tus descargas, ordenadas automáticamente.",
        "settings.heading" => "Reglas de organización",
        "settings.description" => "Define cómo se organizan tus descargas automáticamente.",
        "rules.category" => "Categoría",
        "rules.folder" => "Carpeta de destino",
        "rules.extensions" => "Extensiones",
        "rules.count" => "{0} reglas",
        "button.newCategory" => "+  Nueva categoría",
        "button.edit" => "Editar",
        "button.delete" => "Eliminar",
        "button.organizeNow" => "▶  Organizar ahora",
        "startup.title" => "Iniciar EberoNest con Windows",
        "startup.description" => "Ejecutar silenciosamente al iniciar sesión.",
        "organizer.active" => "Organizador activo",
        "organizer.monitoring" => "Monitoreando la carpeta de Descargas.",
        "about.button" => "Acerca de · {0}",
        "about.title" => "Acerca de EberoNest",
        "about.language" => "Idioma: Español",
        "about.repository" => "Repositorio en GitHub",
        "about.license" => "Licencia MIT · Efzax",
        "language.saveFailure.title" => "Error de idioma",
        "language.saveFailure.body" => "No se pudo guardar la preferencia de idioma.",
        "button.close" => "Cerrar",
        "updates.search" => "Buscar actualizaciones",
        "updates.open" => "Ver actualización",
        "updates.current" => "EberoNest está actualizado.",
        "updates.available" => "Nueva versión disponible · {0}",
        "updates.failure" => "No se pudo comprobar si hay actualizaciones.",
        "updates.notice" => "Nueva versión disponible · {0}",
        "category.new" => "Nueva categoría",
        "category.edit" => "Editar categoría",
        "category.name" => "Nombre",
        "category.destination" => "Carpeta de destino",
        "category.extensions" => "Extensiones",
        "category.extensionHelp" => "Separa extensiones con coma o espacio, por ejemplo: stl, 3mf, obj",
        "button.save" => "Guardar",
        "button.cancel" => "Cancelar",
        "category.saveTitle" => "Revisa los datos",
        "rules.error.atLeastOne" => "Debe existir al menos una categoría.",
        "rules.error.incomplete" => "La configuración contiene una categoría incompleta.",
        "rules.error.name" => "Cada categoría requiere un nombre de hasta 60 caracteres.",
        "rules.error.destination" => "El destino de «{0}» debe ser una carpeta dentro de Descargas.",
        "rules.error.unique" => "Los nombres y destinos de las categorías deben ser únicos.",
        "rules.error.extensions" => "«{0}» debe tener al menos una extensión.",
        "rules.error.extension" => "La extensión «{0}» no es válida o ya pertenece a otra categoría.",
        "rules.error.empty" => "La configuración está vacía.",
        "rules.save.failure.body" => "No se pudieron guardar las reglas.",
        "rules.save.failure.title" => "No se pudieron guardar las reglas",
        "rules.saved" => "Reglas guardadas. Cambios aplicados.",
        "rules.delete.builtin" => "Las categorías incluidas no se pueden eliminar.",
        "rules.delete.confirm" => "¿Eliminar la categoría «{0}»? Los archivos que ya estén en esa carpeta no se moverán.",
        "rules.delete.title" => "Confirmar eliminación",
        "startup.error.title" => "Error de inicio automático",
        "startup.error.body" => "No se pudo cambiar el inicio automático de EberoNest.",
        "startup.error.missingExe" => "No se encontró el ejecutable actual de EberoNest.",
        "startup.error.path" => "No se pudo determinar la ruta del ejecutable actual de EberoNest.",
        "startup.error.confirm" => "Windows no confirmó el inicio automático de EberoNest.",
        "startup.error.host" => "Windows no pudo configurar el inicio automático.",
        "downloads.missing" => "No se encontró la carpeta de Descargas.",
        "organize.error.review" => "No se pudo revisar la carpeta de Descargas.",
        "organize.none" => "No hay archivos organizables en la raíz de Descargas.",
        "organize.confirm" => "Se encontraron {0} archivos en la raíz de Descargas:\n\n{1}\n\n¿Organizarlos ahora?",
        "organize.confirmTitle" => "Organizar archivos existentes",
        "organize.completed" => "Se organizaron {0} de {1} archivos.",
        "organize.error.interrupted" => "La organización se interrumpió.",
        "tray.open" => "Abrir configuración",
        "tray.organize" => "Organizar ahora",
        "tray.exit" => "Salir",
        _ => throw new KeyNotFoundException("Missing Spanish localization key: " + key)
    };

    private static string EnglishText(string key) => key switch
    {
        "app.name" => "EberoNest",
        "app.tagline" => "Your downloads, automatically sorted.",
        "settings.heading" => "Organization rules",
        "settings.description" => "Choose how your downloads are organized automatically.",
        "rules.category" => "Category",
        "rules.folder" => "Destination folder",
        "rules.extensions" => "Extensions",
        "rules.count" => "{0} rules",
        "button.newCategory" => "+  New category",
        "button.edit" => "Edit",
        "button.delete" => "Delete",
        "button.organizeNow" => "▶  Organize now",
        "startup.title" => "Start EberoNest with Windows",
        "startup.description" => "Run quietly when you sign in.",
        "organizer.active" => "Organizer active",
        "organizer.monitoring" => "Monitoring the Downloads folder.",
        "about.button" => "About · {0}",
        "about.title" => "About EberoNest",
        "about.language" => "Language: English",
        "about.repository" => "GitHub repository",
        "about.license" => "MIT License · Efzax",
        "language.saveFailure.title" => "Language error",
        "language.saveFailure.body" => "The language preference could not be saved.",
        "button.close" => "Close",
        "updates.search" => "Check for updates",
        "updates.open" => "View update",
        "updates.current" => "EberoNest is up to date.",
        "updates.available" => "Update available · {0}",
        "updates.failure" => "Could not check for updates.",
        "updates.notice" => "Update available · {0}",
        "category.new" => "New category",
        "category.edit" => "Edit category",
        "category.name" => "Name",
        "category.destination" => "Destination folder",
        "category.extensions" => "Extensions",
        "category.extensionHelp" => "Separate extensions with commas or spaces, for example: stl, 3mf, obj",
        "button.save" => "Save",
        "button.cancel" => "Cancel",
        "category.saveTitle" => "Check the details",
        "rules.error.atLeastOne" => "At least one category is required.",
        "rules.error.incomplete" => "A category is incomplete.",
        "rules.error.name" => "Each category needs a name of up to 60 characters.",
        "rules.error.destination" => "The destination for “{0}” must be a folder inside Downloads.",
        "rules.error.unique" => "Category names and destinations must be unique.",
        "rules.error.extensions" => "“{0}” must have at least one extension.",
        "rules.error.extension" => "The extension “{0}” is invalid or already belongs to another category.",
        "rules.error.empty" => "The configuration is empty.",
        "rules.save.failure.body" => "The rules could not be saved.",
        "rules.save.failure.title" => "Could not save rules",
        "rules.saved" => "Rules saved. Changes applied.",
        "rules.delete.builtin" => "Built-in categories cannot be deleted.",
        "rules.delete.confirm" => "Delete the “{0}” category? Files already in that folder will stay there.",
        "rules.delete.title" => "Confirm deletion",
        "startup.error.title" => "Startup error",
        "startup.error.body" => "Could not change EberoNest startup settings.",
        "startup.error.missingExe" => "The current EberoNest executable could not be found.",
        "startup.error.path" => "The current EberoNest executable path could not be determined.",
        "startup.error.confirm" => "Windows could not confirm the startup setting.",
        "startup.error.host" => "Windows could not configure startup.",
        "downloads.missing" => "The Downloads folder could not be found.",
        "organize.error.review" => "Could not review the Downloads folder.",
        "organize.none" => "There are no files to organize in the Downloads root folder.",
        "organize.confirm" => "{0} files were found in the Downloads root folder:\n\n{1}\n\nOrganize them now?",
        "organize.confirmTitle" => "Organize existing files",
        "organize.completed" => "Organized {0} of {1} files.",
        "organize.error.interrupted" => "Organization was interrupted.",
        "tray.open" => "Open settings",
        "tray.organize" => "Organize now",
        "tray.exit" => "Exit",
        _ => throw new KeyNotFoundException("Missing English localization key: " + key)
    };
}
