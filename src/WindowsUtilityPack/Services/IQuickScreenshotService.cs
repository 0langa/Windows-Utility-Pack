using System.Windows.Media.Imaging;
using System.IO;
using WindowsUtilityPack.Services.ImageTools;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Result payload for quick screenshot actions.
/// </summary>
public sealed class QuickScreenshotResult
{
    public bool Success { get; init; }

    public string FilePath { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Captures quick screenshots for tray and global hotkey workflows.
/// </summary>
public interface IQuickScreenshotService
{
    Task<QuickScreenshotResult> CaptureAsync(AppSettings settings, CancellationToken cancellationToken);
}

/// <summary>
/// Shared state for the latest quick screenshot file.
/// </summary>
public interface IQuickCaptureStateService
{
    string LastCapturePath { get; set; }
}

public sealed class QuickCaptureStateService : IQuickCaptureStateService
{
    public string LastCapturePath { get; set; } = string.Empty;
}

/// <summary>
/// Default quick screenshot service.
/// </summary>
public sealed class QuickScreenshotService : IQuickScreenshotService
{
    private readonly IImageProcessingService _imageProcessing;
    private readonly IClipboardService _clipboard;

    public QuickScreenshotService(IImageProcessingService imageProcessing, IClipboardService clipboard)
    {
        _imageProcessing = imageProcessing ?? throw new ArgumentNullException(nameof(imageProcessing));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
    }

    public async Task<QuickScreenshotResult> CaptureAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var outputDirectory = ResolveOutputDirectory(settings);
        Directory.CreateDirectory(outputDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputPath = Path.Combine(outputDirectory, $"quickshot_{timestamp}.png");

        var capture = await _imageProcessing.CaptureScreenshotAsync(outputPath, cancellationToken).ConfigureAwait(true);
        if (!capture.Success || string.IsNullOrWhiteSpace(capture.ImagePath))
        {
            return new QuickScreenshotResult
            {
                Success = false,
                Message = $"Quick screenshot failed: {capture.ErrorMessage}",
            };
        }

        if (settings.QuickScreenshotBehavior == QuickScreenshotBehavior.CaptureToFileAndClipboard)
        {
            TryCopyImageToClipboard(capture.ImagePath);
        }

        return new QuickScreenshotResult
        {
            Success = true,
            FilePath = capture.ImagePath,
            Message = $"Screenshot saved to {capture.ImagePath}",
        };
    }

    private static string ResolveOutputDirectory(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.QuickScreenshotOutputDirectory))
        {
            return settings.QuickScreenshotOutputDirectory;
        }

        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(pictures, "WindowsUtilityPack", "Screenshots");
    }

    private void TryCopyImageToClipboard(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            _clipboard.TrySetImage(bitmap);
        }
        catch
        {
            // Clipboard copy is best-effort for quick actions.
        }
    }
}
