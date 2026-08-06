using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WHDClient.Behaviors;

/// <summary>
/// Overrides the mouse-wheel scroll distance for a ScrollViewer. WPF scrolls three lines per
/// wheel notch by default, which feels too fast for large content; set PixelsPerNotch to a
/// smaller value (e.g. 24) to scroll a fixed number of pixels per notch instead.
/// </summary>
public static class ScrollWheelSpeed
{
    /// <summary>Pixels scrolled per wheel notch. 0 (default) leaves the built-in behavior intact.</summary>
    public static readonly DependencyProperty PixelsPerNotchProperty =
        DependencyProperty.RegisterAttached("PixelsPerNotch", typeof(double), typeof(ScrollWheelSpeed),
            new PropertyMetadata(0.0, OnPixelsPerNotchChanged));

    public static void SetPixelsPerNotch(DependencyObject d, double value) =>
        d.SetValue(PixelsPerNotchProperty, value);

    public static double GetPixelsPerNotch(DependencyObject d) =>
        (double)d.GetValue(PixelsPerNotchProperty);

    private static void OnPixelsPerNotchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv) return;
        sv.PreviewMouseWheel -= OnPreviewMouseWheel;
        if (GetPixelsPerNotch(sv) > 0)
            sv.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        double px = GetPixelsPerNotch(sv);
        if (px <= 0) return;

        e.Handled = true;
        double steps = e.Delta / 120.0;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - steps * px);
    }
}
