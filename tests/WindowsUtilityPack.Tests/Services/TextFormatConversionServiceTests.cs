using System.IO;
using WindowsUtilityPack.Services.TextConversion;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>
/// Integration-style tests for <see cref="TextFormatConversionService"/>.
/// </summary>
public sealed class TextFormatConversionServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly TextFormatConversionService _service = new();

    public TextFormatConversionServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Theory]
    [InlineData("{\"name\":true}", TextFormatKind.Json)]
    [InlineData("<?xml version=\"1.0\"?><root />", TextFormatKind.Xml)]
    [InlineData("<html><body><p>Hello</p></body></html>", TextFormatKind.Html)]
    [InlineData("# Heading", TextFormatKind.Markdown)]
    [InlineData(@"{\rtf1\ansi Hello}", TextFormatKind.Rtf)]
    public void DetectFormat_RecognizesRepresentativeFormats(string input, TextFormatKind expectedFormat)
    {
        var detectedFormat = _service.DetectFormat(input);

        Assert.Equal(expectedFormat, detectedFormat);
    }

    [Fact]
    public void GetConversionSupport_ReturnsExpectedMatrixFlags()
    {
        var directSupport = _service.GetConversionSupport(TextFormatKind.Json, TextFormatKind.Xml);
        var bestEffortSupport = _service.GetConversionSupport(TextFormatKind.Pdf, TextFormatKind.Markdown);
        var blockedSupport = _service.GetConversionSupport(TextFormatKind.Pdf, TextFormatKind.Json);
        var sameBinarySupport = _service.GetConversionSupport(TextFormatKind.Docx, TextFormatKind.Docx);

        Assert.True(directSupport.IsSupported);
        Assert.False(directSupport.IsBestEffort);

        Assert.True(bestEffortSupport.IsSupported);
        Assert.True(bestEffortSupport.IsBestEffort);

        Assert.False(blockedSupport.IsSupported);

        Assert.True(sameBinarySupport.IsSupported);
        Assert.True(sameBinarySupport.IsBestEffort);
    }

    [Fact]
    public async Task ConvertAsync_FormatsJsonWhenSourceAndTargetMatch()
    {
        var result = await _service.ConvertAsync(
            new TextConversionRequest
            {
                SourceFormat = TextFormatKind.Json,
                TargetFormat = TextFormatKind.Json,
                InputText = "{\"name\":\"alex\",\"count\":1}",
            },
            CancellationToken.None);

        Assert.Contains(Environment.NewLine, result.OutputText);
        Assert.Contains("  \"name\"", result.OutputText);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ConvertAsync_ConvertsJsonToXmlWithRootWarning()
    {
        var result = await _service.ConvertAsync(
            new TextConversionRequest
            {
                SourceFormat = TextFormatKind.Json,
                TargetFormat = TextFormatKind.Xml,
                InputText = "{\"name\":\"alex\"}",
            },
            CancellationToken.None);

        Assert.Contains("<root>", result.OutputText);
        Assert.Contains("JSON to XML uses a generated <root> element", string.Join(" ", result.Warnings));
    }

    [Fact]
    public async Task ConvertAsync_ConvertsHtmlToMarkdownPreservingReadableContent()
    {
        var result = await _service.ConvertAsync(
            new TextConversionRequest
            {
                SourceFormat = TextFormatKind.Html,
                TargetFormat = TextFormatKind.Markdown,
                InputText = "<h1>Title</h1><p>Body <strong>text</strong>.</p>",
            },
            CancellationToken.None);

        Assert.Contains("Title", result.OutputText);
        Assert.Contains("Body", result.OutputText);
        Assert.Contains("text", result.OutputText);
    }

    [Fact]
    public async Task ConvertAsync_ThrowsForMalformedJson()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ConvertAsync(
            new TextConversionRequest
            {
                SourceFormat = TextFormatKind.Json,
                TargetFormat = TextFormatKind.Xml,
                InputText = "{invalid}",
            },
            CancellationToken.None));

        Assert.Contains("not valid JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadFileAsync_RejectsUnsupportedFileExtension()
    {
        var filePath = CreateFile("sample.txt", "plain text");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.LoadFileAsync(filePath, CancellationToken.None));
    }

    [Fact]
    public async Task LoadFileAsync_RejectsFileOverTenMegabytes()
    {
        var filePath = Path.Combine(_tempDirectory, "too-large.json");
        var oversizedContent = new string('a', TextFormatKindExtensions.MaxFileBytes + 1);
        await File.WriteAllTextAsync(filePath, oversizedContent);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.LoadFileAsync(filePath, CancellationToken.None));
    }

    [Fact]
    public async Task LoadFileAsync_RejectsFileOverCharacterLimit()
    {
        var filePath = CreateFile("too-many-characters.md", new string('a', TextFormatKindExtensions.MaxFileCharacters + 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.LoadFileAsync(filePath, CancellationToken.None));
    }

    [Fact]
    public async Task LoadFileAsync_LoadsRtfUsingPlainTextPreview()
    {
        var filePath = CreateFile("sample.rtf", @"{\rtf1\ansi This is \b bold\b0 text.}");

        var loadedFile = await _service.LoadFileAsync(filePath, CancellationToken.None);

        Assert.Equal(TextFormatKind.Rtf, loadedFile.Format);
        Assert.Contains("bold", loadedFile.PreviewText);
        Assert.NotEmpty(loadedFile.Warnings);
    }

    [Theory]
    [InlineData(TextFormatKind.Docx)]
    [InlineData(TextFormatKind.Pdf)]
    public async Task ConvertAsync_GeneratesNonEmptyDocumentOutput(TextFormatKind targetFormat)
    {
        var result = await _service.ConvertAsync(
            new TextConversionRequest
            {
                SourceFormat = TextFormatKind.Markdown,
                TargetFormat = targetFormat,
                InputText = "# Heading\n\nParagraph text.",
            },
            CancellationToken.None);

        Assert.NotEmpty(result.OutputBytes);
        Assert.Contains("Heading", result.PreviewText);
        Assert.True(result.IsBestEffort);
    }

    [Fact]
    public async Task LoadFileAsync_ThrowsForUnreadableDocxContent()
    {
        var filePath = CreateFile("broken.docx", "not-a-docx");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.LoadFileAsync(filePath, CancellationToken.None));

        Assert.Contains("DOCX", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private string CreateFile(string fileName, string content)
    {
        var filePath = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
