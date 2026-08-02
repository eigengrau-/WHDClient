using WHDClient.Core.Services;
using Xunit;

namespace WHDClient.Core.Tests;

public class QualifierBuilderTests
{
    [Fact]
    public void Clause_QuotesStringValues()
    {
        Assert.Equal("(location.locationName = 'ATL')",
            QualifierBuilder.Clause("location.locationName", QualifierBuilder.Op.Eq, "ATL"));
    }

    [Fact]
    public void Clause_NumericValueNotQuoted()
    {
        Assert.Equal("(statusTypeId = 1)",
            QualifierBuilder.Clause("statusTypeId", QualifierBuilder.Op.Eq, "1", valueIsLiteralString: false));
    }

    [Fact]
    public void And_JoinsMultipleClauses()
    {
        var q = QualifierBuilder.And(
            QualifierBuilder.Clause("a", QualifierBuilder.Op.Eq, "x", false),
            QualifierBuilder.Clause("b", QualifierBuilder.Op.Eq, "y", false));
        Assert.Equal("((a = x) and (b = y))", q);
    }

    [Fact]
    public void And_SingleClauseReturnedBare()
    {
        var q = QualifierBuilder.And(QualifierBuilder.Clause("a", QualifierBuilder.Op.Eq, "x", false));
        Assert.Equal("(a = x)", q);
    }

    [Fact]
    public void Or_JoinsWithOr()
    {
        var q = QualifierBuilder.Or("(a = 1)", "(b = 2)");
        Assert.Equal("((a = 1) or (b = 2))", q);
    }

    [Fact]
    public void Quote_EscapesSingleQuotes()
    {
        Assert.Equal("'it\\'s'", QualifierBuilder.Quote("it's"));
    }

    [Fact]
    public void EmptyClauses_ReturnEmpty()
    {
        Assert.Equal("", QualifierBuilder.And("", "  "));
    }
}
