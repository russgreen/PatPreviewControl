using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FillPatternPreview.Model;
using FillPatternPreview.Rendering;

namespace FillPatternPreview.Imaging;

/// <summary>
/// Renders a pattern to an image without a preview control, a window or a running application.
/// It uses the same drawing code as the control, so a thumbnail looks like the control would with
/// the same settings.
/// </summary>
/// <remarks>
/// <para>
/// WPF rasterizes on an STA thread. If the calling thread is not STA (a thread-pool or MTA
/// thread, say) the work is transparently run on a short-lived STA thread and this method blocks
/// until it finishes, so the methods can be called from anywhere, including in parallel.
/// </para>
/// <para>
/// Drawing is bounded by the same work budget as the control: an extreme pattern yields a
/// partially drawn thumbnail rather than hanging. A pattern that cannot be read, or invalid
/// options, throw.
/// </para>
/// </remarks>
public static class PatternThumbnail
{
    /// <summary>The largest image, in pixels, along either side.</summary>
    public const int MaxPixelSize = 4096;

    /// <summary>The largest <see cref="PatternThumbnailOptions.Dpi"/>.</summary>
    public const double MaxDpi = 1200;

    /// <summary>Renders <paramref name="pattern"/> to a frozen bitmap (Pbgra32).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The options are out of range (see <see cref="MaxPixelSize"/>, <see cref="MaxDpi"/>).</exception>
    public static BitmapSource Render(PatternDefinition pattern, PatternThumbnailOptions? options = null)
    {
        var job = Prepare(pattern, options);
        return RunOnSta(() => RenderCore(job));
    }

    /// <summary>Renders <paramref name="pattern"/> to PNG bytes.</summary>
    /// <inheritdoc cref="Render(PatternDefinition, PatternThumbnailOptions?)" path="/exception"/>
    public static byte[] RenderPng(PatternDefinition pattern, PatternThumbnailOptions? options = null)
    {
        var job = Prepare(pattern, options);
        return RunOnSta(() => Encode(RenderCore(job)));
    }

    /// <summary>
    /// Renders a pattern from .pat text to PNG bytes. If <paramref name="patternName"/> is null or
    /// not found, the first pattern in the text is used, as in the control.
    /// </summary>
    /// <exception cref="ArgumentException">The text contains no readable pattern.</exception>
    /// <inheritdoc cref="Render(PatternDefinition, PatternThumbnailOptions?)" path="/exception"/>
    public static byte[] RenderPng(string patText, string? patternName = null, PatternThumbnailOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(patText);
        if (!PatternResolver.TryResolve(patText, null, patternName, out var pattern, out _) || pattern == null)
        {
            throw new ArgumentException("The text contains no readable pattern.", nameof(patText));
        }

        return RenderPng(pattern, options);
    }

    /// <summary>
    /// Renders a pattern from a .pat file to PNG bytes. If <paramref name="patternName"/> is null or
    /// not found, the first pattern in the file is used, as in the control.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="ArgumentException">The file contains no readable pattern.</exception>
    /// <inheritdoc cref="Render(PatternDefinition, PatternThumbnailOptions?)" path="/exception"/>
    public static byte[] RenderPngFromFile(string patFilePath, string? patternName = null, PatternThumbnailOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patFilePath);
        if (!File.Exists(patFilePath))
        {
            throw new FileNotFoundException("The pattern file does not exist.", patFilePath);
        }

        if (!PatternResolver.TryResolve(null, patFilePath, patternName, out var pattern, out _) || pattern == null)
        {
            throw new ArgumentException("The file contains no readable pattern.", nameof(patFilePath));
        }

        return RenderPng(pattern, options);
    }

    /// <summary>Renders <paramref name="pattern"/> as a PNG written to <paramref name="destination"/>. The stream is left open.</summary>
    /// <inheritdoc cref="Render(PatternDefinition, PatternThumbnailOptions?)" path="/exception"/>
    public static void SavePng(PatternDefinition pattern, Stream destination, PatternThumbnailOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var png = RenderPng(pattern, options);
        destination.Write(png, 0, png.Length);
    }

    /// <summary>Renders <paramref name="pattern"/> as a PNG file at <paramref name="path"/>, replacing any existing file.</summary>
    /// <inheritdoc cref="Render(PatternDefinition, PatternThumbnailOptions?)" path="/exception"/>
    public static void SavePng(PatternDefinition pattern, string path, PatternThumbnailOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Render first so a failure doesn't leave an empty file behind.
        File.WriteAllBytes(path, RenderPng(pattern, options));
    }

    // Everything the STA-thread work needs, validated and with brushes snapshotted on the calling
    // thread (a brush can only be read by the thread that owns it).
    private sealed record Job(
        PatternDefinition Pattern,
        Size? TileSize,
        Size Size,
        int PixelWidth,
        int PixelHeight,
        double Dpi,
        Brush? Background,
        PatternRenderOptions Options);

    private static Job Prepare(PatternDefinition pattern, PatternThumbnailOptions? options)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        options ??= new PatternThumbnailOptions();

        if (options.Width < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Width, "Width must be at least 1.");
        }

        if (options.Height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Height, "Height must be at least 1.");
        }

        if (!double.IsFinite(options.Dpi) || options.Dpi <= 0 || options.Dpi > MaxDpi)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Dpi, $"Dpi must be greater than 0 and at most {MaxDpi}.");
        }

        if (!double.IsFinite(options.TilesAcross) || options.TilesAcross <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.TilesAcross, "TilesAcross must be greater than 0.");
        }

        int pixelWidth = ToPixels(options.Width, options.Dpi);
        int pixelHeight = ToPixels(options.Height, options.Dpi);
        if (pixelWidth > MaxPixelSize || pixelHeight > MaxPixelSize)
        {
            throw new ArgumentOutOfRangeException(nameof(options), $"The image would be {pixelWidth}x{pixelHeight} pixels; the limit is {MaxPixelSize} on each side.");
        }

        var tileSize = PatternResolver.TileSizeOf(pattern);
        var renderOptions = new PatternRenderOptions(
            Snapshot(options.LineBrush) ?? Brushes.Black,
            Snapshot(options.FirstTileBrush),
            options.TilePattern,
            ChooseScale(pattern, tileSize, options),
            InteractionMath.CoerceScale(options.Zoom),
            InteractionMath.CoercePan(options.PanOffset));

        return new Job(pattern, tileSize, new Size(options.Width, options.Height), pixelWidth, pixelHeight, options.Dpi, Snapshot(options.Background), renderOptions);
    }

    private static int ToPixels(int deviceIndependent, double dpi)
        => (int)Math.Max(1, Math.Round(deviceIndependent * dpi / 96.0, MidpointRounding.AwayFromZero));

    /// <summary>
    /// An explicit scale wins. Otherwise the pattern's tile is sized so the requested number of
    /// repeats spans the image; patterns with no repeat cell get the control's default scale.
    /// </summary>
    private static double ChooseScale(PatternDefinition pattern, Size? tileSize, PatternThumbnailOptions options)
    {
        if (options.Scale is { } explicitScale)
        {
            return InteractionMath.CoerceScale(explicitScale);
        }

        if (tileSize is { } tile)
        {
            double longestSide = Math.Max(tile.Width, tile.Height);
            double tiles = options.TilePattern ? options.TilesAcross : 1.0;
            double scale = Math.Min(options.Width, options.Height) / (tiles * longestSide * LineFamilyExpander.GetUnitsToDipFactor(pattern));
            if (double.IsFinite(scale) && scale > 0)
            {
                return InteractionMath.CoerceScale(scale);
            }
        }

        return 1.0;
    }

    // A frozen copy that any thread can use; the caller's own brush is left untouched.
    private static Brush? Snapshot(Brush? brush)
    {
        if (brush == null || brush.IsFrozen)
        {
            return brush;
        }

        var copy = brush.CloneCurrentValue();
        if (copy.CanFreeze)
        {
            copy.Freeze();
        }

        return copy;
    }

    private static BitmapSource RenderCore(Job job)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (job.Background != null)
            {
                dc.DrawRectangle(job.Background, null, new Rect(job.Size));
            }

            var budget = new RenderBudget();
            new PatternRenderer(job.Pattern, job.TileSize, null, job.Options, budget).Render(dc, job.Size);
            if (budget.IsExhausted)
            {
                System.Diagnostics.Debug.WriteLine("PatternThumbnail: render budget exhausted; pattern drawn incompletely.");
            }
        }

        var bitmap = new RenderTargetBitmap(job.PixelWidth, job.PixelHeight, job.Dpi, job.Dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] Encode(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static T RunOnSta<T>(Func<T> work)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return work();
        }

        T result = default!;
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = work();
            }
            catch (Exception ex)
            {
                error = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            IsBackground = true,
            Name = "PatternThumbnail",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        error?.Throw();
        return result;
    }
}
