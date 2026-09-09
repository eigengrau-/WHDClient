using System.Net;
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

/// <summary>The quote body is parsed recursively, so it can hold any block — lists, tables, nested quotes.</summary>
public record BbQuote(List<BbNode> Blocks) : BbNode;

public record BbImage(string Url) : BbNode;

public record BbTable(List<BbTableRow> Rows) : BbNode;

public record BbTableRow(List<BbTableCell> Cells);

public record BbTableCell(bool Header, List<BbNode> Inlines);

/// <summary>
/// Parses the BBCode subset supported by Web Help Desk's editors:
/// [b] [i] [u] [list] [list=1] [*] [code] [quote] [img] [url] [table] [tr] [td] [th].
/// WHD stores note text with HTML entities encoded (&#39; &quot; &amp;), so entities
/// are decoded here. Unknown or malformed tags are left as literal text.
/// </summary>
public static partial class BbCodeParser
{
    /// <summary>Parses bbText into a flat list of block nodes.</summary>
    public static List<BbNode> Parse(string? bbText)
    {
        var blocks = new List<BbNode>();
        if (string.IsNullOrWhiteSpace(bbText)) return blocks;

        var text = WebUtility.HtmlDecode(bbText).Replace("\r\n", "\n").Replace('\r', '\n');
        ParseBlocks(text, blocks);
        return blocks;
    }

    /// <summary>
    /// Finds block tags and parses the gaps between them as paragraphs. Open/close
    /// pairs are matched with a depth counter so the same tag can nest (e.g.
    /// [quote] inside [quote]), which a flat regex cannot do.
    /// </summary>
    private static void ParseBlocks(string text, List<BbNode> blocks)
    {
        var pos = 0;  // consumed up to here
        var scan = 0; // next open-tag search starts here
        while (true)
        {
            var open = BlockOpenRegex().Match(text, scan);
            if (!open.Success) break;
            var name = open.Groups[1].Value.ToLowerInvariant();
            var close = FindMatchingClose(text, open.Index + open.Length, name);
            if (close.Start < 0)
            {
                scan = open.Index + open.Length; // unclosed — leave the tag as literal text
                continue;
            }
            AddParagraphs(blocks, text[pos..open.Index]);
            blocks.Add(BuildBlock(name, open.Groups[2].Value, text[(open.Index + open.Length)..close.Start]));
            pos = scan = close.End;
        }
        AddParagraphs(blocks, text[pos..]);
    }

    /// <summary>Finds the close tag matching an open tag, skipping over nested same-name pairs.</summary>
    private static (int Start, int End) FindMatchingClose(string text, int bodyStart, string name)
    {
        var depth = 1;
        foreach (Match m in BlockBoundaryRegex().Matches(text, bodyStart))
        {
            if (!m.Groups[2].Value.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            depth += m.Groups[1].Value == "/" ? -1 : 1;
            if (depth == 0) return (m.Index, m.Index + m.Length);
        }
        return (-1, -1);
    }

    private static BbNode BuildBlock(string name, string arg, string body) => name switch
    {
        "code" => new BbCodeBlock(body.Trim('\n')),
        "quote" => ParseQuote(body),
        "img" => new BbImage(body.Trim()),
        "table" => ParseTable(body),
        _ => ParseList(arg, body) // list / list=1
    };

    /// <summary>The quote body goes through the full block parser, so BBCode nested inside [quote] renders.</summary>
    private static BbQuote ParseQuote(string body)
    {
        var inner = new List<BbNode>();
        ParseBlocks(body.Trim(), inner);
        return new BbQuote(inner);
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
        foreach (var raw in ListItemRegex().Split(body))
        {
            var item = ParseInlines(raw.Trim('\n'));
            if (item.Count > 0) items.Add(item);
        }
        return new BbList(ordered, items);
    }

    private static BbTable ParseTable(string body)
    {
        var rows = new List<BbTableRow>();
        foreach (Match rm in TableRowRegex().Matches(body))
        {
            var cells = new List<BbTableCell>();
            foreach (Match cm in TableCellRegex().Matches(rm.Groups[1].Value))
            {
                var header = cm.Groups[1].Value.Equals("th", StringComparison.OrdinalIgnoreCase);
                cells.Add(new BbTableCell(header, ParseInlines(cm.Groups[2].Value.Trim())));
            }
            if (cells.Count > 0) rows.Add(new BbTableRow(cells));
        }
        return new BbTable(rows);
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

    // Block open tags: [code] [quote] [img] [list(=x)] [table]
    [GeneratedRegex(@"\[(code|quote|img|list|table)(=\w+)?\]", RegexOptions.IgnoreCase)]
    private static partial Regex BlockOpenRegex();

    // Any block open/close tag — used to match pairs while respecting nesting depth.
    [GeneratedRegex(@"\[(\/?)(code|quote|img|list|table)(=\w+)?\]", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBoundaryRegex();

    // List item separator: [*]
    [GeneratedRegex(@"\[\*\]", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemRegex();

    // Table structure: [tr] rows, [td]/[th] cells
    [GeneratedRegex(@"\[tr\](.*?)\[/tr\]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TableRowRegex();

    [GeneratedRegex(@"\[(td|th)\](.*?)\[/\1\]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TableCellRegex();

    // Inline tags: [b] [/b] [i] [/i] [u] [/u] [url] [url=...] [/url]
    [GeneratedRegex(@"\[(\/?)(b|i|u|url)(=[^\]\[]*)?\]", RegexOptions.IgnoreCase)]
    private static partial Regex InlineTagRegex();
}
