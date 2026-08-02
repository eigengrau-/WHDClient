using WHDClient.Core.Models;
using Xunit;

namespace WHDClient.Core.Tests;

public class TicketDisplayTests
{
    [Fact]
    public void DisplaySubject_UsesSubject_WhenPresent()
    {
        var t = new Ticket { Id = 1, Subject = "Printer broken" };
        Assert.Equal("Printer broken", t.DisplaySubject);
    }

    [Fact]
    public void DisplaySubject_CollapsesNewlines_InSubject()
    {
        var t = new Ticket { Id = 1, Subject = "Line one\r\nLine two\nLine   three" };
        Assert.Equal("Line one Line two Line three", t.DisplaySubject);
    }

    [Fact]
    public void DisplaySubject_FallsBackToShortDetail_WithoutNewlines()
    {
        var t = new Ticket { Id = 2, ShortDetail = "First line\nSecond line" };
        Assert.Equal("First line Second line", t.DisplaySubject);
    }

    [Fact]
    public void DisplaySubject_FallsBackToStrippedDetail_WhenNoShortDetail()
    {
        var t = new Ticket { Id = 3, Detail = "<p>Hello<br/>world &amp; friends</p>" };
        Assert.Equal("Hello world & friends", t.DisplaySubject);
    }

    [Fact]
    public void DisplaySubject_TruncatesLongFallback()
    {
        var t = new Ticket { Id = 4, ShortDetail = new string('x', 100) };
        Assert.Equal(61, t.DisplaySubject.Length); // 60 chars + ellipsis
        Assert.EndsWith("…", t.DisplaySubject);
    }

    [Fact]
    public void DisplaySubject_TicketId_WhenEverythingEmpty()
    {
        var t = new Ticket { Id = 5 };
        Assert.Equal("(ticket 5)", t.DisplaySubject);
    }
}
