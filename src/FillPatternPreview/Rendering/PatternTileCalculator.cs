using System;
using System.Windows;
using FillPatternPreview.Model;

namespace FillPatternPreview.Rendering;

/// <summary>
/// Derives a rectangular repeat cell ("tile") for a .pat pattern, in the pattern's own native
/// units. Pure math with no UI dependency so it can be unit tested directly.
/// </summary>
/// <remarks>
/// A line family is invariant under a translation T when T's component across the lines is a
/// whole number k of the family's offset (DeltaY) and, once the k-th copy's shift (k * DeltaX)
/// is accounted for, T's component along the lines is a whole number of dash cycles (any value
/// at all for a solid line). We look for the smallest axis-aligned T = (W, 0) and T = (0, H)
/// that satisfy this for every family at once; the W x H rectangle anchored at the pattern
/// origin is then a valid repeat cell. It is not guaranteed to be the minimum-area cell for
/// patterns whose true lattice is skewed, but tiling it reproduces the pattern exactly.
/// </remarks>
internal static class PatternTileCalculator
{
    private const int MaxMultiple = 256;
    private const double Epsilon = 1e-9;
    private const double IntegerTolerance = 1e-6;

    public static bool TryComputeTileSize(PatternDefinition? pattern, out Size tileSize)
    {
        tileSize = default;
        if (pattern == null || pattern.LineGroups.Count == 0)
        {
            return false;
        }

        var width = CombinedPeriod(pattern, horizontal: true);
        var height = CombinedPeriod(pattern, horizontal: false);
        if (width == null || height == null)
        {
            return false;
        }

        // A direction every family runs along has no period of its own (e.g. all-horizontal
        // solid lines have no horizontal period); borrow the other axis so the cell is square.
        double w = width.Value, h = height.Value;
        if (w == 0 && h == 0)
        {
            return false;
        }

        if (w == 0)
        {
            w = h;
        }
        else if (h == 0)
        {
            h = w;
        }

        tileSize = new Size(w, h);
        return true;
    }

    /// <summary>
    /// The smallest translation along one axis that maps every family onto itself. Returns 0
    /// when no family constrains that axis, or null when at least one family has no period on
    /// it (within <see cref="MaxMultiple"/> repeats).
    /// </summary>
    private static double? CombinedPeriod(PatternDefinition pattern, bool horizontal)
    {
        double combined = 0;
        foreach (var group in pattern.LineGroups)
        {
            var period = GroupPeriod(group, horizontal);
            if (period == null)
            {
                return null;
            }

            if (period.Value == 0)
            {
                continue;
            }

            if (combined == 0)
            {
                combined = period.Value;
                continue;
            }

            var merged = CommonMultiple(combined, period.Value);
            if (merged == null)
            {
                return null;
            }

            combined = merged.Value;
        }

        return combined;
    }

    private static double? GroupPeriod(LineGroup group, bool horizontal)
    {
        double angle = group.AngleDeg * Math.PI / 180.0;
        double c = Math.Cos(angle), s = Math.Sin(angle);

        // Components of the unit axis along the family's direction and across it.
        double axisAlong = horizontal ? c : s;
        double axisAcross = horizontal ? -s : c;

        double cycle = 0;
        foreach (var d in group.DashPattern)
        {
            cycle += Math.Abs(d);
        }

        bool solid = cycle < Epsilon;

        if (Math.Abs(axisAcross) < Epsilon)
        {
            // The axis runs along the lines: copies never map onto each other, so only the
            // dash cycle repeats the pattern.
            return solid ? 0 : cycle / Math.Abs(axisAlong);
        }

        double dy = Math.Abs(group.DeltaY);
        if (dy < Epsilon)
        {
            return null; // a lone line has no repeat across itself
        }

        // W = m * unit, where m copies are crossed; the shift accumulated over those copies
        // must be a whole number of dash cycles.
        double unit = dy / Math.Abs(axisAcross);
        if (solid)
        {
            return unit;
        }

        double sign = Math.Sign(axisAcross) * Math.Sign(group.DeltaY);
        double shiftPerUnit = unit * axisAlong - sign * group.DeltaX;
        for (int m = 1; m <= MaxMultiple; m++)
        {
            if (IsNearInteger(m * shiftPerUnit / cycle))
            {
                return m * unit;
            }
        }

        return null;
    }

    private static double? CommonMultiple(double a, double b)
    {
        for (int j = 1; j <= MaxMultiple; j++)
        {
            if (IsNearInteger(j * a / b))
            {
                return j * a;
            }
        }

        return null;
    }

    private static bool IsNearInteger(double value) => Math.Abs(value - Math.Round(value)) < IntegerTolerance;
}
