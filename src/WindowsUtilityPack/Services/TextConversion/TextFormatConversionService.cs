using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using Markdig;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using ReverseMarkdown;
using Paragraph = System.Windows.Documents.Paragraph;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using WordParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using WordRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace WindowsUtilityPack.Services.TextConversion;

/// <summary>
/// Converts and formats text/document content across the formats supported by
/// the Text Format Converter &amp; Formatter tool.
/// </summary>
public sealed class TextFormatConversionService : ITextFormatConversionService
{
    private static readonly object PdfSharpFontLock = new();
    private static readonly Encoding Utf8NoBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly MarkdownPipeline MarkdownPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private static readonly Converter HtmlToMarkdownConverter = new(new Config
    {
        UnknownTags = Config.UnknownTagsOption.Bypass,
        GithubFlavored = true,
        SmartHrefHandling = true,
    });

    private static readonly Regex JsonDetectionRegex = new(
        @"^\s*[\[{]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex XmlDetectionRegex = new(
        @"^\s*<\??[a-zA-Z!/]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MarkdownDetectionRegex = new(
        @"(^\s{0,3}#{1,6}\s)|(^\s*[-*]\s)|(```)|(!?\[[^\]]\]\([^)]\))",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlTagRegex = new(
        @"<\s*(html|body|div|p|span|h1|h2|h3|h4|h5|h6|ul|ol|li|table|thead|tbody|tr|td|th|pre|code|blockquote|section|article|header|footer|main|nav)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ExcessBlankLinesRegex =
        new(@"(\r?\n){3,}", RegexOptions.Compiled);

    private static readonly HashSet<string> SelfClosingHtmlTags =
        ["br", "hr", "img", "input", "meta", "link", "source", "track", "wbr"];

    private static readonly HashSet<string> InlineHtmlTags =
        ["a", "abbr", "b", "code", "em", "i", "span", "strong", "sub", "sup", "u"];

    /// <inheritdoc/>
    public TextFormatKind? DetectFormat(string text, string? fileName = null)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var fromFile = TextFormatKindExtensions.FromFilePath(fileName);
            if (fromFile is not null)
            {
                return fromFile;
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.TrimStart();
        if (trimmed.StartsWith(@"{\rtf", StringComparison.OrdinalIgnoreCase))
        {
            return TextFormatKind.Rtf;
        }

        if (JsonDetectionRegex.IsMatch(trimmed) && TryFormatJson(trimmed, out _))
        {
            return TextFormatKind.Json;
        }

        if (XmlDetectionRegex.IsMatch(trimmed) && TryFormatXml(trimmed, out _, out _))
        {
            return HtmlTagRegex.IsMatch(trimmed) ? TextFormatKind.Html : TextFormatKind.Xml;
        }

        if (HtmlTagRegex.IsMatch(trimmed))
        {
            return TextFormatKind.Html;
        }

        if (MarkdownDetectionRegex.IsMatch(text))
        {
            return TextFormatKind.Markdown;
        }

        return null;
    }

    /// <inheritdoc/>
    public TextConversionSupport GetConversionSupport(TextFormatKind source, TextFormatKind target)
    {
        if (source == target)
        {
            return new TextConversionSupport(
                true,
                source.IsBinaryDocument(),
                source.IsBinaryDocument()
                    ? "Format-only regeneration is supported using a best-effort text-focused document output."
                    : "Format-only normalization is supported.");
        }

        if (source.GetSupportedTargets().Contains(target))
        {
            var bestEffort =
                target is TextFormatKind.Docx or TextFormatKind.Pdf or TextFormatKind.Rtf ||
                source is TextFormatKind.Docx or TextFormatKind.Pdf or TextFormatKind.Rtf ||
                (source.IsStructured() && target.IsDocumentFamily()) ||
                (target == TextFormatKind.Xml && source == TextFormatKind.Markdown);

            return new TextConversionSupport(
                true,
                bestEffort,
                bestEffort
                    ? "This conversion is supported with best-effort formatting and structure preservation."
                    : "This conversion is directly supported.");
        }

        return new TextConversionSupport(
            false,
            false,
            $"{source.ToDisplayName()} cannot be converted safely to {target.ToDisplayName()}.");
    }

    /// <inheritdoc/>
    public async Task<TextLoadedFile> LoadFileAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException("The selected file no longer exists.");
        }

        var format = TextFormatKindExtensions.FromFilePath(filePath)
                     ?? throw new InvalidOperationException("The selected file type is not supported.");

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > TextFormatKindExtensions.MaxFileBytes)
        {
            throw new InvalidOperationException("Files larger than 10 MB are not supported.");
        }

        var warnings = new List<string>();
        string conversionText;
        string previewText;

        switch (format)
        {
            case TextFormatKind.Docx:
                try
                {
                    conversionText = await ExtractDocxTextAsync(filePath, cancellationToken);
                    previewText = conversionText;
                    warnings.Add("DOCX input is converted from extracted text paragraphs. Complex layout is not preserved.");
                }
                catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException)
                {
                    throw new InvalidOperationException("The selected DOCX file could not be read.", ex);
                }
                break;

            case TextFormatKind.Pdf:
                try
                {
                    conversionText = await ExtractPdfTextAsync(filePath, cancellationToken);
                    previewText = conversionText;
                    warnings.Add("PDF input is converted from extracted text. Visual layout and embedded media are not preserved.");
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException)
                {
                    throw new InvalidOperationException("The selected PDF file could not be read.", ex);
                }
                break;

            case TextFormatKind.Rtf:
                conversionText = await File.ReadAllTextAsync(filePath, cancellationToken);

                try
                {
                    previewText = await ConvertRtfToPlainTextAsync(conversionText, cancellationToken);
                    warnings.Add("RTF input is loaded as rich text, but the preview uses plain text for readability.");
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    throw new InvalidOperationException("The selected RTF file could not be read.", ex);
                }
                break;

            default:
                conversionText = await File.ReadAllTextAsync(filePath, cancellationToken);
                previewText = conversionText;
                break;
        }

        var characterCount = previewText.Length;
        if (characterCount > TextFormatKindExtensions.MaxFileCharacters)
        {
            throw new InvalidOperationException("Files containing more than 100,000 characters are not supported.");
        }

        return new TextLoadedFile
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Format = format,
            ConversionText = conversionText,
            PreviewText = NormalizeLineEndings(previewText),
            FileSizeBytes = fileInfo.Length,
            CharacterCount = characterCount,
            Warnings = warnings,
        };
    }

    /// <inheritdoc/>
    public async Task<TextConversionResult> ConvertAsync(
        TextConversionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var support = GetConversionSupport(request.SourceFormat, request.TargetFormat);
        if (!support.IsSupported)
        {
            throw new InvalidOperationException(support.Reason);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var warnings = new List<string>();
        var normalizedInput = NormalizeLineEndings(request.InputText);
        string outputText;
        string previewText;
        byte[] outputBytes;

        try
        {
            switch (request.TargetFormat)
            {
                case TextFormatKind.Json:
                    outputText = ConvertToJson(request.SourceFormat, normalizedInput, warnings);
                    previewText = outputText;
                    outputBytes = Utf8NoBom.GetBytes(outputText);
                    break;

                case TextFormatKind.Xml:
                    outputText = ConvertToXml(request.SourceFormat, normalizedInput, warnings);
                    previewText = outputText;
                    outputBytes = Utf8NoBom.GetBytes(outputText);
                    break;

                case TextFormatKind.Html:
                    outputText = ConvertToHtml(request.SourceFormat, normalizedInput, warnings);
                    previewText = outputText;
                    outputBytes = Utf8NoBom.GetBytes(outputText);
                    break;

                case TextFormatKind.Markdown:
                    outputText = ConvertToMarkdown(request.SourceFormat, normalizedInput, warnings);
                    previewText = outputText;
                    outputBytes = Utf8NoBom.GetBytes(outputText);
                    break;

                case TextFormatKind.Rtf:
                    previewText = await ConvertSourceToPlainTextAsync(
                        request.SourceFormat,
                        normalizedInput,
                        warnings,
                        cancellationToken);

                    outputText = await ConvertPlainTextToRtfAsync(previewText, cancellationToken);
                    outputBytes = Utf8NoBom.GetBytes(outputText);
                    warnings.Add("RTF output preserves readable paragraph structure, but advanced styling is intentionally kept minimal.");
                    break;

                case TextFormatKind.Docx:
                    previewText = await ConvertSourceToPlainTextAsync(
                        request.SourceFormat,
                        normalizedInput,
                        warnings,
                        cancellationToken);

                    outputText = previewText;
                    outputBytes = CreateDocxDocument(previewText);
                    warnings.Add("DOCX output is generated as a clean text-focused document with paragraph preservation.");
                    break;

                case TextFormatKind.Pdf:
                    previewText = await ConvertSourceToPlainTextAsync(
                        request.SourceFormat,
                        normalizedInput,
                        warnings,
                        cancellationToken);

                    outputText = previewText;
                    outputBytes = CreatePdfDocument(previewText);
                    warnings.Add("PDF output is generated as a text-focused document. Complex source styling cannot be round-tripped.");
                    break;

                default:
                    throw new InvalidOperationException("The selected target format is not supported.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw NormalizeConversionException(ex);
        }

        return new TextConversionResult
        {
            SourceFormat = request.SourceFormat,
            TargetFormat = request.TargetFormat,
            OutputText = outputText,
            PreviewText = NormalizeLineEndings(previewText),
            OutputBytes = outputBytes,
            SuggestedFileName = BuildSuggestedFileName(request.SourceName, request.TargetFormat),
            IsBestEffort = support.IsBestEffort,
            Warnings = warnings,
            StatusMessage = support.IsBestEffort
                ? $"Converted to {request.TargetFormat.ToDisplayName()} using the best available text-preserving strategy."
                : $"Converted to {request.TargetFormat.ToDisplayName()} successfully.",
        };
    }

    private static string BuildSuggestedFileName(string? sourceName, TextFormatKind targetFormat)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName)
            ? "converted"
            : Path.GetFileNameWithoutExtension(sourceName);

        return $"{baseName}{targetFormat.GetDefaultExtension()}";
    }

    private static string ConvertToJson(TextFormatKind sourceFormat, string input, List<string> warnings) =>
        sourceFormat switch
        {
            TextFormatKind.Json => FormatJson(input),
            TextFormatKind.Xml => ConvertXmlToJson(input, warnings),
            _ => throw new InvalidOperationException("Only JSON and XML can be converted safely to JSON."),
        };

    private static string ConvertToXml(TextFormatKind sourceFormat, string input, List<string> warnings) =>
        sourceFormat switch
        {
            TextFormatKind.Xml => FormatXml(input),
            TextFormatKind.Json => ConvertJsonToXml(input, warnings),
            TextFormatKind.Html => ConvertHtmlToXml(input, warnings),
            TextFormatKind.Markdown => ConvertHtmlToXml(
                ConvertMarkdownToHtml(NormalizeMarkdown(input)),
                warnings),
            _ => throw new InvalidOperationException("Only JSON, XML, HTML, and Markdown can be converted safely to XML/XHTML."),
        };

    private static string ConvertToHtml(TextFormatKind sourceFormat, string input, List<string> warnings) =>
        sourceFormat switch
        {
            TextFormatKind.Html => BeautifyHtml(input),
            TextFormatKind.Markdown => BeautifyHtml(ConvertMarkdownToHtml(NormalizeMarkdown(input))),
            TextFormatKind.Json => WrapCodeInHtml(FormatJson(input), TextFormatKind.Json, warnings),
            TextFormatKind.Xml => WrapCodeInHtml(FormatXml(input), TextFormatKind.Xml, warnings),
            TextFormatKind.Rtf or TextFormatKind.Docx or TextFormatKind.Pdf => WrapPlainTextInHtml(input),
            _ => WrapPlainTextInHtml(input),
        };

    private static string ConvertToMarkdown(TextFormatKind sourceFormat, string input, List<string> warnings) =>
        sourceFormat switch
        {
            TextFormatKind.Markdown => NormalizeMarkdown(input),
            TextFormatKind.Html => NormalizeMarkdown(HtmlToMarkdownConverter.Convert(BeautifyHtml(input))),
            TextFormatKind.Json => WrapCodeFence(FormatJson(input), "json", warnings),
            TextFormatKind.Xml => WrapCodeFence(FormatXml(input), "xml", warnings),
            TextFormatKind.Rtf or TextFormatKind.Docx or TextFormatKind.Pdf => PlainTextToMarkdown(input),
            _ => PlainTextToMarkdown(input),
        };

    private static string WrapCodeFence(string text, string language, List<string> warnings)
    {
        warnings.Add($"The source was rendered into Markdown as a fenced {language} block to preserve structure.");
        return $"```{language}{Environment.NewLine}{text}{Environment.NewLine}```";
    }

    private static string WrapCodeInHtml(string text, TextFormatKind sourceFormat, List<string> warnings)
    {
        warnings.Add($"{sourceFormat.ToDisplayName()} content is rendered as preformatted HTML to preserve structure.");
        var encoded = HtmlEncoder.Default.Encode(text);

        return
            $"<!DOCTYPE html>{Environment.NewLine}<html>{Environment.NewLine}  <head>{Environment.NewLine}    <meta charset=\"utf-8\">{Environment.NewLine}    <title>{sourceFormat.ToDisplayName()} Preview</title>{Environment.NewLine}  </head>{Environment.NewLine}  <body>{Environment.NewLine}    <pre>{encoded}</pre>{Environment.NewLine}  </body>{Environment.NewLine}</html>";
    }

    private static string WrapPlainTextInHtml(string text)
    {
        var paragraphs = SplitParagraphs(text);
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html>");
        builder.AppendLine("  <head>");
        builder.AppendLine("    <meta charset=\"utf-8\">");
        builder.AppendLine("    <title>Converted Document</title>");
        builder.AppendLine("  </head>");
        builder.AppendLine("  <body>");

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                continue;
            }

            builder.Append("    <p>")
                .Append(HtmlEncoder.Default.Encode(paragraph))
                .AppendLine("</p>");
        }

        builder.AppendLine("  </body>");
        builder.AppendLine("</html>");
        return builder.ToString().Trim();
    }

    private static string NormalizeMarkdown(string markdown)
    {
        var normalizedLines = new List<string>();

        foreach (var rawLine in NormalizeLineEndings(markdown).Split(Environment.NewLine))
        {
            var line = rawLine.TrimEnd();

            if (Regex.IsMatch(line, @"^#{1,6}[^\s#]", RegexOptions.CultureInvariant))
            {
                var markerLength = line.TakeWhile(c => c == '#').Count();
                line = new string('#', markerLength) + ' ' + line[markerLength..].TrimStart();
            }

            if (Regex.IsMatch(line, @"^\s*[-*][^\s]", RegexOptions.CultureInvariant))
            {
                var trimmed = line.TrimStart();
                line = line[..^trimmed.Length] + trimmed[0] + " " + trimmed[1..].TrimStart();
            }

            normalizedLines.Add(line);
        }

        return ExcessBlankLinesRegex.Replace(
            string.Join(Environment.NewLine, normalizedLines).Trim(),
            Environment.NewLine + Environment.NewLine);
    }

    private static string PlainTextToMarkdown(string text) =>
        string.Join(
            Environment.NewLine + Environment.NewLine,
            SplitParagraphs(text).Select(paragraph => paragraph.TrimEnd()));

    private static string ConvertMarkdownToHtml(string markdown) =>
        Markdig.Markdown.ToHtml(markdown, MarkdownPipeline);

    private static string ConvertHtmlToXml(string html, List<string> warnings)
    {
        warnings.Add("HTML to XML is emitted as XHTML-like markup. Browsers and XML tools may normalize empty elements differently.");

        var document = new HtmlDocument
        {
            OptionOutputAsXml = true,
            OptionWriteEmptyNodes = true,
        };

        document.LoadHtml(html);

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        document.Save(writer);
        return FormatXml(writer.ToString());
    }

    private static string BeautifyHtml(string html)
    {
        var prepared = Regex.Replace(html.Trim(), @">\s*<", ">\n<", RegexOptions.CultureInvariant);
        var lines = prepared.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var builder = new StringBuilder();
        var indent = 0;

        foreach (var originalLine in lines)
        {
            var line = originalLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("</", StringComparison.Ordinal))
            {
                indent = Math.Max(0, indent - 1);
            }

            builder.Append(' ', indent * 2).AppendLine(line);

            if (ShouldIncreaseHtmlIndent(line))
            {
                indent++;
            }
        }

        return builder.ToString().Trim();
    }

    private static bool ShouldIncreaseHtmlIndent(string line)
    {
        if (!line.StartsWith('<') ||
            line.StartsWith("</", StringComparison.Ordinal) ||
            line.StartsWith("<!", StringComparison.Ordinal))
        {
            return false;
        }

        var tagMatch = Regex.Match(line, @"^<\s*([a-zA-Z0-9:-]+)", RegexOptions.CultureInvariant);
        if (!tagMatch.Success)
        {
            return false;
        }

        var tagName = tagMatch.Groups[1].Value.ToLowerInvariant();
        if (SelfClosingHtmlTags.Contains(tagName))
        {
            return false;
        }

        return !line.EndsWith("/>", StringComparison.Ordinal) &&
               !InlineHtmlTags.Contains(tagName) &&
               !line.Contains($"</{tagName}>", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatJson(string json)
    {
        if (!TryFormatJson(json, out var formatted))
        {
            throw new InvalidOperationException("The input is not valid JSON.");
        }

        return formatted;
    }

    private static bool TryFormatJson(string json, out string formatted)
    {
        try
        {
            var node = JsonNode.Parse(json)
                       ?? throw new InvalidOperationException("Empty JSON content.");

            formatted = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            return true;
        }
        catch
        {
            formatted = string.Empty;
            return false;
        }
    }

    private static string FormatXml(string xml)
    {
        if (!TryFormatXml(xml, out var formatted, out _))
        {
            throw new InvalidOperationException("The input is not valid XML.");
        }

        return formatted;
    }

    private static bool TryFormatXml(string xml, out string formatted, out bool hasDeclaration)
    {
        try
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            hasDeclaration = document.Declaration is not null ||
                             xml.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);

            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = !hasDeclaration,
                Encoding = Utf8NoBom,
                NewLineHandling = NewLineHandling.Replace,
            };

            using var writer = new Utf8StringWriter();
            using (var xmlWriter = XmlWriter.Create(writer, settings))
            {
                document.Save(xmlWriter);
            }

            formatted = writer.ToString().Trim();
            return true;
        }
        catch
        {
            formatted = string.Empty;
            hasDeclaration = false;
            return false;
        }
    }

    private static string ConvertJsonToXml(string json, List<string> warnings)
    {
        JsonNode rootNode;
        try
        {
            rootNode = JsonNode.Parse(json)
                       ?? throw new InvalidOperationException("The input is not valid JSON.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("The input is not valid JSON.", ex);
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            ConvertJsonNodeToXml("root", rootNode));

        warnings.Add("JSON to XML uses a generated <root> element and <item> nodes for arrays.");
        return FormatXml(document.ToString());
    }

    private static XElement ConvertJsonNodeToXml(string elementName, JsonNode? node)
    {
        var safeName = XmlConvert.EncodeLocalName(elementName);

        return node switch
        {
            null => new XElement(safeName),
            JsonObject obj => new XElement(
                safeName,
                obj.Select(property => ConvertJsonNodeToXml(property.Key, property.Value))),
            JsonArray array => new XElement(
                safeName,
                array.Select(item => ConvertJsonNodeToXml("item", item))),
            JsonValue value => new XElement(safeName, JsonValueToString(value)),
            _ => new XElement(safeName, node.ToJsonString()),
        };
    }

    private static string JsonValueToString(JsonValue value)
    {
        if (value.TryGetValue<bool>(out var boolValue))
        {
            return boolValue ? "true" : "false";
        }

        if (value.TryGetValue<long>(out var longValue))
        {
            return longValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<double>(out var doubleValue))
        {
            return doubleValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<decimal>(out var decimalValue))
        {
            return decimalValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        return value.ToJsonString();
    }

    private static string ConvertXmlToJson(string xml, List<string> warnings)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException("The input is not valid XML.", ex);
        }

        var root = document.Root
                   ?? throw new InvalidOperationException("The input XML does not contain a root element.");

        var json = new JsonObject
        {
            [root.Name.LocalName] = ConvertXmlElementToJson(root),
        };

        if (root.HasAttributes ||
            root.Descendants().Any(
                descendant =>
                    descendant.HasAttributes ||
                    descendant.Nodes().OfType<XText>().Any(node => !string.IsNullOrWhiteSpace(node.Value)) &&
                    descendant.HasElements))
        {
            warnings.Add("XML to JSON uses @attribute and #text keys when attributes or mixed content are present.");
        }

        return json.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonNode ConvertXmlElementToJson(XElement element)
    {
        var hasChildren = element.Elements().Any();
        var hasAttributes = element.HasAttributes;
        var normalizedText = string.Concat(element.Nodes().OfType<XText>().Select(node => node.Value)).Trim();

        if (!hasChildren && !hasAttributes)
        {
            return JsonValue.Create(string.IsNullOrEmpty(normalizedText) ? string.Empty : normalizedText)
                   ?? JsonValue.Create(string.Empty)!;
        }

        var jsonObject = new JsonObject();

        foreach (var attribute in element.Attributes())
        {
            jsonObject[$"@{attribute.Name.LocalName}"] = attribute.Value;
        }

        foreach (var group in element.Elements().GroupBy(child => child.Name.LocalName))
        {
            if (group.Count() == 1)
            {
                jsonObject[group.Key] = ConvertXmlElementToJson(group.First());
            }
            else
            {
                var array = new JsonArray();
                foreach (var child in group)
                {
                    array.Add(ConvertXmlElementToJson(child));
                }

                jsonObject[group.Key] = array;
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedText))
        {
            jsonObject["#text"] = normalizedText;
        }

        return jsonObject;
    }

    private static string ExtractPlainTextFromHtml(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var plainText = HtmlEntity.DeEntitize(document.DocumentNode.InnerText);
        return NormalizeLineEndings(CollapseWhitespace(plainText));
    }

    private static string CollapseWhitespace(string text)
    {
        var lines = text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.None)
            .Select(line => Regex.Replace(line, @"\s+", " ", RegexOptions.CultureInvariant).TrimEnd())
            .ToArray();

        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static async Task<string> ConvertSourceToPlainTextAsync(
        TextFormatKind sourceFormat,
        string input,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        return sourceFormat switch
        {
            TextFormatKind.Html => ExtractPlainTextFromHtml(input),
            TextFormatKind.Markdown => ExtractPlainTextFromHtml(
                ConvertMarkdownToHtml(NormalizeMarkdown(input))),
            TextFormatKind.Json => FormatJson(input),
            TextFormatKind.Xml => FormatXml(input),
            TextFormatKind.Rtf => await ConvertRtfToPlainTextAsync(input, cancellationToken),
            TextFormatKind.Docx or TextFormatKind.Pdf => NormalizeLineEndings(input),
            _ => NormalizeLineEndings(input),
        };
    }

    private static Task<string> ConvertRtfToPlainTextAsync(string rtf, CancellationToken cancellationToken) =>
        StaThreadInvoker.RunAsync(() =>
        {
            var document = new FlowDocument();
            var range = new TextRange(document.ContentStart, document.ContentEnd);
            using var stream = new MemoryStream(Utf8NoBom.GetBytes(rtf));
            range.Load(stream, DataFormats.Rtf);

            return NormalizeLineEndings(
                new TextRange(document.ContentStart, document.ContentEnd).Text.Trim());
        }, cancellationToken);

    private static Task<string> ConvertPlainTextToRtfAsync(string text, CancellationToken cancellationToken) =>
        StaThreadInvoker.RunAsync(() =>
        {
            var document = new FlowDocument();

            foreach (var paragraph in SplitParagraphs(text))
            {
                document.Blocks.Add(new Paragraph(new System.Windows.Documents.Run(paragraph)));
            }

            if (!document.Blocks.Any())
            {
                document.Blocks.Add(new Paragraph(new System.Windows.Documents.Run(string.Empty)));
            }

            var range = new TextRange(document.ContentStart, document.ContentEnd);
            using var stream = new MemoryStream();
            range.Save(stream, DataFormats.Rtf);
            return Utf8NoBom.GetString(stream.ToArray());
        }, cancellationToken);

    private static Task<string> ExtractDocxTextAsync(string filePath, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            using var document = WordprocessingDocument.Open(filePath, false);
            var mainPart = document.MainDocumentPart
                           ?? throw new InvalidOperationException("The DOCX file does not contain a readable main document part.");

            var body = mainPart.Document?.Body
                       ?? throw new InvalidOperationException("The DOCX file does not contain a readable document body.");

            var paragraphs = body.Elements<WordParagraph>()
                .Select(paragraph => paragraph.InnerText)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();

            return NormalizeLineEndings(
                string.Join(Environment.NewLine + Environment.NewLine, paragraphs));
        }, cancellationToken);

    private static Task<string> ExtractPdfTextAsync(string filePath, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            using var document = PdfPigDocument.Open(filePath);
            var pages = document.GetPages()
                .Select(page => page.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();

            return NormalizeLineEndings(
                string.Join(Environment.NewLine + Environment.NewLine, pages).Trim());
        }, cancellationToken);

    private static byte[] CreateDocxDocument(string text)
    {
        using var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(
                   stream,
                   WordprocessingDocumentType.Document,
                   autoSave: true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new WordDocument(new Body());

            var body = mainPart.Document.Body
                       ?? throw new InvalidOperationException("Unable to create the DOCX document body.");

            foreach (var paragraphText in SplitParagraphs(text))
            {
                body.Append(
                    new WordParagraph(
                        new WordRun(
                            new WordText(paragraphText)
                            {
                                Space = SpaceProcessingModeValues.Preserve,
                            })));
            }

            if (!body.Elements<WordParagraph>().Any())
            {
                body.Append(new WordParagraph(new WordRun(new WordText(string.Empty))));
            }

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static byte[] CreatePdfDocument(string text)
    {
        EnsurePdfSharpFontConfiguration();

        var wrappedLines = WrapLines(text, 95);
        using var stream = new MemoryStream();
        using var document = new PdfDocument();
        document.Info.Title = "Windows Utility Pack Conversion Result";

        const double margin = 40;
        const double lineHeight = 14;
        var font = new XFont("Arial", 10);

        var page = document.AddPage();
        var graphics = XGraphics.FromPdfPage(page);
        var y = margin;
        var maxY = page.Height.Point - margin;

        foreach (var line in wrappedLines)
        {
            if (y + lineHeight > maxY)
            {
                graphics.Dispose();
                page = document.AddPage();
                graphics = XGraphics.FromPdfPage(page);
                y = margin;
                maxY = page.Height.Point - margin;
            }

            graphics.DrawString(line, font, XBrushes.Black, new XPoint(margin, y));
            y += lineHeight;
        }

        graphics.Dispose();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static Exception NormalizeConversionException(Exception exception)
    {
        return exception switch
        {
            InvalidOperationException => exception,
            JsonException => new InvalidOperationException("The input is not valid JSON.", exception),
            XmlException => new InvalidOperationException("The input is not valid XML.", exception),
            OpenXmlPackageException or FileFormatException => new InvalidOperationException("The document content could not be read.", exception),
            _ => exception,
        };
    }

    private static void EnsurePdfSharpFontConfiguration()
    {
        lock (PdfSharpFontLock)
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        }
    }

    private static IEnumerable<string> WrapLines(string text, int maxLineLength)
    {
        foreach (var sourceLine in NormalizeLineEndings(text).Split(Environment.NewLine))
        {
            if (sourceLine.Length <= maxLineLength)
            {
                yield return sourceLine;
                continue;
            }

            var current = sourceLine;
            while (current.Length > maxLineLength)
            {
                var wrapIndex = current.LastIndexOf(' ', maxLineLength);
                if (wrapIndex <= 0)
                {
                    wrapIndex = maxLineLength;
                }

                yield return current[..wrapIndex].TrimEnd();
                current = current[wrapIndex..].TrimStart();
            }

            if (current.Length > 0)
            {
                yield return current;
            }
        }
    }

    private static IReadOnlyList<string> SplitParagraphs(string text) =>
        NormalizeLineEndings(text)
            .Split(new[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.None)
            .Select(paragraph => paragraph.TrimEnd())
            .Where(paragraph => paragraph.Length > 0)
            .DefaultIfEmpty(string.Empty)
            .ToArray();

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Utf8NoBom;
    }
}
