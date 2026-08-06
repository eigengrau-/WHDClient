using System;
using System.Windows;

namespace WHDClient.Services;

/// <summary>
/// Swaps the app-wide theme dictionary and font size at runtime. The theme dictionaries
/// define the same resource keys (brushes, keyed styles); views reference them via
/// DynamicResource so swapping here restyles already-open windows immediately.
/// </summary>
public static class ThemeService
{
    public const string DarkTheme = "Dark";
    public const string LightTheme = "Light";

    public const double DefaultFontSize = 14;
    public const double MinFontSize = 10;
    public const double MaxFontSize = 24;

    private const double MinDerivedFontSize = 8;

    public static string CurrentTheme { get; private set; } = DarkTheme;
    public static double CurrentFontSize { get; private set; } = DefaultFontSize;

    public static bool IsDark => CurrentTheme == DarkTheme;

    /// <summary>Applies the theme (name from <see cref="DarkTheme"/>/<see cref="LightTheme"/>) and font size live.</summary>
    public static void Apply(string theme, double fontSize)
    {
        CurrentTheme = theme == LightTheme ? LightTheme : DarkTheme;
        CurrentFontSize = Math.Clamp(fontSize, MinFontSize, MaxFontSize);

        var app = Application.Current;
        if (app == null) return;

        // Replace whichever theme dictionary is currently merged (Dark or Light).
        var merged = app.Resources.MergedDictionaries;
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source?.OriginalString;
            if (src != null && src.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase))
                merged.RemoveAt(i);
        }
        merged.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{CurrentTheme}Theme.xaml")
        });

        // Font size cascades from the Window style; keyed text styles derive from the base.
        app.Resources["AppFontSize"] = CurrentFontSize;
        app.Resources["HeaderFontSize"] = ClampDerived(CurrentFontSize + 6);
        app.Resources["SubHeaderFontSize"] = ClampDerived(CurrentFontSize);
        app.Resources["DimFontSize"] = ClampDerived(CurrentFontSize - 3);
        app.Resources["FieldFontSize"] = ClampDerived(CurrentFontSize - 4);

        // Re-color the immersive title bar to match (light themes need a light title bar).
        foreach (Window w in app.Windows)
            DarkTitleBar.Apply(w);
    }

    private static double ClampDerived(double value) => Math.Clamp(value, MinDerivedFontSize, MaxFontSize + 8);
}
