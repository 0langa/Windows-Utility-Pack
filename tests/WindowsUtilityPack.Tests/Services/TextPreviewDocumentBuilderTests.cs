using System.Windows.Documents;
using System.Linq;
using WindowsUtilityPack.Services.TextConversion;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>
/// Tests for the preview document builder used by the inline and pop-out previews.
/// </summary>
public sealed class TextPreviewDocumentBuilderTests
{
    private readonly TextPreviewDocumentBuilder _builder = new();

    [Fact]
    public void Build_UsesSyntaxModeForJson()
    {
        var previewDocument = _builder.Build(TextFormatKind.Json, "{\n  \"name\": \"alex\"\n}");

        Assert.Equal(TextPreviewMode.Syntax, previewDocument.Mode);
        Assert.Contains("alex", new TextRange(previewDocument.Document.ContentStart, previewDocument.Document.ContentEnd).Text);
    }

    [Fact]
    public void Build_UsesDocumentModeForPdfPreview()
    {
        var previewDocument = _builder.Build(TextFormatKind.Pdf, "Paragraph one.\n\nParagraph two.");

        Assert.Equal(TextPreviewMode.Document, previewDocument.Mode);
        Assert.True(previewDocument.Document.Blocks.Count >= 2);
    }
}
