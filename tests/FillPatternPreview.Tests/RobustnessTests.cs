using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FillPatternPreview.Parsing;
using FillPatternPreview.Rendering;
using Preview = FillPatternPreview.Controls.FillPatternPreview;

namespace FillPatternPreview.Tests;

public class ParserRobustnessTests
{
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e999")]
    [InlineData("1e12")]
    public void NonFiniteOrAbsurdValues_AreRejectedAsErrors(string token)
    {
        var result = PatParser.ParseText($"*P, d\n0,0,0,0,{token}\n45,0,0,0,4");

        Assert.NotEmpty(result.Errors);
        // The bad line is skipped; the good one survives.
        var pattern = result.Patterns["P"];
        Assert.Single(pattern.LineGroups);
        Assert.Equal(45, pattern.LineGroups[0].AngleDeg);
    }

    [Fact]
    public void TooManyDashEntries_LineIsSkipped()
    {
        var dashes = string.Join(",", Enumerable.Repeat("1,-1", PatParser.MaxDashEntries));
        var result = PatParser.ParseText($"*P, d\n0,0,0,0,4,{dashes}");

        Assert.NotEmpty(result.Errors);
        Assert.Empty(result.Patterns["P"].LineGroups);
    }

    [Fact]
    public void OverlongLine_IsSkippedWithoutTokenizingIt()
    {
        var longLine = "0,0,0,0,4," + new string('1', PatParser.MaxLineLength);
        var result = PatParser.ParseText($"*P, d\n{longLine}\n45,0,0,0,4");

        Assert.Contains(result.Errors, e => e.Contains("too long"));
        Assert.Single(result.Patterns["P"].LineGroups);
    }

    [Fact]
    public void TooManyLineGroups_ExtraLinesAreSkipped()
    {
        var sb = new StringBuilder("*P, d\n");
        for (int i = 0; i < PatParser.MaxLineGroupsPerPattern + 10; i++)
        {
            sb.AppendLine("0,0,0,0,4");
        }

        var result = PatParser.ParseText(sb.ToString());

        Assert.Equal(PatParser.MaxLineGroupsPerPattern, result.Patterns["P"].LineGroups.Count);
        Assert.Equal(10, result.Errors.Count);
    }

    [Fact]
    public void TooManyPatterns_ParsingStopsAtTheLimit()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < PatParser.MaxPatterns + 5; i++)
        {
            sb.Append('*').Append('p').Append(i).Append('\n').Append("0,0,0,0,4\n");
        }

        var result = PatParser.ParseText(sb.ToString());

        Assert.Equal(PatParser.MaxPatterns, result.Patterns.Count);
        Assert.Contains(result.Errors, e => e.Contains("Too many patterns"));
    }

    [Fact]
    public void OversizedText_IsRefusedUpFront()
    {
        var result = PatParser.ParseText(new string(' ', PatParser.MaxInputChars + 1));

        Assert.Empty(result.Patterns);
        Assert.Contains(result.Errors, e => e.Contains("too large"));
    }

    [Fact]
    public void OversizedFile_IsRefusedWithoutReadingIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fpp-big-{Guid.NewGuid():N}.pat");
        try
        {
            using (var fs = File.Create(path))
            {
                fs.SetLength(PatParser.MaxInputChars + 1L); // sparse; never read
            }

            var result = PatParser.ParseFile(path);

            Assert.Empty(result.Patterns);
            Assert.Contains(result.Errors, e => e.Contains("too large"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnreadableFile_ReturnsAnErrorInsteadOfThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fpp-locked-{Guid.NewGuid():N}.pat");
        try
        {
            File.WriteAllText(path, "*P, d\n0,0,0,0,4");
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var result = PatParser.ParseFile(path);

            Assert.Empty(result.Patterns);
            Assert.NotEmpty(result.Errors);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FilePathThatIsADirectory_ReturnsAnError()
    {
        var result = PatParser.ParseFile(Path.GetTempPath());

        Assert.Empty(result.Patterns);
        Assert.NotEmpty(result.Errors);
    }
}

public class GeometryRobustnessTests
{
    private static readonly Rect Viewport = new(0, 0, 400, 300);

    [Fact]
    public void IsRepeatRangeUsable_DoesNotOverflowOnExtremeBounds()
    {
        // In plain int arithmetic int.MaxValue - int.MinValue wraps to -1, which would pass a
        // "> 4000" skip check and send the render loop through four billion iterations.
        Assert.False(LineFamilyExpander.IsRepeatRangeUsable(int.MinValue, int.MaxValue));
        Assert.False(LineFamilyExpander.IsRepeatRangeUsable(int.MinValue, 100));
        Assert.True(LineFamilyExpander.IsRepeatRangeUsable(-10, 10));
    }

    [Theory]
    [InlineData(-1e300, 1e300, false)]   // spans everything: skipped
    [InlineData(-1e12, 5, false)]
    [InlineData(1e300, 1e301, true)]     // clamped to a single far-away index: harmless, one chord that misses
    public void RepeatRangeFromBounds_ExtremeBounds_ClampInsteadOfWrapping(double a, double b, bool usable)
    {
        var (kMin, kMax) = LineFamilyExpander.RepeatRangeFromBounds(a, b);

        Assert.True(kMin <= kMax);
        // The loop bound must stay below int.MaxValue so `k++` can't wrap and never terminate.
        Assert.True(kMax < int.MaxValue);
        Assert.Equal(usable, LineFamilyExpander.IsRepeatRangeUsable(kMin, kMax));
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.NaN)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(1, double.NegativeInfinity)]
    public void RepeatRangeFromBounds_NonFinite_IsEmpty(double a, double b)
    {
        var (kMin, kMax) = LineFamilyExpander.RepeatRangeFromBounds(a, b);

        Assert.True(kMax < kMin);
    }

    [Fact]
    public void ComputeRepeatRange_NaNOrigin_IsEmpty()
    {
        var normal = new Vector(0, 1);
        var (kMin, kMax) = LineFamilyExpander.ComputeRepeatRange(Viewport, new Point(double.NaN, 0), new Vector(0, 4), normal, 1.0);

        Assert.True(kMax < kMin);
    }

    [Fact]
    public void ComputeRepeatRange_HugePan_IsSkippedNotOverflowed()
    {
        // A viewport a billion units away from the origin with 4-unit spacing.
        var rect = new Rect(-1e9, -1e9, 400, 300);
        var (kMin, kMax) = LineFamilyExpander.ComputeRepeatRange(rect, new Point(0, 0), new Vector(0, 4), new Vector(0, 1), 1.0);

        Assert.True(kMax >= kMin);
        Assert.True((long)kMax - kMin < int.MaxValue);
    }

    [Fact]
    public void TryIntersect_NonFiniteInput_ReturnsFalse()
    {
        Assert.False(LineFamilyExpander.TryIntersectInfiniteLineWithRect(new Point(double.NaN, 5), new Vector(1, 0), Viewport, out _, out _));
        Assert.False(LineFamilyExpander.TryIntersectInfiniteLineWithRect(new Point(0, double.PositiveInfinity), new Vector(1, 0), Viewport, out _, out _));
        Assert.False(LineFamilyExpander.TryIntersectInfiniteLineWithRect(new Point(0, 5), new Vector(double.NaN, 0), Viewport, out _, out _));
    }

    [Fact]
    public void ExpandDashSegments_NonFiniteScaleOrEndpoints_YieldNothing()
    {
        var dir = new Vector(1, 0);
        var dashes = new[] { 4.0, -2.0 };

        Assert.Empty(LineFamilyExpander.ExpandDashSegments(new Point(0, 0), new Point(100, 0), new Point(0, 0), dir, dashes, double.PositiveInfinity, 1));
        Assert.Empty(LineFamilyExpander.ExpandDashSegments(new Point(0, 0), new Point(100, 0), new Point(0, 0), dir, dashes, double.NaN, 1));
        Assert.Empty(LineFamilyExpander.ExpandDashSegments(new Point(0, 0), new Point(double.NaN, 0), new Point(0, 0), dir, dashes, 1, 1));
    }
}

public class BudgetAndCoercionTests
{
    [Fact]
    public void RenderBudget_AllowsExactlyLimitUnitsThenRefuses()
    {
        var budget = new RenderBudget(3);

        Assert.True(budget.TryConsume());
        Assert.True(budget.TryConsume());
        Assert.False(budget.IsExhausted);
        Assert.True(budget.TryConsume());
        Assert.True(budget.IsExhausted);
        Assert.False(budget.TryConsume());
        Assert.False(budget.TryConsume());
    }

    [Fact]
    public void RenderBudget_ResetRestoresTheAllowance()
    {
        var budget = new RenderBudget(1);
        Assert.True(budget.TryConsume());
        Assert.False(budget.TryConsume());

        budget.Reset();

        Assert.True(budget.TryConsume());
    }

    [Theory]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, InteractionMath.MaxScaleValue)]
    [InlineData(double.NegativeInfinity, InteractionMath.MinScaleValue)]
    [InlineData(-5, InteractionMath.MinScaleValue)]
    [InlineData(0, InteractionMath.MinScaleValue)]
    [InlineData(2.5, 2.5)]
    public void CoerceScale_KeepsValuesFinitePositiveAndBounded(double input, double expected)
        => Assert.Equal(expected, InteractionMath.CoerceScale(input));

    [Fact]
    public void CoercePan_ReplacesNaNAndBoundsInfinity()
    {
        var pan = InteractionMath.CoercePan(new Point(double.NaN, double.PositiveInfinity));

        Assert.Equal(0, pan.X);
        Assert.Equal(InteractionMath.MaxPan, pan.Y);
        Assert.Equal(new Point(3, -4), InteractionMath.CoercePan(new Point(3, -4)));
    }
}

/// <summary>
/// End-to-end checks against the real control: hostile settings and patterns must neither throw
/// nor stall the UI thread. Each runs on a dedicated STA thread with a generous time limit, so a
/// regression shows up as a failed test rather than a hung test run.
/// </summary>
public class ControlRobustnessTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(Limit), "Control stalled: work did not finish within the time limit.");
        if (error != null)
        {
            throw new Exception("Control threw on the UI thread.", error);
        }
    }

    // Lay out and rasterize the control, exactly as WPF would when showing it.
    private static void Render(Preview control, int width = 400, int height = 300)
    {
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));
        control.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(control);
    }

    [Fact]
    public void ScaleZoomAndPan_AreCoercedToSafeValues()
    {
        RunSta(() =>
        {
            var control = new Preview { Scale = double.NaN, Zoom = -3, PanOffset = new Point(double.NaN, double.PositiveInfinity) };

            Assert.Equal(1.0, control.Scale);
            Assert.Equal(InteractionMath.MinScaleValue, control.Zoom);
            Assert.Equal(0, control.PanOffset.X);
            Assert.Equal(InteractionMath.MaxPan, control.PanOffset.Y);

            control.Zoom = double.PositiveInfinity;
            Assert.Equal(InteractionMath.MaxScaleValue, control.Zoom);
        });
    }

    [Theory]
    [InlineData(1e6, 1e6)]
    [InlineData(1e-4, 1e-4)]
    [InlineData(1e6, 1e-4)]
    public void ExtremeScaleAndZoom_RenderWithoutStalling(double scale, double zoom)
    {
        RunSta(() =>
        {
            var control = new Preview
            {
                PatRawText = "*P, d\n0,0,0,0,4,3,-1\n45,0,0,0,4,0,-2\n90,0,0,2,4",
                Scale = scale,
                Zoom = zoom,
                PanOffset = new Point(1e9, -1e9),
            };

            Render(control);
        });
    }

    [Fact]
    public void ManyDashedFamilies_RenderWithinTheBudget()
    {
        RunSta(() =>
        {
            // 500 drafting families (96 DIPs per native unit) at ~1 px spacing with short dashes:
            // millions of segments if drawn in full.
            var sb = new StringBuilder("*P, hostile\n");
            for (int i = 0; i < PatParser.MaxLineGroupsPerPattern; i++)
            {
                sb.AppendLine($"{i % 180},0,0,0,0.011,0.05,-0.05");
            }

            var control = new Preview { PatRawText = sb.ToString(), HighlightFirstTile = true };
            var clock = Stopwatch.StartNew();

            Render(control, 1200, 900);

            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), $"Render took {clock.Elapsed}.");
        });
    }

    [Fact]
    public void HostileNumbersInPatternText_DoNotBreakTheControl()
    {
        RunSta(() =>
        {
            // Every hostile line is rejected by the parser; the one sane line survives.
            var control = new Preview { PatRawText = "*P, d\nNaN,0,0,0,4\n0,0,0,0,Infinity\n0,1e999,0,0,4\n0,0,0,0,4,1e300,-1e300\n45,0,0,0,4" };

            Render(control);

            Assert.NotNull(control.Pattern);
            Assert.Single(control.Pattern!.LineGroups);
            Assert.Equal(45, control.Pattern.LineGroups[0].AngleDeg);
        });
    }

    [Fact]
    public void BadPatternSources_LeaveNoPatternInsteadOfThrowing()
    {
        RunSta(() =>
        {
            var control = new Preview();

            control.PatFilePath = Path.GetTempPath();          // a directory
            Assert.Null(control.Pattern);

            control.PatFilePath = "\0invalid";                 // illegal path characters
            Assert.Null(control.Pattern);

            control.PatRawText = new string('x', PatParser.MaxInputChars + 1);
            Assert.Null(control.Pattern);
        });
    }

    [Fact]
    public void InteractionAtTheLimits_StaysBoundedAndRenders()
    {
        RunSta(() =>
        {
            var control = new Preview { PatRawText = "*P, d\n45,0,0,0,4", IsInteractive = true };
            Render(control);

            // Hammer the zoom math the way a runaway wheel would.
            for (int i = 0; i < 200; i++)
            {
                var (zoom, pan) = InteractionMath.ZoomAt(control.Zoom, control.PanOffset, 1.0, new Point(200, 150), InteractionMath.WheelFactor(120 * 50));
                control.Zoom = zoom;
                control.PanOffset = pan;
            }

            Assert.Equal(InteractionMath.MaxZoom, control.Zoom);
            Assert.True(double.IsFinite(control.PanOffset.X) && double.IsFinite(control.PanOffset.Y));
            Render(control);
        });
    }
}
