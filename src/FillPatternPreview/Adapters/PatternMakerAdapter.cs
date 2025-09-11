using System;
using System.Collections.Generic;
using System.Linq;
using FillPatternPreview.Model;
using FillPatternPreview.PatternMaker;

namespace FillPatternPreview.Adapters;

public static class PatternMakerAdapter
{
    public sealed record ConvertedPattern(
        PatternDefinition Source,
        PatternDomain Domain,
        IReadOnlyList<PatternGrid> Grids);

    public static ConvertedPattern Build(PatternDefinition definition, bool forceModel = false)
    {
        if (definition.LineGroups.Count == 0)
        {
            throw new ArgumentException("PatternDefinition has no line groups.", nameof(definition));
        }

        double minX = 0, minY = 0; // origin baseline
        double maxX = 0, maxY = 0;

        foreach (var lineGroup in definition.LineGroups)
        {
            var dx = Math.Abs(lineGroup.DeltaX);
            var dy = Math.Abs(lineGroup.DeltaY);
            if (dx < 1e-6 && dy < 1e-6)
            {
                double dashLen = FirstPositiveDash(lineGroup) ?? 10.0;
                dx = Math.Abs(dashLen * Math.Cos(lineGroup.AngleDeg * Math.PI / 180.0));
                dy = Math.Abs(dashLen * Math.Sin(lineGroup.AngleDeg * Math.PI / 180.0));
                if (dx < 1e-3)
                {
                    dx = dashLen;
                }

                if (dy < 1e-3)
                {
                    dy = dashLen;
                }
            }
            maxX = Math.Max(maxX, dx);
            maxY = Math.Max(maxY, dy);
        }

        if (maxX < 1e-6)
        {
            maxX = 10;
        }

        if (maxY < 1e-6)
        {
            maxY = 10;
        }

        var domain = new PatternDomain(minX, minY, maxX, maxY, forceModel || definition.IsModel, expandable: false);
        var grids = new List<PatternGrid>();

        foreach (var lineGroup in definition.LineGroups)
        {
            double segLen = EstimateSegmentLength(lineGroup);
            var angleRad = lineGroup.AngleDeg * Math.PI / 180.0;
            var dirU = Math.Cos(angleRad);
            var dirV = Math.Sin(angleRad);
            var start = new PatternPoint(lineGroup.OriginX, lineGroup.OriginY);
            var end = new PatternPoint(lineGroup.OriginX + dirU * segLen, lineGroup.OriginY + dirV * segLen);
            var line = new PatternLine(start, end);
            grids.Add(new PatternGrid(domain, line));
        }

        return new ConvertedPattern(definition, domain, grids);
    }

    private static double EstimateSegmentLength(LineGroup group)
    {
        var posDashes = group.DashPattern.Where(d => d > 0).ToList();
        if (posDashes.Count > 0)
        {
            double len = posDashes.Sum();
            return Math.Clamp(len, 1.0, 1000.0);
        }
        var deltaMag = Math.Sqrt(group.DeltaX * group.DeltaX + group.DeltaY * group.DeltaY);
        if (deltaMag > 1e-6)
        {
            return Math.Clamp(deltaMag, 1.0, 1000.0);
        }

        return 10.0;
    }

    private static double? FirstPositiveDash(LineGroup group)
    {
        foreach (var d in group.DashPattern)
        {
            if (d > 0)
            {
                return d;
            }
        }

        return null;
    }
}