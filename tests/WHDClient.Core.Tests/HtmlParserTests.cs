using WHDClient.Core.BbCode;
using WHDClient.Core.RichText;
using Xunit;

namespace WHDClient.Core.Tests;

public class HtmlParserTests
{
    [Fact]
    public void OrderedList_ParsesItems()
    {
        var blocks = HtmlParser.Parse("<ol><li>one</li><li>two</li></ol>");
        var list = Assert.IsType<BbList>(Assert.Single(blocks));
        Assert.True(list.Ordered);
        Assert.Equal(2, list.Items.Count);
        Assert.Equal(new BbText("one"), Assert.Single(list.Items[0]));
        Assert.Equal(new BbText("two"), Assert.Single(list.Items[1]));
    }

    [Fact]
    public void UnorderedList_ParsesItems()
    {
        var blocks = HtmlParser.Parse("<ul><li>one</li></ul>");
        var list = Assert.IsType<BbList>(Assert.Single(blocks));
        Assert.False(list.Ordered);
        Assert.Single(list.Items);
    }

    [Fact]
    public void TrailingBreaksInsideItems_AreTrimmed()
    {
        var blocks = HtmlParser.Parse("<ol><li>one<br /> </li><li>two<br /></li></ol>");
        var list = Assert.IsType<BbList>(Assert.Single(blocks));
        Assert.Equal(new BbText("one"), Assert.Single(list.Items[0]));
        Assert.Equal(new BbText("two"), Assert.Single(list.Items[1]));
    }

    [Fact]
    public void BreaksInsideParagraph_BecomeNewlines()
    {
        var blocks = HtmlParser.Parse("line one<br />line two");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbText("line one\nline two"), Assert.Single(p.Inlines));
    }

    [Fact]
    public void Paragraphs_SplitIntoBlocks()
    {
        var blocks = HtmlParser.Parse("<p>one</p><p>two</p>");
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.IsType<BbParagraph>(b));
    }

    [Fact]
    public void InlineFormatting_SetsFlags()
    {
        var blocks = HtmlParser.Parse("a <b>bold</b> <i>it</i> <u>un</u> <strong>str</strong> <em>em</em>");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbText("a "), p.Inlines[0]);
        Assert.Equal(new BbText("bold", Bold: true), p.Inlines[1]);
        Assert.Equal(new BbText("it", Italic: true), p.Inlines[3]);
        Assert.Equal(new BbText("un", Underline: true), p.Inlines[5]);
        Assert.Equal(new BbText("str", Bold: true), p.Inlines[7]);
        Assert.Equal(new BbText("em", Italic: true), p.Inlines[9]);
    }

    [Fact]
    public void Entities_AreDecoded()
    {
        var blocks = HtmlParser.Parse("fish &amp; chips &gt; salad &nbsp;");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal("fish & chips > salad", p.Inlines.Cast<BbText>().Single().Text.Trim());
    }

    [Fact]
    public void Link_CapturesHrefAndText()
    {
        var blocks = HtmlParser.Parse("<a href=\"https://example.com/x\">Example</a>");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbLink("Example", "https://example.com/x"), Assert.Single(p.Inlines));
    }

    [Fact]
    public void Image_CapturesSrc()
    {
        var blocks = HtmlParser.Parse("<img src=\"https://x/y.png\" />");
        Assert.Equal(new BbImage("https://x/y.png"), Assert.Single(blocks));
    }

    [Fact]
    public void PreBlock_KeepsContentRaw()
    {
        var blocks = HtmlParser.Parse("<pre>if (a <b> b)</pre>");
        var code = Assert.IsType<BbCodeBlock>(Assert.Single(blocks));
        Assert.Equal("if (a <b> b)", code.Text);
    }

    [Fact]
    public void Blockquote_BecomesQuote()
    {
        var blocks = HtmlParser.Parse("<blockquote>said <b>this</b></blockquote>");
        var q = Assert.IsType<BbQuote>(Assert.Single(blocks));
        Assert.Equal(new BbText("said "), q.Inlines[0]);
        Assert.Equal(new BbText("this", Bold: true), q.Inlines[1]);
    }

    [Fact]
    public void UnknownTags_AreDroppedButTextKept()
    {
        var blocks = HtmlParser.Parse("<span style=\"color:red\">hi</span>");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbText("hi"), Assert.Single(p.Inlines));
    }

    [Fact]
    public void LoneAngleBracket_IsLiteralText()
    {
        var blocks = HtmlParser.Parse("1 < 2 and 3 > 2");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbText("1 < 2 and 3 > 2"), Assert.Single(p.Inlines));
    }

    [Fact]
    public void FormattingInsideListItems_Parses()
    {
        var blocks = HtmlParser.Parse("<ul><li>slot 12 has a <i>very</i> loose plug</li></ul>");
        var list = Assert.IsType<BbList>(Assert.Single(blocks));
        var item = Assert.Single(list.Items);
        Assert.Equal(new BbText("slot 12 has a "), item[0]);
        Assert.Equal(new BbText("very", Italic: true), item[1]);
        Assert.Equal(new BbText(" loose plug"), item[2]);
    }

    [Fact]
    public void RealWorldWhdDetail_Parses()
    {
        const string detail =
            "Some work on this needs to be done before I can take it to CAB.<br /> " +
            "We will be targeting student workstations and loaner laptops only.<br /> " +
            "<ol><li>Modify and validate the existing MaintainFreeDiskSpace script which I was using for the Spectrum labs<br /> </li>" +
            "<li>Craft scheduled task<br /> </li>" +
            "<li>Determine best method to deploy scheduled task<br /> </li>" +
            "<li>Test and validate<br /> </li>" +
            "<li>Create CAB ticket</li></ol>";
        var blocks = HtmlParser.Parse(detail);
        Assert.Equal(2, blocks.Count);
        Assert.IsType<BbParagraph>(blocks[0]);
        var list = Assert.IsType<BbList>(blocks[1]);
        Assert.True(list.Ordered);
        Assert.Equal(5, list.Items.Count);
        Assert.Equal(new BbText("Create CAB ticket"), Assert.Single(list.Items[4]));
    }

    [Fact]
    public void Dispatcher_PicksHtml_WhenTagsPresent()
    {
        var blocks = RichTextParser.Parse("<p>hi</p>");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbText("hi"), Assert.Single(p.Inlines));
    }

    [Fact]
    public void Dispatcher_PicksBbCode_WhenNoHtmlTags()
    {
        var blocks = RichTextParser.Parse("[b]bold[/b]");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbText("bold", Bold: true), Assert.Single(p.Inlines));
    }

    [Fact]
    public void Dispatcher_PlainComparisonText_StaysLiteral()
    {
        var blocks = RichTextParser.Parse("free space < 10% is bad");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbText("free space < 10% is bad"), Assert.Single(p.Inlines));
    }

    [Fact]
    public void EmailStyleDetail_MailtoLinksWithAttrsAndNumericEntities()
    {
        // Real email-generated tickets carry mailto links with extra attributes
        // and numeric character references (&#64; = @).
        var blocks = HtmlParser.Parse(
            "From: Donovan (<a href=\"mailto:donovan&#64;example.com\" rel=\"nofollow\">donovan&#64;example.com</a>)<br />");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(3, p.Inlines.Count);
        Assert.Equal(new BbText("From: Donovan ("), p.Inlines[0]);
        Assert.Equal(new BbLink("donovan@example.com", "mailto:donovan@example.com"), p.Inlines[1]);
        Assert.Equal(new BbText(")"), p.Inlines[2]);
    }

    [Fact]
    public void EntityEncodedAngleBrackets_StayLiteralText()
    {
        // Email footers wrap bare URLs in angle brackets (&lt;...&gt;); after decoding
        // they are literal text, not tags.
        var blocks = HtmlParser.Parse("[facebook]&lt;https://example.com/page&gt;<br />");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbText("[facebook]<https://example.com/page>"), Assert.Single(p.Inlines));
    }
}
