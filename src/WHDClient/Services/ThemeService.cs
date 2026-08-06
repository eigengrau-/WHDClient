using System;
using System.Collections.Generic;
using System.Windows;

namespace WHDClient.Services;

/// <summary>
/// Swaps the app-wide theme dictionary and font-size scale at runtime. The theme dictionaries
/// define the same resource keys (brushes, keyed styles); views reference them via
/// DynamicResource so swapping here restyles already-open windows immediately.
/// Font sizes are referenced through named resources (AppFontSize, FontSize10..28) that
/// ThemeService recomputes from the chosen scale, so every text element follows the preset.
/// </summary>
public static class ThemeService
{
    public const string DarkTheme = "Dark";
    public const string LightTheme = "Light";

    public const string SmallScale = "Small";
    public const string MediumScale = "Medium";
    public const string LargeScale = "Large";

    /// <summary>Multiplier applied to the design-size font values per preset.</summary>
    public static readonly IReadOnlyDictionary<string, double> ScaleFactors =
        new Dictionary<string, double>
        {
            [SmallScale] = 0.85,
            [MediumScale] = 1.0,
            [LargeScale] = 1.15
        };

    /// <summary>Named font-size resources that scale with the preset.</summary>
    private static readonly (string Key, double DesignSize)[] FontResources =
    {
        ("AppFontSize", 14),      // base body text (Window cascade)
        ("FontSize10", 10),       // tiny labels, small close buttons
        ("FontSize11", 11),       // dim/secondary text
        ("FontSize12", 12),       // notes, details, notifications
        ("FontSize13", 13),
        ("FontSize14", 14),
        ("FontSize15", 15),
        ("FontSize16", 16),
        ("FontSize18", 18),
        ("FontSize20", 20),       // page/header titles
        ("FontSize24", 24),
        ("FontSize28", 28),       // login window title
        ("HeaderFontSize", 20),   // derived: page headers
        ("SubHeaderFontSize", 14),
        ("DimFontSize", 11),
        ("FieldFontSize", 10)
    };

    public static string CurrentTheme { get; private set; } = DarkTheme;
    public static string CurrentFontScale { get; private set; } = MediumScale;

    public static bool IsDark => CurrentTheme == DarkTheme;

    /// <summary>Applies the theme (name from <see cref="DarkTheme"/>/<see cref="LightTheme"/>) and font-scale preset live.</summary>
    public static void Apply(string theme, string fontScale)
    {
        CurrentTheme = theme == LightTheme ? LightTheme : DarkTheme;
        CurrentFontScale = ScaleFactors.ContainsKey(fontScale) ? fontScale : MediumScale;

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

        // Recompute every named font size from the scale factor.
        double factor = ScaleFactors[CurrentFontScale];
        foreach (var (key, designSize) in FontResources)
            app.Resources[key] = Math.Round(designSize * factor, 1);

        // Re-color the immersive title bar to match (light themes need a light title bar).
        foreach (Window w in app.Windows)
            DarkTitleBar.Apply(w);
    }
}
