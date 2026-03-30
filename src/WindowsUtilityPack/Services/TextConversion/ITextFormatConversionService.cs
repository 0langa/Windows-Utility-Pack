namespace WindowsUtilityPack.Services.TextConversion;

/// <summary>
/// Converts, formats, validates, and loads the supported text and document formats.
/// </summary>
public interface ITextFormatConversionService
{
    /// <summary>
    /// Attempts to detect the most likely source format for free-form text input.
    /// Returns <see langword="null"/> when detection is inconclusive.
    /// </summary>
    TextFormatKind? DetectFormat(string text, string? fileName = null);

    /// <summary>
    /// Loads a supported file, validates its size/content limits, and returns
    /// the normalized data required for conversion.
    /// </summary>
    Task<TextLoadedFile> LoadFileAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Returns whether the specified conversion direction is supported and whether
    /// it is a best-effort conversion rather than a semantic round-trip.
    /// </summary>
    TextConversionSupport GetConversionSupport(TextFormatKind source, TextFormatKind target);

    /// <summary>
    /// Converts the given input into the target format and returns both the saveable
    /// bytes and a user-friendly preview representation.
    /// </summary>
    Task<TextConversionResult> ConvertAsync(TextConversionRequest request, CancellationToken cancellationToken);
}