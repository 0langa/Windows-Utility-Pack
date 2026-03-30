namespace WindowsUtilityPack.Services.TextConversion;

/// <summary>
/// Shared helpers for presenting and copying conversion results.
/// </summary>
public static class TextConversionResultUtilities
{
    /// <summary>
    /// Returns the text representation that should be copied to the clipboard.
    /// Text-based formats copy the exact output text; document formats copy the preview text.
    /// </summary>
    public static string GetClipboardText(TextConversionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.TargetFormat.IsBinaryDocument()
            ? result.PreviewText
            : result.OutputText;
    }

    /// <summary>
    /// Formats a byte count into a compact human-readable string.
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        var size = (double)bytes;
        var suffixIndex = 0;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:0.#} {suffixes[suffixIndex]}";
    }
}
