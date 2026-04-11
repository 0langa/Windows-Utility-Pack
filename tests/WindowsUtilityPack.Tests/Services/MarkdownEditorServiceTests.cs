using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class MarkdownEditorServiceTests
{
    [Fact]
    public void GetStats_ComputesLinesWordsAndCharacters()
    {
        var service = new MarkdownEditorService();

        var stats = service.GetStats("# Title\nHello world");

        Assert.Equal(2, stats.LineCount);
        Assert.Equal(3, stats.WordCount);
        Assert.Equal(19, stats.CharacterCount);
    }

    [Fact]
    public void RenderHtml_ConvertsHeading()
    {
        var service = new MarkdownEditorService();

        var html = service.RenderHtml("# Title");

        Assert.Contains("<h1", html, StringComparison.OrdinalIgnoreCase);
    }
}