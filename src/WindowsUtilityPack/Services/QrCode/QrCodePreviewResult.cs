using System.Windows.Media.Imaging;

namespace WindowsUtilityPack.Services.QrCode;

/// <summary>
/// Result payload for preview generation.
/// </summary>
public sealed class QrCodePreviewResult
{
    /// <summary>The rendered preview image.</summary>
    public required BitmapSource Image { get; init; }

    /// <summary>Normalized URL encoded in the QR payload.</summary>
    public required string NormalizedUrl { get; init; }

    /// <summary>Generated SVG representation matching current style options.</summary>
    public required string SvgMarkup { get; init; }

    /// <summary>Generated warnings that may affect scannability/output quality.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
