namespace WindowsUtilityPack.Services.ImageTools;

/// <summary>
/// Supported image output formats for conversion/resizing.
/// </summary>
public enum ImageOutputFormat
{
    Jpeg,
    Png,
    Webp,
    Bmp,
    Tiff,
}

/// <summary>
/// Common processing result for a single image item.
/// </summary>
public sealed class ImageProcessResult
{
    public string InputPath { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public long InputBytes { get; init; }
    public long OutputBytes { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>
/// Resize/compress request model.
/// </summary>
public sealed class ImageResizeRequest
{
    public required IReadOnlyList<string> InputPaths { get; init; }
    public required string OutputDirectory { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public bool KeepAspectRatio { get; init; } = true;
    public int Quality { get; init; } = 85;
    public ImageOutputFormat OutputFormat { get; init; } = ImageOutputFormat.Jpeg;
    public bool Overwrite { get; init; }
}

/// <summary>
/// Format conversion request model.
/// </summary>
public sealed class ImageConvertRequest
{
    public required IReadOnlyList<string> InputPaths { get; init; }
    public required string OutputDirectory { get; init; }
    public ImageOutputFormat OutputFormat { get; init; } = ImageOutputFormat.Png;
    public int Quality { get; init; } = 90;
    public bool Overwrite { get; init; }
}

/// <summary>
/// Annotation type for screenshot/image markup.
/// </summary>
public enum AnnotationType
{
    Rectangle,
    Arrow,
    Text,
    Redaction,
    Blur,
}

/// <summary>
/// A single annotation instruction applied by <see cref="IImageProcessingService"/>.
/// </summary>
public sealed class ImageAnnotation
{
    public AnnotationType Type { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
    public string Text { get; init; } = string.Empty;
    public string ColorHex { get; init; } = "#FF3B30";
    public float StrokeThickness { get; init; } = 3f;
}

/// <summary>
/// Screenshot capture output.
/// </summary>
public sealed class ScreenshotCaptureResult
{
    public bool Success { get; init; }
    public string ImagePath { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
}

/// <summary>
/// Image processing operations for image tools.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Resizes and compresses an image batch.
    /// </summary>
    Task<IReadOnlyList<ImageProcessResult>> ResizeAsync(ImageResizeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Converts an image batch to another format.
    /// </summary>
    Task<IReadOnlyList<ImageProcessResult>> ConvertAsync(ImageConvertRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Captures the virtual desktop into a PNG file.
    /// </summary>
    Task<ScreenshotCaptureResult> CaptureScreenshotAsync(string outputPath, CancellationToken cancellationToken);

    /// <summary>
    /// Applies annotation overlays to an existing image and writes the output image.
    /// </summary>
    Task<ImageProcessResult> AnnotateAsync(
        string inputPath,
        string outputPath,
        IReadOnlyList<ImageAnnotation> annotations,
        ImageOutputFormat outputFormat,
        int quality,
        CancellationToken cancellationToken);
}

