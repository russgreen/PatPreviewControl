using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FillPatternPreview.Imaging;
using FillPatternPreview.Parsing;
using FillPatternPreview.Rendering;
using Preview = FillPatternPreview.Controls.FillPatternPreview;

namespace FillPatternPreview.Tests;

public class PatternResolverTests
{
    private const string TwoPatterns = "*A, first\n0,0,0,0,0.25\n*B, second\n90,0,0,0,0.5";

    [Fact]
    public void NamedPatternIsChosen_CaseInsensitively()
    {
        Assert.True(PatternResolver.TryResolve(TwoPatterns, null, "b", out var pattern, out _));
        Assert.Equal("B", pattern!.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nope")]
    public void MissingOrUnknownName_FallsBackToTheFirstPattern(string? name)
    {
        Assert.True(PatternResolver.TryResolve(TwoPatterns, null, name, out var pattern, out _));
        Assert.Equal("A", pattern!.Name);
    }

    [Fact]
    public void RawTextWinsOverFilePath()
    {
        Assert.True(PatternResolver.TryResolve(TwoPatterns, "does-not-exist.pat", null, out var pattern, out _));
        Assert.Equal("A", pattern!.Name);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("not a pattern at all", null)]
    [InlineData(null, "does-not-exist.pat")]
    public void NoReadablePattern_ReturnsFalseWithoutThrowing(string? text, string? path)
    {
        Assert.False(PatternResolver.TryResolve(text, path, null, out var pattern, out var tile));
        Assert.Null(pattern);
        Assert.Null(tile);
    }

    [Fact]
    public void ResolvedPatternHasATileSize()
    {
        PatternResolver.TryResolve("*T, t\n0,0,0,0,0.25\n90,0,0,0,0.25", null, null, out _, out var tile);
        Assert.NotNull(tile);
    }
}

public class PatternThumbnailTests
{
    private const string Grid = "*GRID, grid\n0,0,0,0,0.25\n90,0,0,0,0.25";
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // Decoded image: Pbgra32 pixels, 4 bytes each, row by row.
    private sealed record Image(int Width, int Height, double Dpi, byte[] Pixels)
    {
        public (byte B, byte G, byte R, byte A) At(int x, int y)
        {
            int i = (y * Width + x) * 4;
            return (Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
        }
    }

    private static T RunSta<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Work did not finish within the time limit.");
        if (error != null)
        {
            throw new Exception("Work threw on the STA thread.", error);
        }

        return result;
    }

    private static Image Decode(byte[] png) => RunSta(() =>
    {
        var decoder = new PngBitmapDecoder(new MemoryStream(png), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return ToImage(decoder.Frames[0]);
    });

    private static Image ToImage(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        return new Image(converted.PixelWidth, converted.PixelHeight, source.DpiX, pixels);
    }

    private static bool IsWhite(Image img, int x, int y) => img.At(x, y) is (255, 255, 255, 255);

    private static IEnumerable<(int X, int Y)> AllPixels(Image img)
    {
        for (int y = 0; y < img.Height; y++)
        {
            for (int x = 0; x < img.Width; x++)
            {
                yield return (x, y);
            }
        }
    }

    private static bool IsReddish(Image img, int x, int y)
    {
        var (b, g, r, a) = img.At(x, y);
        return a == 255 && r > g + 60 && r > b + 60;
    }

    [Fact]
    public void RenderPng_ProducesAPngOfTheRequestedSize()
    {
        var png = PatternThumbnail.RenderPng(Grid, null, new PatternThumbnailOptions { Width = 100, Height = 60 });

        Assert.Equal(PngSignature, png.Take(8).ToArray());
        var img = Decode(png);
        Assert.Equal((100, 60), (img.Width, img.Height));
        Assert.Equal(96, img.Dpi, 0);
    }

    [Fact]
    public void Dpi_ScalesPixelSizeAndIsStoredInThePng()
    {
        var img = Decode(PatternThumbnail.RenderPng(Grid, null, new PatternThumbnailOptions { Width = 100, Height = 60, Dpi = 192 }));

        Assert.Equal((200, 120), (img.Width, img.Height));
        Assert.Equal(192, img.Dpi, 0);
    }

    [Fact]
    public void Thumbnail_IsNeitherBlankNorSolid()
    {
        var img = Decode(PatternThumbnail.RenderPng(Grid));

        Assert.Contains(AllPixels(img), p => IsWhite(img, p.X, p.Y));
        Assert.Contains(AllPixels(img), p => !IsWhite(img, p.X, p.Y));
    }

    [Fact]
    public void TransparentBackground_LeavesEmptyPixelsTransparent()
    {
        var img = Decode(PatternThumbnail.RenderPng(Grid, null, new PatternThumbnailOptions { Background = null }));

        Assert.Contains(AllPixels(img), p => img.At(p.X, p.Y).A == 0);
        Assert.Contains(AllPixels(img), p => img.At(p.X, p.Y).A > 0);
    }

    [Theory]
    [InlineData("*TINY, t\n0,0,0,0,0.01\n90,0,0,0,0.01")]
    [InlineData("*BIG, t\n0,0,0,0,40\n90,0,0,0,40")]
    [InlineData("*DASHED, t\n0,0,0,0,1,0.5,-0.25\n90,0,0,0,1,0.5,-0.25")]
    public void FitTiles_KeepsPatternsOfAnyTileSizeLegible(string pat)
    {
        var img = Decode(PatternThumbnail.RenderPng(pat));

        Assert.Contains(AllPixels(img), p => IsWhite(img, p.X, p.Y));
        Assert.Contains(AllPixels(img), p => !IsWhite(img, p.X, p.Y));
    }

    [Fact]
    public void FitTiles_ShowsTheRequestedNumberOfRepeats()
    {
        // 4 tiles across 128 px: a 32 px pitch, so exactly the grid lines at x = 0, 32, 64, 96.
        var img = Decode(PatternThumbnail.RenderPng("*G, g\n90,0,0,0,1", null, new PatternThumbnailOptions { TilesAcross = 4 }));

        // Lines are 1 px wide and antialiased, so look at the pixel column each one covers.
        bool LineNear(int x) => !IsWhite(img, x - 1, 64) || !IsWhite(img, x, 64);
        Assert.All(new[] { 32, 64, 96 }, x => Assert.True(LineNear(x), $"expected a line near x={x}"));
        Assert.All(new[] { 16, 48, 80 }, x => Assert.False(LineNear(x), $"expected no line near x={x}"));
    }

    [Fact]
    public void ExplicitScale_OverridesFitting()
    {
        var fitted = PatternThumbnail.RenderPng(Grid);
        var explicitScale = PatternThumbnail.RenderPng(Grid, null, new PatternThumbnailOptions { Scale = 0.5 });

        Assert.NotEqual(fitted, explicitScale);
    }

    [Fact]
    public void Thumbnail_MatchesTheControlPixelForPixel()
    {
        const string pat = "*P, d\n0,0,0,0,0.25,0.125,-0.0625\n45,0,0,0.1,0.3\n90,0.05,0.02,0,0.2";
        var options = new PatternThumbnailOptions { Width = 128, Height = 96, Scale = 0.75, Zoom = 1.5, PanOffset = new Point(0.2, -0.1) };

        var thumbnail = Decode(PatternThumbnail.RenderPng(pat, null, options));

        var control = RunSta(() =>
        {
            var c = new Preview { PatRawText = pat, Scale = 0.75, Zoom = 1.5, PanOffset = new Point(0.2, -0.1), Background = Brushes.White };
            c.Measure(new Size(128, 96));
            c.Arrange(new Rect(0, 0, 128, 96));
            c.UpdateLayout();
            var bitmap = new RenderTargetBitmap(128, 96, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(c);
            return ToImage(bitmap);
        });

        Assert.Equal(control.Pixels, thumbnail.Pixels);
    }

    [Fact]
    public void FirstTileBrush_ColoursOnlyTheFirstTile()
    {
        PatternResolver.TryResolve(Grid, null, null, out _, out var tile);
        var options = new PatternThumbnailOptions { Width = 192, Height = 192, Scale = 1, FirstTileBrush = Brushes.Red };

        var img = Decode(PatternThumbnail.RenderPng(Grid, null, options));

        // Scale 1 = 96 px per inch; the tile is anchored at the top-left.
        double tileW = tile!.Value.Width * 96, tileH = tile.Value.Height * 96;
        Assert.Contains(AllPixels(img), p => IsReddish(img, p.X, p.Y));
        Assert.DoesNotContain(AllPixels(img), p => IsReddish(img, p.X, p.Y) && (p.X >= Math.Ceiling(tileW) || p.Y >= Math.Ceiling(tileH)));
        Assert.Contains(AllPixels(img), p => !IsWhite(img, p.X, p.Y) && !IsReddish(img, p.X, p.Y));
    }

    [Fact]
    public void TilePatternFalse_DrawsASingleTileFittedToTheImage()
    {
        var img = Decode(PatternThumbnail.RenderPng(Grid, null, new PatternThumbnailOptions { TilePattern = false }));

        // The tile is scaled to span the image, so it is drawn right up to the far edges.
        Assert.Contains(AllPixels(img), p => p.X > img.Width * 3 / 4 && !IsWhite(img, p.X, p.Y));
        Assert.Contains(AllPixels(img), p => p.Y > img.Height * 3 / 4 && !IsWhite(img, p.X, p.Y));

        // ...and with an explicit small scale only the top-left tile is drawn.
        var small = Decode(PatternThumbnail.RenderPng(Grid, null, new PatternThumbnailOptions { TilePattern = false, Scale = 1 }));
        PatternResolver.TryResolve(Grid, null, null, out _, out var tile);
        int limit = (int)Math.Ceiling(Math.Max(tile!.Value.Width, tile.Value.Height) * 96);
        Assert.DoesNotContain(AllPixels(small), p => (p.X > limit || p.Y > limit) && !IsWhite(small, p.X, p.Y));
    }

    [Fact]
    public void CallerBrushes_AreUsedWithoutBeingFrozen()
    {
        var brush = new SolidColorBrush(Colors.Blue);

        var img = Decode(PatternThumbnail.RenderPng(Grid, null, new PatternThumbnailOptions { LineBrush = brush }));

        Assert.False(brush.IsFrozen);
        Assert.Contains(AllPixels(img), p =>
        {
            var (b, g, r, a) = img.At(p.X, p.Y);
            return a == 255 && b > r + 60 && b > g + 60;
        });
    }

    [Fact]
    public void WorksFromAnMtaThread_AndInParallel()
    {
        Exception? error = null;
        byte[]? png = null;
        var thread = new Thread(() =>
        {
            try { png = PatternThumbnail.RenderPng(Grid); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        Assert.Null(error);
        Assert.Equal(PngSignature, png!.Take(8).ToArray());

        var results = new byte[8][];
        Parallel.For(0, results.Length, i => results[i] = PatternThumbnail.RenderPng(Grid));
        Assert.All(results, r => Assert.Equal(png, r));
    }

    [Fact]
    public void WorksFromAnStaThread()
    {
        var png = RunSta(() => PatternThumbnail.RenderPng(Grid));

        Assert.Equal(PngSignature, png.Take(8).ToArray());
    }

    [Fact]
    public void ExtremePattern_YieldsAThumbnailRatherThanHanging()
    {
        var lines = string.Join("\n", Enumerable.Range(0, 400).Select(i => $"{i * 0.9},0,0,0.001,0.001,0.0001,-0.0001"));

        var png = PatternThumbnail.RenderPng("*HUGE, h\n" + lines, null, new PatternThumbnailOptions { Scale = 1e6 });

        Assert.Equal(PngSignature, png.Take(8).ToArray());
    }

    [Fact]
    public void SavePng_WritesToStreamAndFile()
    {
        PatternResolver.TryResolve(Grid, null, null, out var pattern, out _);

        using var stream = new MemoryStream();
        PatternThumbnail.SavePng(pattern!, stream);
        Assert.True(stream.CanWrite);
        Assert.Equal(PatternThumbnail.RenderPng(pattern!), stream.ToArray());

        var path = Path.Combine(Path.GetTempPath(), $"thumb-{Guid.NewGuid():N}.png");
        try
        {
            PatternThumbnail.SavePng(pattern!, path);
            Assert.Equal(stream.ToArray(), File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RenderPngFromFile_ReadsTheNamedPattern()
    {
        var path = Path.Combine(Path.GetTempPath(), $"thumb-{Guid.NewGuid():N}.pat");
        File.WriteAllText(path, "*A, a\n0,0,0,0,0.25\n*B, b\n90,0,0,0,0.5");
        try
        {
            var fromFile = PatternThumbnail.RenderPngFromFile(path, "B");

            Assert.Equal(PatternThumbnail.RenderPng("*B, b\n90,0,0,0,0.5"), fromFile);
            Assert.NotEqual(fromFile, PatternThumbnail.RenderPngFromFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnreadableSources_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => PatternThumbnail.RenderPng((string)null!));
        Assert.Throws<ArgumentNullException>(() => PatternThumbnail.RenderPng((Model.PatternDefinition)null!));
        Assert.Throws<ArgumentException>(() => PatternThumbnail.RenderPng("   "));
        Assert.Throws<ArgumentException>(() => PatternThumbnail.RenderPng("this is not a pattern"));
        Assert.Throws<ArgumentException>(() => PatternThumbnail.RenderPngFromFile(""));
        Assert.Throws<FileNotFoundException>(() => PatternThumbnail.RenderPngFromFile(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid() + ".pat")));
    }

    [Theory]
    [InlineData(0, 10, 96, 3)]
    [InlineData(10, 0, 96, 3)]
    [InlineData(-5, 10, 96, 3)]
    [InlineData(4097, 10, 96, 3)]
    [InlineData(100000, 10, 96, 3)]
    [InlineData(2049, 10, 192, 3)]
    [InlineData(10, 10, 0, 3)]
    [InlineData(10, 10, double.NaN, 3)]
    [InlineData(10, 10, 5000, 3)]
    [InlineData(10, 10, 96, 0)]
    [InlineData(10, 10, 96, double.PositiveInfinity)]
    public void InvalidOptions_Throw(int width, int height, double dpi, double tilesAcross)
    {
        var options = new PatternThumbnailOptions { Width = width, Height = height, Dpi = dpi, TilesAcross = tilesAcross };

        Assert.Throws<ArgumentOutOfRangeException>(() => PatternThumbnail.RenderPng(Grid, null, options));
    }

    [Fact]
    public void HostileScaleZoomAndPan_AreCoerced()
    {
        var options = new PatternThumbnailOptions
        {
            Scale = double.NaN,
            Zoom = double.NegativeInfinity,
            PanOffset = new Point(double.NaN, double.PositiveInfinity),
        };

        var png = PatternThumbnail.RenderPng(Grid, null, options);

        Assert.Equal(PngSignature, png.Take(8).ToArray());
    }
}
