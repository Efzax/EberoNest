namespace EberoNest;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Organizer organizer;
    private readonly NotifyIcon trayIcon;
    private readonly Icon appIcon;
    private readonly Icon trayNormalIcon;
    private readonly Icon trayActiveIcon;
    private SettingsForm? settings;
    private bool exiting;
    private readonly System.Windows.Forms.Timer activationTimer;
    private readonly EventWaitHandle activationSignal;

    internal TrayApplicationContext(
        string downloadsPath, IEnumerable<RuleCategory> rules, EventWaitHandle activationSignal, bool showSettingsOnStart)
    {
        this.activationSignal = activationSignal;
        organizer = new Organizer(downloadsPath, rules);
        appIcon = LoadEmbeddedIcon("app-icon");
        trayNormalIcon = LoadEmbeddedIcon("tray_normal");
        trayActiveIcon = LoadEmbeddedIcon("tray_active");
        organizer.Start();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir configuración", null, (_, _) => OpenSettings());
        menu.Items.Add("Organizar ahora", null, async (_, _) => await OrganizeNowAsync(null));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, async (_, _) => await ExitAsync());

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
            settings = new SettingsForm(organizer, OrganizeNowAsync,
                new StartupManager(new WindowsStartupEntryStore(), StartupManager.GetCurrentExecutablePath()))
                { Icon = appIcon };
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
            MessageBox.Show(owner, "No se pudo revisar la carpeta de Descargas.\n" + ex.Message,
                "EberoNest", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        int total = plan.Values.Sum();
        if (total == 0)
        {
            MessageBox.Show(owner, "No hay archivos organizables en la raíz de Descargas.",
                "EberoNest", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string summary = string.Join(Environment.NewLine, plan.OrderBy(x => x.Key)
            .Select(x => $"  {x.Key}: {x.Value}"));
        var answer = MessageBox.Show(owner,
            $"Se encontraron {total} archivos en la raíz de Descargas:\n\n{summary}\n\n¿Organizarlos ahora?",
            "Organizar archivos existentes", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        trayIcon.Icon = trayActiveIcon;
        try
        {
            int moved = await organizer.OrganizeExistingFilesAsync();
            MessageBox.Show(owner, $"Se organizaron {moved} de {total} archivos.",
                "EberoNest", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner, "La organización se interrumpió.\n" + ex.Message,
                "EberoNest", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        activationTimer.Stop();
        activationTimer.Dispose();
        trayIcon.Visible = false;
        settings?.Close();
        trayIcon.Dispose();
        appIcon.Dispose();
        trayNormalIcon.Dispose();
        trayActiveIcon.Dispose();
        await organizer.DisposeAsync();
        ExitThread();
    }

    private static Icon LoadEmbeddedIcon(string assetName)
    {
        string resourceName = $"EberoNest.Assets.{assetName}.ico";
        using Stream stream = typeof(TrayApplicationContext).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"No se encontró el icono integrado: {resourceName}");
        return new Icon(stream);
    }
}