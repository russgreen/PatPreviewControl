using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using FillPatternPreview.Model;

namespace FillPatternPreview.Parsing;

/// <summary>
/// .PAT file parser.
/// Supports multiple pattern definitions per file. Each pattern begins with a header line:
///   *NAME, optional description ;%TYPE=MODEL (or DRAFTING)
/// Followed by one or more definition lines of the form:
///   angle, x-origin, y-origin, delta-x, delta-y, [dash1, dash2, ...]
/// Lines starting with ';' are comments. Trailing comments beginning with ';' are ignored.
/// Dash pattern semantics: positive = drawn segment, negative = gap, zero = dot.
/// </summary>
public static class PatParser
{
    public static PatternParseResult ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            return new PatternParseResult(new Dictionary<string, PatternDefinition>(), new[] { $"File not found: {path}" }, null, TimeSpan.Zero);
        }
        return ParseText(File.ReadAllText(path));
    }

    public static PatternParseResult ParseText(string text)
    {
        var sw = Stopwatch.StartNew();
        var errors = new List<string>();
        var warnings = new List<string>();
        var patterns = new Dictionary<string, PatternDefinition>(StringComparer.OrdinalIgnoreCase);

        PatternBuilder? current = null;
        int lineNo = 0;
        foreach (var raw in ReadLines(text))
        {
            lineNo++;
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line[0] == ';') // full line comment
            {
                continue;
            }

            if (line[0] == '*')
            {
                // Commit previous pattern
                CommitCurrent(warnings, patterns, ref current);

                // Split off trailing comment (after first ';') for header tag parsing
                string? trailingComment = null;
                int semiIdx = line.IndexOf(';');
                if (semiIdx >= 0)
                {
                    trailingComment = line[(semiIdx + 1)..];
                    line = line[..semiIdx].TrimEnd();
                }

                // Header base: *NAME, description
                var headerBody = line[1..];
                string name;
                string? description = null;
                int commaIdx = headerBody.IndexOf(',');
                if (commaIdx >= 0)
                {
                    name = headerBody[..commaIdx].Trim();
                    description = headerBody[(commaIdx + 1)..].Trim();
                }
                else
                {
                    name = headerBody.Trim();
                }
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add($"Line {lineNo}: Empty pattern name.");
                    continue;
                }
                bool isModel = false; // default drafting
                if (!string.IsNullOrWhiteSpace(trailingComment))
                {
                    // Look for %TYPE=MODEL or %TYPE=DRAFTING (case-insensitive)
                    var tagIndex = trailingComment.IndexOf("%TYPE=", StringComparison.OrdinalIgnoreCase);
                    if (tagIndex >= 0)
                    {
                        var typeValue = trailingComment[(tagIndex + 6)..].Trim();
                        int space = typeValue.IndexOfAny([' ', '\t', ';']);
                        if (space >= 0)
                        {
                            typeValue = typeValue[..space];
                        }

                        if (typeValue.Equals("MODEL", StringComparison.OrdinalIgnoreCase))
                        {
                            isModel = true;
                        }
                        else if (!typeValue.Equals("DRAFTING", StringComparison.OrdinalIgnoreCase))
                        {
                            warnings.Add($"Line {lineNo}: Unknown %TYPE '{typeValue}'.");
                        }
                    }
                }
                current = new PatternBuilder(name, description, isModel);
                continue;
            }

            if (current == null)
            {
                warnings.Add($"Line {lineNo}: Definition without header ignored.");
                continue;
            }

            // Strip trailing comment for definition line
            int commentIdx = line.IndexOf(';');
            if (commentIdx >= 0)
            {
                line = line[..commentIdx].TrimEnd();
                if (line.Length == 0)
                {
                    continue; // nothing but comment
                }
            }

            // Tokenize by comma
            var tokens = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 5)
            {
                errors.Add($"Line {lineNo}: Not enough values (need at least 5)." );
                continue;
            }
            // Parse numeric tokens invariant culture.
            var numbers = new double[tokens.Length];
            bool allOk = true;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!double.TryParse(tokens[i], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out numbers[i]))
                {
                    errors.Add($"Line {lineNo}: Invalid number '{tokens[i]}'.");
                    allOk = false;
                    break;
                }
            }
            if (!allOk)
            {
                continue;
            }

            double angle = numbers[0];
            double originX = numbers[1];
            double originY = numbers[2];
            double deltaX = numbers[3];
            double deltaY = numbers[4];
            double[] dash = Array.Empty<double>();
            if (numbers.Length > 5)
            {
                dash = numbers.Skip(5).ToArray();
            }

            current.LineGroups.Add(new LineGroup(angle, originX, originY, deltaX, deltaY, dash));
            Debug.WriteLine($"Parsed line group: angle={angle}, origin=({originX},{originY}), delta=({deltaX},{deltaY}), dash=[{string.Join(", ", dash)}]");
        }

        // Commit last
        CommitCurrent(warnings, patterns, ref current);

        sw.Stop();
        return new PatternParseResult(patterns, errors, warnings, sw.Elapsed);
    }

    private static void CommitCurrent(List<string>? warnings, Dictionary<string, PatternDefinition>? patterns, ref PatternBuilder? current)
    {
        if (current == null)
        {
            return;
        }

        var def = new PatternDefinition(current.Name, current.Description, current.IsModel, current.LineGroups.ToList());
        if (!patterns.TryAdd(def.Name, def))
        {
            warnings.Add($"Duplicate pattern name '{def.Name}' replaced previous definition.");
            patterns[def.Name] = def;
        }

        if (def.LineGroups.Count == 0)
        {
            warnings.Add($"Pattern '{def.Name}' has no definition lines.");
        }

        current = null;
    }



    private static IEnumerable<string> ReadLines(string text)
    {
        using var sr = new StringReader(text);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            yield return line;
        }
    }

    private sealed class PatternBuilder
    {
        public string Name { get; }
        public string? Description { get; }
        public bool IsModel { get; }
        public List<LineGroup> LineGroups { get; } = new();
        public PatternBuilder(string name, string? description, bool isModel)
        {
            Name = name;
            Description = description;
            IsModel = isModel;
        }
    }
}
