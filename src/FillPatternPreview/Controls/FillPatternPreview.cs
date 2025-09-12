using System;
using FillPatternPreview.Model;

namespace FillPatternPreview.Controls;

/// <summary>
/// Enumeration of pattern sources for the FillPatternPreview control.
/// </summary>
public enum PatternSource
{
    /// <summary>No pattern source specified.</summary>
    None,
    /// <summary>Pattern loaded from a .pat file.</summary>
    PatFile,
    /// <summary>Pattern loaded from raw .pat text.</summary>
    PatText,
    /// <summary>Pattern loaded from a Revit FillPattern object via reflection.</summary>
    FillPatternObject,
    /// <summary>Pattern provided via internal model.</summary>
    InternalModel
}

/// <summary>
/// Enumeration of rendering modes for the pattern preview.
/// </summary>
public enum RenderMode
{
    /// <summary>Use immediate rendering (draw lines directly).</summary>
    Immediate,
    /// <summary>Use cached bitmap rendering with tiled brush.</summary>
    CachedBitmap,
    /// <summary>Automatically choose the best rendering mode.</summary>
    Auto
}

/// <summary>
/// A WPF control for previewing AutoCAD/Revit style hatch (fill) patterns from multiple input sources.
/// Supports both drafting (paper) and model (world) patterns with performant tiling, zoom, pan, and diagnostics.
/// 
/// NOTE: This is a cross-platform compatible skeleton. The full WPF implementation with FrameworkElement,
/// DependencyProperty, and WPF rendering should replace this when targeting Windows with WPF support.
/// </summary>
public class FillPatternPreview
{
    #region Properties

    /// <summary>
    /// Gets or sets the pattern source type.
    /// </summary>
    public PatternSource PatternSource { get; set; } = PatternSource.None;

    /// <summary>
    /// Gets or sets the path to the .pat file.
    /// </summary>
    public string? PatFilePath { get; set; }

    /// <summary>
    /// Gets or sets the raw .pat text content.
    /// </summary>
    public string? PatRawText { get; set; }

    /// <summary>
    /// Gets or sets the name of the pattern to select (when multiple patterns are available).
    /// </summary>
    public string? PatPatternName { get; set; }

    /// <summary>
    /// Gets or sets the scale factor for the pattern.
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the zoom level for the pattern preview.
    /// </summary>
    public double Zoom 
    { 
        get => _zoom; 
        set => _zoom = Math.Max(0.1, Math.Min(20.0, value)); 
    }
    private double _zoom = 1.0;

    /// <summary>
    /// Gets or sets the pan offset for the pattern preview.
    /// </summary>
    public Point PanOffset { get; set; } = new Point(0, 0);

    /// <summary>
    /// Gets or sets an override for the stroke thickness. If null, uses default thickness.
    /// </summary>
    public double? StrokeThicknessOverride { get; set; }

    /// <summary>
    /// Gets the current pattern definition.
    /// </summary>
    public PatternDefinition? Pattern { get; private set; }

    /// <summary>
    /// Gets the current pattern diagnostics information.
    /// </summary>
    public PatternDiagnostics? Diagnostics { get; private set; }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the pattern changes.
    /// </summary>
    public event EventHandler? PatternChanged;

    /// <summary>
    /// Occurs when pattern parsing fails.
    /// </summary>
    public event EventHandler<PatternErrorEventArgs>? ParseFailed;

    #endregion

    #region Methods

    /// <summary>
    /// Sets the pattern and diagnostics. Used internally by the acquisition workflow.
    /// </summary>
    /// <param name="pattern">The pattern definition</param>
    /// <param name="diagnostics">The diagnostics information</param>
    protected virtual void SetPattern(PatternDefinition? pattern, PatternDiagnostics? diagnostics)
    {
        var oldPattern = Pattern;
        Pattern = pattern;
        Diagnostics = diagnostics;

        if (pattern != oldPattern)
        {
            PatternChanged?.Invoke(this, EventArgs.Empty);
        }

        if (diagnostics?.Success == false)
        {
            ParseFailed?.Invoke(this, new PatternErrorEventArgs(diagnostics.Message ?? "Parse failed"));
        }
    }

    /// <summary>
    /// Triggers pattern acquisition based on current source settings.
    /// </summary>
    public virtual void RefreshPattern()
    {
        // TODO: Implement pattern acquisition workflow
        // This is a placeholder for the full implementation
    }

    #endregion
}

/// <summary>
/// Simple point structure for cross-platform compatibility.
/// </summary>
/// <param name="X">The X coordinate</param>
/// <param name="Y">The Y coordinate</param>
public record Point(double X, double Y);

/// <summary>
/// Event arguments for pattern parsing errors.
/// </summary>
public class PatternErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the exception, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Initializes a new instance of the PatternErrorEventArgs class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The exception, if any.</param>
    public PatternErrorEventArgs(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }
}