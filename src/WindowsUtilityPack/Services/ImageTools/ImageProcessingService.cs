using System.Drawing.Imaging;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SD = System.Drawing;
using SLFonts = SixLabors.Fonts;
using WpfSystemParameters = System.Windows.SystemParameters;

namespace WindowsUtilityPack.Services.ImageTools;

/// <summary>
/// Image processing implementation used by Image Tools.
/// </summary>
public sealed class ImageProcessingService : IImageProcessingService
{
    public async Task<IReadOnlyList<ImageProcessResult>> ResizeAsync(ImageResizeRequest request, CancellationToken cancellationToken)
    {
        ValidateBatchRequest(request.InputPaths, request.OutputDirectory);
        if (request.Width <= 0 || request.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Width and height must be greater than zero.");

        var output = new List<ImageProcessResult>(request.InputPaths.Count);
        Directory.CreateDirectory(request.OutputDirectory);

        foreach (var inputPath in request.InputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Add(await ResizeOneAsync(inputPath, request, cancellationToken).ConfigureAwait(false));
        }

        return output;
    }

    public async Task<IReadOnlyList<ImageProcessResult>> ConvertAsync(ImageConvertRequest request, CancellationToken cancellationToken)
    {
        ValidateBatchRequest(request.InputPaths, request.OutputDirectory);
        var output = new List<ImageProcessResult>(request.InputPaths.Count);
        Directory.CreateDirectory(request.OutputDirectory);

        foreach (var inputPath in request.InputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Add(await ConvertOneAsync(inputPath, request, cancellationToken).ConfigureAwait(false));
        }

        return output;
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

    public async Task<ImageProcessResult> AnnotateAsync(
        string inputPath,
        string outputPath,
        IReadOnlyList<ImageAnnotation> annotations,
        ImageOutputFormat outputFormat,
        int quality,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
            throw new ArgumentException("Input path is required.", nameof(inputPath));
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input image not found.", inputPath);
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path is required.", nameof(outputPath));

        var inputBytes = new FileInfo(inputPath).Length;

        try
        {
            await using var stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            using var image = await Image.LoadAsync<Rgba32>(stream, cancellationToken).ConfigureAwait(false);

            image.Mutate(ctx =>
            {
                foreach (var annotation in annotations)
                {
                    ApplyAnnotation(ctx, annotation, image.Width, image.Height);
                }
            });

            var encoder = GetEncoder(outputFormat, quality);
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDir))
                Directory.CreateDirectory(outputDir);

            await image.SaveAsync(outputPath, encoder, cancellationToken).ConfigureAwait(false);

            return new ImageProcessResult
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Success = true,
                InputBytes = inputBytes,
                OutputBytes = new FileInfo(outputPath).Length,
                Width = image.Width,
                Height = image.Height,
            };
        }
        catch (Exception ex)
        {
            return new ImageProcessResult
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Success = false,
                ErrorMessage = ex.Message,
                InputBytes = inputBytes,
            };
        }
    }

    private static void ValidateBatchRequest(IReadOnlyList<string> inputPaths, string outputDirectory)
    {
        if (inputPaths.Count == 0)
            throw new ArgumentException("At least one input file is required.", nameof(inputPaths));
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
    }

    private static async Task<ImageProcessResult> ResizeOneAsync(string inputPath, ImageResizeRequest request, CancellationToken cancellationToken)
    {
        var inputBytes = File.Exists(inputPath) ? new FileInfo(inputPath).Length : 0;
        var outputPath = BuildOutputPath(inputPath, request.OutputDirectory, request.OutputFormat);

        try
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Input file not found.", inputPath);

            if (!request.Overwrite && File.Exists(outputPath))
                outputPath = BuildNonConflictingPath(outputPath);

            await using var stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            using var image = await Image.LoadAsync<Rgba32>(stream, cancellationToken).ConfigureAwait(false);

            var size = new Size(request.Width, request.Height);
            var options = new ResizeOptions
            {
                Size = size,
                Sampler = KnownResamplers.Lanczos3,
                Mode = request.KeepAspectRatio ? ResizeMode.Max : ResizeMode.Stretch,
            };

            image.Mutate(ctx => ctx.Resize(options));

            var encoder = GetEncoder(request.OutputFormat, request.Quality);
            await image.SaveAsync(outputPath, encoder, cancellationToken).ConfigureAwait(false);

            return new ImageProcessResult
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Success = true,
                InputBytes = inputBytes,
                OutputBytes = new FileInfo(outputPath).Length,
                Width = image.Width,
                Height = image.Height,
            };
        }
        catch (Exception ex)
        {
            return new ImageProcessResult
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Success = false,
                ErrorMessage = ex.Message,
                InputBytes = inputBytes,
            };
        }
    }

    private static async Task<ImageProcessResult> ConvertOneAsync(string inputPath, ImageConvertRequest request, CancellationToken cancellationToken)
    {
        var inputBytes = File.Exists(inputPath) ? new FileInfo(inputPath).Length : 0;
        var outputPath = BuildOutputPath(inputPath, request.OutputDirectory, request.OutputFormat);

        try
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("Input file not found.", inputPath);

            if (!request.Overwrite && File.Exists(outputPath))
                outputPath = BuildNonConflictingPath(outputPath);

            await using var stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            using var image = await Image.LoadAsync<Rgba32>(stream, cancellationToken).ConfigureAwait(false);
            var encoder = GetEncoder(request.OutputFormat, request.Quality);
            await image.SaveAsync(outputPath, encoder, cancellationToken).ConfigureAwait(false);

            return new ImageProcessResult
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Success = true,
                InputBytes = inputBytes,
                OutputBytes = new FileInfo(outputPath).Length,
                Width = image.Width,
                Height = image.Height,
            };
        }
        catch (Exception ex)
        {
            return new ImageProcessResult
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Success = false,
                ErrorMessage = ex.Message,
                InputBytes = inputBytes,
            };
        }
    }

    private static string BuildOutputPath(string inputPath, string outputDirectory, ImageOutputFormat format)
    {
        var extension = format switch
        {
            ImageOutputFormat.Jpeg => ".jpg",
            ImageOutputFormat.Png => ".png",
            ImageOutputFormat.Webp => ".webp",
            ImageOutputFormat.Bmp => ".bmp",
            ImageOutputFormat.Tiff => ".tiff",
            _ => ".png",
        };

        var name = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(outputDirectory, $"{name}{extension}");
    }

    private static string BuildNonConflictingPath(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var index = 1;

        var candidate = path;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{name}_{index}{extension}");
            index++;
        }

        return candidate;
    }

    private static IImageEncoder GetEncoder(ImageOutputFormat format, int quality)
    {
        quality = Math.Clamp(quality, 1, 100);
        return format switch
        {
            ImageOutputFormat.Jpeg => new JpegEncoder { Quality = quality },
            ImageOutputFormat.Png => new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression },
            ImageOutputFormat.Webp => new WebpEncoder { Quality = quality, FileFormat = WebpFileFormatType.Lossy },
            ImageOutputFormat.Bmp => new BmpEncoder(),
            ImageOutputFormat.Tiff => new TiffEncoder(),
            _ => new PngEncoder(),
        };
    }

    private static void ApplyAnnotation(IImageProcessingContext context, ImageAnnotation annotation, int imageWidth, int imageHeight)
    {
        var rectangle = NormalizeRect(annotation.X, annotation.Y, annotation.Width, annotation.Height, imageWidth, imageHeight);
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
            return;

        var color = ParseColor(annotation.ColorHex);
        var thickness = Math.Clamp(annotation.StrokeThickness, 1f, 20f);

        switch (annotation.Type)
        {
            case AnnotationType.Rectangle:
                context.Draw(color, thickness, rectangle);
                break;

            case AnnotationType.Arrow:
                DrawArrow(context, color, thickness, rectangle);
                break;

            case AnnotationType.Text:
                var font = SLFonts.SystemFonts.CreateFont("Segoe UI", Math.Clamp(rectangle.Height * 0.5f, 12f, 72f), SLFonts.FontStyle.Bold);
                context.DrawText(annotation.Text, font, color, new PointF(rectangle.X, rectangle.Y));
                break;

            case AnnotationType.Redaction:
                context.Fill(SixLabors.ImageSharp.Color.Black, rectangle);
                break;

            case AnnotationType.Blur:
                context.GaussianBlur(12f, rectangle);
                break;
        }
    }

    private static Rectangle NormalizeRect(float x, float y, float width, float height, int imageWidth, int imageHeight)
    {
        var left = (int)Math.Clamp(x, 0, imageWidth - 1);
        var top = (int)Math.Clamp(y, 0, imageHeight - 1);
        var safeWidth = (int)Math.Clamp(width, 0, imageWidth - left);
        var safeHeight = (int)Math.Clamp(height, 0, imageHeight - top);
        return new Rectangle(left, top, safeWidth, safeHeight);
    }

    private static void DrawArrow(IImageProcessingContext context, SixLabors.ImageSharp.Color color, float thickness, Rectangle rectangle)
    {
        var start = new PointF(rectangle.X, rectangle.Y);
        var end = new PointF(rectangle.Right, rectangle.Bottom);
        context.DrawLine(color, thickness, start, end);

        var angle = MathF.Atan2(end.Y - start.Y, end.X - start.X);
        var arrowLength = Math.Clamp(thickness * 6f, 10f, 40f);

        var head1 = new PointF(
            end.X - arrowLength * MathF.Cos(angle - MathF.PI / 6f),
            end.Y - arrowLength * MathF.Sin(angle - MathF.PI / 6f));
        var head2 = new PointF(
            end.X - arrowLength * MathF.Cos(angle + MathF.PI / 6f),
            end.Y - arrowLength * MathF.Sin(angle + MathF.PI / 6f));

        context.DrawLine(color, thickness, end, head1);
        context.DrawLine(color, thickness, end, head2);
    }

    private static SixLabors.ImageSharp.Color ParseColor(string colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
            return SixLabors.ImageSharp.Color.Red;

        try
        {
            return SixLabors.ImageSharp.Color.ParseHex(colorHex.Trim().TrimStart('#'));
        }
        catch
        {
            return SixLabors.ImageSharp.Color.Red;
        }
    }
}
