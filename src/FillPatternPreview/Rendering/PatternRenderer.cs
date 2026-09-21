using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using FillPatternPreview.Adapters;
using FillPatternPreview.Model;

namespace FillPatternPreview.Rendering;

/// <summary>What to draw, and how, for one <see cref="PatternRenderer"/>.</summary>
/// <param name="LineBrush">Colour of the pattern's lines.</param>
/// <param name="FirstTileBrush">When set, the first repeat cell is drawn in this colour instead of <paramref name="LineBrush"/>.</param>
/// <param name="TilePattern">When false only the first repeat cell is drawn (if the pattern has one).</param>
/// <param name="Scale">Base scale, before <paramref name="Zoom"/> and the pattern's units factor.</param>
/// <param name="Zoom">Zoom multiplier.</param>
/// <param name="Pan">Pan offset in the pattern's native units.</param>
internal sealed record PatternRenderOptions(
    Brush LineBrush,
    Brush? FirstTileBrush = null,
    bool TilePattern = true,
    double Scale = 1.0,
    double Zoom = 1.0,
    Point Pan = default);

/// <summary>
/// Draws a pattern into a <see cref="DrawingContext"/>. This is the one drawing implementation
/// shared by the on-screen control and the headless thumbnail export, so both always look the same.
/// It has no dependency on any control or visual tree.
/// </summary>
internal sealed class PatternRenderer
{
    private readonly PatternDefinition _pattern;
    private readonly Size? _tileSize;
    private readonly PatternMakerAdapter.ConvertedPattern? _patternMaker;
    private readonly PatternRenderOptions _options;
    private readonly RenderBudget _budget;

    /// <param name="pattern">The pattern to draw.</param>
    /// <param name="tileSize">The first repeat cell in the pattern's native units, if it has one.</param>
    /// <param name="patternMaker">When set, the experimental PatternMaker geometry is drawn instead of the legacy expansion.</param>
    /// <param name="options">Colours, scale and view settings.</param>
    /// <param name="budget">Work allowance for this render pass; the caller resets it between passes.</param>
    public PatternRenderer(
        PatternDefinition pattern,
        Size? tileSize,
        PatternMakerAdapter.ConvertedPattern? patternMaker,
        PatternRenderOptions options,
        RenderBudget budget)
    {
        _pattern = pattern;
        _tileSize = tileSize;
        _patternMaker = patternMaker;
        _options = options;
        _budget = budget;
    }

    /// <summary>
    /// Scale from the pattern's native units to device-independent pixels: Scale * Zoom * units
    /// factor. Scale and Zoom are expected finite and positive; the finite check is a last line of
    /// defence (Math.Max propagates NaN rather than applying the floor).
    /// </summary>
    public static double GetRenderScale(PatternDefinition pattern, double scale, double zoom)
    {
        double result = scale * zoom * LineFamilyExpander.GetUnitsToDipFactor(pattern);
        return double.IsFinite(result) ? Math.Max(0.0001, result) : 1.0;
    }

    /// <summary>
    /// Draws the pattern over the area (0,0)-(size), clipped to it, with the pan offset applied.
    /// The caller draws any background first. Exceptions propagate to the caller.
    /// </summary>
    public void Render(DrawingContext dc, Size size)
    {
        var rect = new Rect(0, 0, size.Width, size.Height);
        using var clip = PushScope.Clip(dc, new RectangleGeometry(rect));

        // Pan is in pattern units: shift the pattern by it, and draw the part of the
        // pattern-space plane that now falls inside the view.
        double panScale = GetRenderScale(_pattern, _options.Scale, _options.Zoom);
        double offsetX = _options.Pan.X * panScale;
        double offsetY = _options.Pan.Y * panScale;
        if (!double.IsFinite(offsetX) || !double.IsFinite(offsetY))
        {
            offsetX = offsetY = 0;
        }

        using var shift = PushScope.Transform(dc, new TranslateTransform(offsetX, offsetY));
        RenderPattern(dc, new Rect(-offsetX, -offsetY, rect.Width, rect.Height));
    }

    private void RenderPattern(DrawingContext dc, Rect rect)
    {
        var lineBrush = _options.LineBrush;
        var tileBrush = _options.FirstTileBrush;
        double scale = GetRenderScale(_pattern, _options.Scale, _options.Zoom);
        var tileRect = _tileSize is { } tileSize ? new Rect(0, 0, tileSize.Width * scale, tileSize.Height * scale) : Rect.Empty;
        if (!tileRect.IsEmpty && double.IsFinite(tileRect.Width + tileRect.Height) && (!_options.TilePattern || tileBrush != null))
        {
            if (!_options.TilePattern)
            {
                // Just the single tile, in the highlight colour if that option is on.
                var onlyTile = Rect.Intersect(rect, tileRect);
                if (!onlyTile.IsEmpty)
                {
                    using var tileClip = PushScope.Clip(dc, new RectangleGeometry(onlyTile));
                    RenderLines(dc, onlyTile, tileBrush ?? lineBrush);
                }

                return;
            }

            // Everything outside the first tile in the normal colour, then the tile itself in
            // its own colour. Clipping (rather than overdrawing) avoids anti-aliased fringes of
            // the normal colour showing through the highlight.
            using (PushScope.Clip(dc, new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(rect), new RectangleGeometry(tileRect))))
            {
                RenderLines(dc, rect, lineBrush);
            }

            var visibleTile = Rect.Intersect(rect, tileRect);
            if (!visibleTile.IsEmpty)
            {
                using var highlightClip = PushScope.Clip(dc, new RectangleGeometry(visibleTile));
                RenderLines(dc, visibleTile, tileBrush ?? lineBrush);
            }
        }
        else
        {
            RenderLines(dc, rect, lineBrush);
        }
    }

    private void RenderLines(DrawingContext dc, Rect rect, Brush stroke)
    {
        if (_patternMaker != null)
        {
            RenderWithPatternMaker(dc, rect, _patternMaker, stroke);
        }
        else
        {
            RenderLegacy(dc, rect, _pattern, stroke);
        }
    }

    // Every draw call against an unfrozen Pen/Brush makes WPF track it as a change-notifying
    // dependency, and that bookkeeping grows with the number of draw calls: a few thousand dashes
    // took seconds, and cost grew quadratically. Frozen resources are plain immutable data. The
    // brush is snapshotted (rather than frozen in place) so the caller's own LineBrush is untouched.
    private static Pen CreateFrozenPen(Brush stroke)
    {
        var brush = stroke;
        if (!brush.IsFrozen)
        {
            brush = stroke.CloneCurrentValue();
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }
        }

        var pen = new Pen(brush, 1);
        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        return pen;
    }

    private void RenderLegacy(DrawingContext dc, Rect rect, PatternDefinition pattern, Brush stroke)
    {
        var pen = CreateFrozenPen(stroke);
        double scale = GetRenderScale(pattern, _options.Scale, _options.Zoom);
        foreach (var g in pattern.LineGroups)
        {
            double angleRad = g.AngleDeg * Math.PI / 180.0;
            var dir = new Vector(Math.Cos(angleRad), Math.Sin(angleRad));
            if (dir.LengthSquared < 1e-12)
            {
                continue;
            }

            dir.Normalize();
            var normal = new Vector(-dir.Y, dir.X);
            var origin = new Point(g.OriginX * scale, g.OriginY * scale);
            var delta = LineFamilyExpander.ComputeWorldDelta(dir, normal, g.DeltaX, g.DeltaY, scale);
            var (kMin, kMax) = LineFamilyExpander.ComputeRepeatRange(rect, origin, delta, normal, scale);
            if (!LineFamilyExpander.IsRepeatRangeUsable(kMin, kMax))
            {
                continue;
            }

            for (int k = kMin; k <= kMax; k++)
            {
                if (!_budget.TryConsume())
                {
                    return; // out of budget for this render pass: stop rather than freeze the caller
                }

                var basePoint = origin + k * delta;
                if (LineFamilyExpander.TryIntersectInfiniteLineWithRect(basePoint, dir, rect, out var p1, out var p2))
                {
                    DrawDashed(dc, pen, p1, p2, basePoint, dir, g.DashPattern, scale);
                }
            }
        }
    }

    private void RenderWithPatternMaker(DrawingContext dc, Rect rect, PatternMakerAdapter.ConvertedPattern converted, Brush stroke)
    {
        var pen = CreateFrozenPen(stroke);
        double scale = GetRenderScale(converted.Source, _options.Scale, _options.Zoom);
        foreach (var grid in converted.Grids)
        {
            double angle = grid.Angle;
            var dir = new Vector(Math.Cos(angle), Math.Sin(angle));
            var normal = new Vector(-dir.Y, dir.X);

            // Use Offset for spacing
            double spacing = Math.Abs(grid.Offset);
            if (spacing < 1e-6)
            {
                spacing = grid.Span;
            }

            if (spacing < 1e-6)
            {
                spacing = 8;
            }

            spacing *= scale;

            // Base origin
            var origin = new Point(grid.Origin.U * scale, grid.Origin.V * scale);
            var corners = new[] { rect.TopLeft, rect.TopRight, rect.BottomLeft, rect.BottomRight };
            double Project(Point p) => p.X * normal.X + p.Y * normal.Y;
            double minProj = corners.Min(Project) - spacing;
            double maxProj = corners.Max(Project) + spacing;
            double originProj = Project(origin);
            var (kMin, kMax) = LineFamilyExpander.RepeatRangeFromBounds((minProj - originProj) / spacing, (maxProj - originProj) / spacing);
            if (!LineFamilyExpander.IsRepeatRangeUsable(kMin, kMax))
            {
                continue;
            }

            for (int k = kMin; k <= kMax; k++)
            {
                if (!_budget.TryConsume())
                {
                    return;
                }

                var basePoint = origin + k * normal * spacing;
                if (LineFamilyExpander.TryIntersectInfiniteLineWithRect(basePoint, dir, rect, out var p1, out var p2))
                {
                    dc.DrawLine(pen, p1, p2);
                }
            }
        }
    }

    private void DrawDashed(DrawingContext dc, Pen pen, Point p1, Point p2, Point basePoint, Vector dir, IReadOnlyList<double> pattern, double scale)
    {
        foreach (var seg in LineFamilyExpander.ExpandDashSegments(p1, p2, basePoint, dir, pattern, scale, pen.Thickness))
        {
            if (!_budget.TryConsume())
            {
                return;
            }

            if (seg.Kind == LineFamilyExpander.SegmentKind.Dot)
            {
                var pt = seg.Start;
                dc.DrawRectangle(pen.Brush, null, new Rect(pt.X - pen.Thickness / 2, pt.Y - pen.Thickness / 2, pen.Thickness, pen.Thickness));
            }
            else
            {
                dc.DrawLine(pen, seg.Start, seg.End);
            }
        }
    }

    // A pushed clip/transform that is always popped, even if drawing inside it throws, so the
    // DrawingContext's push/pop stack stays balanced.
    private sealed class PushScope : IDisposable
    {
        private readonly DrawingContext _dc;
        private PushScope(DrawingContext dc) => _dc = dc;

        public static PushScope Clip(DrawingContext dc, Geometry clip)
        {
            dc.PushClip(clip);
            return new PushScope(dc);
        }

        public static PushScope Transform(DrawingContext dc, System.Windows.Media.Transform transform)
        {
            dc.PushTransform(transform);
            return new PushScope(dc);
        }

        public void Dispose() => _dc.Pop();
    }
}
