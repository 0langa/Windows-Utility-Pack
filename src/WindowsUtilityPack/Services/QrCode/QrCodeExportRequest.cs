namespace WindowsUtilityPack.Services.QrCode;

/// <summary>
/// Export request for persisting a QR code to disk.
/// </summary>
public sealed class QrCodeExportRequest
{
    /// <summary>Normalized URL to encode.</summary>
    public required string NormalizedUrl { get; init; }

    /// <summary>Destination file path.</summary>
    public required string FilePath { get; init; }

    /// <summary>Desired file format.</summary>
    public required QrCodeExportFormat Format { get; init; }

    /// <summary>Render style for export output.</summary>
    public required QrCodeStyleOptions Style { get; init; }

    /// <summary>Export output size in pixels.</summary>
    public int ExportSizePixels { get; init; } = 1024;

    /// <summary>Raster DPI value for PNG/JPEG/BMP outputs.</summary>
    public int RasterDpi { get; init; } = 300;
}
