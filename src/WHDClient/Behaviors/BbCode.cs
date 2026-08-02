using System.Windows;
using System.Windows.Controls;
using WHDClient.Services;

namespace WHDClient.Behaviors;

/// <summary>Attached property that renders a BBCode string into a RichTextBox's document.</summary>
public static class BbCode
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(BbCode),
            new PropertyMetadata(null, OnTextChanged));

    public static void SetText(DependencyObject element, string? value) =>
        element.SetValue(TextProperty, value);

    public static string? GetText(DependencyObject element) =>
        (string?)element.GetValue(TextProperty);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichTextBox rtb)
            rtb.Document = BbCodeRenderer.Render(e.NewValue as string);
    }
}
