namespace EberoNest;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Organizer organizer;
    private readonly NotifyIcon trayIcon;
    private readonly Icon trayNormalIcon;
    private readonly Icon trayActiveIcon;
    private readonly UpdateNotificationService updateNotifications;
    private readonly IReleaseBrowser releaseBrowser = new OfficialReleaseBrowser();
    private readonly ToolStripMenuItem openSettingsMenu = new();
    private readonly ToolStripMenuItem organizeMenu = new();
    private readonly ToolStripMenuItem exitMenu = new();
    private SettingsForm? settings;
    private bool exiting;
    private readonly System.Windows.Forms.Timer activationTimer;
    private readonly EventWaitHandle activationSignal;

    internal TrayApplicationContext(
        string downloadsPath, IEnumerable<RuleCategory> rules, EventWaitHandle activationSignal, bool showSettingsOnStart)
    {
        this.activationSignal = activationSignal;
        organizer = new Organizer(downloadsPath, rules);
        trayNormalIcon = LoadEmbeddedIcon("tray_normal");
        trayActiveIcon = LoadEmbeddedIcon("tray_active");
        organizer.Start();
        SemanticVersion currentVersion = AppVersion.CurrentVersion;
        updateNotifications = new UpdateNotificationService(currentVersion,
            new GitHubLatestReleaseClient(currentVersion), new FileAutomaticUpdateCheckState());
        _ = Task.Run(updateNotifications.CheckAutomaticallyAsync);

        var menu = new ContextMenuStrip();
        UpdateTrayMenuText();
        openSettingsMenu.Click += (_, _) => OpenSettings();
        organizeMenu.Click += async (_, _) => await OrganizeNowAsync(null);
        exitMenu.Click += async (_, _) => await ExitAsync();
        menu.Items.Add(openSettingsMenu);
        menu.Items.Add(organizeMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitMenu);
        AppLocalization.LanguageChanged += Localization_LanguageChanged;

        trayIcon = new NotifyIcon
        {
            Icon = trayNormalIcon,
            Text = "EberoNest",
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) OpenSettings();
        };

        activationTimer = new System.Windows.Forms.Timer { Interval = 200 };
        activationTimer.Tick += (_, _) =>
        {
            if (this.activationSignal.WaitOne(0)) OpenSettings();
        };
        activationTimer.Start();

        if (showSettingsOnStart) OpenSettings();
    }

    private void OpenSettings()
    {
        if (exiting) return;
        if (settings is null || settings.IsDisposed)
        {
            using Icon windowIcon = LoadEmbeddedIcon("app-icon");
            settings = new SettingsForm(organizer, OrganizeNowAsync,
                new StartupManager(new WindowsStartupEntryStore(), StartupManager.GetCurrentExecutablePath()),
                updateNotifications, releaseBrowser)
                { Icon = (Icon)windowIcon.Clone() };
            settings.FormClosing += (_, e) =>
            {
                if (!exiting && e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    settings.Hide();
                }
            };
        }
        if (!settings.Visible) settings.Show();
        settings.WindowState = FormWindowState.Normal;
        settings.Activate();
    }

    private async Task OrganizeNowAsync(IWin32Window? owner)
    {
        IReadOnlyDictionary<string, int> plan;
        try { plan = organizer.PreviewExistingFiles(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner, AppLocalization.Get("organize.error.review") + "\n" + ex.Message,
                AppLocalization.Get("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        int total = plan.Values.Sum();
        if (total == 0)
        {
            MessageBox.Show(owner, AppLocalization.Get("organize.none"),
                AppLocalization.Get("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string summary = string.Join(Environment.NewLine, plan.OrderBy(x => x.Key)
            .Select(x => $"  {x.Key}: {x.Value}"));
        var answer = MessageBox.Show(owner,
            AppLocalization.Get("organize.confirm", total, summary),
            AppLocalization.Get("organize.confirmTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        trayIcon.Icon = trayActiveIcon;
        try
        {
            int moved = await organizer.OrganizeExistingFilesAsync();
            MessageBox.Show(owner, AppLocalization.Get("organize.completed", moved, total),
                AppLocalization.Get("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner, AppLocalization.Get("organize.error.interrupted") + "\n" + ex.Message,
                AppLocalization.Get("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (!exiting) trayIcon.Icon = trayNormalIcon;
        }
    }

    private async Task ExitAsync()
    {
        if (exiting) return;
        exiting = true;
        AppLocalization.LanguageChanged -= Localization_LanguageChanged;
        activationTimer.Stop();
        activationTimer.Dispose();
        updateNotifications.Dispose();
        trayIcon.Visible = false;
        settings?.Close();
        trayIcon.Dispose();
        trayNormalIcon.Dispose();
        trayActiveIcon.Dispose();
        await organizer.DisposeAsync();
        ExitThread();
    }

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        UpdateTrayMenuText();
    }

    private void UpdateTrayMenuText()
    {
        openSettingsMenu.Text = AppLocalization.Get("tray.open");
        organizeMenu.Text = AppLocalization.Get("tray.organize");
        exitMenu.Text = AppLocalization.Get("tray.exit");
    }

    private static Icon LoadEmbeddedIcon(string assetName)
    {
        string resourceName = $"EberoNest.Assets.{assetName}.ico";
        using Stream stream = typeof(TrayApplicationContext).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"No se encontró el icono integrado: {resourceName}");
        return new Icon(stream);
    }
}
