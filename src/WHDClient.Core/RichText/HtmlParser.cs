using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using WHDClient.Core.BbCode;

namespace WHDClient.Core.RichText;

/// <summary>
/// Parses the HTML subset produced by Web Help Desk's rich-text editor (ticket
/// request details, mobile notes) into the same block model as <see cref="BbCodeParser"/>,
/// so a single renderer handles both markup dialects. Unknown tags are dropped
/// but their text content is kept; entities are decoded.
/// </summary>
public static partial class HtmlParser
{
    /// <summary>Parses html into a flat list of block nodes.</summary>
    public static List<BbNode> Parse(string? html)
    {
        var blocks = new List<BbNode>();
        if (string.IsNullOrWhiteSpace(html)) return blocks;
        new Cursor(html, blocks).Run();
        return blocks;
    }

    private readonly record struct Tag(string Name, string Args, bool Closing);

    private sealed class Cursor
    {
        private readonly string _s;
        private readonly List<BbNode> _blocks;
        private readonly InlineSink _para = new();
        private int _pos;

        public Cursor(string s, List<BbNode> blocks)
        {
            _s = s;
            _blocks = blocks;
        }

        public void Run()
        {
            foreach (var token in Tokens())
            {
                if (token is string text) _para.Append(Decode(text));
                else HandleTag((Tag)token, _para, inListItem: false);
            }
            CloseParagraph();
        }

        // Lazily yields tags and decoded-ready text chunks; handlers may advance
        // _pos past paired close tags ([a], [pre], [blockquote]) between MoveNext calls.
        private IEnumerable<object> Tokens()
        {
            while (_pos < _s.Length)
            {
                var m = TagRegex().Match(_s, _pos);
                if (m.Success && m.Index == _pos && TryParseTag(m.Groups[1].Value, out var tag))
                {
                    _pos += m.Length;
                    yield return tag;
                }
                else
                {
                    var next = _s.IndexOf('<', _pos);
                    if (next == _pos) next = _pos + 1; // lone '<' that isn't a tag
                    if (next < 0) next = _s.Length;
                    var text = _s[_pos..next];
                    _pos = next;
                    yield return text;
                }
            }
        }

        private void HandleTag(Tag tag, InlineSink sink, bool inListItem)
        {
            switch (tag.Name)
            {
                case "": // comments, doctype
                    break;
                case "br":
                    sink.Append("\n");
                    break;
                case "p" or "div" or "section" or "article" or "tr" or "table" or "tbody" or "thead":
                    if (inListItem) sink.Append("\n");
                    else CloseParagraph();
                    break;
                case "td" or "th":
                    sink.Append(" ");
                    break;
                case "ol" when !tag.Closing && !inListItem:
                    CloseParagraph();
                    ParseList(ordered: true);
                    break;
                case "ul" when !tag.Closing && !inListItem:
                    CloseParagraph();
                    ParseList(ordered: false);
                    break;
                case "b" or "strong":
                    sink.SetBold(!tag.Closing);
                    break;
                case "i" or "em":
                    sink.SetItalic(!tag.Closing);
                    break;
                case "u":
                    sink.SetUnderline(!tag.Closing);
                    break;
                case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    if (tag.Closing)
                    {
                        sink.SetBold(false);
                        if (!inListItem) CloseParagraph();
                    }
                    else
                    {
                        if (!inListItem) CloseParagraph();
                        sink.SetBold(true);
                    }
                    break;
                case "a" when !tag.Closing:
                    ReadLink(sink, tag.Args);
                    break;
                case "img" when !tag.Closing:
                    var src = GetAttr(tag.Args, "src");
                    if (src != null && !inListItem)
                    {
                        CloseParagraph();
                        _blocks.Add(new BbImage(Decode(src)));
                    }
                    break;
                case "pre" or "code" when !tag.Closing && !inListItem:
                    ReadCodeBlock(tag.Name);
                    break;
                case "blockquote" when !tag.Closing && !inListItem:
                    ReadQuote();
                    break;
                // Unknown tags are dropped; their text content flows through.
            }
        }

        private void ParseList(bool ordered)
        {
            var items = new List<List<BbNode>>();
            var sink = new InlineSink();
            var depth = 1;

            void CloseItem()
            {
                var inlines = TrimInlines(sink.Drain());
                if (inlines.Count > 0) items.Add(inlines);
            }

            foreach (var token in Tokens())
            {
                if (token is string text)
                {
                    sink.Append(Decode(text));
                    continue;
                }
                var tag = (Tag)token;
                switch (tag.Name)
                {
                    case "ol" or "ul" when !tag.Closing:
                        depth++;
                        if (depth > 1) sink.Append("\n");
                        break;
                    case "ol" or "ul":
                        depth--;
                        if (depth == 0)
                        {
                            CloseItem();
                            _blocks.Add(new BbList(ordered, items));
                            return;
                        }
                        break;
                    case "li" when !tag.Closing:
                        if (depth == 1) CloseItem();
                        else sink.Append("\n• ");
                        break;
                    case "br":
                        sink.Append("\n");
                        break;
                    default:
                        HandleTag(tag, sink, inListItem: true);
                        break;
                }
            }
            // Unterminated list: keep whatever was collected.
            CloseItem();
            if (items.Count > 0) _blocks.Add(new BbList(ordered, items));
        }

        private void ReadLink(InlineSink sink, string args)
        {
            var close = _s.IndexOf("</a>", _pos, StringComparison.OrdinalIgnoreCase);
            if (close < 0) return; // unclosed — ignore the tag, text flows
            var inner = _s[_pos..close];
            _pos = close + 4;
            var text = Decode(TagRegex().Replace(inner, "")).Trim();
            var url = Decode(GetAttr(args, "href") ?? text);
            if (text.Length == 0) text = url;
            sink.AddLink(text, url);
        }

        private void ReadCodeBlock(string name)
        {
            var close = _s.IndexOf($"</{name}>", _pos, StringComparison.OrdinalIgnoreCase);
            if (close < 0) return;
            var inner = Decode(_s[_pos..close]).Trim('\n', ' ', '\t');
            _pos = close + name.Length + 3;
            CloseParagraph();
            if (inner.Length > 0) _blocks.Add(new BbCodeBlock(inner));
        }

        private void ReadQuote()
        {
            var close = _s.IndexOf("</blockquote>", _pos, StringComparison.OrdinalIgnoreCase);
            if (close < 0) return;
            var inner = _s[_pos..close];
            _pos = close + 13;
            CloseParagraph();

            var sub = new List<BbNode>();
            new Cursor(inner, sub).Run();
            var inlines = new List<BbNode>();
            void AppendInlines(IEnumerable<BbNode> add)
            {
                var list = add.ToList();
                if (list.Count == 0) return;
                if (inlines.Count > 0) inlines.Add(new BbText("\n"));
                inlines.AddRange(list);
            }
            foreach (var b in sub)
            {
                switch (b)
                {
                    case BbParagraph p: AppendInlines(p.Inlines); break;
                    case BbQuote q: AppendInlines(q.Inlines); break;
                    case BbList l:
                        foreach (var item in l.Items)
                        {
                            if (inlines.Count > 0) inlines.Add(new BbText("\n"));
                            inlines.Add(new BbText("• "));
                            inlines.AddRange(item);
                        }
                        break;
                }
            }
            if (inlines.Count > 0) _blocks.Add(new BbQuote(inlines));
        }

        private void CloseParagraph()
        {
            var inlines = TrimInlines(_para.Drain());
            if (inlines.Count > 0) _blocks.Add(new BbParagraph(inlines));
        }

        private static bool TryParseTag(string inner, out Tag tag)
        {
            tag = default;
            var closing = inner.StartsWith('/');
            var body = closing ? inner[1..] : inner;
            var i = 0;
            while (i < body.Length && char.IsLetterOrDigit(body[i])) i++;
            if (i == 0)
            {
                // Comments/doctype are consumed and ignored; anything else
                // (e.g. "a < b") is literal text, not a tag.
                if (body.StartsWith('!'))
                {
                    tag = new Tag("", "", false);
                    return true;
                }
                return false;
            }
            tag = new Tag(body[..i].ToLowerInvariant(), body[i..], closing);
            return true;
        }

        private static string? GetAttr(string args, string name)
        {
            foreach (Match m in AttrRegex().Matches(args))
            {
                if (!m.Groups[1].Value.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                for (var g = 2; g <= 4; g++)
                    if (m.Groups[g].Success) return m.Groups[g].Value;
            }
            return null;
        }

        private static List<BbNode> TrimInlines(List<BbNode> inlines)
        {
            while (inlines.Count > 0 && inlines[0] is BbText first)
            {
                var t = first.Text.TrimStart('\n', ' ', '\t');
                if (t.Length == 0) inlines.RemoveAt(0);
                else { inlines[0] = first with { Text = t }; break; }
            }
            while (inlines.Count > 0 && inlines[^1] is BbText last)
            {
                var t = last.Text.TrimEnd('\n', ' ', '\t');
                if (t.Length == 0) inlines.RemoveAt(inlines.Count - 1);
                else { inlines[^1] = last with { Text = t }; break; }
            }
            return inlines;
        }

        private static string Decode(string s) => WebUtility.HtmlDecode(s);
    }

    /// <summary>Accumulates inline runs with formatting flags; text flushes on flag changes.</summary>
    private sealed class InlineSink
    {
        private readonly List<BbNode> _inlines = new();
        private readonly StringBuilder _pending = new();
        private bool _bold, _italic, _underline;

        public void Append(string text) => _pending.Append(text);

        public void SetBold(bool on) { if (on != _bold) { Flush(); _bold = on; } }
        public void SetItalic(bool on) { if (on != _italic) { Flush(); _italic = on; } }
        public void SetUnderline(bool on) { if (on != _underline) { Flush(); _underline = on; } }

        public void AddLink(string text, string url)
        {
            Flush();
            _inlines.Add(new BbLink(text, url, _bold, _italic, _underline));
        }

        public List<BbNode> Drain()
        {
            Flush();
            var result = new List<BbNode>(_inlines);
            _inlines.Clear();
            return result;
        }

        private void Flush()
        {
            if (_pending.Length == 0) return;
            _inlines.Add(new BbText(_pending.ToString(), _bold, _italic, _underline));
            _pending.Clear();
        }
    }

    [GeneratedRegex("<([^<>]*)>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\b(href|src)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase)]
    private static partial Regex AttrRegex();
}
