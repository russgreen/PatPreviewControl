using System;
using System.Collections.Generic;

namespace FillPatternPreview.Model;

/// <summary>
/// Immutable internal representation of a fill pattern.
/// </summary>
public sealed record PatternDefinition(
    string Name,
    string? Description,
    bool IsModel,
    IReadOnlyList<LineGroup> LineGroups);

/// <summary>
/// Represents a group of parallel hatch lines.
/// </summary>
public sealed record LineGroup(
    double AngleDeg,
    double OriginX,
    double OriginY,
    double DeltaX,
    double DeltaY,
    IReadOnlyList<double> DashPattern);

/// <summary>
/// Diagnostics for the last parse / render decisions.
/// </summary>
public sealed record PatternDiagnostics(
    bool Success,
    int LineGroupCount,
    int WarningCount,
    int ErrorCount,
    System.Windows.Size? TileSize,
    bool Tileable,
    TimeSpan ParseDuration,
    string? Message);
