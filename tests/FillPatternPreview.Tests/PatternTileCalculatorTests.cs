using FillPatternPreview.Parsing;
using FillPatternPreview.Rendering;

namespace FillPatternPreview.Tests;

public class PatternTileCalculatorTests
{
    private static bool TryTile(string pat, out System.Windows.Size size)
    {
        var pattern = PatParser.ParseText(pat).Patterns.Values.First();
        return PatternTileCalculator.TryComputeTileSize(pattern, out size);
    }

    [Fact]
    public void SolidDiagonalFamily_TileIsOffsetOverSinCos()
    {
        // ANSI31-style: 45 degree lines 4 units apart -> horizontal/vertical repeat is 4/sin45.
        Assert.True(TryTile("*A, d\n45,0,0,0,4", out var size));
        Assert.Equal(4 / Math.Sin(Math.PI / 4), size.Width, 6);
        Assert.Equal(size.Width, size.Height, 6);
    }

    [Fact]
    public void HorizontalSolidLines_TileIsSquareOfTheOffset()
    {
        Assert.True(TryTile("*H, d\n0,0,0,0,4", out var size));
        Assert.Equal(4, size.Width, 6);
        Assert.Equal(4, size.Height, 6);
    }

    [Fact]
    public void RunningBond_ShiftedCoursesRepeatAfterTwoCourses()
    {
        // Horizontal dashed courses (8 drawn, 8 gap) shifted by 8 each course, 4 apart:
        // vertical repeat is 2 courses (8), horizontal repeat is one dash cycle (16).
        Assert.True(TryTile("*B, d\n0,0,0,8,4,8,-8", out var size));
        Assert.Equal(16, size.Width, 6);
        Assert.Equal(8, size.Height, 6);
    }

    [Fact]
    public void OrthogonalSolidFamilies_EachAxisTakesTheCrossingFamilysSpacing()
    {
        // Horizontal lines 4 apart don't constrain X; vertical lines 6 apart don't constrain Y.
        Assert.True(TryTile("*X, d\n0,0,0,0,4\n90,0,0,0,6", out var size));
        Assert.Equal(6, size.Width, 6);
        Assert.Equal(4, size.Height, 6);
    }

    [Fact]
    public void MultipleFamiliesOnTheSameAxis_TileIsCommonMultiple()
    {
        // 45 and 135 degree families 4 and 6 apart: horizontal periods 4/sin45 and 6/sin45
        // (ratio 2:3), so the shared period is 12/sin45.
        Assert.True(TryTile("*M, d\n45,0,0,0,4\n135,0,0,0,6", out var size));
        Assert.Equal(12 / Math.Sin(Math.PI / 4), size.Width, 6);
        Assert.Equal(size.Width, size.Height, 6);
    }

    [Fact]
    public void IncommensurateFamilies_HaveNoTile()
    {
        // 45 and 30 degree families share no rational period.
        Assert.False(TryTile("*I, d\n45,0,0,0,4\n30,0,0,0,4", out _));
    }

    [Fact]
    public void NullOrEmptyPattern_HasNoTile()
    {
        Assert.False(PatternTileCalculator.TryComputeTileSize(null, out _));
        Assert.False(PatternTileCalculator.TryComputeTileSize(new Model.PatternDefinition("E", null, false, []), out _));
    }
}
