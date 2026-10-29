using Microsoft.Win32;

namespace EberoNest;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        StartupMode startupMode = StartupArguments.Parse(args);
        using var instanceMutex = InstanceActivationSignal.CreateMutex(InstanceActivationSignal.MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            InstanceActivationSignal.TrySignal();
            return;
        }

        AppLocalization.Initialize();

        using var activationSignal = new EventWaitHandle(
            initialState: false, EventResetMode.AutoReset, InstanceActivationSignal.Name);

        ApplicationConfiguration.Initialize();
        string downloadsPath = GetDownloadsPath();
        if (!Directory.Exists(downloadsPath))
        {
            MessageBox.Show($"{AppLocalization.Get("downloads.missing")}\n{downloadsPath}",
                AppLocalization.Get("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        List<RuleCategory> rules = RulesStore.Load();
        using var context = new TrayApplicationContext(
            downloadsPath, rules, activationSignal, startupMode == StartupMode.Interactive);
        Application.Run(context);
    }

    private static string GetDownloadsPath()
    {
        const string downloadsId = "{374DE290-123F-4565-9164-39C4925E467B}";
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
            string? configured = key?.GetValue(downloadsId) as string;
            if (!string.IsNullOrWhiteSpace(configured)) return Environment.ExpandEnvironmentVariables(configured);
        }
        catch (UnauthorizedAccessException) { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
}
