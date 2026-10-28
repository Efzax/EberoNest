namespace EberoNest;

internal enum StartupMode
{
    Interactive,
    Background
}

internal static class StartupArguments
{
    internal const string BackgroundArgument = "--background";

    internal static StartupMode Parse(IEnumerable<string> arguments) =>
        arguments.Any(argument => string.Equals(
            argument, BackgroundArgument, StringComparison.OrdinalIgnoreCase))
            ? StartupMode.Background
            : StartupMode.Interactive;
}

internal static class InstanceActivationSignal
{
    internal const string MutexName = @"Local\DownloadsOrganizer.Instance";
    internal const string Name = @"Local\EberoDropNest.ShowSettings";

    internal static Mutex CreateMutex(string name, out bool createdNew) =>
        new(initiallyOwned: true, name, out createdNew);

    internal static bool TrySignal(string name = Name)
    {
        const int attempts = 20;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                using EventWaitHandle signal = EventWaitHandle.OpenExisting(name);
                signal.Set();
                return true;
            }
            catch (WaitHandleCannotBeOpenedException) when (attempt + 1 < attempts)
            {
                Thread.Sleep(50);
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
        }
        return false;
    }
}