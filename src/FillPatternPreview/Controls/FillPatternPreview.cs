using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

    public double Scale { get => (double)GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }
    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(nameof(Scale), typeof(double), typeof(FillPatternPreview), new PropertyMetadata(1.0, OnVisualPropertyChanged));

    public double Zoom { get => (double)GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(FillPatternPreview), new PropertyMetadata(1.0, OnVisualPropertyChanged));

    public Point PanOffset { get => (Point)GetValue(PanOffsetProperty); set => SetValue(PanOffsetProperty, value); }
    public static readonly DependencyProperty PanOffsetProperty = DependencyProperty.Register(nameof(PanOffset), typeof(Point), typeof(FillPatternPreview), new PropertyMetadata(new Point(0,0), OnVisualPropertyChanged));

    public bool UsePatternMaker { get => (bool)GetValue(UsePatternMakerProperty); set => SetValue(UsePatternMakerProperty, value); }
    public static readonly DependencyProperty UsePatternMakerProperty = DependencyProperty.Register(nameof(UsePatternMaker), typeof(bool), typeof(FillPatternPreview), new PropertyMetadata(false, OnVisualPropertyChanged));

    public PatternDefinition? Pattern { get => (PatternDefinition?)GetValue(PatternProperty); private set => SetValue(PatternPropertyKey, value); }
    private static readonly DependencyPropertyKey PatternPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Pattern), typeof(PatternDefinition), typeof(FillPatternPreview), new PropertyMetadata(null, OnVisualPropertyChanged));
    public static readonly DependencyProperty PatternProperty = PatternPropertyKey.DependencyProperty;

    #endregion

    public event EventHandler? PatternChanged;

    private PatternMakerAdapter.ConvertedPattern? _pmConverted;

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
        if (!string.IsNullOrWhiteSpace(PatRawText))
        {
            var result = PatParser.ParseText(PatRawText);
            if (result.Patterns.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(PatPatternName) && result.Patterns.TryGetValue(PatPatternName, out var named))
                {
                    target = named;
                }
                else
                {
                    target = result.Patterns.Values.FirstOrDefault();
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(PatFilePath))
        {
            try
            {
                var result = PatParser.ParseFile(PatFilePath);
                if (result.Patterns.Count > 0)
                {
                    if (!string.IsNullOrWhiteSpace(PatPatternName) && result.Patterns.TryGetValue(PatPatternName, out var named))
                    {
                        target = named;
                    }
                    else
                    {
                        target = result.Patterns.Values.FirstOrDefault();
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }
        Pattern = target;
        _pmConverted = null;
        if (UsePatternMaker && Pattern != null)
        {
            try { _pmConverted = PatternMakerAdapter.Build(Pattern); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"PatternMaker adapter failed: {ex.Message}"); }
        }
        PatternChanged?.Invoke(this, EventArgs.Empty);
    }

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

        using var clip = new DrawingContextClip(dc, rect);

        if (UsePatternMaker && _pmConverted != null)
        {
            RenderWithPatternMaker(dc, rect, _pmConverted);
        }
        else
        {
            RenderLegacy(dc, rect, Pattern);
        }
    }

    private void RenderLegacy(DrawingContext dc, Rect rect, PatternDefinition pattern)
    {
        var stroke = LineBrush ?? Brushes.Black; var pen = new Pen(stroke,1); double scale = Math.Max(0.0001, Scale*Zoom*LineFamilyExpander.GetUnitsToDipFactor(pattern));
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
            if (kMax-kMin>4000)
            {
                continue;
            }

            for (int k=kMin;k<=kMax;k++){
                var basePoint = origin + k*delta; if(LineFamilyExpander.TryIntersectInfiniteLineWithRect(basePoint,dir,rect,out var p1,out var p2))
                {
                    DrawDashed(dc,pen,p1,p2,basePoint,dir,g.DashPattern,scale);
                }
            }
        }
    }

    private void RenderWithPatternMaker(DrawingContext dc, Rect rect, PatternMakerAdapter.ConvertedPattern converted)
    {
        var stroke = LineBrush ?? Brushes.Black; var pen = new Pen(stroke,1); double scale = Math.Max(0.0001, Scale*Zoom*LineFamilyExpander.GetUnitsToDipFactor(converted.Source));
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
            int kMin=(int)Math.Floor((minProj-originProj)/spacing); int kMax=(int)Math.Ceiling((maxProj-originProj)/spacing); if(kMax-kMin>4000)
            {
                continue;
            }

            for (int k=kMin;k<=kMax;k++){
                var basePoint = origin + k*normal*spacing; if(LineFamilyExpander.TryIntersectInfiniteLineWithRect(basePoint,dir,rect,out var p1,out var p2))
                {
                    dc.DrawLine(pen,p1,p2);
                }
            }
        }
    }

    private static void DrawDashed(DrawingContext dc, Pen pen, Point p1, Point p2, Point basePoint, Vector dir, System.Collections.Generic.IReadOnlyList<double> pattern, double scale)
    {
        foreach (var seg in LineFamilyExpander.ExpandDashSegments(p1, p2, basePoint, dir, pattern, scale, pen.Thickness))
        {
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

    private sealed class DrawingContextClip : System.IDisposable { private readonly DrawingContext _dc; public DrawingContextClip(DrawingContext dc, Rect rect){ _dc=dc; _dc.PushClip(new RectangleGeometry(rect)); } public void Dispose()=>_dc.Pop(); }
}
