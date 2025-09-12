using System.Collections.Generic;

namespace FillPatternPreview.Model;

/// <summary>
/// Represents a size with width and height.
/// </summary>
/// <param name="Width">The width</param>
/// <param name="Height">The height</param>
public record Size(double Width, double Height);

/// <summary>
/// Represents a fill pattern definition with line groups describing the pattern geometry.
/// </summary>
/// <param name="Name">The name of the pattern</param>
/// <param name="Description">Optional description of the pattern</param>
/// <param name="IsModel">Whether this is a model pattern (true) or drafting pattern (false)</param>
/// <param name="LineGroups">Collection of line groups that define the pattern</param>
public record PatternDefinition(
    string Name,
    string? Description,
    bool IsModel,
    IReadOnlyList<LineGroup> LineGroups);

/// <summary>
/// Represents a group of parallel lines in a fill pattern.
/// </summary>
/// <param name="AngleDeg">Angle of the lines in degrees (relative to X-axis)</param>
/// <param name="OriginX">X coordinate of the starting point</param>
/// <param name="OriginY">Y coordinate of the starting point</param>
/// <param name="DeltaX">X offset to repeat the line group</param>
/// <param name="DeltaY">Y offset to repeat the line group</param>
/// <param name="DashPattern">Dash pattern where positive=drawn segment, negative=gap, zero=dot</param>
public record LineGroup(
    double AngleDeg,
    double OriginX,
    double OriginY,
    double DeltaX,
    double DeltaY,
    IReadOnlyList<double> DashPattern);

/// <summary>
/// Contains diagnostic information about pattern parsing and rendering.
/// </summary>
/// <param name="Success">Whether the pattern was successfully parsed/rendered</param>
/// <param name="LineGroupCount">Number of line groups in the pattern</param>
/// <param name="WarningCount">Number of warnings encountered</param>
/// <param name="ErrorCount">Number of errors encountered</param>
/// <param name="TileSize">Computed tile size for tiled rendering (if applicable)</param>
/// <param name="Tileable">Whether the pattern can be rendered using tiles</param>
/// <param name="ParseDuration">Time taken to parse the pattern</param>
/// <param name="Message">Summary message or first error description</param>
public record PatternDiagnostics(
    bool Success,
    int LineGroupCount,
    int WarningCount,
    int ErrorCount,
    Size? TileSize,
    bool Tileable,
    System.TimeSpan ParseDuration,
    string? Message);