using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WHDClient.Core.BbCode;
using WHDClient.Core.RichText;

namespace WHDClient.Services;

/// <summary>Renders parsed BBCode or HTML (auto-detected, see <see cref="RichTextParser"/>) into a WPF FlowDocument.</summary>
public static partial class BbCodeRenderer
{
    public static FlowDocument Render(string? bbText)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left
        };

        foreach (var block in RichTextParser.Parse(bbText))
        {
            switch (block)
            {
                case BbParagraph p:
                    var para = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
                    AddInlines(para, p.Inlines);
                    doc.Blocks.Add(para);
                    break;

                case BbList list:
                    var wpfList = new List
                    {
                        MarkerStyle = list.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                        Margin = new Thickness(0, 0, 0, 4),
                        Padding = new Thickness(24, 0, 0, 0)
                    };
                    foreach (var item in list.Items)
                    {
                        var ip = new Paragraph { Margin = new Thickness(0) };
                        AddInlines(ip, item);
                        wpfList.ListItems.Add(new ListItem(ip));
                    }
                    doc.Blocks.Add(wpfList);
                    break;

                case BbCodeBlock code:
                    doc.Blocks.Add(new Paragraph(new Run(code.Text))
                    {
                        FontFamily = new FontFamily("Consolas"),
                        // No explicit FontSize: inherit from the host RichTextBox so the
                        // whole rendered document follows the app-wide font scale live.
                        Background = Resource<Brush>("Panel2Brush"),
                        BorderBrush = Resource<Brush>("BorderBrushDim"),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(8, 4, 8, 4),
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                    break;

                case BbQuote quote:
                    var qp = new Paragraph
                    {
                        FontStyle = FontStyles.Italic,
                        Foreground = Resource<Brush>("TextDimBrush"),
                        BorderBrush = Resource<Brush>("HeaderBrush"),
                        BorderThickness = new Thickness(3, 0, 0, 0),
                        Padding = new Thickness(8, 0, 0, 0),
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    AddInlines(qp, quote.Inlines);
                    doc.Blocks.Add(qp);
                    break;

                case BbImage img:
                    var image = CreateImage(img.Url);
                    if (image != null)
                        doc.Blocks.Add(new BlockUIContainer(image) { Margin = new Thickness(0, 0, 0, 4) });
                    else
                        doc.Blocks.Add(new Paragraph(MakeLink(img.Url, img.Url)) { Margin = new Thickness(0, 0, 0, 4) });
                    break;
            }
        }
        return doc;
    }

    private static void AddInlines(Paragraph para, List<BbNode> inlines)
    {
        foreach (var node in inlines)
        {
            switch (node)
            {
                case BbText t:
                    AddTextRuns(para, t);
                    break;
                case BbLink l:
                    para.Inlines.Add(ApplyFormat(MakeLink(l.Text, l.Url), l));
                    break;
            }
        }
    }

    /// <summary>Adds a text node, converting embedded newlines into line breaks.</summary>
    private static void AddTextRuns(Paragraph para, BbText t)
    {
        var lines = t.Text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) para.Inlines.Add(new LineBreak());
            AddRunsAutolinked(para, lines[i], t);
        }
    }

    /// <summary>Adds text as runs, turning bare http(s) URLs into clickable hyperlinks.</summary>
    private static void AddRunsAutolinked(Paragraph para, string text, BbText format)
    {
        var pos = 0;
        foreach (Match m in BareUrlRegex().Matches(text))
        {
            // Trim punctuation that typically follows a URL in prose (e.g. "<https://x>").
            var url = m.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '>', '"', '\'');
            if (url.Length == 0) continue;
            if (m.Index > pos)
                para.Inlines.Add(ApplyFormat(new Run(text[pos..m.Index]), format));
            para.Inlines.Add(ApplyFormat(MakeLink(url, url), format));
            pos = m.Index + url.Length; // trailing punctuation stays as plain text
        }
        if (pos < text.Length)
            para.Inlines.Add(ApplyFormat(new Run(text[pos..]), format));
    }

    private static Inline ApplyFormat(Inline inline, BbNode node)
    {
        var (bold, italic, underline) = node switch
        {
            BbText t => (t.Bold, t.Italic, t.Underline),
            BbLink l => (l.Bold, l.Italic, l.Underline),
            _ => (false, false, false)
        };
        if (bold) inline.FontWeight = FontWeights.SemiBold;
        if (italic) inline.FontStyle = FontStyles.Italic;
        if (underline) inline.TextDecorations = System.Windows.TextDecorations.Underline;
        return inline;
    }

    private static Hyperlink MakeLink(string text, string url)
    {
        var link = new Hyperlink(new Run(text))
        {
            Foreground = Resource<Brush>("LinkBrush"),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto))
        {
            link.Click += (_, _) => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        return link;
    }

    private static System.Windows.Controls.Image? CreateImage(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null;
        try
        {
            // BitmapImage downloads in the background; a failed download just shows nothing.
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            return new System.Windows.Controls.Image
            {
                Source = bmp,
                MaxWidth = 500,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left
            };
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex BareUrlRegex();

    private static T Resource<T>(string key) =>
        Application.Current?.TryFindResource(key) is T v
            ? v
            : default!;
}
