namespace WindowsUtilityPack.Services.TextConversion;

/// <summary>
/// Saves conversion results to disk using the application's shared file dialog flow.
/// </summary>
public interface ITextResultExportService
{
    /// <summary>
    /// Prompts for a save location and writes the conversion result to disk.
    /// Returns the saved file path, or <see langword="null"/> when cancelled.
    /// </summary>
    Task<string?> SaveAsync(TextConversionResult result, CancellationToken cancellationToken);
}
