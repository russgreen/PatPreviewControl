using System.Windows;
using FillPatternPreview.Rendering;

namespace FillPatternPreview.Tests;

public class InteractionMathTests
{
    // Device position of a pattern-space point under the view convention (w + pan) * scale.
    private static Point ToDevice(Point w, Point pan, double scale)
        => new((w.X + pan.X) * scale, (w.Y + pan.Y) * scale);

    [Theory]
    [InlineData(0.001, InteractionMath.MinZoom)]
    [InlineData(1.0, 1.0)]
    [InlineData(500, InteractionMath.MaxZoom)]
    public void ClampZoom_KeepsZoomInRange(double input, double expected)
        => Assert.Equal(expected, InteractionMath.ClampZoom(input));

    [Fact]
    public void ClampZoom_NaN_FallsBackToOne()
        => Assert.Equal(1.0, InteractionMath.ClampZoom(double.NaN));

    [Fact]
    public void WheelFactor_IsExponentialPerNotch()
    {
        Assert.Equal(1.0, InteractionMath.WheelFactor(0));
        Assert.Equal(InteractionMath.WheelStep, InteractionMath.WheelFactor(120), 9);
        Assert.Equal(InteractionMath.WheelStep * InteractionMath.WheelStep, InteractionMath.WheelFactor(240), 9);
        Assert.Equal(1 / InteractionMath.WheelStep, InteractionMath.WheelFactor(-120), 9);
    }

    [Theory]
    [InlineData(1.0, 0, 0, 100, 60, 1.1)]
    [InlineData(1.0, 0, 0, 100, 60, 0.5)]
    [InlineData(2.0, 5, -3, 0, 0, 3.0)]
    [InlineData(0.7, -12, 8, 250, 190, 1.1)]
    public void ZoomAt_KeepsPointUnderAnchorFixed(double zoom, double panX, double panY, double ax, double ay, double factor)
    {
        const double baseScale = 3.0;
        var pan = new Point(panX, panY);
        var anchor = new Point(ax, ay);

        // The pattern point currently under the anchor.
        double scale = baseScale * zoom;
        var w = new Point(anchor.X / scale - pan.X, anchor.Y / scale - pan.Y);

        var (newZoom, newPan) = InteractionMath.ZoomAt(zoom, pan, baseScale, anchor, factor);

        Assert.Equal(zoom * factor, newZoom, 9);
        var after = ToDevice(w, newPan, baseScale * newZoom);
        Assert.Equal(anchor.X, after.X, 6);
        Assert.Equal(anchor.Y, after.Y, 6);
    }

    [Fact]
    public void ZoomAt_AtMaxZoom_ChangesNothing()
    {
        var pan = new Point(4, 5);
        var (zoom, newPan) = InteractionMath.ZoomAt(InteractionMath.MaxZoom, pan, 1.0, new Point(10, 10), 1.5);
        Assert.Equal(InteractionMath.MaxZoom, zoom);
        Assert.Equal(pan, newPan);
    }

    [Fact]
    public void ZoomAt_ClampsAtLimitAndStillKeepsAnchorFixed()
    {
        // 15 * 2 would exceed the max; the result must land on the max, with the anchor held.
        var anchor = new Point(80, 40);
        var (zoom, pan) = InteractionMath.ZoomAt(15, new Point(0, 0), 2.0, anchor, 2.0);
        Assert.Equal(InteractionMath.MaxZoom, zoom);

        var w = new Point(anchor.X / (2.0 * 15), anchor.Y / (2.0 * 15));
        var after = ToDevice(w, pan, 2.0 * zoom);
        Assert.Equal(anchor.X, after.X, 6);
        Assert.Equal(anchor.Y, after.Y, 6);
    }

    [Fact]
    public void ZoomAt_NonPositiveBaseScale_ChangesNothing()
    {
        var pan = new Point(1, 2);
        var (zoom, newPan) = InteractionMath.ZoomAt(1.0, pan, 0, new Point(5, 5), 1.1);
        Assert.Equal(1.0, zoom);
        Assert.Equal(pan, newPan);
    }

    [Fact]
    public void PanBy_MovesPatternByTheDragDistanceOnScreen()
    {
        const double scale = 4.0;
        var w = new Point(7, 3);
        var pan = new Point(1, 2);
        var before = ToDevice(w, pan, scale);

        var newPan = InteractionMath.PanBy(pan, new Vector(20, -8), scale);

        var after = ToDevice(w, newPan, scale);
        Assert.Equal(before.X + 20, after.X, 9);
        Assert.Equal(before.Y - 8, after.Y, 9);
    }

    [Fact]
    public void PanBy_NonPositiveScale_ChangesNothing()
    {
        var pan = new Point(1, 2);
        Assert.Equal(pan, InteractionMath.PanBy(pan, new Vector(10, 10), 0));
    }
}
