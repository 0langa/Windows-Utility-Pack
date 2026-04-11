using System.IO;
using System.Text.RegularExpressions;
using Markdig;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Provides markdown document operations.
/// </summary>
public interface IMarkdownEditorService
{
    Task<string> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    Task SaveAsync(string filePath, string markdownText, CancellationToken cancellationToken = default);

    string RenderHtml(string markdownText);

    MarkdownDocumentStats GetStats(string markdownText);
}

/// <summary>
/// Default markdown editor service.
/// </summary>
public sealed class MarkdownEditorService : IMarkdownEditorService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly Regex WordRegex = new("\\b\\w+\\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<string> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The markdown file was not found.", filePath);
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(string filePath, string markdownText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, markdownText ?? string.Empty, cancellationToken).ConfigureAwait(false);
    }

    public string RenderHtml(string markdownText)
    {
        return Markdown.ToHtml(markdownText ?? string.Empty, Pipeline);
    }

    public MarkdownDocumentStats GetStats(string markdownText)
    {
        var text = markdownText ?? string.Empty;
        var lineCount = text.Length == 0
            ? 0
            : text.Split(["\r\n", "\n"], StringSplitOptions.None).Length;

        return new MarkdownDocumentStats
        {
            LineCount = lineCount,
            WordCount = WordRegex.Matches(text).Count,
            CharacterCount = text.Length,
        };
    }
}