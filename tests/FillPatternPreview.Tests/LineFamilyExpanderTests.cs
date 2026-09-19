using System;
using System.Linq;
using System.Windows;
using FillPatternPreview.Model;
using FillPatternPreview.Rendering;

namespace FillPatternPreview.Tests;

/// <summary>
/// Regression tests for the .pat line-family expansion math extracted into
/// <see cref="LineFamilyExpander"/>. Each test below corresponds to a specific rendering bug
/// found and fixed while getting real-world .pat files (ANSI31 hatches, a brick-coursing
/// detail, a soldier-bond brick pattern, and a drafting "Concrete" scatter pattern) to render
/// correctly - see specs/FillPatternPreview.spec.md section 8 for the narrative.
/// </summary>
public class LineFamilyExpanderTests
{
    private const double Tolerance = 1e-9;

    private static Vector DirectionFor(double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180.0;
        var dir = new Vector(Math.Cos(rad), Math.Sin(rad));
        dir.Normalize();
        return dir;
    }

    private static Vector NormalFor(Vector dir) => new(-dir.Y, dir.X);

    [Theory]
    [InlineData(0)]   // horizontal: local frame coincides with world axes
    [InlineData(90)]  // vertical: local frame is a 90-degree rotation of world axes
    [InlineData(180)]
    [InlineData(-90)]
    [InlineData(45)]  // oblique: exercises the general rotation, not just axis swaps/negation
    public void ComputeWorldDelta_ShiftIsAlongDirection_OffsetIsAlongNormal(double angleDeg)
    {
        var dir = DirectionFor(angleDeg);
        var normal = NormalFor(dir);

        var delta = LineFamilyExpander.ComputeWorldDelta(dir, normal, deltaX: 3, deltaY: 5, scale: 1.0);

        // By construction, delta must decompose exactly back into 3 units along dir and 5 along
        // normal - this is the "DeltaX = shift along direction, DeltaY = offset along normal"
        // contract from the .pat format (spec section 4). Before this was fixed, DeltaX/DeltaY
        // were used as a raw (X,Y) world vector instead, which only happened to match this
        // decomposition at angle 0.
        Assert.Equal(3, Vector.Multiply(delta, dir), Tolerance);
        Assert.Equal(5, Vector.Multiply(delta, normal), Tolerance);
    }

    [Fact]
    public void ComputeRepeatRange_NegativePerpStep_StillCoversFullRectWidth()
    {
        // Exact regression case: "*Vertical90\n90,0,0,0,-75" previewed in an 800-wide control.
        // angle=90 -> dir=(0,1), normal=(-1,0). worldDelta for (shift=0, offset=-75) is (75,0):
        // delta·normal = 75*(-1) = -75 (negative), even though the copies clearly need to march
        // in +X to cover the rect. Using |delta·normal| instead of the signed value here used to
        // clamp kMax to about 1, rendering only the first 2-3 copies regardless of rect width.
        var dir = DirectionFor(90);
        var normal = NormalFor(dir);
        var delta = LineFamilyExpander.ComputeWorldDelta(dir, normal, deltaX: 0, deltaY: -75, scale: 1.0);
        var rect = new Rect(0, 0, 800, 450);

        var origin = new Point(0, 0);
        var (kMin, kMax) = LineFamilyExpander.ComputeRepeatRange(rect, origin, delta, normal, scale: 1.0);

        // The raw span (kMax-kMin) is NOT sufficient to check here: the sign bug produces a
        // similarly-sized span by pairing a huge, useless negative kMin (all off-screen, since
        // delta.X > 0) with a small kMax - so the actual on-screen coverage must be checked
        // directly. copies must reach from at or before the left edge to at or after the right.
        double minX = Math.Min(origin.X + kMin * delta.X, origin.X + kMax * delta.X);
        double maxX = Math.Max(origin.X + kMin * delta.X, origin.X + kMax * delta.X);
        Assert.True(minX <= rect.Left, $"Expected coverage back to the left edge (0), got minX={minX} (kMin={kMin}, kMax={kMax}).");
        Assert.True(maxX >= rect.Right, $"Expected coverage out to the right edge (800), got maxX={maxX} (kMin={kMin}, kMax={kMax}).");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(135)]
    [InlineData(180)]
    [InlineData(-45)]
    [InlineData(-90)]
    [InlineData(-135)]
    public void TryIntersectInfiniteLineWithRect_OrdersChordAlongDirection(double angleDeg)
    {
        var dir = DirectionFor(angleDeg);
        var rect = new Rect(0, 0, 200, 100);

        var found = LineFamilyExpander.TryIntersectInfiniteLineWithRect(new Point(100, 50), dir, rect, out var p1, out var p2);

        Assert.True(found);
        // Walking from p1 toward p2 must move in +direction, never away from it - this is what
        // dash expansion (ExpandDashSegments) assumes. Before this was fixed, families whose
        // direction pointed "backwards" relative to a naive left/top-first intersection order
        // (e.g. 180 degrees, -90 degrees) had this backwards, which pushed their entire dashed
        // geometry outside the render clip.
        Assert.True(Vector.Multiply(p2 - p1, dir) >= -Tolerance,
            $"Chord for angle {angleDeg} is ordered backwards relative to direction.");
    }

    [Fact]
    public void ExpandDashSegments_EmptyPattern_YieldsSingleSolidSegment()
    {
        var p1 = new Point(0, 0);
        var p2 = new Point(100, 0);

        var segments = LineFamilyExpander.ExpandDashSegments(p1, p2, p1, new Vector(1, 0), Array.Empty<double>(), scale: 1.0, strokeThickness: 1.0).ToList();

        var seg = Assert.Single(segments);
        Assert.Equal(LineFamilyExpander.SegmentKind.Line, seg.Kind);
        Assert.Equal(p1, seg.Start);
        Assert.Equal(p2, seg.End);
    }

    [Fact]
    public void ExpandDashSegments_PhaseIsAnchoredToBasePoint_NotToP1()
    {
        var p1 = new Point(0, 0);
        var p2 = new Point(100, 0);
        var dir = new Vector(1, 0);
        var pattern = new double[] { 10, -10 };

        // basePoint sits 5 units before p1 along dir, so p1 is already 5 units into the first
        // "draw" segment - the first segment returned must be the remaining 5 units, not a
        // fresh 10-unit segment starting at p1. Before the phase-anchor fix, the dash cycle
        // always restarted at pos=0 from p1, discarding the family's true origin/shift and
        // misplacing dashes relative to the pattern's actual coursing.
        var basePoint = new Point(-5, 0);

        var first = LineFamilyExpander.ExpandDashSegments(p1, p2, basePoint, dir, pattern, scale: 1.0, strokeThickness: 1.0).First();

        Assert.Equal(LineFamilyExpander.SegmentKind.Line, first.Kind);
        Assert.Equal(new Point(0, 0), first.Start);
        Assert.Equal(new Point(5, 0), first.End);
    }

    [Fact]
    public void ExpandDashSegments_SubPixelCycle_DrawsSolidInsteadOfHanging()
    {
        // Exact regression case: a drafting "Concrete" pattern with dash=[0.03,-0.33] previewed
        // at Scale=1 (before units-aware scaling existed) produces a cycle far under 1 device
        // pixel. Walking it segment-by-segment across a long visible chord used to require
        // millions of draw calls per line and hang the UI thread; it must now short-circuit to
        // a single solid segment.
        var p1 = new Point(0, 0);
        var p2 = new Point(100000, 0); // a long chord, as a wide/zoomed preview would produce

        var segments = LineFamilyExpander.ExpandDashSegments(p1, p2, p1, new Vector(1, 0), new[] { 0.03, -0.33 }, scale: 1.0, strokeThickness: 1.0).ToList();

        var seg = Assert.Single(segments);
        Assert.Equal(LineFamilyExpander.SegmentKind.Line, seg.Kind);
        Assert.Equal(p1, seg.Start);
        Assert.Equal(p2, seg.End);
    }

    [Fact]
    public void ExpandDashSegments_NeverExceedsMaxSegmentCap()
    {
        // A cycle just at the sub-pixel-shortcut threshold (not small enough to trigger it) over
        // an extremely long chord: without a hard cap this would iterate ~1,000,000 times.
        var p1 = new Point(0, 0);
        var p2 = new Point(1_000_000, 0);

        var segments = LineFamilyExpander.ExpandDashSegments(p1, p2, p1, new Vector(1, 0), new[] { 0.5, -0.5 }, scale: 1.0, strokeThickness: 1.0).ToList();

        Assert.True(segments.Count <= LineFamilyExpander.MaxDashSegmentsPerChord,
            $"Expected at most {LineFamilyExpander.MaxDashSegmentsPerChord} segments, got {segments.Count}.");
    }

    [Fact]
    public void ExpandDashSegments_DrawsExpectedSegmentBoundaries()
    {
        var p1 = new Point(0, 0);
        var p2 = new Point(50, 0);

        var segments = LineFamilyExpander.ExpandDashSegments(p1, p2, p1, new Vector(1, 0), new[] { 10.0, -5.0 }, scale: 1.0, strokeThickness: 1.0).ToList();

        // Cycle is 15 (10 drawn + 5 gap): draw 0-10, gap 10-15, draw 15-25, gap 25-30, draw
        // 30-40, gap 40-45, then a final draw clipped to the chord's length at 50.
        Assert.Equal(new[] { (0.0, 10.0), (15.0, 25.0), (30.0, 40.0), (45.0, 50.0) }, segments.Select(s => (s.Start.X, s.End.X)));
    }

    [Fact]
    public void ExpandDashSegments_ZeroDashProducesDot()
    {
        var p1 = new Point(0, 0);
        // Short enough that only the first pattern entry (the dot) is reached before length
        // is exhausted - the dot advances by strokeThickness*2 = 2, then the following gap
        // entry (-5) would only complete at pos 7, past this chord's length of 5.
        var p2 = new Point(5, 0);

        var segments = LineFamilyExpander.ExpandDashSegments(p1, p2, p1, new Vector(1, 0), new[] { 0.0, -5.0 }, scale: 1.0, strokeThickness: 1.0).ToList();

        var dot = Assert.Single(segments);
        Assert.Equal(LineFamilyExpander.SegmentKind.Dot, dot.Kind);
        Assert.Equal(p1, dot.Start);
        Assert.Equal(dot.Start, dot.End);
    }

    private static PatternDefinition MakePattern(bool isModel, string? units) =>
        new("Test", null, isModel, Array.Empty<LineGroup>(), units);

    [Fact]
    public void GetUnitsToDipFactor_ModelPatternsAreAlwaysOneToOne()
    {
        Assert.Equal(1.0, LineFamilyExpander.GetUnitsToDipFactor(MakePattern(isModel: true, units: "MM")));
        Assert.Equal(1.0, LineFamilyExpander.GetUnitsToDipFactor(MakePattern(isModel: true, units: null)));
    }

    [Theory]
    [InlineData(null, 96.0)]          // unspecified: AutoCAD/Revit default drafting-pattern unit is inches
    [InlineData("INCH", 96.0)]
    [InlineData("BOGUS", 96.0)]       // unrecognized falls back to inch, not a crash
    public void GetUnitsToDipFactor_DraftingInchVariants(string? units, double expected)
    {
        Assert.Equal(expected, LineFamilyExpander.GetUnitsToDipFactor(MakePattern(isModel: false, units)), Tolerance);
    }

    [Fact]
    public void GetUnitsToDipFactor_DraftingMillimeters_ConvertsThroughInches()
    {
        // Exact regression case: the "Concrete" drafting pattern's ";%UNITS=MM" needs 96/25.4
        // DIPs per native unit to render at print-true size by default.
        var actual = LineFamilyExpander.GetUnitsToDipFactor(MakePattern(isModel: false, "MM"));
        Assert.Equal(96.0 / 25.4, actual, Tolerance);
    }
}
