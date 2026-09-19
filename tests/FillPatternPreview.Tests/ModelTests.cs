using FillPatternPreview.Model;

namespace FillPatternPreview.Tests;

/// <summary>
/// Tests for the data model classes.
/// </summary>
public class ModelTests
{
    [Fact]
    public void PatternDefinition_CanBeCreated()
    {
        // Arrange
        var lineGroups = new List<LineGroup>
        {
            new(0, 0, 0, 1, 0, new List<double> { 1.0, -0.5 })
        };

        // Act
        var pattern = new PatternDefinition("TEST", "Test pattern", false, lineGroups);

        // Assert
        Assert.Equal("TEST", pattern.Name);
        Assert.Equal("Test pattern", pattern.Description);
        Assert.False(pattern.IsModel);
        Assert.Single(pattern.LineGroups);
    }

    [Fact]
    public void LineGroup_CanBeCreated()
    {
        // Arrange & Act
        var lineGroup = new LineGroup(45, 0, 0, 1, 1, new List<double> { 2.0, -1.0 });

        // Assert
        Assert.Equal(45, lineGroup.AngleDeg);
        Assert.Equal(0, lineGroup.OriginX);
        Assert.Equal(0, lineGroup.OriginY);
        Assert.Equal(1, lineGroup.DeltaX);
        Assert.Equal(1, lineGroup.DeltaY);
        Assert.Equal(2, lineGroup.DashPattern.Count);
    }

    [Fact]
    public void PatternDiagnostics_CanBeCreated()
    {
        // Arrange & Act
        var diagnostics = new PatternDiagnostics(
            Success: true,
            LineGroupCount: 2,
            WarningCount: 0,
            ErrorCount: 0,
            TileSize: new Size(10, 10),
            Tileable: true,
            ParseDuration: TimeSpan.FromMilliseconds(25),
            Message: "Success");

        // Assert
        Assert.True(diagnostics.Success);
        Assert.Equal(2, diagnostics.LineGroupCount);
        Assert.Equal(0, diagnostics.WarningCount);
        Assert.Equal(0, diagnostics.ErrorCount);
        Assert.NotNull(diagnostics.TileSize);
        Assert.True(diagnostics.Tileable);
        Assert.Equal(25, diagnostics.ParseDuration.TotalMilliseconds);
        Assert.Equal("Success", diagnostics.Message);
    }

    [Fact]
    public void Size_CanBeCreated()
    {
        // Arrange & Act
        var size = new Size(100, 200);

        // Assert
        Assert.Equal(100, size.Width);
        Assert.Equal(200, size.Height);
    }
}