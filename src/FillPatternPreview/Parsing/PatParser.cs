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
    // Hard limits so hostile or corrupt input can't make parsing (which runs on the UI thread)
    // consume unbounded time or memory. All are far beyond anything a real .pat file needs
    // (AutoCAD's stock acad.pat is ~100 KB, ~80 patterns, at most 6 dash entries per line).
    public const int MaxInputChars = 4 * 1024 * 1024;
    public const int MaxLineLength = 8192;
    public const int MaxPatterns = 10_000;
    public const int MaxLineGroupsPerPattern = 512;
    public const int MaxDashEntries = 64;
    public const double MaxAbsValue = 1e9;

    public static PatternParseResult ParseFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return Failure($"File not found: {path}");
            }

            // Check the size first: File.ReadAllText on a multi-gigabyte file would stall the
            // caller and can run out of memory.
            long length = new FileInfo(path).Length;
            if (length > MaxInputChars)
            {
                return Failure($"File is too large ({length:N0} bytes; limit {MaxInputChars:N0}): {path}");
            }

            return ParseText(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException or ArgumentException)
        {
            return Failure($"Could not read '{path}': {ex.Message}");
        }
    }

    private static PatternParseResult Failure(string error)
        => new(new Dictionary<string, PatternDefinition>(), new[] { error }, null, TimeSpan.Zero);

    public static PatternParseResult ParseText(string text)
    {
        if (text.Length > MaxInputChars)
        {
            return Failure($"Text is too large ({text.Length:N0} characters; limit {MaxInputChars:N0}).");
        }

        var sw = Stopwatch.StartNew();
        var errors = new List<string>();
        var warnings = new List<string>();
        var patterns = new Dictionary<string, PatternDefinition>(StringComparer.OrdinalIgnoreCase);

        PatternBuilder? current = null;
        // %UNITS=... is conventionally a file-level declaration (before the first pattern
        // header) that applies to every pattern in the file, though it may also be repeated
        // per-pattern; track the most recently seen value and apply it to each new pattern.
        string? fileUnits = null;
        int lineNo = 0;
        foreach (var raw in ReadLines(text))
        {
            lineNo++;
            if (raw.Length > MaxLineLength)
            {
                errors.Add($"Line {lineNo}: Line too long ({raw.Length:N0} characters; limit {MaxLineLength:N0}).");
                continue;
            }

            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line[0] == ';') // full line comment
            {
                // %TYPE=MODEL/DRAFTING conventionally appears as its own comment line
                // immediately after the header, not inline on the "*Name" line.
                var unitsTag = TryParseUnitsTag(line[1..]);
                if (unitsTag != null)
                {
                    fileUnits = unitsTag;
                    if (current != null)
                    {
                        current.Units = unitsTag;
                    }
                }
                else if (current != null)
                {
                    ApplyTypeTag(line[1..], lineNo, warnings, current);
                }
                continue;
            }

            if (line[0] == '*')
            {
                // Commit previous pattern
                CommitCurrent(warnings, patterns, ref current);
                if (patterns.Count >= MaxPatterns)
                {
                    errors.Add($"Line {lineNo}: Too many patterns; stopped after {MaxPatterns:N0}.");
                    break;
                }

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
                current = new PatternBuilder(name, description) { Units = fileUnits };
                if (!string.IsNullOrWhiteSpace(trailingComment))
                {
                    ApplyTypeTag(trailingComment, lineNo, warnings, current);
                }
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
            if (tokens.Length - 5 > MaxDashEntries)
            {
                errors.Add($"Line {lineNo}: Too many dash values ({tokens.Length - 5}; limit {MaxDashEntries}).");
                continue;
            }

            if (current.LineGroups.Count >= MaxLineGroupsPerPattern)
            {
                errors.Add($"Line {lineNo}: Pattern '{current.Name}' has too many definition lines (limit {MaxLineGroupsPerPattern}); line skipped.");
                continue;
            }

            var numbers = new double[tokens.Length];
            bool allOk = true;
            for (int i = 0; i < tokens.Length; i++)
            {
                // double.TryParse accepts "NaN", "Infinity" and overflowing exponents like
                // "1e999"; none of those are meaningful geometry and they poison every
                // downstream calculation, so reject them (and absurd magnitudes) here.
                if (!double.TryParse(tokens[i], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out numbers[i])
                    || !double.IsFinite(numbers[i]))
                {
                    errors.Add($"Line {lineNo}: Invalid number '{tokens[i]}'.");
                    allOk = false;
                    break;
                }

                if (Math.Abs(numbers[i]) > MaxAbsValue)
                {
                    errors.Add($"Line {lineNo}: Value '{tokens[i]}' is out of range (limit ±{MaxAbsValue:E0}).");
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

    private static void ApplyTypeTag(string commentText, int lineNo, List<string> warnings, PatternBuilder current)
    {
        // Look for %TYPE=MODEL or %TYPE=DRAFTING (case-insensitive)
        var tagIndex = commentText.IndexOf("%TYPE=", StringComparison.OrdinalIgnoreCase);
        if (tagIndex < 0)
        {
            return;
        }

        var typeValue = commentText[(tagIndex + 6)..].Trim();
        int space = typeValue.IndexOfAny([' ', '\t', ';']);
        if (space >= 0)
        {
            typeValue = typeValue[..space];
        }

        if (typeValue.Equals("MODEL", StringComparison.OrdinalIgnoreCase))
        {
            current.IsModel = true;
        }
        else if (typeValue.Equals("DRAFTING", StringComparison.OrdinalIgnoreCase))
        {
            current.IsModel = false;
        }
        else
        {
            warnings.Add($"Line {lineNo}: Unknown %TYPE '{typeValue}'.");
        }
    }

    private static string? TryParseUnitsTag(string commentText)
    {
        var tagIndex = commentText.IndexOf("%UNITS=", StringComparison.OrdinalIgnoreCase);
        if (tagIndex < 0)
        {
            return null;
        }

        var value = commentText[(tagIndex + 7)..].Trim();
        int space = value.IndexOfAny([' ', '\t', ';']);
        if (space >= 0)
        {
            value = value[..space];
        }

        return value.Length == 0 ? null : value.ToUpperInvariant();
    }

    private static void CommitCurrent(List<string>? warnings, Dictionary<string, PatternDefinition>? patterns, ref PatternBuilder? current)
    {
        if (current == null)
        {
            return;
        }

        var def = new PatternDefinition(current.Name, current.Description, current.IsModel, current.LineGroups.ToList(), current.Units);
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
        public bool IsModel { get; set; } // default drafting
        public string? Units { get; set; }
        public List<LineGroup> LineGroups { get; } = new();
        public PatternBuilder(string name, string? description)
        {
            Name = name;
            Description = description;
        }
    }
}
