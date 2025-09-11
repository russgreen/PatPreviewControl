using System;
using System.Collections.Generic;
using FillPatternPreview.Model;

namespace FillPatternPreview.Parsing;

public sealed class PatternParseResult
{
    public IReadOnlyDictionary<string, PatternDefinition> Patterns { get; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public TimeSpan Duration { get; }
    public bool Success => Patterns.Count > 0 && Errors.Count == 0;

    public PatternParseResult(Dictionary<string, PatternDefinition> patterns, IEnumerable<string>? errors, IEnumerable<string>? warnings, TimeSpan duration)
    {
        Patterns = patterns;
        if (errors != null)
        {
            Errors.AddRange(errors);
        }

        if (warnings != null)
        {
            Warnings.AddRange(warnings);
        }

        Duration = duration;
    }
}
