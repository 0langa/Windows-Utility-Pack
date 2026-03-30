using System.Collections.ObjectModel;
using System.IO;

namespace WindowsUtilityPack.Services.TextConversion;

/// <summary>
/// Supported source and target formats for the text conversion tool.
/// </summary>
public enum TextFormatKind
{
    Html,
    Xml,
    Markdown,
    Rtf,
    Pdf,
    Docx,
    Json,
}

/// <summary>
/// Small helper record describing whether a conversion path is directly supported,
/// best-effort, or blocked.
/// </summary>
public sealed record TextConversionSupport(bool IsSupported, bool IsBestEffort, string Reason);

/// <summary>
/// Represents a file selected by the user and normalized for conversion.
/// </summary>
public sealed class TextLoadedFile
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required TextFormatKind Format { get; init; }
    public required string ConversionText { get; init; }
    public required string PreviewText { get; init; }
    public required long FileSizeBytes { get; init; }
    public required int CharacterCount { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Request contract for a conversion or format-only operation.
/// </summary>
public sealed class TextConversionRequest
{
    public required TextFormatKind SourceFormat { get; init; }
    public required TextFormatKind TargetFormat { get; init; }
    public required string InputText { get; init; }
    public string? SourceName { get; init; }
    public bool IsFileSource { get; init; }
}

/// <summary>
/// Result returned after a conversion has completed successfully.
/// </summary>
public sealed class TextConversionResult
{
    public required TextFormatKind SourceFormat { get; init; }
    public required TextFormatKind TargetFormat { get; init; }
    public required string OutputText { get; init; }
    public required string PreviewText { get; init; }
    public required byte[] OutputBytes { get; init; }
    public required string SuggestedFileName { get; init; }
    public bool IsBestEffort { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string StatusMessage { get; init; } = "Conversion completed.";
}

/// <summary>
/// Shared format metadata and supported conversion matrix helpers.
/// </summary>
public static class TextFormatKindExtensions
{
    private static readonly ReadOnlyCollection<TextFormatKind> AllFormats =
        Array.AsReadOnly(Enum.GetValues<TextFormatKind>());

    /// <summary>Maximum character count accepted for direct text input.</summary>
    public const int MaxDirectInputCharacters = 5_000;

    /// <summary>Maximum extracted character count accepted for file input.</summary>
    public const int MaxFileCharacters = 100_000;

    /// <summary>Maximum raw file size accepted for file input.</summary>
    public const int MaxFileBytes = 10 * 1024 * 1024;

    public static IReadOnlyList<TextFormatKind> GetAllFormats() => AllFormats;

    public static string ToDisplayName(this TextFormatKind kind) => kind switch
    {
        TextFormatKind.Html => "HTML",
        TextFormatKind.Xml => "XML",
        TextFormatKind.Markdown => "Markdown",
        TextFormatKind.Rtf => "RTF",
        TextFormatKind.Pdf => "PDF",
        TextFormatKind.Docx => "DOCX",
        TextFormatKind.Json => "JSON",
        _ => kind.ToString(),
    };

    public static string GetDefaultExtension(this TextFormatKind kind) => kind switch
    {
        TextFormatKind.Html => ".html",
        TextFormatKind.Xml => ".xml",
        TextFormatKind.Markdown => ".md",
        TextFormatKind.Rtf => ".rtf",
        TextFormatKind.Pdf => ".pdf",
        TextFormatKind.Docx => ".docx",
        TextFormatKind.Json => ".json",
        _ => ".txt",
    };

    public static bool IsTextBased(this TextFormatKind kind)
        => kind is not TextFormatKind.Docx and not TextFormatKind.Pdf;

    public static bool IsStructured(this TextFormatKind kind)
        => kind is TextFormatKind.Json or TextFormatKind.Xml;

    public static bool IsDocumentFamily(this TextFormatKind kind)
        => kind is TextFormatKind.Html or TextFormatKind.Markdown or TextFormatKind.Rtf or TextFormatKind.Docx or TextFormatKind.Pdf;

    public static TextFormatKind? FromFilePath(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return extension.ToLowerInvariant() switch
        {
            ".html" or ".htm" => TextFormatKind.Html,
            ".xml" => TextFormatKind.Xml,
            ".md" or ".markdown" => TextFormatKind.Markdown,
            ".rtf" => TextFormatKind.Rtf,
            ".pdf" => TextFormatKind.Pdf,
            ".docx" => TextFormatKind.Docx,
            ".json" => TextFormatKind.Json,
            _ => null,
        };
    }

    public static string BuildOpenFileFilter()
        => "Supported formats|*.html;*.htm;*.xml;*.md;*.markdown;*.rtf;*.pdf;*.docx;*.json|HTML (*.html;*.htm)|*.html;*.htm|XML (*.xml)|*.xml|Markdown (*.md;*.markdown)|*.md;*.markdown|RTF (*.rtf)|*.rtf|PDF (*.pdf)|*.pdf|DOCX (*.docx)|*.docx|JSON (*.json)|*.json";

    public static string BuildSaveFilter(this TextFormatKind kind)
        => $"{kind.ToDisplayName()} (*{kind.GetDefaultExtension()})|*{kind.GetDefaultExtension()}|All files (*.*)|*.*";

    public static IReadOnlyList<TextFormatKind> GetSupportedTargets(this TextFormatKind source)
    {
        IReadOnlyList<TextFormatKind> targets = source switch
        {
            TextFormatKind.Json => [TextFormatKind.Json, TextFormatKind.Xml, TextFormatKind.Html, TextFormatKind.Markdown, TextFormatKind.Rtf, TextFormatKind.Docx, TextFormatKind.Pdf],
            TextFormatKind.Xml => [TextFormatKind.Xml, TextFormatKind.Json, TextFormatKind.Html, TextFormatKind.Markdown, TextFormatKind.Rtf, TextFormatKind.Docx, TextFormatKind.Pdf],
            TextFormatKind.Html => [TextFormatKind.Html, TextFormatKind.Xml, TextFormatKind.Markdown, TextFormatKind.Rtf, TextFormatKind.Docx, TextFormatKind.Pdf],
            TextFormatKind.Markdown => [TextFormatKind.Markdown, TextFormatKind.Html, TextFormatKind.Xml, TextFormatKind.Rtf, TextFormatKind.Docx, TextFormatKind.Pdf],
            TextFormatKind.Rtf => [TextFormatKind.Rtf, TextFormatKind.Html, TextFormatKind.Markdown, TextFormatKind.Docx, TextFormatKind.Pdf],
            TextFormatKind.Docx => [TextFormatKind.Docx, TextFormatKind.Html, TextFormatKind.Markdown, TextFormatKind.Rtf, TextFormatKind.Pdf],
            TextFormatKind.Pdf => [TextFormatKind.Pdf, TextFormatKind.Html, TextFormatKind.Markdown, TextFormatKind.Rtf, TextFormatKind.Docx],
            _ => [source],
        };

        return targets;
    }
}