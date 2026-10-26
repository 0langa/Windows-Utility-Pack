using ImageMagick;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsUtilityPack.Services.ImageTools;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class ImageProcessingServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wup-image-" + Guid.NewGuid().ToString("N"));
    private readonly ImageProcessingService _service = new();

    public ImageProcessingServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task StripMetadata_PreservesAllTiffPagesAndRemovesTags()
    {
        var input = Path.Combine(_root, "pages.tiff");
        using (var frames = new MagickImageCollection())
        {
            frames.Add(new MagickImage(MagickColors.Red, 20, 30));
            frames.Add(new MagickImage(MagickColors.Blue, 40, 50));
            foreach (var frame in frames) frame.SetAttribute("comment", "private fixture description");
            frames.Write(input, MagickFormat.Tiff);
        }
        await _service.StripMetadataAsync(input, input);
        using var stream = File.OpenRead(input);
        var decoded = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.Equal(2, decoded.Frames.Count);
        Assert.Equal(20, decoded.Frames[0].PixelWidth);
        Assert.Equal(40, decoded.Frames[1].PixelWidth);
        using var result = new MagickImageCollection(input);
        Assert.All(result, frame => Assert.Null(frame.GetAttribute("comment")));
    }

    [Fact]
    public async Task StripMetadata_RejectsMisleadingExtensionAndPreservesDestination()
    {
        var input = CreatePng();
        var output = Path.Combine(_root, "existing.jpg");
        await File.WriteAllTextAsync(output, "keep destination");
        await Assert.ThrowsAsync<InvalidDataException>(() => _service.StripMetadataAsync(input, output));
        Assert.Equal("keep destination", await File.ReadAllTextAsync(output));
    }

    [Fact]
    public async Task StripAudio_InvalidInputPreservesDestinationAndCleansTemporaryFile()
    {
        var input = Path.Combine(_root, "invalid.mp3");
        var output = Path.Combine(_root, "existing.mp3");
        await File.WriteAllTextAsync(input, "not audio");
        await File.WriteAllTextAsync(output, "keep destination");
        await Assert.ThrowsAnyAsync<Exception>(() => WindowsUtilityPack.Services.FileTools.AudioMetadataService.StripAsync(input, output));
        Assert.Equal("keep destination", await File.ReadAllTextAsync(output));
        Assert.Empty(Directory.GetFiles(_root, ".wup-tags-*"));
    }

    [Theory]
    [InlineData(ImageOutputFormat.Jpeg, MagickFormat.Jpeg)]
    [InlineData(ImageOutputFormat.Png, MagickFormat.Png)]
    [InlineData(ImageOutputFormat.Webp, MagickFormat.WebP)]
    [InlineData(ImageOutputFormat.Bmp, MagickFormat.Bmp)]
    [InlineData(ImageOutputFormat.Tiff, MagickFormat.Tiff)]
    public async Task ConvertAsync_WritesAdvertisedFormatAndReadablePixels(ImageOutputFormat format, MagickFormat expected)
    {
        var input = CreatePng();
        var result = Assert.Single(await Convert(input, format));
        Assert.True(result.Success, result.ErrorMessage);
        using var image = new MagickImage(result.OutputPath);
        Assert.Equal(expected, image.Format);
        Assert.Equal(120u, image.Width);
        Assert.Equal(80u, image.Height);
        var pixel = Pixel(image, 60, 40);
        Assert.True(pixel.R > 240 && pixel.G < 15 && pixel.B < 15);
        Assert.Equal(new FileInfo(input).Length, result.InputBytes);
        Assert.Equal(new FileInfo(result.OutputPath).Length, result.OutputBytes);
        if (format != ImageOutputFormat.Webp)
        {
            // Independently decode with Windows WIC, including packaged BMP/TIFF compatibility.
            using var stream = File.OpenRead(result.OutputPath);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            Assert.Equal(120, decoder.Frames[0].PixelWidth);
            Assert.Equal(80, decoder.Frames[0].PixelHeight);
        }
    }

    [Theory]
    [InlineData(MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.WebP)]
    [InlineData(MagickFormat.Bmp)]
    [InlineData(MagickFormat.Tiff)]
    [InlineData(MagickFormat.Gif)]
    public async Task ConvertAsync_ReadsSupportedRasterDataRegardlessOfExtension(MagickFormat format)
    {
        var input = Path.Combine(_root, "source.unusual-extension");
        using (var image = new MagickImage(MagickColors.Red, 32, 24))
            image.Write(input, format);
        var result = Assert.Single(await Convert(input));
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(32, result.Width);
        Assert.Equal(24, result.Height);
    }

    [Fact]
    public async Task ConvertAsync_ReadsHeicWithoutInstalledWindowsCodec()
    {
        var input = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Images", "orientation-1.heic");
        var result = Assert.Single(await Convert(input));
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1200, result.Width);
        Assert.Equal(800, result.Height);
        using var stream = File.OpenRead(result.OutputPath);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        Assert.Equal(1200, decoder.Frames[0].PixelWidth);
        Assert.Equal(800, decoder.Frames[0].PixelHeight);
    }

    [Theory]
    [InlineData(true, 60, 40)]
    [InlineData(false, 60, 60)]
    public async Task ResizeAsync_UsesRequestedAspectRatioMode(bool keepAspectRatio, int width, int height)
    {
        var result = Assert.Single(await _service.ResizeAsync(new ImageResizeRequest
        {
            InputPaths = [CreatePng()], OutputDirectory = Path.Combine(_root, "out"), Width = 60, Height = 60,
            KeepAspectRatio = keepAspectRatio, OutputFormat = ImageOutputFormat.Png,
        }, CancellationToken.None));
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(width, result.Width);
        Assert.Equal(height, result.Height);
        using var image = new MagickImage(result.OutputPath);
        Assert.Equal((uint)width, image.Width);
        Assert.Equal((uint)height, image.Height);
    }

    [Fact]
    public async Task ResizeAsync_RejectsExcessiveDimensionsBeforeWriting()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.ResizeAsync(new ImageResizeRequest
        {
            InputPaths = [CreatePng()], OutputDirectory = Path.Combine(_root, "out"), Width = 10000, Height = 10000,
        }, CancellationToken.None));
        Assert.False(Directory.Exists(Path.Combine(_root, "out")));
    }

    [Fact]
    public async Task ResizeAsync_RejectsAggregateFrameExpansionBeforeAllocation()
    {
        var input = Path.Combine(_root, "small.tiff");
        using (var frames = new MagickImageCollection())
        {
            frames.Add(new MagickImage(MagickColors.Red, 1, 1));
            frames.Add(new MagickImage(MagickColors.Blue, 1, 1));
            frames.Write(input);
        }
        var result = Assert.Single(await _service.ResizeAsync(new ImageResizeRequest
        {
            InputPaths = [input], OutputDirectory = Path.Combine(_root, "out"), Width = 8000, Height = 8000,
            OutputFormat = ImageOutputFormat.Tiff,
        }, CancellationToken.None));
        Assert.False(result.Success);
        Assert.Contains("Resized frames exceed", result.ErrorMessage);
        Assert.False(File.Exists(result.OutputPath));
    }

    [Fact]
    public async Task ConvertAsync_SamePathOverwriteAndNonOverwriteKeepOriginalUsable()
    {
        var input = CreatePng();
        var original = await File.ReadAllBytesAsync(input);
        var separate = Assert.Single(await Convert(input, directory: _root));
        Assert.True(separate.Success, separate.ErrorMessage);
        Assert.EndsWith("input_1.png", separate.OutputPath);
        Assert.Equal(original, await File.ReadAllBytesAsync(input));
        var replacement = Assert.Single(await Convert(input, overwrite: true, directory: _root));
        Assert.True(replacement.Success, replacement.ErrorMessage);
        Assert.Equal(input, replacement.OutputPath);
        using var image = new MagickImage(input);
        Assert.Equal(120u, image.Width);
        Assert.Empty(Directory.GetFiles(_root, ".wup-image-*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ConvertAsync_CorruptInputPreservesDestinationAndContinuesBatch()
    {
        var bad = Path.Combine(_root, "corrupt.png");
        await File.WriteAllBytesAsync(bad, [137, 80, 78, 71, 13, 10, 26, 10, 0, 0]);
        var directory = Path.Combine(_root, "out");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "corrupt.png");
        await File.WriteAllTextAsync(destination, "existing content");
        var results = await _service.ConvertAsync(new ImageConvertRequest
        {
            InputPaths = [bad, CreatePng()], OutputDirectory = directory, Overwrite = true,
        }, CancellationToken.None);
        Assert.False(results[0].Success);
        Assert.True(results[1].Success, results[1].ErrorMessage);
        Assert.Equal("existing content", await File.ReadAllTextAsync(destination));
        Assert.Empty(Directory.GetFiles(_root, ".wup-image-*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ConvertAsync_FailedCommitCleansTemporaryFile()
    {
        var directory = Path.Combine(_root, "out");
        Directory.CreateDirectory(Path.Combine(directory, "input.png"));
        var result = Assert.Single(await Convert(CreatePng(), overwrite: true, directory: directory));
        Assert.False(result.Success);
        Assert.True(Directory.Exists(Path.Combine(directory, "input.png")));
        Assert.Empty(Directory.GetFiles(_root, ".wup-image-*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ConvertAsync_CancellationPropagatesWithoutWriting()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.ConvertAsync(new ImageConvertRequest
        {
            InputPaths = [CreatePng()], OutputDirectory = Path.Combine(_root, "out"),
        }, source.Token));
        Assert.False(Directory.Exists(Path.Combine(_root, "out")));
    }

    [Theory]
    [InlineData(ImageOutputFormat.Png)]
    [InlineData(ImageOutputFormat.Webp)]
    [InlineData(ImageOutputFormat.Tiff)]
    public async Task ConvertAsync_PreservesTransparency(ImageOutputFormat format)
    {
        var result = Assert.Single(await Convert(CreatePng(Colors.Transparent), format));
        Assert.True(result.Success, result.ErrorMessage);
        using var image = new MagickImage(result.OutputPath);
        Assert.Equal((byte)0, Pixel(image, 10, 10).A);
    }

    [Fact]
    public async Task ConvertAsync_JpegFlattensTransparentPixelsOntoWhite()
    {
        var result = Assert.Single(await Convert(CreatePng(Colors.Transparent), ImageOutputFormat.Jpeg));
        Assert.True(result.Success, result.ErrorMessage);
        using var image = new MagickImage(result.OutputPath);
        var pixel = Pixel(image, 10, 10);
        Assert.True(pixel.R > 250 && pixel.G > 250 && pixel.B > 250);
    }

    [Theory]
    [InlineData(ImageOutputFormat.Tiff)]
    [InlineData(ImageOutputFormat.Webp)]
    public async Task ConvertAsync_PreservesAnimationFrames(ImageOutputFormat format)
    {
        var input = Path.Combine(_root, "animated.gif");
        using (var frames = new MagickImageCollection())
        {
            frames.Add(new MagickImage(MagickColors.Red, 32, 24) { AnimationDelay = 15 });
            frames.Add(new MagickImage(MagickColors.Blue, 32, 24) { AnimationDelay = 25 });
            frames.Write(input);
        }
        var result = Assert.Single(await Convert(input, format));
        Assert.True(result.Success, result.ErrorMessage);
        using var output = new MagickImageCollection(result.OutputPath);
        Assert.Equal(2, output.Count);
        Assert.True(Pixel(output[0], 10, 10).R > 240);
        Assert.True(Pixel(output[1], 10, 10).B > 240);
        if (format == ImageOutputFormat.Webp)
        {
            Assert.Equal(0.15, (double)output[0].AnimationDelay / output[0].AnimationTicksPerSecond, 2);
            Assert.Equal(0.25, (double)output[1].AnimationDelay / output[1].AnimationTicksPerSecond, 2);
        }
    }

    [Theory]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'/>")]
    [InlineData("%PDF-1.7\n")]
    public async Task ConvertAsync_RejectsDocumentsInsteadOfInvokingExternalDecoders(string data)
    {
        var input = Path.Combine(_root, "disguised.png");
        await File.WriteAllTextAsync(input, data);
        var result = Assert.Single(await Convert(input));
        Assert.False(result.Success);
        Assert.Contains("Unsupported image", result.ErrorMessage);
        Assert.False(File.Exists(result.OutputPath));
    }

    [Theory]
    [InlineData(AnnotationType.Rectangle, 10, 20)]
    [InlineData(AnnotationType.Arrow, 40, 40)]
    public async Task AnnotateAsync_DrawsOutlineAndArrow(AnnotationType type, int pixelX, int pixelY)
    {
        var result = await Annotate(CreatePng(Colors.White), new ImageAnnotation
        {
            Type = type, X = 10, Y = 10, Width = 60, Height = 60, X2 = 70, Y2 = 70,
            ColorHex = "#FF0000", StrokeThickness = 3,
        });
        Assert.True(result.Success, result.ErrorMessage);
        using var image = new MagickImage(result.OutputPath);
        var pixel = Pixel(image, pixelX, pixelY);
        Assert.True(pixel.R > 240 && pixel.G < 30 && pixel.B < 30);
        Assert.Equal(MagickColors.White, Pixel(image, 100, 60));
        if (type == AnnotationType.Rectangle) Assert.Equal(MagickColors.White, Pixel(image, 40, 40));
    }

    [Fact]
    public async Task AnnotateAsync_RendersTextInRequestedRegion()
    {
        var result = await Annotate(CreatePng(Colors.White), new ImageAnnotation
        {
            Type = AnnotationType.Text, X = 10, Y = 10, Width = 80, Height = 40, Text = "Hello", ColorHex = "#0000FF",
        });
        Assert.True(result.Success, result.ErrorMessage);
        using var image = new MagickImage(result.OutputPath);
        var ink = 0;
        for (var y = 10; y < 50; y++)
            for (var x = 10; x < 90; x++)
            {
                var pixel = Pixel(image, x, y);
                if (pixel.B > 200 && pixel.R < 100) ink++;
            }
        Assert.True(ink > 50, $"Expected visible text, found {ink} blue pixels.");
        Assert.Equal(MagickColors.White, Pixel(image, 110, 70));
    }

    [Fact]
    public async Task AnnotateAsync_RedactsAllFramesAndRemovesEmbeddedMetadata()
    {
        var input = Path.Combine(_root, "private.tiff");
        using (var frames = new MagickImageCollection())
        {
            for (var i = 0; i < 2; i++)
            {
                var image = new MagickImage(i == 0 ? MagickColors.White : MagickColors.Red, 120, 80);
                var profile = new ExifProfile();
                profile.SetValue(ExifTag.ImageDescription, "private description and unredacted thumbnail metadata");
                image.SetProfile(profile);
                frames.Add(image);
            }
            frames.Write(input);
        }
        var result = await _service.AnnotateAsync(input, input, [new ImageAnnotation
        {
            Type = AnnotationType.Redaction, X = 10, Y = 10, Width = 40, Height = 30,
        }], ImageOutputFormat.Tiff, 90, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var output = new MagickImageCollection(input);
        Assert.Equal(2, output.Count);
        foreach (var frame in output)
        {
            Assert.Equal(MagickColors.Black, Pixel(frame, 20, 20));
            Assert.Equal((byte)255, Pixel(frame, 20, 20).A);
            Assert.Null(frame.GetExifProfile());
        }
    }

    [Fact]
    public async Task AnnotateAsync_BlursOnlySelectedRectangle()
    {
        var input = CreatePng(Colors.Black, checkerboard: true);
        var result = await Annotate(input, new ImageAnnotation
        {
            Type = AnnotationType.Blur, X = 10, Y = 10, Width = 60, Height = 50,
        });
        Assert.True(result.Success, result.ErrorMessage);
        using var image = new MagickImage(result.OutputPath);
        Assert.InRange(Pixel(image, 40, 40).R, (byte)80, (byte)175);
        Assert.Equal(MagickColors.Black, Pixel(image, 90, 60));
    }

    [Theory]
    [InlineData("#80FF0000", 128)]
    [InlineData("bad color", 0)]
    public async Task AnnotateAsync_UsesWpfArgbColorsAndSafeFallback(string color, int expectedGreen)
    {
        var result = await Annotate(CreatePng(Colors.White), new ImageAnnotation
        {
            Type = AnnotationType.Rectangle, X = 10, Y = 10, Width = 60, Height = 40, ColorHex = color, StrokeThickness = 3,
        });
        Assert.True(result.Success, result.ErrorMessage);
        using var image = new MagickImage(result.OutputPath);
        var pixel = Pixel(image, 10, 25);
        Assert.True(pixel.R > 250);
        Assert.InRange((int)pixel.G, Math.Max(0, expectedGreen - 3), expectedGreen + 3);
        Assert.InRange((int)pixel.B, Math.Max(0, expectedGreen - 3), expectedGreen + 3);
    }

    private Task<IReadOnlyList<ImageProcessResult>> Convert(string input, ImageOutputFormat format = ImageOutputFormat.Png,
        bool overwrite = false, string? directory = null) => _service.ConvertAsync(new ImageConvertRequest
    {
        InputPaths = [input], OutputDirectory = directory ?? Path.Combine(_root, "out"), OutputFormat = format,
        Quality = 90, Overwrite = overwrite,
    }, CancellationToken.None);

    private Task<ImageProcessResult> Annotate(string input, ImageAnnotation annotation) => _service.AnnotateAsync(input,
        Path.Combine(_root, "annotated.png"), [annotation], ImageOutputFormat.Png, 90, CancellationToken.None);

    private string CreatePng(Color? color = null, bool checkerboard = false)
    {
        const int width = 120, height = 80;
        var pixels = new byte[width * height * 4];
        var fill = color ?? Colors.Red;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var current = checkerboard ? ((x + y) % 2 == 0 ? Colors.Black : Colors.White) : fill;
                var offset = (y * width + x) * 4;
                pixels[offset] = current.B;
                pixels[offset + 1] = current.G;
                pixels[offset + 2] = current.R;
                pixels[offset + 3] = current.A;
            }
        // The input fixture is encoded independently of the replacement library.
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.Combine(_root, "input.png");
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private static IMagickColor<byte> Pixel(IMagickImage<byte> image, int x, int y)
    {
        using var pixels = image.GetPixels();
        return pixels.GetPixel(x, y).ToColor()!;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
