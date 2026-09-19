using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FillPatternPreview.Model;

namespace FillPatternPreview.Rendering;

/// <summary>
/// Pure geometry for expanding a .pat line family (an infinite, periodically repeated,
/// optionally dashed line) against a visible rectangle. Deliberately free of any
/// <see cref="System.Windows.Media.DrawingContext"/>/UI dependency so it can be unit tested
/// directly - this is where every rendering-correctness bug in this control has lived.
/// </summary>
internal static class LineFamilyExpander
{
    public enum SegmentKind { Line, Dot }

    /// <summary>
    /// One piece of a family's dash expansion: a drawn line segment, or a single dot position
    /// (Start == End). Gaps produce no segment at all.
    /// </summary>
    public readonly record struct DashSegment(SegmentKind Kind, Point Start, Point End)
    {
        public static DashSegment LineSegment(Point start, Point end) => new(SegmentKind.Line, start, end);
        public static DashSegment DotSegment(Point at) => new(SegmentKind.Dot, at, at);
    }

    // Drafting patterns are authored in paper/plot units (inches by convention, or whatever
    // %UNITS= declares), not screen pixels, so a native unit has no natural pixel size on its
    // own - unlike model patterns, which are real-world sizes that scale with view zoom. WPF's
    // own device-independent unit is defined as 1/96 inch, so "96 DIPs per inch" is the one
    // print-true default that requires no per-pattern guessing: at Scale=1 (its default), a
    // drafting pattern renders at the same size it would print at 100%. Model patterns are
    // left at a 1:1 native-unit-to-DIP factor, since their "correct" apparent size is inherently
    // a matter of view zoom, not a fixed paper conversion.
    public static double GetUnitsToDipFactor(PatternDefinition? pattern)
    {
        if (pattern == null || pattern.IsModel)
        {
            return 1.0;
        }

        const double DipsPerInch = 96.0;
        return pattern.Units switch
        {
            "MM" or "MILLIMETER" or "MILLIMETERS" => DipsPerInch / 25.4,
            "CM" or "CENTIMETER" or "CENTIMETERS" => DipsPerInch / 2.54,
            "M" or "METER" or "METERS" => DipsPerInch / 0.0254,
            "FOOT" or "FEET" or "FT" => DipsPerInch * 12.0,
            _ => DipsPerInch, // INCH, unspecified: AutoCAD/Revit default drafting-pattern unit
        };
    }

    /// <summary>
    /// Converts a LineGroup's (DeltaX, DeltaY) - defined in the family's own rotated frame,
    /// per the .pat format - into a world-space step vector. DeltaX ("shift") is measured
    /// along the line's own direction and offsets each successive copy's dash phase; DeltaY
    /// ("offset") is measured perpendicular to the line and is the spacing between copies.
    /// </summary>
    public static Vector ComputeWorldDelta(Vector direction, Vector normal, double deltaX, double deltaY, double scale)
        => (direction * deltaX + normal * deltaY) * scale;

    /// <summary>
    /// The inclusive range of repeat indices k (basePoint = origin + k*delta) needed for at
    /// least one copy's chord to cover every point of <paramref name="rect"/>, with a margin
    /// of one spacing unit on each side.
    /// </summary>
    public static (int KMin, int KMax) ComputeRepeatRange(Rect rect, Point origin, Vector delta, Vector normal, double scale)
    {
        double Project(Point p) => p.X * normal.X + p.Y * normal.Y;

        // perpStep is the SIGNED change in projection-onto-normal per unit k (delta·normal); it
        // can be negative depending on the family's angle/offset sign. Using its magnitude
        // (as if k always increased the projection) silently clamps the usable k range to a
        // tiny positive-only band near k=0 whenever a positive k step actually decreases the
        // projection - rendering only the first couple of copies of the family and none beyond.
        double perpStep = Vector.Multiply(delta, normal);
        double offsetDist = Math.Abs(perpStep);
        if (offsetDist < 1e-6)
        {
            offsetDist = delta.Length;
            perpStep = offsetDist; // degenerate family (no real perpendicular component): crude positive fallback
        }

        if (offsetDist < 1e-6)
        {
            offsetDist = 8 * scale; // last-resort constant so a wholly-degenerate family still enumerates something
            perpStep = offsetDist;
        }

        var corners = new[] { rect.TopLeft, rect.TopRight, rect.BottomLeft, rect.BottomRight };
        double minProj = corners.Min(Project) - offsetDist;
        double maxProj = corners.Max(Project) + offsetDist;
        double originProj = Project(origin);

        double kAtMin = (minProj - originProj) / perpStep;
        double kAtMax = (maxProj - originProj) / perpStep;
        int kMin = (int)Math.Floor(Math.Min(kAtMin, kAtMax));
        int kMax = (int)Math.Ceiling(Math.Max(kAtMin, kAtMax));
        return (kMin, kMax);
    }

    /// <summary>
    /// Intersects the infinite line through <paramref name="pointOnLine"/> in <paramref name="direction"/>
    /// with <paramref name="rect"/>, returning the visible chord ordered so that walking from
    /// p1 by direction*t for increasing t moves toward p2, never away from it. That ordering
    /// matters only for dashed lines (a solid line looks identical either way): if the pair
    /// came back backwards relative to direction, dash-expanding from p1 would walk the whole
    /// pattern outside the visible rect and it would silently disappear.
    /// </summary>
    public static bool TryIntersectInfiniteLineWithRect(Point pointOnLine, Vector direction, Rect rect, out Point p1, out Point p2)
    {
        p1 = default; p2 = default;
        var intersections = new Point[4];
        int count = 0;
        const double eps = 1e-9;
        double dx = direction.X, dy = direction.Y;
        if (Math.Abs(dx) < eps && Math.Abs(dy) < eps)
        {
            return false;
        }

        if (Math.Abs(dx) > eps)
        {
            double tL = (rect.Left - pointOnLine.X) / dx;
            var yL = pointOnLine.Y + tL * dy;
            if (yL >= rect.Top - eps && yL <= rect.Bottom + eps)
            {
                intersections[count++] = new Point(rect.Left, yL);
            }

            double tR = (rect.Right - pointOnLine.X) / dx;
            var yR = pointOnLine.Y + tR * dy;
            if (yR >= rect.Top - eps && yR <= rect.Bottom + eps)
            {
                intersections[count++] = new Point(rect.Right, yR);
            }
        }

        if (Math.Abs(dy) > eps)
        {
            double tT = (rect.Top - pointOnLine.Y) / dy;
            var xT = pointOnLine.X + tT * dx;
            if (xT >= rect.Left - eps && xT <= rect.Right + eps)
            {
                intersections[count++] = new Point(xT, rect.Top);
            }

            double tB = (rect.Bottom - pointOnLine.Y) / dy;
            var xB = pointOnLine.X + tB * dx;
            if (xB >= rect.Left - eps && xB <= rect.Right + eps)
            {
                intersections[count++] = new Point(xB, rect.Bottom);
            }
        }

        if (count < 2)
        {
            return false;
        }

        double best = -1;
        Point bp1 = intersections[0], bp2 = intersections[1];
        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                var d = (intersections[i] - intersections[j]).LengthSquared;
                if (d > best)
                {
                    best = d;
                    bp1 = intersections[i];
                    bp2 = intersections[j];
                }
            }
        }

        if (Vector.Multiply(bp2 - bp1, direction) < 0)
        {
            (bp1, bp2) = (bp2, bp1);
        }

        p1 = bp1; p2 = bp2; return true;
    }

    /// <summary>Hard cap on dash segments per visible chord - a defense-in-depth backstop against
    /// pathological combinations (e.g. an extreme Zoom) that produce an unreasonable segment count
    /// despite the sub-pixel-cycle shortcut below.</summary>
    public const int MaxDashSegmentsPerChord = 20000;

    /// <summary>
    /// Expands a dash pattern along the visible chord (p1, p2), with phase anchored to
    /// basePoint (the family copy's true origin) rather than to p1 (wherever the infinite line
    /// happens to enter the visible rect) - copies shifted by the "shift" component of delta
    /// need their dash phase to shift correspondingly to produce correct staggered/coursed
    /// patterns (e.g. brick running bond).
    /// </summary>
    public static IEnumerable<DashSegment> ExpandDashSegments(Point p1, Point p2, Point basePoint, Vector dir, IReadOnlyList<double> pattern, double scale, double strokeThickness)
    {
        if (pattern.Count == 0)
        {
            yield return DashSegment.LineSegment(p1, p2);
            yield break;
        }

        var lineVec = p2 - p1;
        double length = lineVec.Length;
        if (length < 0.5)
        {
            yield break;
        }

        dir.Normalize();
        var scaled = pattern.Select(v => v * scale).ToArray();
        if (scaled.All(v => Math.Abs(v) < 1e-9))
        {
            yield return DashSegment.LineSegment(p1, p2);
            yield break;
        }

        double cycleLength = scaled.Sum(v => Math.Abs(v));

        // Drafting patterns are authored in tiny native units (a plot-scale multiplier is
        // expected before display; see GetUnitsToDipFactor), so a cycle can still come out
        // far smaller than a device pixel in unusual scale/zoom combinations. Walking such a
        // cycle segment-by-segment across a long visible chord would require millions of draw
        // calls for no visible difference from a solid line - draw solid instead of hanging.
        if (cycleLength < 1.0)
        {
            yield return DashSegment.LineSegment(p1, p2);
            yield break;
        }

        double offsetAlongDir = Vector.Multiply(p1 - basePoint, dir);
        double phase = ((offsetAlongDir % cycleLength) + cycleLength) % cycleLength;

        double pos = -phase;
        int idx = 0;
        int iterations = 0;
        while (pos < length && iterations++ < MaxDashSegmentsPerChord)
        {
            double dash = scaled[idx];
            idx = (idx + 1) % scaled.Length;
            if (Math.Abs(dash) < 1e-9)
            {
                double dotPos = pos;
                if (dotPos >= 0 && dotPos < length)
                {
                    yield return DashSegment.DotSegment(p1 + dir * dotPos);
                }

                pos += strokeThickness * 2;
                continue;
            }

            bool draw = dash > 0;
            double segLen = Math.Abs(dash);
            double start = Math.Max(0, pos);
            double end = Math.Min(length, pos + segLen);
            if (draw && end > start)
            {
                yield return DashSegment.LineSegment(p1 + dir * start, p1 + dir * end);
            }

            pos += segLen;
        }
    }
}
