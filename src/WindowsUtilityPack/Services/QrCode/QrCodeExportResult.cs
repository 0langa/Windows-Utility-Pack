namespace WindowsUtilityPack.Services.QrCode;

/// <summary>
/// Result of a completed export operation.
/// </summary>
public sealed class QrCodeExportResult
{
    /// <summary>Final file path written to disk.</summary>
    public required string FilePath { get; init; }

    /// <summary>Output format.</summary>
    public required QrCodeExportFormat Format { get; init; }

    /// <summary>Warnings generated while exporting.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
