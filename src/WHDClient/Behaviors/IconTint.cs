using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WHDClient.Behaviors;

/// <summary>
/// Recolors a monochrome (white) icon to a target brush so it reads on any theme.
/// Bind <c>IconTint.Tint</c> to the theme text brush (e.g. via DynamicResource) and the
/// attached Image is re-tinted whenever the brush or source changes.
/// </summary>
public static class IconTint
{
    public static readonly DependencyProperty TintProperty =
        DependencyProperty.RegisterAttached("Tint", typeof(Brush), typeof(IconTint),
            new PropertyMetadata(null, OnTintChanged));

    public static void SetTint(DependencyObject d, Brush? value) => d.SetValue(TintProperty, value);
    public static Brush? GetTint(DependencyObject d) => (Brush?)d.GetValue(TintProperty);

    private static readonly DependencyPropertyDescriptor SourceDescriptor =
        DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(Image));

    private static bool _applying;

    private static void OnTintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image) return;
        SourceDescriptor.RemoveValueChanged(image, OnSourceChanged);
        SourceDescriptor.AddValueChanged(image, OnSourceChanged);
        Apply(image);
    }

    private static void OnSourceChanged(object? sender, EventArgs e)
    {
        if (sender is Image image) Apply(image);
    }

    private static void Apply(Image image)
    {
        if (_applying) return;
        if (GetTint(image) is not SolidColorBrush brush) return;
        if (image.Source is not BitmapSource source || source.PixelWidth == 0) return;

        var tinted = Tint(source, brush.Color);
        if (tinted == null) return;

        // SetCurrentValue keeps any Source binding alive (tab/bookmark icons change at runtime).
        _applying = true;
        try { image.SetCurrentValue(Image.SourceProperty, tinted); }
        finally { _applying = false; }
    }

    private static BitmapSource? Tint(BitmapSource source, Color color)
    {
        try
        {
            var fmt = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var w = fmt.PixelWidth;
            var h = fmt.PixelHeight;
            var stride = w * 4;
            var pixels = new byte[stride * h];
            fmt.CopyPixels(pixels, stride, 0);

            // Icons are monochrome white: keep each pixel's alpha and replace its RGB
            // with the tint color, so anti-aliased edges and transparency are preserved.
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = color.B;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.R;
            }

            var result = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            result.Freeze();
            return result;
        }
        catch
        {
            return null;
        }
    }
}
