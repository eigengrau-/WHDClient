using System.Text;
using System.Text.RegularExpressions;

namespace WHDClient.Core.BbCode;

// Block and inline nodes produced by BbCodeParser. Inline nodes carry their
// formatting as flags so arbitrary nesting ([b][i]x[/i][/b]) needs no tree.
public abstract record BbNode;

public record BbText(string Text, bool Bold = false, bool Italic = false, bool Underline = false) : BbNode;

public record BbLink(string Text, string Url, bool Bold = false, bool Italic = false, bool Underline = false) : BbNode;

public record BbParagraph(List<BbNode> Inlines) : BbNode;

public record BbList(bool Ordered, List<List<BbNode>> Items) : BbNode;

public record BbCodeBlock(string Text) : BbNode;

public record BbQuote(List<BbNode> Inlines) : BbNode;

public record BbImage(string Url) : BbNode;

/// <summary>
/// Parses the BBCode subset supported by Web Help Desk's editors:
/// [b] [i] [u] [list] [list=1] [*] [code] [quote] [img] [url].
/// Unknown or malformed tags are left as literal text.
/// </summary>
public static partial class BbCodeParser
{
    /// <summary>Parses bbText into a flat list of block nodes.</summary>
    public static List<BbNode> Parse(string? bbText)
    {
        var blocks = new List<BbNode>();
        if (string.IsNullOrWhiteSpace(bbText)) return blocks;

        var text = bbText.Replace("\r\n", "\n").Replace('\r', '\n');
        var pos = 0;
        foreach (Match m in BlockTagRegex().Matches(text))
        {
            AddParagraphs(blocks, text[pos..m.Index]);
            blocks.Add(m.Groups[1].Value.ToLowerInvariant() switch
            {
                "code" => new BbCodeBlock(m.Groups[3].Value.Trim('\n')),
                "quote" => new BbQuote(ParseInlines(m.Groups[3].Value.Trim())),
                "img" => new BbImage(m.Groups[3].Value.Trim()),
                _ => ParseList(m.Groups[2].Value, m.Groups[3].Value) // list / list=1
            });
            pos = m.Index + m.Length;
        }
        AddParagraphs(blocks, text[pos..]);
        return blocks;
    }

    /// <summary>Splits plain text into paragraphs on blank lines and inline-parses each.</summary>
    private static void AddParagraphs(List<BbNode> blocks, string text)
    {
        foreach (var chunk in Regex.Split(text, @"\n\s*\n"))
        {
            var inlines = ParseInlines(chunk.Trim('\n'));
            if (inlines.Count > 0) blocks.Add(new BbParagraph(inlines));
        }
    }

    private static BbList ParseList(string arg, string body)
    {
        var ordered = arg is "=1" or "=a" or "=A";
        var items = new List<List<BbNode>>();
        foreach (var raw in body.Split("[*]"))
        {
            var item = ParseInlines(raw.Trim('\n'));
            if (item.Count > 0) items.Add(item);
        }
        return new BbList(ordered, items);
    }

    /// <summary>
    /// Parses inline tags ([b] [i] [u] [url]) into runs with formatting flags.
    /// A close tag only applies when its tag is currently open; anything else
    /// is emitted literally.
    /// </summary>
    private static List<BbNode> ParseInlines(string s)
    {
        var nodes = new List<BbNode>();
        if (string.IsNullOrEmpty(s)) return nodes;

        var bold = false; var italic = false; var underline = false;
        var last = 0;
        var pending = new StringBuilder();

        void Flush()
        {
            if (pending.Length == 0) return;
            nodes.Add(new BbText(pending.ToString(), bold, italic, underline));
            pending.Clear();
        }

        foreach (Match m in InlineTagRegex().Matches(s))
        {
            if (m.Index < last) continue; // already consumed (e.g. [/url] after a [url] pair)
            pending.Append(s, last, m.Index - last);
            last = m.Index + m.Length;

            var closing = m.Groups[1].Value == "/";
            var tag = m.Groups[2].Value.ToLowerInvariant();
            var arg = m.Groups[3].Value; // e.g. =https://... on [url=...]

            if (tag == "url" && !closing)
            {
                Flush();
                var close = s.IndexOf("[/url]", last, StringComparison.OrdinalIgnoreCase);
                if (close < 0)
                {
                    pending.Append(m.Value); // unclosed — literal
                    continue;
                }
                var inner = s[last..close];
                var url = arg.Length > 1 ? arg[1..] : inner;
                nodes.Add(new BbLink(inner, url.Trim(), bold, italic, underline));
                last = close + 6;
            }
            else if (tag == "url") // stray [/url]
            {
                pending.Append(m.Value);
            }
            else if (!closing)
            {
                Flush();
                if (tag == "b") bold = true; else if (tag == "i") italic = true; else underline = true;
            }
            else
            {
                var wasOpen = tag == "b" ? bold : tag == "i" ? italic : underline;
                if (!wasOpen)
                {
                    pending.Append(m.Value); // close without open — literal
                    continue;
                }
                Flush();
                if (tag == "b") bold = false; else if (tag == "i") italic = false; else underline = false;
            }
        }
        pending.Append(s, last, s.Length - last);
        Flush();
        return nodes;
    }

    // Block tags: [code]..[/code], [quote]..[/quote], [img]..[/img], [list(=x)]..[/list]
    [GeneratedRegex(@"\[(code|quote|img|list)(=\w+)?\](.*?)\[/\1\]",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BlockTagRegex();

    // Inline tags: [b] [/b] [i] [/i] [u] [/u] [url] [url=...] [/url]
    [GeneratedRegex(@"\[(\/?)(b|i|u|url)(=[^\]\[]*)?\]", RegexOptions.IgnoreCase)]
    private static partial Regex InlineTagRegex();
}
