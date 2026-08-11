using System.Text.RegularExpressions;
using WHDClient.Core.BbCode;

namespace WHDClient.Core.RichText;

/// <summary>
/// Detects whether a WHD text field is HTML (ticket request details, mobile notes)
/// or BBCode (tech/client notes) and parses it into the shared block model.
/// </summary>
public static partial class RichTextParser
{
    public static List<BbNode> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<BbNode>();
        return HtmlTagRegex().IsMatch(text) ? HtmlParser.Parse(text) : BbCodeParser.Parse(text);
    }

    [GeneratedRegex(@"<(br|p|div|span|ol|ul|li|dl|dt|dd|b|strong|i|em|u|s|strike|del|a|img|pre|code|blockquote|font|h[1-6]|table|thead|tbody|tr|td|th)(\s[^<>]*)?/?>",
        RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTagRegex();
}
