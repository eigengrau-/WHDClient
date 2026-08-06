using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WHDClient.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

public class BoolToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class NullToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var vis = value != null ? Visibility.Visible : Visibility.Collapsed;
        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase))
            vis = vis == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        return vis;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class PositiveIntToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a ticket status name to a badge color.</summary>
public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = (value as string ?? "").ToLowerInvariant();
        var color = s switch
        {
            _ when s.Contains("open") => "#008363",        // green
            _ when s.Contains("progress") => "#2f7d5c",    // soft green
            _ when s.Contains("pending") => "#8a6d3b",     // amber
            _ when s.Contains("waiting") => "#8a6d3b",
            _ when s.Contains("hold") => "#8a6d3b",
            _ when s.Contains("resolved") => "#506a80",    // blue-gray
            _ when s.Contains("closed") => "#506a80",
            _ when s.Contains("complete") => "#506a80",
            _ when s.Contains("cancel") => "#535353",      // gray
            _ when s.Contains("project") => "#5e8ca0",     // link blue
            _ => "#535353"
        };
        return new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class DateTimeLocalConverter : IValueConverter
{
    /// <summary>Shows the date exactly as the WHD API returned it (no local-timezone shift).</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DateTimeOffset dto ? dto.ToString("yyyy-MM-dd HH:mm") : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class PriorityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = (value as string ?? "").ToLowerInvariant();
        var color = s switch
        {
            _ when s.Contains("urgent") => "#a03a3a",        // red
            _ when s.Contains("critical") => "#a03a3a",
            _ when s.Contains("high") => "#b06030",          // orange
            _ when s.Contains("medium") => "#8a6d3b",        // amber
            _ when s.Contains("normal") => "#506a80",        // blue-gray
            _ when s.Contains("low") => "#5e8ca0",           // link blue
            _ => "#535353"                                    // gray (No Due Date, custom, unknown)
        };
        return new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
