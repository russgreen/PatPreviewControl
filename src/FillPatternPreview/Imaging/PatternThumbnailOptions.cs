using System.Windows;
using System.Windows.Media;

namespace FillPatternPreview.Imaging;

/// <summary>
/// Settings for <see cref="PatternThumbnail"/>. Every property has a default, so
/// <c>new PatternThumbnailOptions()</c> gives a 128x128 black-on-white thumbnail showing about
/// three repeats of the pattern. Use <c>with</c> to change individual settings.
/// </summary>
/// <remarks>
/// Brushes are read on the calling thread and copied, but a brush owned by another thread cannot
/// be read, so prefer frozen brushes (the <see cref="Brushes"/> constants are frozen).
/// </remarks>
public sealed record PatternThumbnailOptions
{
    /// <summary>Width of the image in device-independent pixels (1/96 inch). The pixel width is this scaled by <see cref="Dpi"/>.</summary>
    public int Width { get; init; } = 128;

    /// <summary>Height of the image in device-independent pixels (1/96 inch). The pixel height is this scaled by <see cref="Dpi"/>.</summary>
    public int Height { get; init; } = 128;

    /// <summary>
    /// Resolution of the image; 96 gives one pixel per device-independent pixel. Use 192 for a
    /// high-DPI thumbnail of the same logical size. The value is stored in the PNG.
    /// </summary>
    public double Dpi { get; init; } = 96;

    /// <summary>Background fill. Null leaves the image transparent. Defaults to white.</summary>
    public Brush? Background { get; init; } = Brushes.White;

    /// <summary>Colour of the pattern's lines. Defaults to black.</summary>
    public Brush LineBrush { get; init; } = Brushes.Black;

    /// <summary>
    /// When set, the pattern's first repeat cell (anchored at the pattern origin, the image's
    /// top-left corner) is drawn in this colour instead of <see cref="LineBrush"/>. Has no effect
    /// for patterns whose repeat cell cannot be determined.
    /// </summary>
    public Brush? FirstTileBrush { get; init; }

    /// <summary>
    /// When true (the default) the pattern is repeated across the whole image. When false only a
    /// single tile (the pattern's first repeat cell, at the top-left) is drawn, sized to fit the
    /// image. Has no effect for patterns whose repeat cell cannot be determined.
    /// </summary>
    public bool TilePattern { get; init; } = true;

    /// <summary>
    /// How many repeats of the pattern's tile should span the image (its longer tile side spans
    /// <c>Min(Width, Height) / TilesAcross</c>), so thumbnails of patterns with very different
    /// tile sizes all look legible. Used only when <see cref="Scale"/> is null, the pattern has a
    /// repeat cell and <see cref="TilePattern"/> is true. Defaults to 3.
    /// </summary>
    public double TilesAcross { get; init; } = 3;

    /// <summary>
    /// An explicit scale, with the same meaning as the preview control's <c>Scale</c> (1 draws a
    /// pattern unit at 96 device-independent pixels for inch-based patterns). Null (the default)
    /// chooses a scale from <see cref="TilesAcross"/>.
    /// </summary>
    public double? Scale { get; init; }

    /// <summary>Multiplier applied on top of the scale, as the control's <c>Zoom</c>. Defaults to 1.</summary>
    public double Zoom { get; init; } = 1;

    /// <summary>Offset of the pattern in its own native units, as the control's <c>PanOffset</c>. Defaults to (0, 0).</summary>
    public Point PanOffset { get; init; }
}
