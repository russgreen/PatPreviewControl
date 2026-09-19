using System;
using System.Windows;

namespace FillPatternPreview.Rendering;

/// <summary>
/// Pure zoom/pan math for the control's interaction (spec section 15). No UI dependency so it
/// can be unit tested directly.
/// </summary>
/// <remarks>
/// The view maps a pattern-space point <c>w</c> to a device point <c>(w + pan) * scale</c>,
/// where <c>scale = baseScale * zoom</c> and <c>pan</c> is in the pattern's native units (so it
/// is the same <see cref="System.Windows.Point"/> the control exposes as <c>PanOffset</c>).
/// </remarks>
internal static class InteractionMath
{
    public const double MinZoom = 0.1;
    public const double MaxZoom = 20.0;

    /// <summary>Zoom factor applied per wheel notch (a wheel delta of 120).</summary>
    public const double WheelStep = 1.1;

    /// <summary>Zoom factor applied per +/- key press.</summary>
    public const double KeyStep = 1.1;

    /// <summary>Distance, in device units, an arrow key press pans by.</summary>
    public const double KeyPanPixels = 10.0;

    /// <summary>Bounds for the <c>Scale</c>/<c>Zoom</c> dependency properties (programmatic values).</summary>
    public const double MinScaleValue = 0.0001;
    public const double MaxScaleValue = 1_000_000;

    /// <summary>Largest |PanOffset| component, in pattern units.</summary>
    public const double MaxPan = 1_000_000_000;

    /// <summary>Coerces a Scale/Zoom value into a finite positive range; NaN falls back to 1.</summary>
    public static double CoerceScale(double value)
    {
        if (double.IsNaN(value))
        {
            return 1.0;
        }

        return Math.Min(MaxScaleValue, Math.Max(MinScaleValue, value));
    }

    /// <summary>Coerces a pan offset so both components are finite and bounded.</summary>
    public static Point CoercePan(Point pan) => new(CoercePanComponent(pan.X), CoercePanComponent(pan.Y));

    private static double CoercePanComponent(double v)
        => double.IsNaN(v) ? 0.0 : Math.Min(MaxPan, Math.Max(-MaxPan, v));

    public static double ClampZoom(double zoom)
    {
        if (double.IsNaN(zoom))
        {
            return 1.0;
        }

        return Math.Min(MaxZoom, Math.Max(MinZoom, zoom));
    }

    /// <summary>Exponential zoom factor for a mouse wheel delta (120 per notch).</summary>
    public static double WheelFactor(int wheelDelta) => Math.Pow(WheelStep, wheelDelta / 120.0);

    /// <summary>
    /// Multiplies <paramref name="zoom"/> by <paramref name="factor"/> (clamped to the allowed
    /// range) and adjusts <paramref name="pan"/> so the pattern point under
    /// <paramref name="anchor"/> (a device point) stays under it.
    /// </summary>
    public static (double Zoom, Point Pan) ZoomAt(double zoom, Point pan, double baseScale, Point anchor, double factor)
    {
        double newZoom = ClampZoom(zoom * factor);
        if (baseScale <= 0 || newZoom == zoom)
        {
            return (zoom, pan);
        }

        double oldScale = baseScale * zoom;
        double newScale = baseScale * newZoom;

        // w = anchor / oldScale - pan is fixed, so pan' = anchor / newScale - w.
        var newPan = new Point(
            pan.X + anchor.X / newScale - anchor.X / oldScale,
            pan.Y + anchor.Y / newScale - anchor.Y / oldScale);
        return (newZoom, newPan);
    }

    /// <summary>Moves the view by a device-space delta, so the pattern follows the pointer.</summary>
    public static Point PanBy(Point pan, Vector deviceDelta, double scale)
    {
        if (scale <= 0)
        {
            return pan;
        }

        return new Point(pan.X + deviceDelta.X / scale, pan.Y + deviceDelta.Y / scale);
    }
}
