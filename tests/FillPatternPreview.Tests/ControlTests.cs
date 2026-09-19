using FillPatternPreview.Controls;

namespace FillPatternPreview.Tests;

/// <summary>
/// Tests for the FillPatternPreview control.
/// </summary>
public class ControlTests
{
    [Fact]
    public void FillPatternPreview_CanBeCreated()
    {
        // Act
        var control = new FillPatternPreview.Controls.FillPatternPreview();

        // Assert
        Assert.NotNull(control);
        Assert.Equal(PatternSource.None, control.PatternSource);
        Assert.Equal(1.0, control.Scale);
        Assert.Equal(1.0, control.Zoom);
    }

    [Fact]
    public void Zoom_CoercesToValidRange()
    {
        // Arrange
        var control = new FillPatternPreview.Controls.FillPatternPreview();

        // Act & Assert - Test minimum
        control.Zoom = -1.0;
        Assert.Equal(0.1, control.Zoom);

        // Act & Assert - Test maximum
        control.Zoom = 100.0;
        Assert.Equal(20.0, control.Zoom);

        // Act & Assert - Test valid value
        control.Zoom = 5.0;
        Assert.Equal(5.0, control.Zoom);
    }

    [Fact]
    public void PatternSource_CanBeChanged()
    {
        // Arrange
        var control = new FillPatternPreview.Controls.FillPatternPreview();

        // Act
        control.PatternSource = PatternSource.PatFile;

        // Assert
        Assert.Equal(PatternSource.PatFile, control.PatternSource);
    }

    [Fact]
    public void PanOffset_CanBeSet()
    {
        // Arrange
        var control = new FillPatternPreview.Controls.FillPatternPreview();
        var point = new Point(10, 20);

        // Act
        control.PanOffset = point;

        // Assert
        Assert.Equal(10, control.PanOffset.X);
        Assert.Equal(20, control.PanOffset.Y);
    }

    [Fact]
    public void Scale_CanBeSet()
    {
        // Arrange
        var control = new FillPatternPreview.Controls.FillPatternPreview();

        // Act
        control.Scale = 2.5;

        // Assert
        Assert.Equal(2.5, control.Scale);
    }
}