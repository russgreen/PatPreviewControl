using System;
using System.Linq;
using System.Windows;
using FillPatternPreview.Model;
using FillPatternPreview.Parsing;

namespace FillPatternPreview.Rendering;

/// <summary>
/// Turns a pattern source (raw text or a file) plus an optional pattern name into the pattern to
/// draw. The one implementation of the selection rules, shared by the control and thumbnail export.
/// </summary>
internal static class PatternResolver
{
    /// <summary>
    /// Raw text takes priority over the file path. The named pattern is used if present, otherwise
    /// the first pattern in the source. Never throws: any failure just means "no pattern".
    /// </summary>
    /// <returns>True if a pattern was found.</returns>
    public static bool TryResolve(string? rawText, string? filePath, string? patternName, out PatternDefinition? pattern, out Size? tileSize)
    {
        pattern = null;
        tileSize = null;
        try
        {
            PatternParseResult? result = null;
            if (!string.IsNullOrWhiteSpace(rawText))
            {
                result = PatParser.ParseText(rawText);
            }
            else if (!string.IsNullOrWhiteSpace(filePath))
            {
                result = PatParser.ParseFile(filePath);
            }

            if (result is { Patterns.Count: > 0 })
            {
                pattern = !string.IsNullOrWhiteSpace(patternName) && result.Patterns.TryGetValue(patternName, out var named)
                    ? named
                    : result.Patterns.Values.FirstOrDefault();
            }

            tileSize = TileSizeOf(pattern);
            return pattern != null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Loading pattern failed: {ex}");
            pattern = null;
            tileSize = null;
            return false;
        }
    }

    /// <summary>The pattern's first repeat cell in native units, or null if it has none.</summary>
    public static Size? TileSizeOf(PatternDefinition? pattern)
        => PatternTileCalculator.TryComputeTileSize(pattern, out var computed) ? computed : null;
}
