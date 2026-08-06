using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WHDClient.Services;

/// <summary>
/// Enables the dark immersive title bar on a window via DWM so it matches the app theme.
/// </summary>
public static class DarkTitleBar
{
    // Attribute 20 is used by Windows 11 and recent Windows 10 builds; 19 by older Windows 10 builds.
    private const int DwmwaUseImmersiveDarkModeOld = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void Apply(Window window)
    {
        bool dark = ThemeService.IsDark;
        if (window.IsLoaded)
            ApplyCore(new WindowInteropHelper(window).Handle, dark);
        else
            window.SourceInitialized += (_, _) => ApplyCore(new WindowInteropHelper(window).Handle, dark);
    }

    private static void ApplyCore(IntPtr hwnd, bool dark)
    {
        if (hwnd == IntPtr.Zero) return;
        int useDark = dark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeOld, ref useDark, sizeof(int));
    }
}
