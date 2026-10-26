using System.Drawing.Imaging;
using System.IO;
using ImageMagick;
using ImageMagick.Drawing;
using SD = System.Drawing;
using WpfSystemParameters = System.Windows.SystemParameters;

namespace WindowsUtilityPack.Services.ImageTools;

/// <summary>Raster image processing for conversion, resizing, and annotation.</summary>
public sealed class ImageProcessingService : IImageProcessingService
{
    private const uint MaxDimension = 32768;
    private const ulong MaxTotalPixels = 64_000_000;
    private const uint MaxFrames = 256;

    // Native limits also apply while reading headers, before we can inspect dimensions.
    private static readonly Lazy<bool> CodecLimits = new(() =>
    {
        ResourceLimits.Width = MaxDimension;
        ResourceLimits.Height = MaxDimension;
        ResourceLimits.ListLength = MaxFrames;
        ResourceLimits.Memory = 256UL * 1024 * 1024;
        ResourceLimits.Disk = 512UL * 1024 * 1024;
        ResourceLimits.Thread = 2;
        return true;
    });

    public Task<IReadOnlyList<ImageProcessResult>> ResizeAsync(ImageResizeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateBatchRequest(request.InputPaths, request.OutputDirectory);
        if (request.Width <= 0 || request.Height <= 0 || request.Width > MaxDimension || request.Height > MaxDimension
            || (ulong)request.Width * (ulong)request.Height > MaxTotalPixels)
            throw new ArgumentOutOfRangeException(nameof(request), "Dimensions must be positive, at most 32768 per side and 64 million pixels.");

        var inputs = request.InputPaths.ToArray();
        return Task.Run(() => ProcessBatchAsync(inputs, request.OutputDirectory, request.OutputFormat, request.Quality,
            request.Overwrite, images =>
            {
                ulong projectedPixels = 0;
                foreach (var image in images)
                {
                    var scale = Math.Min((double)request.Width / image.Width, (double)request.Height / image.Height);
                    var width = request.KeepAspectRatio ? Math.Max(1, Math.Ceiling(image.Width * scale)) : request.Width;
                    var height = request.KeepAspectRatio ? Math.Max(1, Math.Ceiling(image.Height * scale)) : request.Height;
                    projectedPixels += (ulong)width * (ulong)height;
                    if (projectedPixels > MaxTotalPixels)
                        throw new InvalidDataException("Resized frames exceed the 64 million pixel processing limit.");
                }
                foreach (var image in images)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    image.Resize(new MagickGeometry((uint)request.Width, (uint)request.Height)
                    {
                        IgnoreAspectRatio = !request.KeepAspectRatio,
                    }, FilterType.Lanczos);
                    image.ResetPage();
                }
            }, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<ImageProcessResult>> ConvertAsync(ImageConvertRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateBatchRequest(request.InputPaths, request.OutputDirectory);
        var inputs = request.InputPaths.ToArray();
        return Task.Run(() => ProcessBatchAsync(inputs, request.OutputDirectory, request.OutputFormat, request.Quality,
            request.Overwrite, null, cancellationToken), cancellationToken);
    }

    public Task<ScreenshotCaptureResult> CaptureScreenshotAsync(string outputPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            try
            {
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(outputDir))
                    Directory.CreateDirectory(outputDir);

                var left = (int)WpfSystemParameters.VirtualScreenLeft;
                var top = (int)WpfSystemParameters.VirtualScreenTop;
                var width = (int)WpfSystemParameters.VirtualScreenWidth;
                var height = (int)WpfSystemParameters.VirtualScreenHeight;

                using var bitmap = new SD.Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var graphics = SD.Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(left, top, 0, 0, new SD.Size(width, height), SD.CopyPixelOperation.SourceCopy);
                }

                bitmap.Save(outputPath, ImageFormat.Png);
                return new ScreenshotCaptureResult
                {
                    Success = true,
                    ImagePath = outputPath,
                    Width = width,
                    Height = height,
                };
            }
            catch (Exception ex)
            {
                return new ScreenshotCaptureResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ImagePath = outputPath,
                };
            }
        }, cancellationToken);
    }


    public Task<ImageProcessResult> AnnotateAsync(string inputPath, string outputPath,
        IReadOnlyList<ImageAnnotation> annotations, ImageOutputFormat outputFormat, int quality, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(annotations);
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input image not found.", inputPath);
        var overlays = annotations.ToArray();
        return Task.Run(() => ProcessOneAsync(inputPath, outputPath, outputFormat, quality, true, images =>
        {
            foreach (var image in images)
            {
                foreach (var annotation in overlays)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ApplyAnnotation(image, annotation);
                }
                // Embedded previews and profiles must not retain an unredacted copy.
                image.Strip();
            }
        }, cancellationToken), cancellationToken);
    }

    private static void ValidateBatchRequest(IReadOnlyList<string> inputPaths, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(inputPaths);
        if (inputPaths.Count == 0)
            throw new ArgumentException("At least one input file is required.", nameof(inputPaths));
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
    }

    private static async Task<IReadOnlyList<ImageProcessResult>> ProcessBatchAsync(IReadOnlyList<string> inputs,
        string directory, ImageOutputFormat format, int quality, bool overwrite,
        Action<MagickImageCollection>? transform, CancellationToken cancellationToken)
    {
        var results = new List<ImageProcessResult>(inputs.Count);
        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ProcessOneAsync(input, BuildOutputPath(input, directory, format), format,
                quality, overwrite, transform, cancellationToken).ConfigureAwait(false));
        }
        return results;
    }

    private static async Task<ImageProcessResult> ProcessOneAsync(string inputPath, string outputPath,
        ImageOutputFormat format, int quality, bool overwrite, Action<MagickImageCollection>? transform,
        CancellationToken cancellationToken)
    {
        long inputBytes = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = CodecLimits.Value;
            inputBytes = new FileInfo(inputPath).Length;
            outputPath = Path.GetFullPath(outputPath);
            if (!overwrite)
                outputPath = BuildNonConflictingPath(outputPath);

            using var images = await ReadRasterAsync(inputPath, cancellationToken).ConfigureAwait(false);
            foreach (var image in images)
                image.Progress += (_, progress) => progress.Cancel = cancellationToken.IsCancellationRequested;
            cancellationToken.ThrowIfCancellationRequested();
            transform?.Invoke(images);
            foreach (var image in images)
            {
                cancellationToken.ThrowIfCancellationRequested();
                image.Quality = (uint)Math.Clamp(quality, 1, 100);
                if (format == ImageOutputFormat.Jpeg)
                {
                    image.BackgroundColor = MagickColors.White;
                    image.Alpha(AlphaAction.Remove);
                }
            }
            ValidateFrames(images);
            await WriteAtomicallyAsync(images, outputPath, GetFormat(format), overwrite, cancellationToken).ConfigureAwait(false);
            return new ImageProcessResult
            {
                InputPath = inputPath, OutputPath = outputPath, Success = true, InputBytes = inputBytes,
                OutputBytes = new FileInfo(outputPath).Length, Width = (int)images[0].Width, Height = (int)images[0].Height,
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception ex)
        {
            return new ImageProcessResult
            {
                InputPath = inputPath, OutputPath = outputPath, InputBytes = inputBytes, ErrorMessage = ex.Message,
            };
        }
    }

    internal static async Task<MagickImageCollection> ReadRasterAsync(string path, CancellationToken cancellationToken)
    {
        _ = CodecLimits.Value;
        // Pass bytes with an explicit raster decoder, never a native pseudo-filename or document delegate.
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var format = DetectRasterFormat(stream);
        var settings = new MagickReadSettings { Format = format };
        using (var metadata = new MagickImageCollection())
        {
            await metadata.PingAsync(stream, settings, cancellationToken).ConfigureAwait(false);
            ValidateFrames(metadata);
            if (metadata.Count > 1 && format is MagickFormat.Gif or MagickFormat.WebP)
            {
                var canvasWidth = Math.Max(metadata[0].Width, metadata[0].Page.Width);
                var canvasHeight = Math.Max(metadata[0].Height, metadata[0].Page.Height);
                if ((ulong)canvasWidth * canvasHeight * (ulong)metadata.Count > MaxTotalPixels)
                    throw new InvalidDataException("Animation canvas exceeds the 64 million pixel processing limit.");
            }
        }
        stream.Position = 0;
        var images = new MagickImageCollection();
        try
        {
            await images.ReadAsync(stream, settings, cancellationToken).ConfigureAwait(false);
            if (images.Count > 1 && format is MagickFormat.Gif or MagickFormat.WebP)
                images.Coalesce();
            ValidateFrames(images);
            return images;
        }
        catch { images.Dispose(); throw; }
    }

    public Task StripMetadataAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        outputPath = Path.GetFullPath(outputPath);
        using var images = await ReadRasterAsync(inputPath, cancellationToken).ConfigureAwait(false);
        var format = Path.GetExtension(outputPath).ToLowerInvariant() switch
        {
            ".png" => MagickFormat.Png, ".jpg" or ".jpeg" => MagickFormat.Jpeg,
            ".bmp" => MagickFormat.Bmp, ".tif" or ".tiff" => MagickFormat.Tiff,
            _ => throw new NotSupportedException("Metadata removal supports PNG, JPEG, BMP and TIFF images.")
        };
        if (images[0].Format != format) throw new InvalidDataException("Keep the original image format when removing metadata.");
        if (images.Count > 1 && format != MagickFormat.Tiff)
            throw new NotSupportedException("Only TIFF supports multiple frames in metadata removal.");
        foreach (var frame in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frame.AutoOrient();
            frame.Strip();
        }
        await WriteAtomicallyAsync(images, outputPath, format, true, cancellationToken).ConfigureAwait(false);
    }, cancellationToken);

    private static MagickFormat DetectRasterFormat(Stream stream)
    {
        Span<byte> header = stackalloc byte[16];
        var count = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Position = 0;
        var bytes = header[..count];
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return MagickFormat.Png;
        if (bytes.StartsWith(new byte[] { 255, 216, 255 })) return MagickFormat.Jpeg;
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) return MagickFormat.Gif;
        if (bytes.StartsWith("BM"u8)) return MagickFormat.Bmp;
        if (bytes.StartsWith("II\x2a\0"u8) || bytes.StartsWith("MM\0\x2a"u8)
            || bytes.StartsWith("II\x2b\0"u8) || bytes.StartsWith("MM\0\x2b"u8)) return MagickFormat.Tiff;
        if (count >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return MagickFormat.WebP;
        if (count >= 12 && bytes[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = System.Text.Encoding.ASCII.GetString(bytes[8..12]);
            if (brand is "heic" or "heix" or "hevc" or "hevx" or "mif1" or "msf1") return MagickFormat.Heic;
        }
        throw new InvalidDataException("Unsupported image. Choose PNG, JPEG, WebP, BMP, TIFF, GIF, or HEIC raster data.");
    }

    private static void ValidateFrames(IMagickImageCollection<byte> images)
    {
        if (images.Count == 0 || images.Count > MaxFrames)
            throw new InvalidDataException("An image must contain between 1 and 256 frames.");
        ulong pixels = 0;
        foreach (var image in images)
        {
            if (image.Width == 0 || image.Height == 0 || image.Width > MaxDimension || image.Height > MaxDimension)
                throw new InvalidDataException("Image dimensions must be positive and at most 32768 per side.");
            pixels += (ulong)image.Width * image.Height;
            if (pixels > MaxTotalPixels)
                throw new InvalidDataException("Image exceeds the 64 million pixel processing limit across all frames.");
        }
    }

    private static async Task WriteAtomicallyAsync(MagickImageCollection images, string outputPath,
        MagickFormat format, bool overwrite, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outputPath)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".wup-image-{Guid.NewGuid():N}.tmp");
        try
        {
            // Multi-page TIFF updates directory offsets by seeking and reading its output.
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, true))
            {
                // TIFF and WebP support multiple pages/frames. Other export formats use the first frame.
                if (format is MagickFormat.Tiff or MagickFormat.WebP)
                    await images.WriteAsync(stream, format, cancellationToken).ConfigureAwait(false);
                else
                    await images[0].WriteAsync(stream, format, cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, outputPath, overwrite);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static MagickFormat GetFormat(ImageOutputFormat format) => format switch
    {
        ImageOutputFormat.Jpeg => MagickFormat.Jpeg,
        ImageOutputFormat.Png => MagickFormat.Png,
        ImageOutputFormat.Webp => MagickFormat.WebP,
        ImageOutputFormat.Bmp => MagickFormat.Bmp,
        ImageOutputFormat.Tiff => MagickFormat.Tiff,
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    private static string BuildOutputPath(string input, string directory, ImageOutputFormat format)
    {
        var extension = format switch
        {
            ImageOutputFormat.Jpeg => ".jpg", ImageOutputFormat.Png => ".png", ImageOutputFormat.Webp => ".webp",
            ImageOutputFormat.Bmp => ".bmp", ImageOutputFormat.Tiff => ".tiff",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(input) + extension);
    }

    private static string BuildNonConflictingPath(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var candidate = path;
        for (var index = 1; File.Exists(candidate); index++)
            candidate = Path.Combine(directory, $"{name}_{index}{extension}");
        return candidate;
    }

    private static void ApplyAnnotation(IMagickImage<byte> image, ImageAnnotation annotation)
    {
        var color = ParseColor(annotation.ColorHex);
        var thickness = float.IsFinite(annotation.StrokeThickness) ? Math.Clamp(annotation.StrokeThickness, 1f, 20f) : 3f;
        var rectangle = NormalizeRect(annotation.X, annotation.Y, annotation.Width, annotation.Height, image.Width, image.Height);
        if (annotation.Type == AnnotationType.Arrow)
        {
            var start = NormalizePoint(annotation.X, annotation.Y, image.Width, image.Height);
            var end = annotation.X2 is float x2 && annotation.Y2 is float y2
                ? NormalizePoint(x2, y2, image.Width, image.Height)
                : new SD.PointF(rectangle.Right, rectangle.Bottom);
            if (start != end)
                DrawArrow(image, color, thickness, start, end);
            return;
        }
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
            return;

        switch (annotation.Type)
        {
            case AnnotationType.Rectangle:
                new Drawables().FillColor(MagickColors.Transparent).StrokeColor(color).StrokeWidth(thickness)
                    .Rectangle(rectangle.Left, rectangle.Top, rectangle.Right - 1, rectangle.Bottom - 1).Draw(image);
                break;
            case AnnotationType.Text:
                if (string.IsNullOrEmpty(annotation.Text)) return;
                var font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeuib.ttf");
                var draw = new Drawables().Font(font).FontPointSize(Math.Clamp(rectangle.Height * 0.5, 12, 72))
                    .TextEncoding(System.Text.Encoding.UTF8).FillColor(color).StrokeColor(MagickColors.Transparent);
                var metrics = draw.FontTypeMetrics(annotation.Text);
                draw.Text(rectangle.X, rectangle.Y + (metrics?.Ascent ?? 12), annotation.Text).Draw(image);
                break;
            case AnnotationType.Redaction:
                using (var redaction = new MagickImage(MagickColors.Black, (uint)rectangle.Width, (uint)rectangle.Height))
                    image.Composite(redaction, rectangle.X, rectangle.Y, CompositeOperator.Copy);
                break;
            case AnnotationType.Blur:
                using (var region = image.CloneArea(rectangle.X, rectangle.Y, (uint)rectangle.Width, (uint)rectangle.Height))
                {
                    region.ResetPage();
                    region.GaussianBlur(0, 12);
                    image.Composite(region, rectangle.X, rectangle.Y, CompositeOperator.Copy);
                }
                break;
        }
    }

    private static SD.Rectangle NormalizeRect(float x, float y, float width, float height, uint imageWidth, uint imageHeight)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(width) || !float.IsFinite(height))
            return SD.Rectangle.Empty;
        var left = (int)Math.Clamp(x, 0, imageWidth);
        var top = (int)Math.Clamp(y, 0, imageHeight);
        var right = (int)Math.Clamp(x + Math.Max(0, width), left, imageWidth);
        var bottom = (int)Math.Clamp(y + Math.Max(0, height), top, imageHeight);
        return SD.Rectangle.FromLTRB(left, top, right, bottom);
    }

    private static SD.PointF NormalizePoint(float x, float y, uint width, uint height) => new(
        float.IsFinite(x) ? Math.Clamp(x, 0, width - 1) : 0,
        float.IsFinite(y) ? Math.Clamp(y, 0, height - 1) : 0);

    private static void DrawArrow(IMagickImage<byte> image, MagickColor color, float thickness, SD.PointF start, SD.PointF end)
    {
        var angle = MathF.Atan2(end.Y - start.Y, end.X - start.X);
        var length = Math.Clamp(thickness * 6f, 10f, 40f);
        new Drawables().FillColor(MagickColors.Transparent).StrokeColor(color).StrokeWidth(thickness)
            .Line(start.X, start.Y, end.X, end.Y)
            .Line(end.X, end.Y, end.X - length * MathF.Cos(angle - MathF.PI / 6f), end.Y - length * MathF.Sin(angle - MathF.PI / 6f))
            .Line(end.X, end.Y, end.X - length * MathF.Cos(angle + MathF.PI / 6f), end.Y - length * MathF.Sin(angle + MathF.PI / 6f))
            .Draw(image);
    }

    private static MagickColor ParseColor(string colorHex)
    {
        try
        {
            var hex = colorHex.Trim();
            if (!hex.StartsWith('#')) hex = "#" + hex;
            // WPF's editor uses ARGB for eight-digit colors.
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return new MagickColor(color.R, color.G, color.B, color.A);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException or NullReferenceException)
        {
            return MagickColors.Red;
        }
    }
}
