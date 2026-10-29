using EberoNest;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

if (args.SequenceEqual(["--live-github"]))
{
    await VerifyLiveGitHubRelease();
    return;
}

string temporaryRoot = Path.Combine(Path.GetTempPath(), "EberoNest-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporaryRoot);
try
{
    VerifyLocalization(temporaryRoot);
    Require(RulesStore.DefaultPath == RulesStore.GetCanonicalPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)), "default rules path uses the EberoNest data directory");
    VerifyStartupAndActivation();
    VerifyStartupManagement(temporaryRoot);
    VerifyStartupShortcutRecentIsolation(temporaryRoot);
    VerifyOfficialIconResources();
    await VerifyUpdateNotifications(temporaryRoot);
    VerifySettingsFormSmoke(temporaryRoot);
    VerifyRulesPersistence(temporaryRoot);
    VerifyLegacyRulesMigration(temporaryRoot);
    await VerifyReparsePointHandling(temporaryRoot);
    string nested = Path.Combine(temporaryRoot, "nested");
    Directory.CreateDirectory(nested);
            string existing = Path.Combine(temporaryRoot, "existing.pdf");
            File.WriteAllText(existing, "already here");
    string backlogModel = Path.Combine(temporaryRoot, "backlog.stl");
    File.WriteAllText(backlogModel, "existing model");
    await using var organizer = new Organizer(temporaryRoot, RulesStore.DefaultRules());
    organizer.Start();

            await Task.Delay(1400);
            Require(File.Exists(existing), "pre-existing file is not scanned on startup");
            Require(File.Exists(backlogModel), "backlog does not move at startup");
            IReadOnlyDictionary<string, int> preview = organizer.PreviewExistingFiles();
            Require(preview.GetValueOrDefault("Documents") == 1 && preview.GetValueOrDefault("3D Printing") == 1,
                "organize-now preview counts eligible root files by destination");
            int manuallyMoved = await organizer.OrganizeExistingFilesAsync();
            Require(manuallyMoved == 2, "organize-now moves only its eligible backlog files");
            Require(File.Exists(Path.Combine(temporaryRoot, "Documents", "existing.pdf")),
                "organize-now moves existing root document");
            Require(File.Exists(Path.Combine(temporaryRoot, "3D Printing", "backlog.stl")),
                "organize-now applies the active 3D rule to backlog");

    string document = Path.Combine(temporaryRoot, "sample.pdf");
    await File.WriteAllTextAsync(document, "document");
    await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "Documents", "sample.pdf")), "root document moves");

            string image = Path.Combine(temporaryRoot, "sample.png");
            string archive = Path.Combine(temporaryRoot, "sample.zip");
            string installer = Path.Combine(temporaryRoot, "sample.msi");
            string video = Path.Combine(temporaryRoot, "sample.mp4");
            await File.WriteAllTextAsync(image, "image");
            await File.WriteAllTextAsync(archive, "archive");
            await File.WriteAllTextAsync(installer, "installer");
            await File.WriteAllTextAsync(video, "video");
            await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "Images", "sample.png")), "image category moves");
            await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "Archives", "sample.zip")), "archive category moves");
            await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "Installers", "sample.msi")), "installer category moves");
            await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "Videos", "sample.mp4")), "video category moves");

    string csv = Path.Combine(temporaryRoot, "table.csv");
    string[] printingFiles = { "model.stl", "model.3mf", "model.obj" };
    string[] cadFiles = { "drawing.dxf", "drawing.dwg" };
    await File.WriteAllTextAsync(csv, "a,b");
    foreach (string name in printingFiles)
        await File.WriteAllTextAsync(Path.Combine(temporaryRoot, name), "model");
    foreach (string name in cadFiles)
        await File.WriteAllTextAsync(Path.Combine(temporaryRoot, name), "drawing");
    await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "Documents", "table.csv")), "CSV moves to Documents");
    foreach (string name in printingFiles)
        await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "3D Printing", name)), name + " moves to 3D Printing");
    foreach (string name in cadFiles)
        await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "CAD", name)), name + " moves to CAD");

    var reconfiguredRules = RulesStore.DefaultRules();
    RuleCategory reconfiguredCad = reconfiguredRules.Single(rule => rule.Name == "CAD");
    reconfiguredCad.FolderName = "CAD Models";
    reconfiguredCad.Extensions.Add(".step");
    organizer.UpdateRules(reconfiguredRules);
    string configuredByUser = Path.Combine(temporaryRoot, "bracket.step");
    await File.WriteAllTextAsync(configuredByUser, "step model");
    await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "CAD Models", "bracket.step")),
        "watcher immediately applies edited destination and added extension");

            string duplicateDocument = Path.Combine(temporaryRoot, "sample.pdf");
            await File.WriteAllTextAsync(duplicateDocument, "second document");
            await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "Documents", "sample (1).pdf")), "name collision gets a safe suffix");

    string unknown = Path.Combine(temporaryRoot, "unknown.xyz");
    await File.WriteAllTextAsync(unknown, "unknown");
    string directoryWithKnownExtension = Path.Combine(temporaryRoot, "folder.pdf");
    Directory.CreateDirectory(directoryWithKnownExtension);
    string insideKnownDirectory = Path.Combine(directoryWithKnownExtension, "inside.txt");
    await File.WriteAllTextAsync(insideKnownDirectory, "keep inside folder");
    string temporary = Path.Combine(temporaryRoot, "unfinished.pdf.crdownload");
    await File.WriteAllTextAsync(temporary, "unfinished");
            string[] otherTemporary = { "unfinished.tmp", "unfinished.part", "unfinished.download" };
            foreach (string name in otherTemporary)
                await File.WriteAllTextAsync(Path.Combine(temporaryRoot, name), "unfinished");
    string nestedFile = Path.Combine(nested, "nested.jpg");
    await File.WriteAllTextAsync(nestedFile, "nested");

    await Task.Delay(1600);
    Require(organizer.PreviewExistingFiles().Values.Sum() == 0,
        "organize-now preview excludes unknown, temporary, nested, and directory contents");
    Require(await organizer.OrganizeExistingFilesAsync() == 0,
        "organize-now does not process unknown, temporary, nested, or directory contents");
    Require(File.Exists(unknown), "unknown extension remains in root");
    Require(Directory.Exists(directoryWithKnownExtension), "folder with a known extension remains in root");
    Require(File.Exists(insideKnownDirectory), "folder contents remain untouched");
    Require(File.Exists(temporary), "temporary download remains in root");
            foreach (string name in otherTemporary)
                Require(File.Exists(Path.Combine(temporaryRoot, name)), name + " file remains in root");
    Require(File.Exists(nestedFile), "nested file remains untouched");
    Require(Directory.Exists(nested), "nested folder remains untouched");

    string renameSource = Path.Combine(temporaryRoot, "renamed.m4a.crdownload");
    await File.WriteAllTextAsync(renameSource, "music");
    string finalized = Path.Combine(temporaryRoot, "renamed.m4a");
    File.Move(renameSource, finalized);
    await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "Music", "renamed.m4a")), "rename finalizes and moves");

    string locked = Path.Combine(temporaryRoot, "locked.txt");
    using (var lockStream = new FileStream(locked, FileMode.Create, FileAccess.Write, FileShare.None))
    {
        await lockStream.WriteAsync("locked"u8.ToArray());
        await lockStream.FlushAsync();
        await Task.Delay(2200);
        Require(File.Exists(locked), "locked file is not moved while open");
    }
    await WaitForAsync(() => File.Exists(Path.Combine(temporaryRoot, "Documents", "locked.txt")), "locked file retries after release");

            Console.WriteLine("PASS: SettingsForm/Acerca de smoke, startup settings, defaults, rule migration, backlog, root-only watching, temporaries, folders, renames, collisions, reparse-point rejection, and locked-file retries.");
}
finally
{
    Directory.Delete(temporaryRoot, recursive: true);
}




static void VerifyStartupManagement(string temporaryRoot)
{
    string startupTestDirectory = Path.Combine(temporaryRoot, "startup-test");
    string developmentExe = Path.Combine(startupTestDirectory, "dist", "EberoNest.exe");
    string installedExe = Path.Combine(startupTestDirectory, "Programs", "EberoNest", "EberoNest.exe");
    string otherCopyExe = Path.Combine(startupTestDirectory, "other-copy", "EberoNest.exe");
    string localAppData = Path.Combine(startupTestDirectory, "local-app-data");
    string oldDropNestInstalled = Path.Combine(localAppData, "Programs", "Ebero DropNest", "EberoDropNest.exe");
    string oldDropNestAdjacent = Path.Combine(Path.GetDirectoryName(developmentExe)!, "EberoDropNest.exe");
    string oldDownloadsOrganizer = Path.Combine(localAppData, "Programs", "DownloadsOrganizer", "DownloadsOrganizer.exe");
    foreach (string path in new[] { developmentExe, installedExe, otherCopyExe, oldDropNestInstalled, oldDropNestAdjacent, oldDownloadsOrganizer })
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllBytes(developmentExe, [1, 2, 3, 4]);
    File.WriteAllBytes(installedExe, [1, 2, 3, 4]);
    File.WriteAllBytes(otherCopyExe, [9, 8, 7, 6]);
    File.WriteAllBytes(oldDropNestInstalled, []);
    File.WriteAllBytes(oldDropNestAdjacent, []);
    File.WriteAllBytes(oldDownloadsOrganizer, []);

    string actualProcessPath = Environment.ProcessPath ?? throw new InvalidOperationException("Verification process path is unavailable.");
    Require(StartupManager.GetCurrentExecutablePath() == Path.GetFullPath(actualProcessPath), "current executable path is resolved as a full process path");
    Require(StartupManager.ShortcutFileName == "EberoNest.lnk", "canonical startup shortcut uses EberoNest.lnk");

    var store = new FakeStartupEntryStore();
    var manager = new StartupManager(store, developmentExe, localAppData);
    var unrelated = new StartupEntry(Path.Combine(startupTestDirectory, "OtherApp.exe"), "--keep");
    store.Entries["OtherApp.lnk"] = unrelated;

    // Both legacy entries point to expected executables and the target files exist.
    store.Entries[StartupManager.LegacyDropNestShortcutFileName] = new StartupEntry(oldDropNestInstalled, "--background");
    store.Entries[StartupManager.LegacyDownloadsOrganizerShortcutFileName] = new StartupEntry(oldDownloadsOrganizer, "");
    manager.Enable();
    Require(manager.IsEnabled && store.Read(StartupManager.ShortcutFileName)?.TargetPath == Path.GetFullPath(developmentExe) &&
            store.Read(StartupManager.ShortcutFileName)?.Arguments == "--background", "Enable writes the current executable with the exact background argument");
    Require(!store.Exists(StartupManager.LegacyDropNestShortcutFileName) && !store.Exists(StartupManager.LegacyDownloadsOrganizerShortcutFileName),
        "Enable removes valid legacy shortcuts with existing targets, including the old empty-argument DownloadsOrganizer link");
    Require(store.Entries.Keys.Where(name => name != "OtherApp.lnk").SequenceEqual([StartupManager.ShortcutFileName]),
        "Enable leaves only EberoNest.lnk among the recognized application startup entries");
    int writesAfterEnable = store.WriteCount;
    manager.Enable();
    Require(store.WriteCount == writesAfterEnable, "Enable is idempotent");

    // A known target path remains identifiable after its EXE has been removed.
    File.Delete(oldDropNestInstalled);
    store.Entries[StartupManager.LegacyDropNestShortcutFileName] = new StartupEntry(oldDropNestInstalled, "--background");
    manager.Enable();
    Require(!store.Exists(StartupManager.LegacyDropNestShortcutFileName), "valid Ebero DropNest shortcut is removed even after its target EXE disappears");
    File.Delete(oldDownloadsOrganizer);
    store.Entries[StartupManager.LegacyDownloadsOrganizerShortcutFileName] = new StartupEntry(oldDownloadsOrganizer, "");
    manager.Enable();
    Require(!store.Exists(StartupManager.LegacyDownloadsOrganizerShortcutFileName), "valid DownloadsOrganizer shortcut is removed even after its target EXE disappears");

    // The observed development shortcut uses the previous EXE name beside the current EXE.
    store.Entries[StartupManager.LegacyDropNestShortcutFileName] = new StartupEntry(oldDropNestAdjacent, "--background");
    File.Delete(oldDropNestAdjacent);
    manager.Enable();
    Require(!store.Exists(StartupManager.LegacyDropNestShortcutFileName), "expected adjacent legacy EXE path is recognized after its target disappears");

    store.Entries[StartupManager.LegacyDropNestShortcutFileName] = new StartupEntry(oldDropNestInstalled, "--wrong");
    store.Entries[StartupManager.LegacyDownloadsOrganizerShortcutFileName] = new StartupEntry(oldDownloadsOrganizer, "--wrong");
    store.Entries["Ebero DropNest - Copy.lnk"] = new StartupEntry(Path.Combine(startupTestDirectory, "unrecognized", "EberoDropNest.exe"), "--background");
    manager.Enable();
    Require(store.Exists(StartupManager.LegacyDropNestShortcutFileName) && store.Exists(StartupManager.LegacyDownloadsOrganizerShortcutFileName),
        "legacy shortcuts with incorrect arguments are preserved");
    Require(store.Exists("Ebero DropNest - Copy.lnk"), "a similarly named shortcut with an unrecognized target is preserved");
    Require(store.Read("OtherApp.lnk") == unrelated, "unrelated startup entries remain untouched");

    manager.Disable();
    Require(!manager.IsEnabled && !store.Exists(StartupManager.ShortcutFileName), "Disable removes the canonical owned entry");
    int deletesAfterDisable = store.DeleteCount;
    manager.Disable();
    Require(store.DeleteCount == deletesAfterDisable, "Disable is idempotent");

    var installedManager = new StartupManager(store, installedExe, localAppData);
    installedManager.Enable();
    Require(installedManager.IsEnabled && store.Read(StartupManager.ShortcutFileName)?.TargetPath == Path.GetFullPath(installedExe), "normal installed path is used as startup target");
    Require(!manager.IsEnabled, "IsEnabled compares the shortcut to the current executable path");
    store.Entries[StartupManager.ShortcutFileName] = new StartupEntry(otherCopyExe, "--background");
    bool foreignEntryProtected = false;
    try { manager.Disable(); } catch (InvalidOperationException) { foreignEntryProtected = true; }
    Require(!manager.IsEnabled && foreignEntryProtected && store.Read(StartupManager.ShortcutFileName)?.TargetPath == otherCopyExe,
        "Disable preserves a canonical-named shortcut targeting another executable copy");

    // A development shortcut is adoptable only when its exact payload and arguments match this installed build.
    store.Entries[StartupManager.ShortcutFileName] = new StartupEntry(developmentExe, "--background");
    Require(!installedManager.IsEnabled, "a same-payload development path is not reported as the installed canonical target before adoption");
    int writesBeforeAdoption = store.WriteCount;
    installedManager.Enable();
    Require(store.WriteCount == writesBeforeAdoption + 1 && installedManager.IsEnabled &&
            store.Read(StartupManager.ShortcutFileName) == new StartupEntry(Path.GetFullPath(installedExe), "--background"),
        "Enable adopts an authentic same-payload development shortcut and writes the installed target");
    int writesAfterAdoption = store.WriteCount;
    installedManager.Enable();
    Require(store.WriteCount == writesAfterAdoption, "Enable is idempotent after same-payload shortcut adoption");

    store.Entries[StartupManager.ShortcutFileName] = new StartupEntry(otherCopyExe, "--background");
    bool foreignSameNameRejected = false;
    try { installedManager.Enable(); } catch (InvalidOperationException) { foreignSameNameRejected = true; }
    Require(foreignSameNameRejected && store.Read(StartupManager.ShortcutFileName)?.TargetPath == otherCopyExe,
        "an unrelated binary named EberoNest.exe is rejected and preserved");

    store.Entries[StartupManager.ShortcutFileName] = new StartupEntry(developmentExe, "--wrong");
    bool wrongArgumentsRejected = false;
    try { installedManager.Enable(); } catch (InvalidOperationException) { wrongArgumentsRejected = true; }
    Require(wrongArgumentsRejected && store.Read(StartupManager.ShortcutFileName)?.Arguments == "--wrong",
        "a same-payload development shortcut with incorrect arguments is rejected and preserved");

    string missingSameNameExe = Path.Combine(startupTestDirectory, "missing-copy", "EberoNest.exe");
    store.Entries[StartupManager.ShortcutFileName] = new StartupEntry(missingSameNameExe, "--background");
    bool missingTargetRejected = false;
    try { installedManager.Enable(); } catch (InvalidOperationException) { missingTargetRejected = true; }
    Require(missingTargetRejected && store.Read(StartupManager.ShortcutFileName)?.TargetPath == missingSameNameExe,
        "a same-name development shortcut with a missing target is rejected and preserved");

    store.Entries[StartupManager.ShortcutFileName] = new StartupEntry(developmentExe, "--background");
    installedManager.Disable();
    Require(!store.Exists(StartupManager.ShortcutFileName), "Disable safely removes a same-payload canonical shortcut alias");
    Require(InstanceActivationSignal.MutexName == @"Local\DownloadsOrganizer.Instance" &&
            InstanceActivationSignal.Name == @"Local\EberoDropNest.ShowSettings", "historical mutex and event identifiers remain stable for inter-version activation");
}

static void VerifyStartupShortcutRecentIsolation(string temporaryRoot)
{
    string startupDirectory = Path.Combine(temporaryRoot, "isolated-startup-recents");
    string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    string startupManagerSource = File.ReadAllText(Path.Combine(projectRoot, "StartupManager.cs"));
    Require(!startupManagerSource.Contains("SHAddToRecentDocs", StringComparison.OrdinalIgnoreCase) &&
            !startupManagerSource.Contains("SHARD_PATH", StringComparison.OrdinalIgnoreCase),
        "Startup shortcut management does not call Windows APIs that add links to Recent history");

    string recentShortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Windows", "Recent", StartupManager.ShortcutFileName);
    byte[]? recentBefore = File.Exists(recentShortcutPath) ? SHA256.HashData(File.ReadAllBytes(recentShortcutPath)) : null;
    var store = new WindowsStartupEntryStore(startupDirectory);
    var manager = new StartupManager(store, StartupManager.GetCurrentExecutablePath());

    manager.Enable();
    manager.Enable();
    Require(manager.IsEnabled && store.Exists(StartupManager.ShortcutFileName),
        "isolated Startup shortcut creation and repair succeeds without using the user's Startup folder");

    byte[]? recentAfter = File.Exists(recentShortcutPath) ? SHA256.HashData(File.ReadAllBytes(recentShortcutPath)) : null;
    Require((recentBefore is null && recentAfter is null) ||
            (recentBefore is not null && recentAfter is not null && CryptographicOperations.FixedTimeEquals(recentBefore, recentAfter)),
        "creating or repairing the EberoNest Startup shortcut leaves the user's Recent shortcut untouched");
    manager.Disable();
    Console.WriteLine("PASS: Startup shortcut create/repair is isolated from Windows Recent history.");
}
static void VerifyLocalization(string temporaryRoot)
{
    var noPreference = new FakeLanguagePreferenceStore();
    AppLocalization.Initialize(noPreference, CultureInfo.GetCultureInfo("es-ES"));
    Require(AppLocalization.Current == AppLanguage.Spanish && noPreference.SaveCount == 0,
        "Spanish Windows UI defaults to Spanish without writing a preference");

    AppLocalization.Initialize(noPreference, CultureInfo.GetCultureInfo("fr-FR"));
    Require(AppLocalization.Current == AppLanguage.English,
        "a non-Spanish Windows UI defaults to English");

    AppLocalization.Initialize(noPreference, CultureInfo.GetCultureInfo("en-US"));
    AppLocalization.SetLanguage(AppLanguage.Spanish);
    Require(noPreference.Value == "es" && noPreference.SaveCount == 1,
        "English-to-Spanish selection persists the Spanish preference");
    AppLocalization.SetLanguage(AppLanguage.English);
    Require(noPreference.Value == "en" && noPreference.SaveCount == 2,
        "Spanish-to-English selection persists the English preference");

    noPreference.Value = "es";
    AppLocalization.Initialize(noPreference, CultureInfo.GetCultureInfo("en-US"));
    Require(AppLocalization.Current == AppLanguage.Spanish, "a persisted Spanish preference overrides Windows language");
    noPreference.Value = "en";
    AppLocalization.Initialize(noPreference, CultureInfo.GetCultureInfo("es-ES"));
    Require(AppLocalization.Current == AppLanguage.English, "a persisted English preference overrides Windows language");

    string statePath = Path.Combine(temporaryRoot, "language-state", "language.txt");
    var fileStore = new FileLanguagePreferenceStore(statePath);
    Require(fileStore.Load() is null, "missing language preference is treated as absent");
    fileStore.Save(AppLanguage.Spanish);
    Require(fileStore.Load() == "es", "Spanish language preference is stored separately as a compact value");
    File.WriteAllText(statePath, "corrupt language state");
    Require(fileStore.Load() == "corrupt language state", "corrupt language state is read as an unrecognized value");
    AppLocalization.Initialize(fileStore, CultureInfo.GetCultureInfo("en-US"));
    Require(AppLocalization.Current == AppLanguage.English,
        "corrupt language preference safely falls back to the Windows language");

    var userRules = new[]
    {
        new RuleCategory { Name = "Documents", FolderName = "Documents", Extensions = [".pdf"] },
        new RuleCategory { Name = "3D Printing", FolderName = "3D Printing", Extensions = [".stl"] },
        new RuleCategory { Name = "Custom media", FolderName = "Custom media", Extensions = [".xyz"] }
    };
    string[] originalRuleNames = userRules.Select(rule => rule.Name).ToArray();
    AppLocalization.Initialize(new FakeLanguagePreferenceStore("es"), CultureInfo.GetCultureInfo("en-US"));
    AppLocalization.SetLanguage(AppLanguage.English);
    AppLocalization.SetLanguage(AppLanguage.Spanish);
    Require(userRules.Select(rule => rule.Name).SequenceEqual(originalRuleNames),
        "switching languages never translates or changes saved user category names");

    Require(AppLocalization.Get(AppLanguage.Spanish, "updates.current") == "EberoNest está actualizado." &&
            AppLocalization.Get(AppLanguage.English, "updates.current") == "EberoNest is up to date." &&
            AppLocalization.Get(AppLanguage.Spanish, "updates.failure") == "No se pudo comprobar si hay actualizaciones." &&
            AppLocalization.Get(AppLanguage.English, "updates.failure") == "Could not check for updates.",
        "Update Checker current and failure messages are available in both languages");
    Require(AppLocalization.Get(AppLanguage.Spanish, "tray.open") == "Abrir configuración" &&
            AppLocalization.Get(AppLanguage.English, "tray.open") == "Open settings" &&
            AppLocalization.Get(AppLanguage.Spanish, "category.new") == "Nueva categoría" &&
            AppLocalization.Get(AppLanguage.English, "category.edit") == "Edit category",
        "tray and category dialog labels are available in both languages");
    Require(AppLocalization.LocalizeRuleError(new InvalidDataException("Debe existir al menos una categoría.")) == "Debe existir al menos una categoría.",
        "rule validation errors are localized in Spanish");
    AppLocalization.Initialize(new FakeLanguagePreferenceStore("en"), CultureInfo.GetCultureInfo("es-ES"));
    Require(AppLocalization.LocalizeRuleError(new InvalidDataException("Debe existir al menos una categoría.")) == "At least one category is required.",
        "rule validation errors are localized in English");
    Require(OfficialRepositoryLink.TryValidate(OfficialRepositoryLink.Url, out _) &&
            !OfficialRepositoryLink.TryValidate("http://github.com/Efzax/EberoNest", out _) &&
            !OfficialRepositoryLink.TryValidate("https://github.com/Other/EberoNest", out _) &&
            !OfficialRepositoryLink.TryValidate("https://github.com/Efzax/Other", out _) &&
            !OfficialRepositoryLink.TryValidate("https://github.com@evil.example/Efzax/EberoNest", out _) &&
            !OfficialRepositoryLink.TryValidate("https://github.com/Efzax/EberoNest?next=https://evil.example", out _),
        "About accepts only the canonical official HTTPS repository link");
    Require(!OfficialRepositoryLink.TryOpen("http://github.com/Efzax/EberoNest"),
        "an invalid repository link is rejected without opening a browser");
    AppLocalization.Initialize(new FakeLanguagePreferenceStore("es"), CultureInfo.GetCultureInfo("en-US"));
    Console.WriteLine("PASS: language defaults, preference persistence/fallback, unchanged rule data, localized messages, tray strings, and repository-link validation.");
}

static async Task VerifyUpdateNotifications(string temporaryRoot)
{
    SemanticVersion v100 = default, v101 = default, v190 = default, v1100 = default;
    Require(SemanticVersion.TryParseTag("v1.0.0", out v100) &&
            SemanticVersion.TryParseTag("v1.0.1", out v101) &&
            SemanticVersion.TryParseTag("v1.9.0", out v190) &&
            SemanticVersion.TryParseTag("v1.10.0", out v1100),
        "stable release tags accept the vX.Y.Z format");
    Require(v100.CompareTo(new SemanticVersion(1, 0, 0)) == 0, "semantic version 1.0.0 equals 1.0.0");
    Require(v101.CompareTo(v100) > 0, "semantic version 1.0.1 is newer than 1.0.0");
    Require(v1100.CompareTo(v190) > 0, "semantic comparison orders 1.10.0 after 1.9.0 numerically");
    Require(!SemanticVersion.TryParseTag("1.0.1", out _) && !SemanticVersion.TryParseTag("v1.0", out _) &&
            !SemanticVersion.TryParseTag("v01.0.1", out _) && !SemanticVersion.TryParseTag("v1.0.1-rc.1", out _),
        "unprefixed, incomplete, leading-zero, and prerelease tags are rejected");

    string stableJson = ReleaseJson("v1.0.1", draft: false, prerelease: false);
    OfficialRelease? parsed = GitHubLatestReleaseClient.ParseLatestStableRelease(stableJson);
    Require(parsed?.Tag == "v1.0.1", "a stable official release is parsed");
    Require(GitHubLatestReleaseClient.ParseLatestStableRelease(ReleaseJson("v9.0.0", draft: true, prerelease: false)) is null,
        "draft releases are ignored even if they have a higher version");
    Require(GitHubLatestReleaseClient.ParseLatestStableRelease(ReleaseJson("v9.0.0", draft: false, prerelease: true)) is null,
        "prereleases are ignored even if they have a higher version");
    bool invalidJsonRejected = false;
    try { GitHubLatestReleaseClient.ParseLatestStableRelease("{ invalid json"); }
    catch (JsonException) { invalidJsonRejected = true; }
    Require(invalidJsonRejected, "invalid release JSON is rejected");
    bool missingStableFlagsRejected = false;
    try { GitHubLatestReleaseClient.ParseLatestStableRelease("""{"tag_name":"v1.0.1","html_url":"https://github.com/Efzax/EberoNest/releases/tag/v1.0.1"}"""); }
    catch (InvalidDataException) { missingStableFlagsRejected = true; }
    Require(missingStableFlagsRejected, "a release response without draft/prerelease flags is rejected as ambiguous");

    HttpRequestMessage? observedRequest = null;
    using (var httpClient = new HttpClient(new FakeHttpMessageHandler((request, _) =>
               {
                   observedRequest = request;
                   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                   {
                       Content = new StringContent(ReleaseJson("v1.0.0", draft: false, prerelease: false))
                   });
               })))
    using (var releaseClient = new GitHubLatestReleaseClient(v101, httpClient, TimeSpan.FromSeconds(1)))
    {
        _ = await releaseClient.GetLatestStableReleaseAsync();
        Require(observedRequest?.RequestUri?.AbsoluteUri == GitHubLatestReleaseClient.Endpoint &&
                observedRequest.Headers.UserAgent.ToString() == "EberoNest/1.0.1" && observedRequest.Content is null,
            "the read-only request uses only the official HTTPS endpoint and app/version User-Agent without a request body");
    }

    Require(OfficialReleaseUrl.TryValidate("https://github.com/Efzax/EberoNest/releases/tag/v1.0.1", v101, out _),
        "the exact official HTTPS release URL is accepted");
    Require(!OfficialReleaseUrl.TryValidate("http://github.com/Efzax/EberoNest/releases/tag/v1.0.1", v101, out _) &&
            !OfficialReleaseUrl.TryValidate("https://evil.example/Efzax/EberoNest/releases/tag/v1.0.1", v101, out _),
        "non-HTTPS and non-GitHub hosts are rejected");
    Require(!OfficialReleaseUrl.TryValidate("https://github.com/Other/EberoNest/releases/tag/v1.0.1", v101, out _) &&
            !OfficialReleaseUrl.TryValidate("https://github.com/Efzax/Other/releases/tag/v1.0.1", v101, out _),
        "incorrect GitHub owner and repository are rejected");
    Require(!OfficialReleaseUrl.TryValidate("https://github.com/Efzax/EberoNest/releases/tag/v1.0.2", v101, out _) &&
            !OfficialReleaseUrl.TryValidate("https://github.com:444/Efzax/EberoNest/releases/tag/v1.0.1", v101, out _),
        "release URL with a mismatched tag or nonstandard port is rejected");

    string statePath = Path.Combine(temporaryRoot, "update-state", "last-check.txt");
    var fileState = new FileAutomaticUpdateCheckState(statePath);
    Require(fileState.ReadLastSuccessfulCheckDate() is null, "missing automatic update state is treated as absent");
    DateOnly savedDate = new(2026, 10, 28);
    fileState.SaveLastSuccessfulCheckDate(savedDate);
    Require(fileState.ReadLastSuccessfulCheckDate() == savedDate, "valid daily update state is persisted and read");
    File.WriteAllText(statePath, "corrupt state");
    Require(fileState.ReadLastSuccessfulCheckDate() is null, "corrupt automatic update state is treated as absent");

    var autoState = new FakeAutomaticUpdateCheckState();
    var autoSource = new FakeLatestReleaseClient(_ => Task.FromResult(MakeRelease("v1.0.0")));
    using (var autoService = new UpdateNotificationService(v101, autoSource, autoState))
    {
        await autoService.CheckAutomaticallyAsync();
        await autoService.CheckAutomaticallyAsync();
        Require(autoSource.CallCount == 1 && autoState.SaveCount == 1 &&
                autoState.LastDate == DateOnly.FromDateTime(DateTime.Now),
            "automatic successful check is recorded and does not repeat the same day");
    }

    var failedAutoState = new FakeAutomaticUpdateCheckState();
    using (var failedAutoService = new UpdateNotificationService(v101,
               new FakeLatestReleaseClient(_ => throw new HttpRequestException("offline")), failedAutoState))
    {
        await failedAutoService.CheckAutomaticallyAsync();
        Require(failedAutoState.SaveCount == 0 && failedAutoState.LastDate is null,
            "a failed automatic request is silent and does not record a successful daily check");
    }

    var manualState = new FakeAutomaticUpdateCheckState();
    var manualSource = new FakeLatestReleaseClient(_ => Task.FromResult(MakeRelease("v1.0.0")));
    using (var manualService = new UpdateNotificationService(v101, manualSource, manualState))
    {
        UpdateCheckResult first = await manualService.CheckManuallyAsync();
        UpdateCheckResult second = await manualService.CheckManuallyAsync();
        Require(first.Status == UpdateCheckStatus.UpToDate && second.Status == UpdateCheckStatus.UpToDate &&
                manualSource.CallCount == 2 && manualState.SaveCount == 0,
            "manual checks can run repeatedly without the daily automatic limit");
    }

    var availableService = new UpdateNotificationService(v101,
        new FakeLatestReleaseClient(_ => Task.FromResult(MakeRelease("v1.0.2"))), new FakeAutomaticUpdateCheckState());
    using (availableService)
    {
        UpdateCheckResult available = await availableService.CheckManuallyAsync();
        Require(available.Status == UpdateCheckStatus.UpdateAvailable && available.LatestRelease?.Tag == "v1.0.2",
            "a numerically newer stable release is reported as available");
    }

    using (var httpClient = new HttpClient(new FakeHttpMessageHandler((_, _) =>
               Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)))))
    using (var failingService = new UpdateNotificationService(v101,
               new GitHubLatestReleaseClient(v101, httpClient, TimeSpan.FromSeconds(1)), new FakeAutomaticUpdateCheckState()))
    {
        UpdateCheckResult failed = await failingService.CheckManuallyAsync();
        Require(failed.Status == UpdateCheckStatus.Failed, "HTTP failure returns the generic failed status");
    }

    using (var httpClient = new HttpClient(new FakeHttpMessageHandler(async (_, token) =>
               {
                   await Task.Delay(Timeout.InfiniteTimeSpan, token);
                   return new HttpResponseMessage(HttpStatusCode.OK);
               })))
    using (var timeoutService = new UpdateNotificationService(v101,
               new GitHubLatestReleaseClient(v101, httpClient, TimeSpan.FromMilliseconds(40)), new FakeAutomaticUpdateCheckState()))
    {
        UpdateCheckResult timedOut = await timeoutService.CheckManuallyAsync();
        Require(timedOut.Status == UpdateCheckStatus.Failed, "request timeout returns the generic failed status");
    }

    string downloadsPath = Path.Combine(temporaryRoot, "update-failure-organizer");
    Directory.CreateDirectory(downloadsPath);
    await using var organizer = new Organizer(downloadsPath, RulesStore.DefaultRules());
    organizer.Start();
    using (var isolatedFailureService = new UpdateNotificationService(v101,
               new FakeLatestReleaseClient(_ => throw new HttpRequestException("offline")), new FakeAutomaticUpdateCheckState()))
        await isolatedFailureService.CheckAutomaticallyAsync();
    string sample = Path.Combine(downloadsPath, "update-failure-still-organizes.pdf");
    await File.WriteAllTextAsync(sample, "organizer remains independent");
    await WaitForAsync(() => File.Exists(Path.Combine(downloadsPath, "Documents", Path.GetFileName(sample))),
        "update checker failure does not interrupt the Organizer");
}

static async Task VerifyLiveGitHubRelease()
{
    SemanticVersion currentVersion = new(1, 0, 2);
    using var client = new GitHubLatestReleaseClient(currentVersion);
    OfficialRelease release = await client.GetLatestStableReleaseAsync();
    Require(release.Tag == "v1.0.1" && release.Version.CompareTo(currentVersion) <= 0,
        $"official GitHub latest stable release is v1.0.1 and does not exceed current {currentVersion.ToTag()}");
    Console.WriteLine($"PASS: live HTTPS read from {GitHubLatestReleaseClient.Endpoint}; latest stable tag {release.Tag}; no update available for {currentVersion.ToTag()}.");
}

static OfficialRelease MakeRelease(string tag)
{
    if (!SemanticVersion.TryParseTag(tag, out SemanticVersion version))
        throw new InvalidOperationException("Invalid test release tag: " + tag);
    return new OfficialRelease(version, tag, OfficialReleaseUrl.Create(version));
}

static string ReleaseJson(string tag, bool draft, bool prerelease) =>
    $$"""{"tag_name":"{{tag}}","draft":{{draft.ToString().ToLowerInvariant()}},"prerelease":{{prerelease.ToString().ToLowerInvariant()}},"html_url":"https://github.com/Efzax/EberoNest/releases/tag/{{tag}}"}""";

static void VerifySettingsFormSmoke(string temporaryRoot)
{
    string downloadsPath = Path.Combine(temporaryRoot, "settings-form-smoke");
    Directory.CreateDirectory(downloadsPath);
    Exception? failure = null;
    var uiThread = new Thread(() =>
    {
        var languagePreferences = new FakeLanguagePreferenceStore("es");
        AppLocalization.Initialize(languagePreferences, CultureInfo.GetCultureInfo("en-US"));
        Organizer organizer = new(downloadsPath, RulesStore.DefaultRules());
        try
        {
            var updateService = new FakeUpdateNotificationService();
            var releaseBrowser = new FakeReleaseBrowser();
            using Icon appTitleBarIcon = LoadEmbeddedIcon("app-icon");
            using var form = new SettingsForm(organizer, _ => Task.CompletedTask, new FakeStartupManager(), updateService, releaseBrowser)
                { Icon = (Icon)appTitleBarIcon.Clone() };
            Require(IconsRenderSame(form.Icon, appTitleBarIcon), "main SettingsForm title bar uses app-icon.ico");
            Require(ImageMatchesResource(form.HeaderLogoForSmoke.Image, "EberoNest.Assets.app-icon.png"),
                "main header renders app-icon.png as the large product logo");
            Require(form.Text == "EberoNest", "settings form uses the canonical product name");
            Require(!form.UpdateNoticeForSmoke.Visible && form.UpdateNoticeForSmoke.Text.Length == 0,
                "settings footer remains clean when no update is available");
            form.CreateControl();
            form.PerformLayout();
            form.Show();
            Application.DoEvents();
            Require(form.SmallTitleBarIconMatchesForSmoke,
                $"main window uses tray_normal.ico as ICON_SMALL while Form.Icon remains app-icon.ico ({form.SmallTitleBarIconDiagnosticForSmoke})");
            Require(form.FormBorderStyle == FormBorderStyle.Sizable && form.MinimizeBox && form.MaximizeBox && form.ControlBox,
                "main settings keeps native minimize, maximize, close, resize, and title-bar behavior enabled");
            if (Environment.OSVersion.Version.Build >= 22000)
                Require(form.DarkChromeAppliedForSmoke, "Windows 11 applies the native DWM dark title-bar attributes");
            form.WindowState = FormWindowState.Minimized;
            Application.DoEvents();
            Require(form.WindowState == FormWindowState.Minimized, "native minimize state remains functional");
            form.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            Require(form.WindowState == FormWindowState.Maximized, "native maximize state remains functional");
            form.WindowState = FormWindowState.Normal;
            Application.DoEvents();
            Require(form.WindowState == FormWindowState.Normal, "native restore state remains functional");
            Button aboutButton = form.AboutButtonForSmoke;
            TableLayoutPanel aboutArea = form.AboutAreaForSmoke;
            Rectangle aboutWithoutUpdate = aboutButton.Bounds;
            Require(aboutButton.Right == aboutArea.ClientSize.Width,
                "without an update, About remains aligned to the right edge of its footer area");
            Require(aboutButton.Text == "Acerca de · v1.0.2" && form.RulesListForSmoke.AccessibleName == "Reglas de organización",
                "SettingsForm starts with Spanish text and preserves its localized footer alignment");
            updateService.PublishResult(new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, MakeRelease("v1.0.3")));
            form.PerformLayout();
            aboutArea.PerformLayout();
            Application.DoEvents();
            Require(form.UpdateNoticeForSmoke.Visible && form.UpdateNoticeForSmoke.Text == "Nueva versión disponible · v1.0.3" &&
                    form.UpdateNoticeForSmoke.LinkColor == SettingsForm.Accent,
                "settings footer shows a subtle blue update notice only when a newer release exists");
            Require(aboutButton.Bounds == aboutWithoutUpdate && form.UpdateNoticeForSmoke.Right <= aboutButton.Left,
                "with an update, About keeps exactly the same right-aligned position and the notice stays to its left");
            updateService.PublishResult(new UpdateCheckResult(UpdateCheckStatus.UpToDate, MakeRelease("v1.0.1")));
            form.PerformLayout();
            aboutArea.PerformLayout();
            Application.DoEvents();
            Require(!form.UpdateNoticeForSmoke.Visible && aboutButton.Bounds == aboutWithoutUpdate,
                "hiding the update notice restores the identical footer layout without moving About");
            RulesListControl list = form.RulesListForSmoke;
            Require(list.ItemCount == organizer.Rules.Count, "settings form loads each rule into the custom list");
            Require(list.ChipCount == organizer.Rules.Sum(rule => rule.Extensions.Count), "custom list lays out every extension as a chip");
            Require(list.SelectedRule is not null, "custom list starts with a selected rule");
            if (list.ItemCount > 1)
            {
                list.SelectIndex(1);
                Require(list.SelectedRule is not null && organizer.Rules.Any(rule => rule.Name.Equals(list.SelectedRule.Name, StringComparison.OrdinalIgnoreCase)), "custom list selection resolves a rule from the existing model");
            }

            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            using (var listBitmap = new Bitmap(Math.Max(1, list.Width), Math.Max(1, list.Height)))
                list.DrawToBitmap(listBitmap, new Rectangle(Point.Empty, list.Size));

            form.ClientSize = new Size(860, 640);
            form.PerformLayout();
            aboutArea.PerformLayout();
            Require(aboutButton.Right == aboutArea.ClientSize.Width,
                "at the minimum window size, About remains aligned to the right edge of its footer area");
            using (var minimumBitmap = new Bitmap(form.Width, form.Height))
                form.DrawToBitmap(minimumBitmap, new Rectangle(Point.Empty, form.Size));
            Require(list.Width > 0 && list.Height > 0, "custom list remains laid out at the minimum supported window size");
            using (var minimumListBitmap = new Bitmap(Math.Max(1, list.Width), Math.Max(1, list.Height)))
                list.DrawToBitmap(minimumListBitmap, new Rectangle(Point.Empty, list.Size));

            using var longList = new RulesListControl { Size = new Size(720, 260) };
            longList.CreateControl();
            RuleCategory[] repeatedRules = Enumerable.Range(0, 24).SelectMany(_ => organizer.Rules).ToArray();
            longList.SetRules(repeatedRules);
            Require(longList.HasScrollableContent, "custom list enables scrolling when rules exceed the viewport");
            longList.SelectIndex(longList.ItemCount - 1);
            Require(longList.ScrollOffsetForSmoke > 0, "selecting a lower rule scrolls it into view");
            Require(longList.ChipCount == repeatedRules.Sum(rule => rule.Extensions.Count), "scrolling list keeps every extension as a chip");
            using var longListBitmap = new Bitmap(longList.Width, longList.Height);
            longList.DrawToBitmap(longListBitmap, new Rectangle(Point.Empty, longList.Size));

            using var about = new AboutForm(form.Icon, updateService, releaseBrowser);
            Require(IconsRenderSame(about.Icon, appTitleBarIcon), "About title bar uses app-icon.ico");
            Require(ImageMatchesResource(about.LogoForSmoke.Image, "EberoNest.Assets.app-icon.png"),
                "About renders app-icon.png as the large product logo");
            Require(about.Text == "Acerca de EberoNest", "About uses the canonical product name");
            Require(about.Controls.OfType<TableLayoutPanel>().SelectMany(panel => panel.Controls.OfType<Label>()).Any(label => label.Text == AppVersion.Display), "About displays the current product version metadata");
            Require(AppVersion.Display == "v1.0.2" && about.SearchUpdatesForSmoke.Text == "Buscar actualizaciones",
                "v1.0.2 metadata and manual update action are shown in About");
            about.CreateControl();
            about.PerformLayout();
            about.Show();
            Application.DoEvents();
            Require(about.SmallTitleBarIconMatchesForSmoke,
                "About uses tray_normal.ico as ICON_SMALL while its Form.Icon remains app-icon.ico");
            Require(about.ClientSize.Width <= 400 && about.ClientSize.Height <= 420 &&
                    EnumerateControls(about).Where(control => control.Parent is not null)
                        .All(control => control.Parent!.ClientRectangle.Contains(control.Bounds)),
                "About remains compact and its child controls fit within their parent areas");
            DarkLanguageSelector selector = about.LanguageSelectorForSmoke;
            Rectangle selectorBounds = selector.Bounds;
            Size aboutClientSize = about.ClientSize;
            if (Environment.OSVersion.Version.Build >= 22000)
                Require(about.DarkChromeAppliedForSmoke, "About applies the native DWM dark title-bar attributes");
            Require(about.LanguageSelectorForSmoke.SelectedItem?.ToString() == "Idioma: Español" &&
                    about.LanguageSelectorForSmoke.AccessibleName == "Idioma: Español" &&
                    selector.BackColor == SettingsForm.Elevated && selector.ForeColor == Color.FromArgb(236, 238, 242) &&
                    selector.Size == new Size(190, 23) && selector.AccessibleRole == AccessibleRole.ComboBox &&
                    selector.DeviceDpi > 0 &&
                    about.RepositoryLinkForSmoke.Text == "Repositorio en GitHub" &&
                    about.Controls.OfType<TableLayoutPanel>().SelectMany(panel => panel.Controls.OfType<Label>())
                        .Any(label => label.Text == "Licencia MIT · Efzax") &&
                    about.AutoScaleMode == AutoScaleMode.Dpi,
                "About in Spanish contains the language selector, validated repository link, MIT reference, Efzax attribution, and DPI scaling");

            selector.SetHoveredForSmoke(true);
            using (var selectorBitmap = new Bitmap(selector.Width, selector.Height))
            {
                selector.DrawToBitmap(selectorBitmap, new Rectangle(Point.Empty, selector.Size));
                Require(selectorBitmap.GetPixel(5, selector.Height / 2) == selector.HoverSurfaceForSmoke,
                    "language selector hover state paints the dark elevated surface");
            }
            selector.SetHoveredForSmoke(false);

            selector.OpenDropDownForSmoke();
            Application.DoEvents();
            ToolStripMenuItem[] spanishOptions = selector.DropDownForSmoke.Items.OfType<ToolStripMenuItem>().ToArray();
            Require(selector.IsDropDownOpenForSmoke && selector.DropDownForSmoke.BackColor == SettingsForm.Elevated &&
                    spanishOptions.Length == 2 && spanishOptions[0].Text == "Idioma: Español" &&
                    spanishOptions[1].Text == "Language: English" && spanishOptions.All(option => option.ForeColor == Color.FromArgb(236, 238, 242)) &&
                    spanishOptions[0].Checked && !spanishOptions[1].Checked,
                "Spanish dropdown renders both language options on the dark surface with a clear selected state");
            spanishOptions[1].PerformClick();
            Application.DoEvents();
            Require(selector.SelectedIndex == 1 && languagePreferences.Value == "en" && languagePreferences.SaveCount == 1 &&
                    selector.Bounds == selectorBounds && about.ClientSize == aboutClientSize,
                "selecting English applies immediately, persists the preference, and preserves About geometry");
            selector.OpenDropDownForSmoke();
            Application.DoEvents();
            ToolStripMenuItem[] englishOptions = selector.DropDownForSmoke.Items.OfType<ToolStripMenuItem>().ToArray();
            Require(selector.IsDropDownOpenForSmoke && englishOptions[0].Text == "Idioma: Español" &&
                    englishOptions[1].Text == "Language: English" && englishOptions[1].Checked &&
                    englishOptions.All(option => option.BackColor == SettingsForm.Elevated && option.ForeColor == Color.FromArgb(236, 238, 242)),
                $"English dropdown keeps its options and selected state dark and readable (open={selector.IsDropDownOpenForSmoke}; options={string.Join(" | ", englishOptions.Select(option => $"{option.Text}:checked={option.Checked},back={option.BackColor},fore={option.ForeColor}"))})");
            englishOptions[0].PerformClick();
            Application.DoEvents();
            Require(selector.SelectedIndex == 0 && languagePreferences.Value == "es" && languagePreferences.SaveCount == 2 &&
                    about.Text == "Acerca de EberoNest" && selector.Bounds == selectorBounds && about.ClientSize == aboutClientSize,
                "selecting Spanish restores the language immediately, persists it, and leaves About layout unchanged");

            updateService.QueueManualResults(
                new UpdateCheckResult(UpdateCheckStatus.UpToDate, MakeRelease("v1.0.1")),
                new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, MakeRelease("v1.0.3")),
                new UpdateCheckResult(UpdateCheckStatus.Failed, null));
            about.CheckForUpdatesForSmokeAsync().GetAwaiter().GetResult();
            Require(about.UpdateStatusForSmoke.Text == "EberoNest está actualizado." && !about.OpenUpdateForSmoke.Visible,
                "About manual check shows the up-to-date state");
            about.CheckForUpdatesForSmokeAsync().GetAwaiter().GetResult();
            Require(about.UpdateStatusForSmoke.Text == "Nueva versión disponible · v1.0.3" && about.OpenUpdateForSmoke.Visible &&
                    about.OpenUpdateForSmoke.Text == "Ver actualización",
                "About manual check shows an available release and the view-update action");
            about.OpenUpdatePageForSmoke();
            Require(releaseBrowser.LastOpened?.Tag == "v1.0.3", "About opens only the validated official release");
            about.CheckForUpdatesForSmokeAsync().GetAwaiter().GetResult();
            Require(about.UpdateStatusForSmoke.Text == "No se pudo comprobar si hay actualizaciones." && !about.OpenUpdateForSmoke.Visible,
                "About manual check shows the generic failure state");

            about.LanguageSelectorForSmoke.SelectedIndex = 1;
            Application.DoEvents();
            Require(form.AboutButtonForSmoke.Text == "About · v1.0.2" &&
                    form.RulesListForSmoke.AccessibleName == "Organization rules" &&
                    about.Text == "About EberoNest" &&
                    about.LanguageSelectorForSmoke.SelectedItem?.ToString() == "Language: English" &&
                    about.LanguageSelectorForSmoke.AccessibleName == "Language: English" &&
                    about.RepositoryLinkForSmoke.Text == "GitHub repository" &&
                    about.Controls.OfType<TableLayoutPanel>().SelectMany(panel => panel.Controls.OfType<Label>())
                        .Any(label => label.Text == "MIT License · Efzax"),
                "changing the About selector applies English immediately to both open windows");
            Rectangle aboutAtMinimumWithoutUpdate = form.AboutButtonForSmoke.Bounds;
            Require(form.AboutButtonForSmoke.Right == form.AboutAreaForSmoke.ClientSize.Width,
                "in English at minimum size, About remains aligned to the right edge of its footer area");
            updateService.QueueManualResults(
                new UpdateCheckResult(UpdateCheckStatus.UpToDate, MakeRelease("v1.0.1")),
                new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, MakeRelease("v1.0.3")),
                new UpdateCheckResult(UpdateCheckStatus.Failed, null));
            about.CheckForUpdatesForSmokeAsync().GetAwaiter().GetResult();
            Require(about.UpdateStatusForSmoke.Text == "EberoNest is up to date.",
                "About manual check displays the English up-to-date state");
            about.CheckForUpdatesForSmokeAsync().GetAwaiter().GetResult();
            Require(about.UpdateStatusForSmoke.Text == "Update available · v1.0.3" && about.OpenUpdateForSmoke.Visible,
                "About manual check displays the English update-available state");
            about.CheckForUpdatesForSmokeAsync().GetAwaiter().GetResult();
            Require(about.UpdateStatusForSmoke.Text == "Could not check for updates.",
                "About manual check displays the English failure state");

            updateService.PublishResult(new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, MakeRelease("v1.0.3")));
            form.PerformLayout();
            Application.DoEvents();
            Require(form.UpdateNoticeForSmoke.Text == "Update available · v1.0.3" &&
                    form.AboutButtonForSmoke.Bounds == aboutAtMinimumWithoutUpdate,
                "the English update notice does not move the About button");
            updateService.PublishResult(new UpdateCheckResult(UpdateCheckStatus.UpToDate, MakeRelease("v1.0.1")));
            form.PerformLayout();
            Application.DoEvents();
            Require(!form.UpdateNoticeForSmoke.Visible && form.AboutButtonForSmoke.Bounds == aboutAtMinimumWithoutUpdate,
                "hiding the English notice restores the original About position");
            about.LanguageSelectorForSmoke.SelectedIndex = 0;
            Application.DoEvents();
            about.Hide();

            using var aboutBitmap = new Bitmap(about.Width, about.Height);
            about.DrawToBitmap(aboutBitmap, new Rectangle(Point.Empty, about.Size));

            using var dialogIcon = LoadEmbeddedIcon("app-icon");
            VerifyRuleEditorDialogSmoke(null, organizer.Rules, dialogIcon);
            VerifyRuleEditorDialogSmoke(organizer.Rules[0], organizer.Rules, dialogIcon);
            AppLocalization.SetLanguage(AppLanguage.English);
            VerifyRuleEditorDialogSmoke(null, organizer.Rules, dialogIcon);
            VerifyRuleEditorDialogSmoke(organizer.Rules[0], organizer.Rules, dialogIcon);
            AppLocalization.SetLanguage(AppLanguage.Spanish);

            form.Activate();
            Application.DoEvents();
            form.Close();
            Application.DoEvents();
            Require(form.IsDisposed, "the settings form closes cleanly through its standard close path");
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            organizer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    });
    uiThread.SetApartmentState(ApartmentState.STA);
    uiThread.Start();
    uiThread.Join();
    if (failure is not null)
        throw new InvalidOperationException("FAIL: SettingsForm/custom rules list/Acerca de initialization smoke test", failure);
}

static void VerifyOfficialIconResources()
{
    string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    string assetsDirectory = Path.Combine(projectRoot, "assets");
    foreach (string assetName in new[] { "app-icon", "tray_normal", "tray_active", "tray_paused" })
    {
        string path = Path.Combine(assetsDirectory, assetName + ".ico");
        using var icon = new Icon(path);
        using Bitmap rendered = icon.ToBitmap();
        using var canvas = new Bitmap(Math.Max(1, rendered.Width), Math.Max(1, rendered.Height));
        using Graphics graphics = Graphics.FromImage(canvas);
        graphics.DrawImage(rendered, new Rectangle(Point.Empty, canvas.Size));
        Require(rendered.Width > 0 && rendered.Height > 0, $"{assetName}.ico loads and renders");

        using Stream resource = typeof(SettingsForm).Assembly.GetManifestResourceStream($"EberoNest.Assets.{assetName}.ico")
            ?? throw new InvalidOperationException($"FAIL: embedded {assetName}.ico resource is missing");
        using var embeddedIcon = new Icon(resource);
        using Bitmap embeddedBitmap = embeddedIcon.ToBitmap();
        Require(embeddedBitmap.Width > 0 && embeddedBitmap.Height > 0,
            $"embedded {assetName}.ico resource loads and renders");
    }

    foreach (string assetName in new[] { "app-icon", "tray_normal", "tray_active", "tray_paused" })
    {
        string path = Path.Combine(assetsDirectory, assetName + ".png");
        using Image image = Image.FromFile(path);
        using var canvas = new Bitmap(image.Width, image.Height);
        using Graphics graphics = Graphics.FromImage(canvas);
        graphics.DrawImage(image, new Rectangle(Point.Empty, canvas.Size));
        Require(image.Width == 256 && image.Height == 256, $"{assetName}.png loads and renders at 256×256");
    }

    string project = File.ReadAllText(Path.Combine(projectRoot, "EberoNest.csproj"));
    Require(project.Contains("<ApplicationIcon>assets\\app-icon.ico</ApplicationIcon>", StringComparison.Ordinal),
        "the application executable uses app-icon.ico as its embedded executable icon");
    string settingsSource = File.ReadAllText(Path.Combine(projectRoot, "SettingsForm.cs"));
    Require(settingsSource.Contains("GetManifestResourceStream(\"EberoNest.Assets.app-icon.png\")", StringComparison.Ordinal),
        "the main header and About large logos load the official app-icon.png resource");
    string trayContext = File.ReadAllText(Path.Combine(projectRoot, "TrayApplicationContext.cs"));
    Require(trayContext.Contains("Icon = trayNormalIcon", StringComparison.Ordinal) &&
            trayContext.Contains("trayIcon.Icon = trayActiveIcon", StringComparison.Ordinal),
        "the system tray assigns tray_normal.ico and tray_active.ico to their respective states");
    Require(trayContext.Contains("using Icon windowIcon = LoadEmbeddedIcon(\"app-icon\")", StringComparison.Ordinal) &&
            trayContext.Contains("Icon = (Icon)windowIcon.Clone()", StringComparison.Ordinal) &&
            !trayContext.Contains("Icon = (Icon)trayNormalIcon.Clone()", StringComparison.Ordinal),
        "the main window uses app-icon.ico and tray_normal.ico is kept out of Form.Icon");
    Require(project.Contains("EberoNest.Assets.tray_paused.ico", StringComparison.Ordinal),
        "tray_paused.ico remains embedded as a prepared resource");
}

static Icon LoadEmbeddedIcon(string assetName)
{
    using Stream stream = typeof(SettingsForm).Assembly.GetManifestResourceStream($"EberoNest.Assets.{assetName}.ico")
        ?? throw new InvalidOperationException($"FAIL: embedded {assetName}.ico resource is missing");
    return new Icon(stream);
}

static bool IconsRenderSame(Icon? actual, Icon expected)
{
    if (actual is null) return false;
    using Bitmap actualBitmap = actual.ToBitmap();
    using Bitmap expectedBitmap = expected.ToBitmap();
    if (actualBitmap.Size != expectedBitmap.Size) return false;
    for (int y = 0; y < actualBitmap.Height; y++)
        for (int x = 0; x < actualBitmap.Width; x++)
            if (actualBitmap.GetPixel(x, y) != expectedBitmap.GetPixel(x, y)) return false;
    return true;
}

static bool ImageMatchesResource(Image? actual, string resourceName)
{
    if (actual is null) return false;
    using Stream stream = typeof(SettingsForm).Assembly.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"FAIL: embedded image {resourceName} is missing");
    using Image expectedImage = Image.FromStream(stream);
    return actual.Size == expectedImage.Size && actual.Size == new Size(256, 256);
}

static void VerifyRuleEditorDialogSmoke(RuleCategory? category, IReadOnlyList<RuleCategory> existingRules, Icon icon)
{
    using var dialogIcon = (Icon)icon.Clone();
    using var editor = new RuleEditorForm(category, existingRules) { Icon = dialogIcon };
    editor.CreateControl();
    _ = editor.Handle;
    editor.Show();
    Application.DoEvents();
    string expectedTitle = AppLocalization.Get(category is null ? "category.new" : "category.edit");
    Require(editor.Text == expectedTitle, $"category editor displays the {expectedTitle} title");
    Require(IconsRenderSame(editor.Icon, icon) && editor.SmallTitleBarIconMatchesForSmoke,
        "category editor keeps app-icon.ico as Form.Icon and tray_normal.ico as ICON_SMALL");
    Require(editor.AutoScaleMode == AutoScaleMode.Dpi, "category editor scales with display DPI");
    Require(editor.FormBorderStyle == FormBorderStyle.FixedDialog && editor.ControlBox,
        "category dialogs retain their native fixed border and close control");
    if (Environment.OSVersion.Version.Build >= 22000)
        Require(editor.DarkChromeAppliedForSmoke, "category dialogs apply the native DWM dark title-bar attributes");
    Require(editor.BackColor == SettingsForm.Canvas, "category editor uses the EberoNest dark canvas");
    Require(editor.AcceptButton is Button save && save.Text == AppLocalization.Get("button.save") && save.BackColor == SettingsForm.Accent && save.ForeColor == Color.FromArgb(255, 255, 255),
        "category editor keeps the primary Save action themed in EberoNest blue");
    Require(editor.CancelButton is Button cancel && cancel.Text == AppLocalization.Get("button.cancel") && cancel.BackColor == Color.FromArgb(32, 35, 41) && cancel.ForeColor == Color.FromArgb(236, 238, 242),
        "category editor keeps the secondary Cancel action dark and readable");
    Require(editor.Controls.OfType<TableLayoutPanel>().SelectMany(panel => panel.Controls.OfType<Label>())
            .Any(label => label.Text == AppLocalization.Get("category.destination")),
        "category editor localizes its fields without changing their stored data");

    editor.CreateControl();
    editor.PerformLayout();
    Control[] controls = EnumerateControls(editor).ToArray();
    TextBox[] inputs = controls.OfType<TextBox>().ToArray();
    Require(inputs.Length == 3 && inputs.All(input => input.BackColor == SettingsForm.Elevated && input.ForeColor == Color.FromArgb(236, 238, 242)),
        "all category editor inputs use dark EberoNest surfaces and readable text");
    Require(controls.OfType<Label>().All(label => label.BackColor == SettingsForm.Canvas &&
            (label.ForeColor == Color.FromArgb(236, 238, 242) || label.ForeColor == Color.FromArgb(166, 172, 182))),
        "category editor labels use only the approved dark canvas and text colors");
    Require(controls.OfType<Label>().Any(label => label.Text == AppLocalization.Get("category.name")) &&
            controls.OfType<Label>().Any(label => label.Text == AppLocalization.Get("category.destination")) &&
            controls.OfType<Label>().Any(label => label.Text == AppLocalization.Get("category.extensions")) &&
            controls.OfType<Label>().Any(label => label.Text == AppLocalization.Get("category.extensionHelp")),
        "category editor retains its localized field labels and help text");
    if (category is null)
        Require(inputs.All(input => input.Text.Length == 0), "new category dialog starts with empty values");
    else
        Require(inputs[0].Text == category.Name && inputs[1].Text == category.FolderName && inputs[2].Text == string.Join(", ", category.Extensions),
            "edit category dialog retains the selected rule values");
    Require(controls.OfType<Button>().All(button => button.FlatStyle == FlatStyle.Flat && !button.UseVisualStyleBackColor),
        "category editor actions do not fall back to native light button styling");

    using var bitmap = new Bitmap(editor.Width, editor.Height);
    editor.DrawToBitmap(bitmap, new Rectangle(Point.Empty, editor.Size));
    editor.Hide();
}

static IEnumerable<Control> EnumerateControls(Control parent)
{
    yield return parent;
    foreach (Control child in parent.Controls)
    {
        foreach (Control nested in EnumerateControls(child))
            yield return nested;
    }
}


static void VerifyStartupAndActivation()
{
    Require(StartupArguments.Parse(Array.Empty<string>()) == StartupMode.Interactive,
        "manual launch defaults to interactive settings");
    Require(StartupArguments.Parse([StartupArguments.BackgroundArgument]) == StartupMode.Background,
        "explicit startup argument selects background mode");
    Require(StartupArguments.Parse(["--unknown"]) == StartupMode.Interactive,
        "unrecognized arguments do not silently suppress the settings window");

    string verificationId = Guid.NewGuid().ToString("N");
    string mutexName = @"Local\EberoNest.Verification." + verificationId;
    using var firstMutex = InstanceActivationSignal.CreateMutex(mutexName, out bool isFirstInstance);
    Require(isFirstInstance, "first execution acquires the unique-instance mutex");
    using var secondMutex = InstanceActivationSignal.CreateMutex(mutexName, out bool isSecondInstance);
    Require(!isSecondInstance, "second execution is rejected by the existing unique-instance mutex");

    string signalName = @"Local\EberoNest.Verification." + verificationId + ".Signal";
    using var listener = new EventWaitHandle(false, EventResetMode.AutoReset, signalName);
    Require(!listener.WaitOne(0), "activation signal starts unsignaled");
    Require(InstanceActivationSignal.TrySignal(signalName),
        "a second instance can open and signal the existing instance activation event");
    Require(listener.WaitOne(1000), "existing instance receives the activation signal");
    Require(!listener.WaitOne(0), "activation signal resets after it is received");
}
static void VerifyLegacyRulesMigration(string root)
{
    string localAppData = Path.Combine(root, "local-app-data");
    string canonical = RulesStore.GetCanonicalPath(localAppData);
    string dropNestLegacy = Path.Combine(localAppData, "Ebero DropNest", "rules.json");
    string downloadsLegacy = Path.Combine(localAppData, "DownloadsOrganizer", "rules.json");
    Directory.CreateDirectory(Path.GetDirectoryName(dropNestLegacy)!);
    Directory.CreateDirectory(Path.GetDirectoryName(downloadsLegacy)!);
    string dropNestJson = System.Text.Json.JsonSerializer.Serialize(new RulesDocument { Categories = RulesStore.DefaultRules() });
    List<RuleCategory> downloadsRules = RulesStore.DefaultRules();
    downloadsRules.Single(rule => rule.Name == "Documents").FolderName = "Legacy Documents";
    string downloadsJson = System.Text.Json.JsonSerializer.Serialize(new RulesDocument { Categories = downloadsRules });
    File.WriteAllText(dropNestLegacy, dropNestJson);
    File.WriteAllText(downloadsLegacy, downloadsJson);
    Require(RulesStore.ResolvePath(localAppData) == canonical && File.ReadAllText(canonical) == dropNestJson, "Ebero DropNest takes priority over DownloadsOrganizer rules");
    Require(File.ReadAllText(dropNestLegacy) == dropNestJson && File.ReadAllText(downloadsLegacy) == downloadsJson, "migration preserves both legacy sources");

    string existingRoot = Path.Combine(root, "canonical-priority");
    string existingCanonical = RulesStore.GetCanonicalPath(existingRoot);
    Directory.CreateDirectory(Path.GetDirectoryName(existingCanonical)!);
    File.WriteAllText(existingCanonical, "newer EberoNest rules");
    string oldOne = Path.Combine(existingRoot, "Ebero DropNest", "rules.json");
    string oldTwo = Path.Combine(existingRoot, "DownloadsOrganizer", "rules.json");
    Directory.CreateDirectory(Path.GetDirectoryName(oldOne)!);
    Directory.CreateDirectory(Path.GetDirectoryName(oldTwo)!);
    File.WriteAllText(oldOne, dropNestJson);
    File.WriteAllText(oldTwo, downloadsJson);
    Require(RulesStore.ResolvePath(existingRoot) == existingCanonical && File.ReadAllText(existingCanonical) == "newer EberoNest rules", "canonical rules are never overwritten by either legacy source");

    string downloadsOnlyRoot = Path.Combine(root, "downloads-only");
    string downloadsOnly = Path.Combine(downloadsOnlyRoot, "DownloadsOrganizer", "rules.json");
    Directory.CreateDirectory(Path.GetDirectoryName(downloadsOnly)!);
    File.WriteAllText(downloadsOnly, downloadsJson);
    string resolvedDownloads = RulesStore.ResolvePath(downloadsOnlyRoot);
    Require(resolvedDownloads == RulesStore.GetCanonicalPath(downloadsOnlyRoot) && File.ReadAllText(resolvedDownloads) == downloadsJson && File.ReadAllText(downloadsOnly) == downloadsJson, "DownloadsOrganizer migrates when Ebero DropNest is absent and its source remains intact");
}static async Task WaitForAsync(Func<bool> condition, string assertion)
{
    for (int i = 0; i < 100; i++)
    {
        if (condition())
            return;
        await Task.Delay(100);
    }
    throw new InvalidOperationException("FAIL: " + assertion);
}

static void Require(bool condition, string assertion)
{
    if (!condition)
        throw new InvalidOperationException("FAIL: " + assertion);
}

static async Task VerifyReparsePointHandling(string root)
{
    string fixtureRoot = Path.Combine(root, "reparse-fixtures");
    string downloads = Path.Combine(fixtureRoot, "Downloads");
    string insideTarget = Path.Combine(downloads, "JunctionTarget");
    string outsideTarget = Path.Combine(fixtureRoot, "OutsideDownloads");
    string documentsLink = Path.Combine(downloads, "Documents");
    string imagesLink = Path.Combine(downloads, "Images");
    string internalSource = Path.Combine(downloads, "junction-internal.pdf");
    string externalSource = Path.Combine(downloads, "junction-external.png");
    string normalSource = Path.Combine(downloads, "normal-after-rejection.mp4");
    string symlinkSource = Path.Combine(downloads, "linked-source.pdf");
    string symlinkTarget = Path.Combine(outsideTarget, "actual.pdf");
    Directory.CreateDirectory(insideTarget);
    Directory.CreateDirectory(outsideTarget);
    try
    {
        CreateJunction(documentsLink, insideTarget);
        CreateJunction(imagesLink, outsideTarget);
        await using var organizer = new Organizer(downloads, RulesStore.DefaultRules());
        organizer.Start();
        File.WriteAllText(internalSource, "internal junction source");
        File.WriteAllText(externalSource, "external junction source");
        await Task.Delay(TimeSpan.FromSeconds(4));
        Require(File.Exists(internalSource) && File.ReadAllText(internalSource) == "internal junction source" && !File.Exists(Path.Combine(insideTarget, Path.GetFileName(internalSource))),
            "a destination junction inside the Downloads root is rejected and leaves its source intact");
        Require(File.Exists(externalSource) && File.ReadAllText(externalSource) == "external junction source" && !File.Exists(Path.Combine(outsideTarget, Path.GetFileName(externalSource))),
            "a destination junction pointing outside Downloads is rejected without moving or copying the source");

        File.WriteAllText(normalSource, "normal source");
        await WaitForAsync(() => File.Exists(Path.Combine(downloads, "Videos", Path.GetFileName(normalSource))),
            "watcher continues organizing normal files after rejecting junction destinations");

        bool sourceLinkCreated = false;
        try
        {
            File.WriteAllText(symlinkTarget, "external target remains unchanged");
            File.CreateSymbolicLink(symlinkSource, symlinkTarget);
            sourceLinkCreated = true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            Console.WriteLine("INFO: file symlink fixture unavailable; source-link rejection case skipped on this Windows configuration.");
        }
        if (sourceLinkCreated)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            Require(File.Exists(symlinkSource) && (File.GetAttributes(symlinkSource) & FileAttributes.ReparsePoint) != 0,
                "a source file symlink is rejected and remains in Downloads as a link");
            Require(File.ReadAllText(symlinkTarget) == "external target remains unchanged",
                "rejecting a source file symlink does not modify its external target");
        }
    }
    finally
    {
        if (File.Exists(symlinkSource)) File.Delete(symlinkSource);
        foreach (string link in new[] { documentsLink, imagesLink })
        {
            if (Directory.Exists(link)) Directory.Delete(link, recursive: false);
        }
    }
}

static void CreateJunction(string linkPath, string targetPath)
{
    var startInfo = new System.Diagnostics.ProcessStartInfo
    {
        FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe",
        UseShellExecute = false,
        CreateNoWindow = true
    };
    startInfo.ArgumentList.Add("/c");
    startInfo.ArgumentList.Add("mklink");
    startInfo.ArgumentList.Add("/J");
    startInfo.ArgumentList.Add(linkPath);
    startInfo.ArgumentList.Add(targetPath);
    using System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not create junction test fixture.");
    process.WaitForExit();
    Require(process.ExitCode == 0 && Directory.Exists(linkPath) &&
            (File.GetAttributes(linkPath) & FileAttributes.ReparsePoint) != 0,
        "Windows junction fixture is created as a reparse point");
}
static void VerifyRulesPersistence(string root)
{
    string configPath = Path.Combine(root, "settings", "rules.json");
    List<RuleCategory> defaults = RulesStore.Load(configPath);
    Require(defaults.Count == 8, "first run creates eight approved default categories");
    Require(defaults.Any(rule => rule.Name == "Documents" && rule.Extensions.Contains(".csv")),
        "default Documents rule includes CSV");
    Require(!defaults.Any(rule => rule.Name.Equals("Others", StringComparison.OrdinalIgnoreCase)),
        "defaults do not create an Others rule");

    var changed = RulesStore.Clone(defaults);
    RuleCategory cad = changed.Single(rule => rule.Name == "CAD");
    cad.FolderName = "CAD Models";
    cad.Extensions.Add(".STEP");
    changed.Add(new RuleCategory
    {
        Name = "Design Assets", FolderName = "Design Assets", Extensions = [".blend"], IsBuiltIn = false
    });
    RulesStore.Save(changed, configPath);
    List<RuleCategory> persisted = RulesStore.Load(configPath);
    Require(persisted.Single(rule => rule.Name == "CAD").FolderName == "CAD Models" &&
            persisted.Single(rule => rule.Name == "CAD").Extensions.Contains(".step"),
        "edited destination and extensions persist in JSON");
    Require(persisted.Any(rule => rule.Name == "Design Assets" && rule.Extensions.Contains(".blend")),
        "custom rule persists in JSON");

    cad = persisted.Single(rule => rule.Name == "CAD");
    cad.Extensions.Add(".csv");
    bool duplicateRejected = false;
    try { RulesStore.Save(persisted, configPath); }
    catch (InvalidDataException) { duplicateRejected = true; }
    Require(duplicateRejected, "duplicate extensions are rejected safely");

    persisted.Single(rule => rule.Name == "CAD").FolderName = "..\\outside";
    bool unsafeDestinationRejected = false;
    try { RulesStore.Save(persisted, configPath); }
    catch (InvalidDataException) { unsafeDestinationRejected = true; }
    Require(unsafeDestinationRejected, "destination traversal is rejected");

    string malformedPath = Path.Combine(root, "settings", "malformed-rules.json");
    byte[][] malformedDocuments =
    [
        System.Text.Encoding.UTF8.GetBytes("{ invalid json"),
        System.Text.Encoding.UTF8.GetBytes("""{"Version":1,"Categories":null}"""),
        System.Text.Encoding.UTF8.GetBytes("""{"Version":1,"Categories":[{"Name":"Broken","FolderName":"Broken","Extensions":null}]}"""),
        System.Text.Encoding.UTF8.GetBytes("""{"Version":1,"Categories":[{"Name":"Broken","FolderName":"Broken","Extensions":[null]}]}"""),
        System.Text.Encoding.UTF8.GetBytes("""{"Version":99,"Categories":[]}""")
    ];
    for (int index = 0; index < malformedDocuments.Length; index++)
    {
        string casePath = malformedPath + "." + index;
        byte[] originalBytes = malformedDocuments[index];
        File.WriteAllBytes(casePath, originalBytes);
        List<RuleCategory> fallbackRules = RulesStore.Load(casePath);
        Require(fallbackRules.Count == 8 && File.ReadAllBytes(casePath).SequenceEqual(originalBytes),
            "malformed, structurally invalid, or unsupported rules fall back without altering the source");
        string staleTemporary = casePath + ".tmp";
        File.WriteAllText(staleTemporary, "preserve pre-existing temporary sibling");
        RulesStore.Save(fallbackRules, casePath);
        string[] backups = Directory.GetFiles(Path.GetDirectoryName(casePath)!, Path.GetFileName(casePath) + ".invalid-*.bak");
        Require(backups.Length == 1 && File.ReadAllBytes(backups[0]).SequenceEqual(originalBytes),
            "saving fallback rules preserves the exact invalid rules bytes in a unique backup");
        Require(File.ReadAllText(staleTemporary) == "preserve pre-existing temporary sibling",
            "rules save does not truncate a pre-existing fixed-name temporary sibling");
        Require(RulesStore.Load(casePath).Count == 8, "rules saved after malformed input remain readable");
    }
}

sealed class FakeAutomaticUpdateCheckState : IAutomaticUpdateCheckState
{
    public DateOnly? LastDate { get; private set; }
    public int SaveCount { get; private set; }
    public DateOnly? ReadLastSuccessfulCheckDate() => LastDate;
    public void SaveLastSuccessfulCheckDate(DateOnly date) { LastDate = date; SaveCount++; }
}

sealed class FakeLatestReleaseClient(Func<CancellationToken, Task<OfficialRelease>> getLatest) : ILatestReleaseClient
{
    public int CallCount { get; private set; }

    public Task<OfficialRelease> GetLatestStableReleaseAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;
        return getLatest(cancellationToken);
    }
}

sealed class FakeUpdateNotificationService : IUpdateNotificationService
{
    private readonly Queue<UpdateCheckResult> manualResults = new();
    public event Action<UpdateCheckResult>? CheckCompleted;
    public UpdateCheckResult? LastSuccessfulResult { get; private set; }

    public Task CheckAutomaticallyAsync() => Task.CompletedTask;

    public Task<UpdateCheckResult> CheckManuallyAsync()
    {
        UpdateCheckResult result = manualResults.Count > 0
            ? manualResults.Dequeue()
            : new UpdateCheckResult(UpdateCheckStatus.UpToDate, null);
        if (result.Status != UpdateCheckStatus.Failed)
        {
            LastSuccessfulResult = result;
            CheckCompleted?.Invoke(result);
        }
        return Task.FromResult(result);
    }

    internal void QueueManualResults(params UpdateCheckResult[] results)
    {
        foreach (UpdateCheckResult result in results) manualResults.Enqueue(result);
    }

    internal void PublishResult(UpdateCheckResult result)
    {
        if (result.Status != UpdateCheckStatus.Failed) LastSuccessfulResult = result;
        CheckCompleted?.Invoke(result);
    }
}

sealed class FakeReleaseBrowser : IReleaseBrowser
{
    public OfficialRelease? LastOpened { get; private set; }
    public bool TryOpen(OfficialRelease release) { LastOpened = release; return true; }
}

sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        sendAsync(request, cancellationToken);
}

sealed class FakeLanguagePreferenceStore(string? value = null) : ILanguagePreferenceStore
{
    internal string? Value { get; set; } = value;
    internal int SaveCount { get; private set; }
    public string? Load() => Value;
    public void Save(AppLanguage language)
    {
        Value = language == AppLanguage.Spanish ? "es" : "en";
        SaveCount++;
    }
}

sealed class FakeStartupEntryStore : IStartupEntryStore
{
    internal Dictionary<string, StartupEntry> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal int WriteCount { get; private set; }
    internal int DeleteCount { get; private set; }
    public bool Exists(string shortcutFileName) => Entries.ContainsKey(shortcutFileName);
    public StartupEntry? Read(string shortcutFileName) => Entries.GetValueOrDefault(shortcutFileName);
    public void Write(string shortcutFileName, StartupEntry entry) { Entries[shortcutFileName] = entry; WriteCount++; }
    public void Delete(string shortcutFileName) { if (Entries.Remove(shortcutFileName)) DeleteCount++; }
}sealed class FakeStartupManager : IStartupManager
{
    public bool IsEnabled => false;
    public void Enable() => throw new InvalidOperationException("UI smoke test must not enable real startup.");
    public void Disable() => throw new InvalidOperationException("UI smoke test must not disable real startup.");
}
