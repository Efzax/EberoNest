using System.Runtime.InteropServices;

namespace EberoNest;

internal class EberoNestForm : Form
{
    private const int WmSetIcon = 0x0080;
    private const int WmGetIcon = 0x007F;
    private const int IconSmall = 0;
    private const int WmThemeChanged = 0x031A;
    private const int WmDpiChanged = 0x02E0;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private bool darkChromeApplied;
    private Icon? titleBarIcon;

    internal bool DarkChromeAppliedForSmoke => darkChromeApplied;

    internal string SmallTitleBarIconDiagnosticForSmoke => GetSmallTitleBarIconDiagnostic();

    internal bool SmallTitleBarIconMatchesForSmoke
    {
        get
        {
            return GetSmallTitleBarIconDiagnostic() == "match";
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplySmallTitleBarIcon();
        ApplyDarkChrome();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplySmallTitleBarIcon();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (OperatingSystem.IsWindows() && IsHandleCreated)
            _ = SendMessage(Handle, WmSetIcon, new IntPtr(IconSmall), IntPtr.Zero);
        titleBarIcon?.Dispose();
        titleBarIcon = null;
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg is WmThemeChanged or WmDpiChanged)
            ApplyDarkChrome();
    }

    private void ApplyDarkChrome()
    {
        darkChromeApplied = DarkWindowChrome.TryApply(Handle);
    }

    private void ApplySmallTitleBarIcon()
    {
        if (!OperatingSystem.IsWindows()) return;
        if (titleBarIcon is null)
        {
            using Stream stream = typeof(EberoNestForm).Assembly.GetManifestResourceStream("EberoNest.Assets.tray_normal.ico")
                ?? throw new InvalidOperationException("No se encontró el icono compacto de la barra de título.");
            titleBarIcon = new Icon(stream);
        }
        _ = SendMessage(Handle, WmSetIcon, new IntPtr(IconSmall), titleBarIcon.Handle);
    }

    private string GetSmallTitleBarIconDiagnostic()
    {
        if (!OperatingSystem.IsWindows()) return "not-windows";
        if (titleBarIcon is null) return "resource-not-loaded";
        IntPtr nativeHandle = SendMessage(Handle, WmGetIcon, new IntPtr(IconSmall), IntPtr.Zero);
        if (nativeHandle == IntPtr.Zero) return "WM_GETICON returned a null handle";
        using Bitmap actual = Icon.FromHandle(nativeHandle).ToBitmap();
        using Bitmap expected = titleBarIcon.ToBitmap();
        if (actual.Size != expected.Size) return $"size actual={actual.Size}, expected={expected.Size}";
        for (int y = 0; y < actual.Height; y++)
            for (int x = 0; x < actual.Width; x++)
                if (actual.GetPixel(x, y) != expected.GetPixel(x, y))
                    return $"pixel mismatch at {x},{y}: actual={actual.GetPixel(x, y)}, expected={expected.GetPixel(x, y)}";
        return "match";
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW", PreserveSig = true)]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
}

internal static class DarkWindowChrome
{
    internal static bool TryApply(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !OperatingSystem.IsWindows()) return false;
        try
        {
            int enabled = 1;
            int result = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
            if (result != 0)
                result = DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
            if (result != 0) return false;

            int captionColor = ToColorRef(SettingsForm.Canvas);
            int textColor = ToColorRef(Color.FromArgb(236, 238, 242));
            _ = DwmSetWindowAttribute(handle, 35, ref captionColor, sizeof(int));
            _ = DwmSetWindowAttribute(handle, 36, ref textColor, sizeof(int));
            return true;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or
                                          ExternalException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
