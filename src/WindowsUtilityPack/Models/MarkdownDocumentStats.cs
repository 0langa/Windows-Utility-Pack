namespace WindowsUtilityPack.Models;

/// <summary>
/// Summary statistics for markdown source text.
/// </summary>
public sealed class MarkdownDocumentStats
{
    public int LineCount { get; init; }

    public int WordCount { get; init; }

    public int CharacterCount { get; init; }
}