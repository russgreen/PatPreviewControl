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
        PatternMakerAdapter.ConvertedPattern? converted = null;

        // This runs inside a dependency-property callback, so an exception here would surface
        // from whatever code set the property (often XAML loading or a binding update) and can
        // take the host down. The resolver never throws: any failure just means "no pattern".
        PatternResolver.TryResolve(PatRawText, PatFilePath, PatPatternName, out var target, out var tileSize);

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
            var options = new PatternRenderOptions(
                LineBrush ?? Brushes.Black,
                HighlightFirstTile ? FirstTileBrush : null,
                TilePattern,
                Scale,
                Zoom,
                PanOffset);
            new PatternRenderer(Pattern, _tileSize, UsePatternMaker ? _pmConverted : null, options, _budget)
                .Render(dc, rect.Size);
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
}
