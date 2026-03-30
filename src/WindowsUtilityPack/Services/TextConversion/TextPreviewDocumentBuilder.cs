using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WindowsUtilityPack.Services.TextConversion;

/// <summary>
/// Builds theme-aware preview documents for both syntax-oriented and document-style output.
/// </summary>
public sealed class TextPreviewDocumentBuilder : ITextPreviewDocumentBuilder
{
    private static readonly Regex JsonTokenRegex = new(
        "\"(?:\\\\.|[^\"\\\\])*\"(?=\\s*:)|\"(?:\\\\.|[^\"\\\\])*\"|-?\\b\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?\\b|\\btrue\\b|\\bfalse\\b|\\bnull\\b|[{}\\[\\],:]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex XmlTagRegex = new(
        @"</?[^>]+?>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex XmlAttributeRegex = new(
        @"(?<name>[\w:-]+)\s*=\s*(?<value>""[^""]*"")",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MarkdownInlineRegex = new(
        @"(```.*?```|`[^`]+`|^#{1,6}\s.*$|^\s*[-*+]\s.*$|!\[[^\]]*\]\([^)]*\)|\[[^\]]+\]\([^)]*\))",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public TextPreviewDocument Build(TextFormatKind format, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return format.IsBinaryDocument()
            ? BuildDocumentPreview(text)
            : BuildSyntaxPreview(format, text);
    }

    private static TextPreviewDocument BuildDocumentPreview(string text)
    {
        var document = CreateBaseDocument(isMonospace: false);

        foreach (var paragraphText in SplitParagraphs(text))
        {
            document.Blocks.Add(new Paragraph(new Run(paragraphText)));
        }

        if (!document.Blocks.Any())
        {
            document.Blocks.Add(new Paragraph(new Run(string.Empty)));
        }

        return new TextPreviewDocument
        {
            Document = document,
            Mode = TextPreviewMode.Document,
        };
    }

    private static TextPreviewDocument BuildSyntaxPreview(TextFormatKind format, string text)
    {
        var document = CreateBaseDocument(isMonospace: true);
        var normalizedText = NormalizeLineEndings(text);
        var lines = normalizedText.Split(Environment.NewLine, StringSplitOptions.None);

        if (lines.Length == 0)
        {
            lines = [string.Empty];
        }

        foreach (var line in lines)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
            };

            AppendSyntaxRuns(paragraph.Inlines, format, line);
            document.Blocks.Add(paragraph);
        }

        return new TextPreviewDocument
        {
            Document = document,
            Mode = TextPreviewMode.Syntax,
        };
    }

    private static void AppendSyntaxRuns(InlineCollection inlines, TextFormatKind format, string line)
    {
        switch (format)
        {
            case TextFormatKind.Json:
                AppendMatches(inlines, line, JsonTokenRegex, GetJsonBrush);
                break;

            case TextFormatKind.Xml:
            case TextFormatKind.Html:
                AppendXmlStyledLine(inlines, line);
                break;

            case TextFormatKind.Markdown:
                AppendMatches(inlines, line, MarkdownInlineRegex, GetMarkdownBrush);
                break;

            default:
                inlines.Add(new Run(line));
                break;
        }
    }

    private static void AppendMatches(
        InlineCollection inlines,
        string line,
        Regex regex,
        Func<string, Brush> brushSelector)
    {
        var currentIndex = 0;

        foreach (Match match in regex.Matches(line))
        {
            if (match.Index > currentIndex)
            {
                inlines.Add(new Run(line[currentIndex..match.Index]));
            }

            inlines.Add(new Run(match.Value)
            {
                Foreground = brushSelector(match.Value),
            });

            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < line.Length)
        {
            inlines.Add(new Run(line[currentIndex..]));
        }
    }

    private static void AppendXmlStyledLine(InlineCollection inlines, string line)
    {
        var currentIndex = 0;

        foreach (Match match in XmlTagRegex.Matches(line))
        {
            if (match.Index > currentIndex)
            {
                inlines.Add(new Run(line[currentIndex..match.Index]));
            }

            AppendXmlTag(inlines, match.Value);
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < line.Length)
        {
            inlines.Add(new Run(line[currentIndex..]));
        }
    }

    private static void AppendXmlTag(InlineCollection inlines, string tagText)
    {
        var tagBrush = GetBrush("AccentBrush", Brushes.SteelBlue);
        var attributeBrush = GetBrush("SecondaryTextBrush", Brushes.SlateGray);
        var valueBrush = GetBrush("SuccessBrush", Brushes.SeaGreen);
        var punctuationBrush = GetBrush("AccentHoverBrush", Brushes.CadetBlue);

        if (string.IsNullOrWhiteSpace(tagText))
        {
            return;
        }

        var openingLength = tagText.StartsWith("</", StringComparison.Ordinal) ? 2 : 1;
        var closingLength = tagText.EndsWith("/>", StringComparison.Ordinal) ? 2 : 1;

        if (tagText.Length <= openingLength + closingLength)
        {
            inlines.Add(new Run(tagText) { Foreground = tagBrush });
            return;
        }

        inlines.Add(new Run(tagText[..openingLength]) { Foreground = punctuationBrush });

        var innerText = tagText.Substring(openingLength, tagText.Length - openingLength - closingLength);
        var nameEndIndex = innerText.IndexOfAny([' ', '\t', '\r', '\n']);
        var tagName = nameEndIndex >= 0 ? innerText[..nameEndIndex] : innerText;
        var attributesText = nameEndIndex >= 0 ? innerText[nameEndIndex..] : string.Empty;

        inlines.Add(new Run(tagName) { Foreground = tagBrush, FontWeight = FontWeights.SemiBold });

        var attributeIndex = 0;
        foreach (Match match in XmlAttributeRegex.Matches(attributesText))
        {
            if (match.Index > attributeIndex)
            {
                inlines.Add(new Run(attributesText[attributeIndex..match.Index]));
            }

            inlines.Add(new Run(match.Groups["name"].Value) { Foreground = attributeBrush });
            inlines.Add(new Run("=") { Foreground = punctuationBrush });
            inlines.Add(new Run(match.Groups["value"].Value) { Foreground = valueBrush });
            attributeIndex = match.Index + match.Length;
        }

        if (attributeIndex < attributesText.Length)
        {
            inlines.Add(new Run(attributesText[attributeIndex..]));
        }

        inlines.Add(new Run(tagText[^closingLength..]) { Foreground = punctuationBrush });
    }

    private static Brush GetJsonBrush(string token)
    {
        if (token.StartsWith('"'))
        {
            return token.EndsWith(':')
                ? GetBrush("SecondaryTextBrush", Brushes.SlateGray)
                : GetBrush("SuccessBrush", Brushes.SeaGreen);
        }

        if (token is "true" or "false" or "null")
        {
            return GetBrush("AccentHoverBrush", Brushes.IndianRed);
        }

        if (char.IsDigit(token[0]) || token[0] == '-')
        {
            return GetBrush("AccentBrush", Brushes.SteelBlue);
        }

        return GetBrush("SecondaryTextBrush", Brushes.SlateGray);
    }

    private static Brush GetMarkdownBrush(string token)
    {
        if (token.StartsWith("```", StringComparison.Ordinal) || token.StartsWith("`", StringComparison.Ordinal))
        {
            return GetBrush("AccentBrush", Brushes.SteelBlue);
        }

        if (token.StartsWith("#", StringComparison.Ordinal))
        {
            return GetBrush("AccentHoverBrush", Brushes.IndianRed);
        }

        if (token.StartsWith("![", StringComparison.Ordinal) || token.StartsWith("[", StringComparison.Ordinal))
        {
            return GetBrush("SuccessBrush", Brushes.SeaGreen);
        }

        return GetBrush("SecondaryTextBrush", Brushes.SlateGray);
    }

    private static FlowDocument CreateBaseDocument(bool isMonospace)
    {
        return new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = isMonospace ? new FontFamily("Consolas") : new FontFamily("Segoe UI"),
            FontSize = isMonospace ? 13 : 14,
            Foreground = GetBrush("PrimaryTextBrush", Brushes.Black),
            Background = Brushes.Transparent,
            TextAlignment = TextAlignment.Left,
            LineHeight = isMonospace ? 18 : 22,
        };
    }

    private static IEnumerable<string> SplitParagraphs(string text)
    {
        return NormalizeLineEndings(text)
            .Split([Environment.NewLine + Environment.NewLine], StringSplitOptions.None)
            .Select(paragraph => paragraph.Trim())
            .Where(paragraph => paragraph.Length > 0)
            .DefaultIfEmpty(string.Empty);
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    private static Brush GetBrush(string resourceKey, Brush fallback)
    {
        var application = Application.Current;
        if (application is not null && application.TryFindResource(resourceKey) is Brush brush)
        {
            return brush;
        }

        return fallback;
    }
}
