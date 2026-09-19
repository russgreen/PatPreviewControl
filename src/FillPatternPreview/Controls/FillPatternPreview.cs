using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FillPatternPreview.Model;
using FillPatternPreview.Parsing;
using FillPatternPreview.Adapters;
using FillPatternPreview.Rendering;

namespace FillPatternPreview.Controls;

public class FillPatternPreview : Control
{
    static FillPatternPreview()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FillPatternPreview), new FrameworkPropertyMetadata(typeof(FillPatternPreview)));
    }

    #region Dependency Properties
    public string? PatFilePath { get => (string?)GetValue(PatFilePathProperty); set => SetValue(PatFilePathProperty, value); }
    public static readonly DependencyProperty PatFilePathProperty = DependencyProperty.Register(nameof(PatFilePath), typeof(string), typeof(FillPatternPreview), new PropertyMetadata(null, OnPatternSourceChanged));

    public string? PatRawText { get => (string?)GetValue(PatRawTextProperty); set => SetValue(PatRawTextProperty, value); }
    public static readonly DependencyProperty PatRawTextProperty = DependencyProperty.Register(nameof(PatRawText), typeof(string), typeof(FillPatternPreview), new PropertyMetadata(null, OnPatternSourceChanged));

    public string? PatPatternName { get => (string?)GetValue(PatPatternNameProperty); set => SetValue(PatPatternNameProperty, value); }
    public static readonly DependencyProperty PatPatternNameProperty = DependencyProperty.Register(nameof(PatPatternName), typeof(string), typeof(FillPatternPreview), new PropertyMetadata(null, OnPatternSourceChanged));

    public Brush LineBrush { get => (Brush)GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(nameof(LineBrush), typeof(Brush), typeof(FillPatternPreview), new PropertyMetadata(Brushes.Black, OnVisualPropertyChanged));

    /// <summary>
    /// When true, the pattern's first repeat cell (anchored at the pattern origin, the control's
    /// top-left corner) is drawn with <see cref="FirstTileBrush"/> instead of <see cref="LineBrush"/>.
    /// Has no effect for patterns whose repeat cell cannot be determined.
    /// </summary>
    /// <summary>
    /// When true (the default) the pattern is repeated across the whole control. When false only
    /// a single tile (the pattern's first repeat cell, at the top-left) is drawn. Has no effect
    /// for patterns whose repeat cell cannot be determined; those are always tiled.
    /// </summary>
    public bool TilePattern { get => (bool)GetValue(TilePatternProperty); set => SetValue(TilePatternProperty, value); }
    public static readonly DependencyProperty TilePatternProperty = DependencyProperty.Register(nameof(TilePattern), typeof(bool), typeof(FillPatternPreview), new PropertyMetadata(true, OnVisualPropertyChanged));

    public bool HighlightFirstTile { get => (bool)GetValue(HighlightFirstTileProperty); set => SetValue(HighlightFirstTileProperty, value); }
    public static readonly DependencyProperty HighlightFirstTileProperty = DependencyProperty.Register(nameof(HighlightFirstTile), typeof(bool), typeof(FillPatternPreview), new PropertyMetadata(false, OnVisualPropertyChanged));

    public Brush FirstTileBrush { get => (Brush)GetValue(FirstTileBrushProperty); set => SetValue(FirstTileBrushProperty, value); }
    public static readonly DependencyProperty FirstTileBrushProperty = DependencyProperty.Register(nameof(FirstTileBrush), typeof(Brush), typeof(FillPatternPreview), new PropertyMetadata(Brushes.Red, OnVisualPropertyChanged));

    public double Scale { get => (double)GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }
    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(nameof(Scale), typeof(double), typeof(FillPatternPreview), new PropertyMetadata(1.0, OnVisualPropertyChanged, CoerceScaleOrZoom));

    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(FillPatternPreview), new PropertyMetadata(1.0, OnVisualPropertyChanged, CoerceScaleOrZoom));

    public Point PanOffset { get => (Point)GetValue(PanOffsetProperty); set => SetValue(PanOffsetProperty, value); }
    public static readonly DependencyProperty PanOffsetProperty = DependencyProperty.Register(nameof(PanOffset), typeof(Point), typeof(FillPatternPreview), new PropertyMetadata(new Point(0,0), OnVisualPropertyChanged, CoercePanOffset));

    // Scale/Zoom are kept finite and in [0.0001, 1e6] and PanOffset finite and bounded, so a bad
    // binding value (NaN, Infinity, negative) can't drive the renderer into pathological work.
    private static object CoerceScaleOrZoom(DependencyObject d, object value) => InteractionMath.CoerceScale((double)value);

    private static object CoercePanOffset(DependencyObject d, object value) => InteractionMath.CoercePan((Point)value);

    public bool UsePatternMaker { get => (bool)GetValue(UsePatternMakerProperty); set => SetValue(UsePatternMakerProperty, value); }
    public static readonly DependencyProperty UsePatternMakerProperty = DependencyProperty.Register(nameof(UsePatternMaker), typeof(bool), typeof(FillPatternPreview), new PropertyMetadata(false, OnVisualPropertyChanged));

    /// <summary>
    /// When true, the mouse wheel zooms around the cursor, dragging pans, double-click resets,
    /// and (while focused) +/- zoom, the arrow keys pan and Ctrl+0 resets. Off by default.
    /// </summary>
    public bool IsInteractive { get => (bool)GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }
    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.Register(nameof(IsInteractive), typeof(bool), typeof(FillPatternPreview), new PropertyMetadata(false, OnIsInteractiveChanged));

    public PatternDefinition? Pattern { get => (PatternDefinition?)GetValue(PatternProperty); private set => SetValue(PatternPropertyKey, value); }
    private static readonly DependencyPropertyKey PatternPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Pattern), typeof(PatternDefinition), typeof(FillPatternPreview), new PropertyMetadata(null, OnVisualPropertyChanged));
    public static readonly DependencyProperty PatternProperty = PatternPropertyKey.DependencyProperty;

    #endregion

    public event EventHandler? PatternChanged;

    /// <summary>Raised after user interaction changes <see cref="Zoom"/> or <see cref="PanOffset"/>, including a reset.</summary>
    public event EventHandler? InteractionChanged;

    private PatternMakerAdapter.ConvertedPattern? _pmConverted;
    private Size? _tileSize; // first repeat cell in the pattern's native units, if one exists
    private Point? _dragLast;
    private readonly RenderBudget _budget = new();

    private static void OnIsInteractiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue)
        {
            var ctrl = (FillPatternPreview)d;
            if (ctrl.IsMouseCaptured)
            {
                ctrl.ReleaseMouseCapture();
            }
        }
    }

    private static void OnPatternSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (FillPatternPreview)d;
        ctrl.LoadPattern();
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((FillPatternPreview)d).InvalidateVisual();
    }

    private void LoadPattern()
    {
        PatternDefinition? target = null;
        Size? tileSize = null;
        PatternMakerAdapter.ConvertedPattern? converted = null;

        // This runs inside a dependency-property callback, so an exception here would surface
        // from whatever code set the property (often XAML loading or a binding update) and can
        // take the host down. Any failure just means "no pattern".
        try
        {
            PatternParseResult? result = null;
            if (!string.IsNullOrWhiteSpace(PatRawText))
            {
                result = PatParser.ParseText(PatRawText);
            }
            else if (!string.IsNullOrWhiteSpace(PatFilePath))
            {
                result = PatParser.ParseFile(PatFilePath);
            }

            if (result is { Patterns.Count: > 0 })
            {
                target = !string.IsNullOrWhiteSpace(PatPatternName) && result.Patterns.TryGetValue(PatPatternName, out var named)
                    ? named
                    : result.Patterns.Values.FirstOrDefault();
            }

            tileSize = PatternTileCalculator.TryComputeTileSize(target, out var computed) ? computed : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Loading pattern failed: {ex}");
            target = null;
            tileSize = null;
        }

        if (UsePatternMaker && target != null)
        {
            // Experimental path: if it fails the pattern itself is still fine to render normally.
            try { converted = PatternMakerAdapter.Build(target); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"PatternMaker adapter failed: {ex.Message}"); }
        }

        Pattern = target;
        _tileSize = tileSize;
        _pmConverted = converted;
        PatternChanged?.Invoke(this, EventArgs.Empty);
    }

    #region Interaction
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!IsInteractive || e.Delta == 0)
        {
            return;
        }

        ZoomBy(InteractionMath.WheelFactor(e.Delta), e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsInteractive)
        {
            return;
        }

        Focus();
        if (e.ClickCount == 2)
        {
            ResetView();
        }
        else if (CaptureMouse())
        {
            _dragLast = e.GetPosition(this);
        }

        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!IsInteractive || _dragLast is not { } last)
        {
            return;
        }

        var current = e.GetPosition(this);
        _dragLast = current;
        SetView(Zoom, InteractionMath.PanBy(PanOffset, current - last, GetCurrentScale()));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragLast != null)
        {
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _dragLast = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsInteractive)
        {
            return;
        }

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        double step = InteractionMath.KeyPanPixels;
        var centre = new Point(ActualWidth / 2, ActualHeight / 2);
        switch (e.Key)
        {
            case Key.D0 or Key.NumPad0 when ctrl:
                ResetView();
                break;
            case Key.OemPlus or Key.Add:
                ZoomBy(InteractionMath.KeyStep, centre);
                break;
            case Key.OemMinus or Key.Subtract:
                ZoomBy(1 / InteractionMath.KeyStep, centre);
                break;
            case Key.Left:
                PanByPixels(new Vector(-step, 0));
                break;
            case Key.Right:
                PanByPixels(new Vector(step, 0));
                break;
            case Key.Up:
                PanByPixels(new Vector(0, -step));
                break;
            case Key.Down:
                PanByPixels(new Vector(0, step));
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    // Scale and units factor without Zoom - the part of the render scale interaction doesn't change.
    private double GetBaseScale()
        => Math.Max(0.0001, Scale * (Pattern != null ? LineFamilyExpander.GetUnitsToDipFactor(Pattern) : 1.0));

    private void ZoomBy(double factor, Point anchor)
    {
        var (zoom, pan) = InteractionMath.ZoomAt(Zoom, PanOffset, GetBaseScale(), anchor, factor);
        SetView(zoom, pan);
    }

    private double GetCurrentScale() => Math.Max(0.0001, GetBaseScale() * Zoom);

    // Arrow keys move the pattern in the arrow's direction, the same as dragging that way.
    private void PanByPixels(Vector deviceDelta)
        => SetView(Zoom, InteractionMath.PanBy(PanOffset, deviceDelta, GetCurrentScale()));

    private void ResetView() => SetView(1.0, new Point(0, 0));

    private void SetView(double zoom, Point pan)
    {
        if (zoom == Zoom && pan == PanOffset)
        {
            return;
        }

        Zoom = zoom;
        PanOffset = pan;
        InteractionChanged?.Invoke(this, EventArgs.Empty);
    }
    #endregion

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(0,0,ActualWidth,ActualHeight);
        if (rect.IsEmpty)
        {
            return;
        }

        dc.DrawRectangle(Background ?? Brushes.Transparent, null, rect);
        if (Pattern == null)
        {
            return;
        }

        // OnRender runs inside WPF's layout/render pass: an exception escaping it is an unhandled
        // exception on the UI thread and takes the whole host application down. A preview control
        // is never worth that, so anything that goes wrong here just leaves the pattern undrawn.
        try
        {
            _budget.Reset();
            using var clip = PushScope.Clip(dc, new RectangleGeometry(rect));

            // PanOffset is in pattern units: shift the pattern by it, and draw the part of the
            // pattern-space plane that now falls inside the control.
            double panScale = GetRenderScale(Pattern);
            double offsetX = PanOffset.X * panScale, offsetY = PanOffset.Y * panScale;
            if (!double.IsFinite(offsetX) || !double.IsFinite(offsetY))
            {
                offsetX = offsetY = 0;
            }

            using var shift = PushScope.Transform(dc, new TranslateTransform(offsetX, offsetY));
            RenderPattern(dc, Pattern, new Rect(-offsetX, -offsetY, rect.Width, rect.Height));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FillPatternPreview render failed: {ex}");
        }

        if (_budget.IsExhausted)
        {
            System.Diagnostics.Debug.WriteLine("FillPatternPreview: render budget exhausted; pattern drawn incompletely.");
        }
    }

    private void RenderPattern(DrawingContext dc, PatternDefinition pattern, Rect rect)
    {
        var lineBrush = LineBrush ?? Brushes.Black;
        var tileBrush = HighlightFirstTile ? FirstTileBrush : null;
        double scale = GetRenderScale(pattern);
        var tileRect = _tileSize is { } tileSize ? new Rect(0, 0, tileSize.Width * scale, tileSize.Height * scale) : Rect.Empty;
        if (!tileRect.IsEmpty && double.IsFinite(tileRect.Width + tileRect.Height) && (!TilePattern || tileBrush != null))
        {
            if (!TilePattern)
            {
                // Just the single tile, in the highlight colour if that option is on.
                var onlyTile = Rect.Intersect(rect, tileRect);
                if (!onlyTile.IsEmpty)
                {
                    using var tileClip = PushScope.Clip(dc, new RectangleGeometry(onlyTile));
                    RenderLines(dc, onlyTile, tileBrush ?? lineBrush);
                }

                return;
            }

            // Everything outside the first tile in the normal colour, then the tile itself in
            // its own colour. Clipping (rather than overdrawing) avoids anti-aliased fringes of
            // the normal colour showing through the highlight.
            using (PushScope.Clip(dc, new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(rect), new RectangleGeometry(tileRect))))
            {
                RenderLines(dc, rect, lineBrush);
            }

            var visibleTile = Rect.Intersect(rect, tileRect);
            if (!visibleTile.IsEmpty)
            {
                using var highlightClip = PushScope.Clip(dc, new RectangleGeometry(visibleTile));
                RenderLines(dc, visibleTile, tileBrush ?? lineBrush);
            }
        }
        else
        {
            RenderLines(dc, rect, lineBrush);
        }
    }

    // Scale*Zoom are coerced finite and positive by the DPs; the finite check is a last line of
    // defence (Math.Max propagates NaN rather than applying the floor).
    private double GetRenderScale(PatternDefinition pattern)
    {
        double scale = Scale * Zoom * LineFamilyExpander.GetUnitsToDipFactor(pattern);
        return double.IsFinite(scale) ? Math.Max(0.0001, scale) : 1.0;
    }

    private void RenderLines(DrawingContext dc, Rect rect, Brush stroke)
    {
        if (UsePatternMaker && _pmConverted != null)
        {
            RenderWithPatternMaker(dc, rect, _pmConverted, stroke);
        }
        else
        {
            RenderLegacy(dc, rect, Pattern!, stroke);
        }
    }

    // Every draw call against an unfrozen Pen/Brush makes WPF track it as a change-notifying
    // dependency, and that bookkeeping grows with the number of draw calls: a few thousand dashes
    // took seconds, and cost grew quadratically. Frozen resources are plain immutable data. The
    // brush is snapshotted (rather than frozen in place) so the caller's own LineBrush is untouched.
    private static Pen CreateFrozenPen(Brush stroke)
    {
        var brush = stroke;
        if (!brush.IsFrozen)
        {
            brush = stroke.CloneCurrentValue();
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }
        }

        var pen = new Pen(brush, 1);
        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        return pen;
    }

    private void RenderLegacy(DrawingContext dc, Rect rect, PatternDefinition pattern, Brush stroke)
    {
        var pen = CreateFrozenPen(stroke); double scale = GetRenderScale(pattern);
        foreach (var g in pattern.LineGroups)
        {
            double angleRad = g.AngleDeg * Math.PI/180.0; var dir = new Vector(Math.Cos(angleRad), Math.Sin(angleRad)); if (dir.LengthSquared<1e-12)
            {
                continue;
            }

            dir.Normalize(); var normal = new Vector(-dir.Y, dir.X);
            var origin = new Point(g.OriginX*scale, g.OriginY*scale);
            var delta = LineFamilyExpander.ComputeWorldDelta(dir, normal, g.DeltaX, g.DeltaY, scale);
            var (kMin, kMax) = LineFamilyExpander.ComputeRepeatRange(rect, origin, delta, normal, scale);
            if (!LineFamilyExpander.IsRepeatRangeUsable(kMin, kMax))
            {
                continue;
            }

            for (int k=kMin;k<=kMax;k++){
                if (!_budget.TryConsume())
                {
                    return; // out of budget for this render pass: stop rather than freeze the UI
                }

                var basePoint = origin + k*delta; if(LineFamilyExpander.TryIntersectInfiniteLineWithRect(basePoint,dir,rect,out var p1,out var p2))
                {
                    DrawDashed(dc,pen,p1,p2,basePoint,dir,g.DashPattern,scale);
                }
            }
        }
    }

    private void RenderWithPatternMaker(DrawingContext dc, Rect rect, PatternMakerAdapter.ConvertedPattern converted, Brush stroke)
    {
        var pen = CreateFrozenPen(stroke); double scale = GetRenderScale(converted.Source);
        foreach (var grid in converted.Grids)
        {
            double angle = grid.Angle; var dir = new Vector(Math.Cos(angle), Math.Sin(angle)); var normal = new Vector(-dir.Y, dir.X);
            // Use Offset for spacing
            double spacing = Math.Abs(grid.Offset); if (spacing < 1e-6)
            {
                spacing = grid.Span;
            }

            if (spacing < 1e-6)
            {
                spacing = 8;
            }

            spacing *=scale;
            // Base origin
            var origin = new Point(grid.Origin.U*scale, grid.Origin.V*scale);
            var corners = new[]{rect.TopLeft,rect.TopRight,rect.BottomLeft,rect.BottomRight}; double Project(Point p)=>p.X*normal.X+p.Y*normal.Y; double minProj=corners.Min(Project)-spacing; double maxProj=corners.Max(Project)+spacing; double originProj=Project(origin);
            var (kMin, kMax) = LineFamilyExpander.RepeatRangeFromBounds((minProj-originProj)/spacing, (maxProj-originProj)/spacing);
            if (!LineFamilyExpander.IsRepeatRangeUsable(kMin, kMax))
            {
                continue;
            }

            for (int k=kMin;k<=kMax;k++){
                if (!_budget.TryConsume())
                {
                    return;
                }

                var basePoint = origin + k*normal*spacing; if(LineFamilyExpander.TryIntersectInfiniteLineWithRect(basePoint,dir,rect,out var p1,out var p2))
                {
                    dc.DrawLine(pen,p1,p2);
                }
            }
        }
    }

    private void DrawDashed(DrawingContext dc, Pen pen, Point p1, Point p2, Point basePoint, Vector dir, System.Collections.Generic.IReadOnlyList<double> pattern, double scale)
    {
        foreach (var seg in LineFamilyExpander.ExpandDashSegments(p1, p2, basePoint, dir, pattern, scale, pen.Thickness))
        {
            if (!_budget.TryConsume())
            {
                return;
            }

            if (seg.Kind == LineFamilyExpander.SegmentKind.Dot)
            {
                var pt = seg.Start;
                dc.DrawRectangle(pen.Brush, null, new Rect(pt.X-pen.Thickness/2, pt.Y-pen.Thickness/2, pen.Thickness, pen.Thickness));
            }
            else
            {
                dc.DrawLine(pen, seg.Start, seg.End);
            }
        }
    }

    // A pushed clip/transform that is always popped, even if drawing inside it throws, so the
    // DrawingContext's push/pop stack stays balanced.
    private sealed class PushScope : System.IDisposable
    {
        private readonly DrawingContext _dc;
        private PushScope(DrawingContext dc) => _dc = dc;

        public static PushScope Clip(DrawingContext dc, Geometry clip)
        {
            dc.PushClip(clip);
            return new PushScope(dc);
        }

        public static PushScope Transform(DrawingContext dc, System.Windows.Media.Transform transform)
        {
            dc.PushTransform(transform);
            return new PushScope(dc);
        }

        public void Dispose() => _dc.Pop();
    }
}
