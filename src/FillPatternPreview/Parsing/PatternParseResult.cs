using System.Collections.Generic;
using FillPatternPreview.Model;

namespace FillPatternPreview.Parsing;

/// <summary>
/// Result of parsing a .pat file or text containing pattern definitions.
/// </summary>
public class PatternParseResult
{
    /// <summary>
    /// Gets a value indicating whether the parsing was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the list of errors encountered during parsing.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = new List<string>();

    /// <summary>
    /// Gets the list of warnings encountered during parsing.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = new List<string>();

    /// <summary>
    /// Gets the dictionary of parsed pattern definitions keyed by pattern name.
    /// </summary>
    public IReadOnlyDictionary<string, PatternDefinition> Patterns { get; init; } = new Dictionary<string, PatternDefinition>();

    /// <summary>
    /// Gets the first pattern in the result, or null if no patterns were parsed.
    /// </summary>
    public PatternDefinition? FirstPattern => 
        Patterns.Count > 0 ? Patterns.Values.First() : null;

    /// <summary>
    /// Gets a pattern by name (case-insensitive), or null if not found.
    /// </summary>
    /// <param name="name">The pattern name to search for</param>
    /// <returns>The pattern definition or null if not found</returns>
    public PatternDefinition? GetPattern(string name)
    {
        return Patterns.FirstOrDefault(kvp => 
            string.Equals(kvp.Key, name, System.StringComparison.OrdinalIgnoreCase)).Value;
    }
}