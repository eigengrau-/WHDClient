using System.Windows;
using System.Windows.Controls;

namespace WHDClient.Views;

/// <summary>
/// BBCode toolbar bound to a target TextBox. Buttons wrap the current selection
/// in tags (or insert a template when nothing is selected), like the Web Help Desk editor.
/// </summary>
public partial class BbCodeToolbar : UserControl
{
    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register(nameof(Target), typeof(TextBox), typeof(BbCodeToolbar));

    public TextBox? Target
    {
        get => (TextBox?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public BbCodeToolbar()
    {
        InitializeComponent();
    }

    /// <summary>Replaces the selection with pre + selection + post; inserts placeholder when empty.</summary>
    private void Wrap(string pre, string post, string placeholder = "")
    {
        var tb = Target;
        if (tb == null) return;
        var sel = tb.SelectedText;
        if (sel.Length == 0) sel = placeholder;
        var start = tb.SelectionStart;
        tb.SelectedText = pre + sel + post;
        tb.CaretIndex = start + pre.Length + sel.Length + post.Length;
        tb.Focus();
    }

    private void WrapList(bool ordered)
    {
        var tb = Target;
        if (tb == null) return;
        var tag = ordered ? "[list=1]" : "[list]";
        var sel = tb.SelectedText;
        string replacement;
        if (string.IsNullOrWhiteSpace(sel))
        {
            replacement = $"{tag}\n[*]item\n[/list]";
        }
        else
        {
            var lines = sel.Replace("\r\n", "\n").Split('\n');
            replacement = $"{tag}\n[*]{string.Join("\n[*]", lines)}\n[/list]";
        }
        var start = tb.SelectionStart;
        tb.SelectedText = replacement;
        tb.CaretIndex = start + replacement.Length;
        tb.Focus();
    }

    private void Bold_Click(object sender, RoutedEventArgs e) => Wrap("[b]", "[/b]", "bold text");
    private void Italic_Click(object sender, RoutedEventArgs e) => Wrap("[i]", "[/i]", "italic text");
    private void Underline_Click(object sender, RoutedEventArgs e) => Wrap("[u]", "[/u]", "underlined text");
    private void BulletList_Click(object sender, RoutedEventArgs e) => WrapList(false);
    private void NumberedList_Click(object sender, RoutedEventArgs e) => WrapList(true);
    private void Code_Click(object sender, RoutedEventArgs e) => Wrap("[code]", "[/code]", "code");
    private void Quote_Click(object sender, RoutedEventArgs e) => Wrap("[quote]", "[/quote]", "quote");
    private void Image_Click(object sender, RoutedEventArgs e) => Wrap("[img]", "[/img]", "https://");

    private void Link_Click(object sender, RoutedEventArgs e)
    {
        var tb = Target;
        if (tb == null) return;
        var sel = tb.SelectedText;
        if (sel.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
            Wrap("[url]", "[/url]");
        else
            Wrap("[url=https://]", "[/url]", "link text");
    }
}
