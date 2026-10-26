using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.DeveloperProductivity.MarkdownEditor;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class MarkdownEditorViewModelTests
{
    [Fact]
    public void Timestamp_OutOfRangeClearsCustomOutput()
    {
        var vm = new WindowsUtilityPack.Tools.DeveloperProductivity.TimestampConverter.TimestampConverterViewModel(new StubClipboard())
            { UnixSeconds = "0" };
        Assert.NotEmpty(vm.CustomFormatResult);
        vm.UnixSeconds = long.MaxValue.ToString();
        Assert.Empty(vm.CustomFormatResult);
        vm.CustomFormat = "yyyy";
        Assert.Empty(vm.CustomFormatResult);
    }

    [Fact]
    public async Task Save_ClearsDirtyStateOnlyAfterCompletion()
    {
        var vm = new MarkdownEditorViewModel(new StubService(), new StubClipboard(), new StubDialogs(false))
            { MarkdownText = "draft", FilePath = "draft.md" };
        Assert.False(vm.CanNavigateAway());
        await vm.SaveAsync();
        Assert.False(vm.IsDirty);
        Assert.True(vm.CanNavigateAway());
    }
    [Fact]
    public void NewDocument_DeclinedDiscardPreservesDraft()
    {
        var vm = new MarkdownEditorViewModel(new StubService(), new StubClipboard(), new StubDialogs(false))
            { MarkdownText = "unsaved draft", FilePath = "draft.md" };
        vm.NewDocumentCommand.Execute(null);
        Assert.Equal("unsaved draft", vm.MarkdownText);
        Assert.Equal("draft.md", vm.FilePath);
    }
    [Fact]
    public async Task Render_UpdatesHtmlAndStats()
    {
        var vm = new MarkdownEditorViewModel(new StubService(), new StubClipboard(), new StubDialogs())
        {
            MarkdownText = "# Test",
        };

        await vm.RenderAsync();

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

    private sealed class StubDialogs(bool confirm = true) : IUserDialogService
    {
        public bool Confirm(string title, string message) => confirm;

        public void ShowError(string title, string message) { }

        public void ShowInfo(string title, string message) { }
    }
}
