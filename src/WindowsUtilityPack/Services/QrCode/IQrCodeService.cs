namespace WindowsUtilityPack.Services.QrCode;

/// <summary>
/// Handles URL validation/normalization, QR rendering, and export operations.
/// </summary>
public interface IQrCodeService
{
    /// <summary>
    /// Validates and normalizes URL input for QR encoding.
    /// </summary>
    bool TryNormalizeUrl(string input, out string normalizedUrl, out string errorMessage);

    /// <summary>Builds a domain-aware file name suggestion.</summary>
    string BuildSuggestedFileName(string normalizedUrl, bool includeTimestamp);

    /// <summary>Creates a preview image and companion SVG for the supplied options.</summary>
    Task<QrCodePreviewResult> GeneratePreviewAsync(string normalizedUrl, QrCodeStyleOptions style, CancellationToken cancellationToken);

    /// <summary>Exports a rendered QR code to disk in the target format.</summary>
    Task<QrCodeExportResult> ExportAsync(QrCodeExportRequest request, CancellationToken cancellationToken);

    /// <summary>Analyzes style options for potential scanner reliability concerns.</summary>
    QrScannabilityReport AnalyzeScannability(QrCodeStyleOptions style);
}
