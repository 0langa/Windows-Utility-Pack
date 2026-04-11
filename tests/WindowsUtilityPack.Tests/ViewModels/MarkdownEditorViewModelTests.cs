using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.DeveloperProductivity.MarkdownEditor;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class MarkdownEditorViewModelTests
{
    [Fact]
    public void Render_UpdatesHtmlAndStats()
    {
        var vm = new MarkdownEditorViewModel(new StubService(), new StubClipboard(), new StubDialogs())
        {
            MarkdownText = "# Test",
        };

        vm.Render();

        Assert.Equal("<h1>Test</h1>", vm.RenderedHtml);
        Assert.Equal(1, vm.LineCount);
    }

    private sealed class StubService : IMarkdownEditorService
    {
        public MarkdownDocumentStats GetStats(string markdownText)
            => new() { LineCount = 1, WordCount = 1, CharacterCount = markdownText.Length };

        public Task<string> LoadAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult("loaded");

        public string RenderHtml(string markdownText)
            => "<h1>Test</h1>";

        public Task SaveAsync(string filePath, string markdownText, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubClipboard : IClipboardService
    {
        public bool TryGetText(out string text)
        {
            text = string.Empty;
            return false;
        }

        public void SetText(string text) { }

        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }

    private sealed class StubDialogs : IUserDialogService
    {
        public bool Confirm(string title, string message) => true;

        public void ShowError(string title, string message) { }

        public void ShowInfo(string title, string message) { }
    }
}