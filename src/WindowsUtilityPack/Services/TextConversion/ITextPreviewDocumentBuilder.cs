using WindowsUtilityPack.Services.TextConversion;

namespace WindowsUtilityPack.Services.TextConversion;

/// <summary>
/// Builds read-only preview documents for converted text and document content.
/// </summary>
public interface ITextPreviewDocumentBuilder
{
    /// <summary>
    /// Builds a preview document optimized for the target <paramref name="format"/>.
    /// </summary>
    TextPreviewDocument Build(TextFormatKind format, string text);
}
