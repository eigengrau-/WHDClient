using WHDClient.Core.BbCode;
using Xunit;

namespace WHDClient.Core.Tests;

public class BbCodeParserTests
{
    [Fact]
    public void PlainText_BecomesSingleParagraph()
    {
        var blocks = BbCodeParser.Parse("Hello world");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        var t = Assert.IsType<BbText>(Assert.Single(p.Inlines));
        Assert.Equal("Hello world", t.Text);
        Assert.False(t.Bold);
    }

    [Fact]
    public void Empty_And_Null_ReturnNoBlocks()
    {
        Assert.Empty(BbCodeParser.Parse(null));
        Assert.Empty(BbCodeParser.Parse("   "));
    }

    [Fact]
    public void Bold_SetsFlagOnRun()
    {
        var blocks = BbCodeParser.Parse("a [b]bold[/b] c");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(3, p.Inlines.Count);
        Assert.Equal(new BbText("a "), p.Inlines[0]);
        Assert.Equal(new BbText("bold", Bold: true), p.Inlines[1]);
        Assert.Equal(new BbText(" c"), p.Inlines[2]);
    }

    [Fact]
    public void NestedFormatting_CombinesFlags()
    {
        var blocks = BbCodeParser.Parse("[b][i]both[/i][/b]");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        var t = Assert.IsType<BbText>(Assert.Single(p.Inlines));
        Assert.Equal(new BbText("both", Bold: true, Italic: true), t);
    }

    [Fact]
    public void UnorderedList_ParsesItems()
    {
        var blocks = BbCodeParser.Parse("[list][*]one[*]two[/list]");
        var list = Assert.IsType<BbList>(Assert.Single(blocks));
        Assert.False(list.Ordered);
        Assert.Equal(2, list.Items.Count);
        Assert.Equal(new BbText("one"), Assert.Single(list.Items[0]));
        Assert.Equal(new BbText("two"), Assert.Single(list.Items[1]));
    }

    [Fact]
    public void OrderedList_DetectedByEquals1()
    {
        var blocks = BbCodeParser.Parse("[list=1][*]one[/list]");
        var list = Assert.IsType<BbList>(Assert.Single(blocks));
        Assert.True(list.Ordered);
        Assert.Single(list.Items);
    }

    [Fact]
    public void ListItem_FormattingParsed()
    {
        var blocks = BbCodeParser.Parse("[list][*][b]one[/b][/list]");
        var list = Assert.IsType<BbList>(Assert.Single(blocks));
        Assert.Equal(new BbText("one", Bold: true), Assert.Single(list.Items[0]));
    }

    [Fact]
    public void CodeBlock_KeepsContentRaw()
    {
        var blocks = BbCodeParser.Parse("[code][b]not bold[/b][/code]");
        var code = Assert.IsType<BbCodeBlock>(Assert.Single(blocks));
        Assert.Equal("[b]not bold[/b]", code.Text);
    }

    [Fact]
    public void Quote_ParsesInlines()
    {
        var blocks = BbCodeParser.Parse("[quote]said [b]this[/b][/quote]");
        var q = Assert.IsType<BbQuote>(Assert.Single(blocks));
        var p = Assert.IsType<BbParagraph>(Assert.Single(q.Blocks));
        Assert.Equal(2, p.Inlines.Count);
        Assert.Equal(new BbText("said "), p.Inlines[0]);
        Assert.Equal(new BbText("this", Bold: true), p.Inlines[1]);
    }

    [Fact]
    public void Image_CapturesUrl()
    {
        var blocks = BbCodeParser.Parse("[img]https://x/y.png[/img]");
        Assert.Equal(new BbImage("https://x/y.png"), Assert.Single(blocks));
    }

    [Fact]
    public void UrlWithParameter_UsesParamAsTarget()
    {
        var blocks = BbCodeParser.Parse("[url=https://example.com]Example[/url]");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbLink("Example", "https://example.com"), Assert.Single(p.Inlines));
    }

    [Fact]
    public void UrlWithoutParameter_UsesInnerTextAsTarget()
    {
        var blocks = BbCodeParser.Parse("[url]https://example.com[/url]");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbLink("https://example.com", "https://example.com"), Assert.Single(p.Inlines));
    }

    [Fact]
    public void UnknownTags_AreLeftLiteral()
    {
        var blocks = BbCodeParser.Parse("[color=red]x[/color]");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        var t = Assert.IsType<BbText>(Assert.Single(p.Inlines));
        Assert.Equal("[color=red]x[/color]", t.Text);
    }

    [Fact]
    public void StrayCloseTag_IsLeftLiteral()
    {
        var blocks = BbCodeParser.Parse("a[/b]");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbText("a[/b]"), Assert.Single(p.Inlines));
    }

    [Fact]
    public void BlankLines_SplitParagraphs()
    {
        var blocks = BbCodeParser.Parse("one\n\ntwo");
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.IsType<BbParagraph>(b));
    }

    [Fact]
    public void TextAroundBlock_PreservesOrder()
    {
        var blocks = BbCodeParser.Parse("before\n[code]x[/code]\nafter");
        Assert.Equal(3, blocks.Count);
        Assert.IsType<BbParagraph>(blocks[0]);
        Assert.IsType<BbCodeBlock>(blocks[1]);
        Assert.IsType<BbParagraph>(blocks[2]);
    }

    [Fact]
    public void HtmlEntities_AreDecoded()
    {
        var blocks = BbCodeParser.Parse("It&#39;s a &quot;test&quot; &amp; more");
        var p = Assert.IsType<BbParagraph>(Assert.Single(blocks));
        Assert.Equal(new BbText("It's a \"test\" & more"), Assert.Single(p.Inlines));
    }

    [Fact]
    public void Table_ParsesRowsAndCells()
    {
        var blocks = BbCodeParser.Parse(
            "[table][tr][th]Name[/th][th]Value[/th][/tr][tr][td]a[/td][td][b]b[/b][/td][/tr][/table]");
        var table = Assert.IsType<BbTable>(Assert.Single(blocks));
        Assert.Equal(2, table.Rows.Count);
        Assert.True(table.Rows[0].Cells[0].Header);
        Assert.Equal(new BbText("Name"), Assert.Single(table.Rows[0].Cells[0].Inlines));
        Assert.False(table.Rows[1].Cells[1].Header);
        Assert.Equal(new BbText("b", Bold: true), Assert.Single(table.Rows[1].Cells[1].Inlines));
    }

    [Fact]
    public void Quote_WithNestedBlockTag_ParsesRecursively()
    {
        var blocks = BbCodeParser.Parse("[quote]notes:[list][*]one[/list][/quote]");
        var q = Assert.IsType<BbQuote>(Assert.Single(blocks));
        Assert.Equal(2, q.Blocks.Count);
        Assert.IsType<BbParagraph>(q.Blocks[0]);
        var list = Assert.IsType<BbList>(q.Blocks[1]);
        Assert.Equal(new BbText("one"), Assert.Single(Assert.Single(list.Items)));
    }

    [Fact]
    public void NestedQuotes_ParseFully()
    {
        var blocks = BbCodeParser.Parse("[quote]a[quote]b[/quote]c[/quote]");
        var q = Assert.IsType<BbQuote>(Assert.Single(blocks));
        Assert.Equal(3, q.Blocks.Count);
        Assert.Equal(new BbText("a"), Assert.Single(Assert.IsType<BbParagraph>(q.Blocks[0]).Inlines));
        var inner = Assert.IsType<BbQuote>(q.Blocks[1]);
        Assert.Equal(new BbText("b"), Assert.Single(Assert.IsType<BbParagraph>(Assert.Single(inner.Blocks)).Inlines));
        Assert.Equal(new BbText("c"), Assert.Single(Assert.IsType<BbParagraph>(q.Blocks[2]).Inlines));
    }
}
