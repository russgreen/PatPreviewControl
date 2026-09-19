using System.Globalization;
using System.Threading;
using FillPatternPreview.Parsing;

namespace FillPatternPreview.Tests;

public class ParsingTests
{
    [Fact]
    public void ParsesSingleLineGroup()
    {
        var result = PatParser.ParseText("*ANSI31\n45,0,0,0,4");

        Assert.True(result.Success);
        var pattern = Assert.Single(result.Patterns.Values);
        Assert.Equal("ANSI31", pattern.Name);
        var group = Assert.Single(pattern.LineGroups);
        Assert.Equal(45, group.AngleDeg);
        Assert.Equal(0, group.OriginX);
        Assert.Equal(0, group.OriginY);
        Assert.Equal(0, group.DeltaX);
        Assert.Equal(4, group.DeltaY);
        Assert.Empty(group.DashPattern);
    }

    [Fact]
    public void ParsesHeaderDescriptionAndDashArray()
    {
        var result = PatParser.ParseText("*Concrete, aggregate texture\n50,0,0,-0.19481,-0.2359158,0.03,-0.33");

        var pattern = Assert.Single(result.Patterns.Values);
        Assert.Equal("Concrete", pattern.Name);
        Assert.Equal("aggregate texture", pattern.Description);
        var group = Assert.Single(pattern.LineGroups);
        Assert.Equal(new[] { 0.03, -0.33 }, group.DashPattern);
    }

    [Theory]
    [InlineData("*Name ;%TYPE=MODEL\n0,0,0,0,10")]
    [InlineData("*Name\n;%TYPE=MODEL\n0,0,0,0,10")]
    public void RecognizesTypeModelInlineOrStandalone(string text)
    {
        var result = PatParser.ParseText(text);

        var pattern = Assert.Single(result.Patterns.Values);
        Assert.True(pattern.IsModel);
    }

    [Fact]
    public void DefaultsToDraftingWhenTypeTagAbsent()
    {
        var result = PatParser.ParseText("*Name\n0,0,0,0,10");

        var pattern = Assert.Single(result.Patterns.Values);
        Assert.False(pattern.IsModel);
    }

    [Fact]
    public void ParsesFileLevelUnitsAndAppliesToAllPatterns()
    {
        var result = PatParser.ParseText(";%UNITS=INCH\n*First\n0,0,0,0,10\n*Second\n90,0,0,0,10");

        Assert.Equal("INCH", result.Patterns["First"].Units);
        Assert.Equal("INCH", result.Patterns["Second"].Units);
    }

    [Fact]
    public void PerPatternUnitsOverrideFileLevelDefault()
    {
        var result = PatParser.ParseText(";%UNITS=INCH\n*First\n0,0,0,0,10\n*Second\n;%UNITS=MM\n90,0,0,0,10");

        Assert.Equal("INCH", result.Patterns["First"].Units);
        Assert.Equal("MM", result.Patterns["Second"].Units);
    }

    [Fact]
    public void SkipsFullLineAndTrailingComments()
    {
        var result = PatParser.ParseText("; leading comment\n*Name ; trailing header comment\n; another comment\n0,0,0,0,10 ; trailing data comment");

        var pattern = Assert.Single(result.Patterns.Values);
        var group = Assert.Single(pattern.LineGroups);
        Assert.Equal(10, group.DeltaY);
    }

    [Fact]
    public void RecordsErrorAndSkipsLineWithTooFewTokens()
    {
        var result = PatParser.ParseText("*Name\n0,0,0,0\n90,0,0,0,10");

        Assert.NotEmpty(result.Errors);
        var pattern = Assert.Single(result.Patterns.Values);
        var group = Assert.Single(pattern.LineGroups);
        Assert.Equal(90, group.AngleDeg);
    }

    [Fact]
    public void RecordsErrorAndSkipsLineWithUnparsableNumber()
    {
        var result = PatParser.ParseText("*Name\nabc,0,0,0,10\n90,0,0,0,10");

        Assert.NotEmpty(result.Errors);
        var pattern = Assert.Single(result.Patterns.Values);
        Assert.Single(pattern.LineGroups);
    }

    [Fact]
    public void DuplicatePatternNameKeepsLastDefinitionAndWarns()
    {
        var result = PatParser.ParseText("*Name\n0,0,0,0,10\n*Name\n90,0,0,0,20");

        Assert.NotEmpty(result.Warnings);
        var pattern = Assert.Single(result.Patterns.Values);
        var group = Assert.Single(pattern.LineGroups);
        Assert.Equal(90, group.AngleDeg); // last-wins, not first-wins - see spec section 7
    }

    [Fact]
    public void SelectsNamedPatternFromMultiPatternFile()
    {
        var result = PatParser.ParseText("*First\n0,0,0,0,10\n*Second\n90,0,0,0,20");

        Assert.Equal(2, result.Patterns.Count);
        Assert.Equal(90, result.Patterns["Second"].LineGroups[0].AngleDeg);
    }

    [Fact]
    public void ParsesNumbersWithInvariantCultureRegardlessOfThreadCulture()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            // A culture that uses ',' as the decimal separator would misparse "0.5" as 5 (or
            // fail) if the parser ever used CurrentCulture instead of InvariantCulture.
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var result = PatParser.ParseText("*Name\n45,0,0,0,0.5");

            var group = Assert.Single(Assert.Single(result.Patterns.Values).LineGroups);
            Assert.Equal(0.5, group.DeltaY);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void EmptyPatternIsKeptWithWarning()
    {
        var result = PatParser.ParseText("*EmptyPattern\n");

        var pattern = Assert.Single(result.Patterns.Values);
        Assert.Empty(pattern.LineGroups);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void FileNotFoundReturnsErrorWithoutThrowing()
    {
        var result = PatParser.ParseFile(@"Z:\does\not\exist.pat");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }
}
