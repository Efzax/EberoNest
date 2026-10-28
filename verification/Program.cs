using EberoNest;

string temporaryRoot = Path.Combine(Path.GetTempPath(), "EberoNest-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporaryRoot);
try
{
    Require(RulesStore.DefaultPath == RulesStore.GetCanonicalPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)), "default rules path uses the EberoNest data directory");
    VerifyStartupAndActivation();
    VerifyStartupManagement(temporaryRoot);
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
static void VerifySettingsFormSmoke(string temporaryRoot)
{
    string downloadsPath = Path.Combine(temporaryRoot, "settings-form-smoke");
    Directory.CreateDirectory(downloadsPath);
    Exception? failure = null;
    var uiThread = new Thread(() =>
    {
        Organizer organizer = new(downloadsPath, RulesStore.DefaultRules());
        try
        {
            using var form = new SettingsForm(organizer, _ => Task.CompletedTask, new FakeStartupManager());
            Require(form.Text == "EberoNest", "settings form uses the canonical product name");
            form.CreateControl();
            form.PerformLayout();
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

            using var about = new AboutForm(form.Icon);
            Require(about.Text == "Acerca de EberoNest", "About uses the canonical product name");
            Require(about.Controls.OfType<TableLayoutPanel>().SelectMany(panel => panel.Controls.OfType<Label>()).Any(label => label.Text == AppVersion.Display), "About displays the current product version metadata");
            about.CreateControl();
            about.PerformLayout();
            using var aboutBitmap = new Bitmap(about.Width, about.Height);
            about.DrawToBitmap(aboutBitmap, new Rectangle(Point.Empty, about.Size));

            using Stream iconStream = typeof(SettingsForm).Assembly.GetManifestResourceStream("EberoNest.Assets.app-icon.ico")
                ?? throw new InvalidOperationException("FAIL: category dialogs can load the official EberoNest icon resource");
            using var dialogIcon = new Icon(iconStream);
            VerifyRuleEditorDialogSmoke(null, organizer.Rules, dialogIcon);
            VerifyRuleEditorDialogSmoke(organizer.Rules[0], organizer.Rules, dialogIcon);
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

static void VerifyRuleEditorDialogSmoke(RuleCategory? category, IReadOnlyList<RuleCategory> existingRules, Icon icon)
{
    using var dialogIcon = (Icon)icon.Clone();
    using var editor = new RuleEditorForm(category, existingRules) { Icon = dialogIcon };
    string expectedTitle = category is null ? "Nueva categoría" : "Editar categoría";
    Require(editor.Text == expectedTitle, $"category editor displays the {expectedTitle} title");
    Require(editor.Icon is not null, "category editor has the official application icon");
    Require(editor.AutoScaleMode == AutoScaleMode.Dpi, "category editor scales with display DPI");
    Require(editor.BackColor == SettingsForm.Canvas, "category editor uses the EberoNest dark canvas");
    Require(editor.AcceptButton is Button save && save.Text == "Guardar" && save.BackColor == SettingsForm.Accent && save.ForeColor == Color.FromArgb(255, 255, 255),
        "category editor keeps the primary Save action themed in EberoNest blue");
    Require(editor.CancelButton is Button cancel && cancel.Text == "Cancelar" && cancel.BackColor == Color.FromArgb(32, 35, 41) && cancel.ForeColor == Color.FromArgb(236, 238, 242),
        "category editor keeps the secondary Cancel action dark and readable");

    editor.CreateControl();
    editor.PerformLayout();
    Control[] controls = EnumerateControls(editor).ToArray();
    TextBox[] inputs = controls.OfType<TextBox>().ToArray();
    Require(inputs.Length == 3 && inputs.All(input => input.BackColor == SettingsForm.Elevated && input.ForeColor == Color.FromArgb(236, 238, 242)),
        "all category editor inputs use dark EberoNest surfaces and readable text");
    Require(controls.OfType<Label>().All(label => label.BackColor == SettingsForm.Canvas &&
            (label.ForeColor == Color.FromArgb(236, 238, 242) || label.ForeColor == Color.FromArgb(166, 172, 182))),
        "category editor labels use only the approved dark canvas and text colors");
    Require(controls.OfType<Label>().Any(label => label.Text == "Nombre") &&
            controls.OfType<Label>().Any(label => label.Text == "Carpeta de destino") &&
            controls.OfType<Label>().Any(label => label.Text == "Extensiones") &&
            controls.OfType<Label>().Any(label => label.Text == "Separa extensiones con coma o espacio, por ejemplo: stl, 3mf, obj"),
        "category editor retains all existing field labels and help text");
    if (category is null)
        Require(inputs.All(input => input.Text.Length == 0), "new category dialog starts with empty values");
    else
        Require(inputs[0].Text == category.Name && inputs[1].Text == category.FolderName && inputs[2].Text == string.Join(", ", category.Extensions),
            "edit category dialog retains the selected rule values");
    Require(controls.OfType<Button>().All(button => button.FlatStyle == FlatStyle.Flat && !button.UseVisualStyleBackColor),
        "category editor actions do not fall back to native light button styling");

    using var bitmap = new Bitmap(editor.Width, editor.Height);
    editor.DrawToBitmap(bitmap, new Rectangle(Point.Empty, editor.Size));
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
