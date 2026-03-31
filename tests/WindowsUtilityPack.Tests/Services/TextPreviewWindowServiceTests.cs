using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.TextConversion;
using WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>
/// Tests the modeless preview window reuse semantics without creating real WPF windows.
/// </summary>
public sealed class TextPreviewWindowServiceTests
{
    [Fact]
    public void ShowOrUpdate_ReusesExistingHost()
    {
        var factory = new FakePreviewWindowHostFactory();
        var service = new TextPreviewWindowService(factory);

        service.ShowOrUpdate(CreatePreviewViewModel("first"));
        service.ShowOrUpdate(CreatePreviewViewModel("second"));

        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(1, factory.CurrentHost.ActivateCount);
        Assert.True(service.HasOpenPreview);
        Assert.Equal("Text Preview · JSON", ((TextPreviewWindowViewModel)factory.CurrentHost.DataContext!).WindowTitle);
    }

    [Fact]
    public void ShowOrUpdate_CreatesNewHostAfterClose()
    {
        var factory = new FakePreviewWindowHostFactory();
        var service = new TextPreviewWindowService(factory);

        service.ShowOrUpdate(CreatePreviewViewModel("first"));
        factory.CurrentHost.RaiseClosed();
        service.ShowOrUpdate(CreatePreviewViewModel("second"));

        Assert.Equal(2, factory.CreateCount);
        Assert.True(service.HasOpenPreview);
    }

    private static TextPreviewWindowViewModel CreatePreviewViewModel(string outputText)
    {
        return new TextPreviewWindowViewModel(
            new TextConversionResult
            {
                SourceFormat = TextFormatKind.Json,
                TargetFormat = TextFormatKind.Json,
                OutputText = outputText,
                PreviewText = outputText,
                OutputBytes = [1, 2, 3],
                SuggestedFileName = "result.json",
                StatusMessage = "Done.",
            },
            new TextPreviewDocumentBuilder(),
            new StubClipboardService(),
            new StubExportService(),
            new StubDialogService());
    }

    private sealed class FakePreviewWindowHostFactory : ITextPreviewWindowHostFactory
    {
        public int CreateCount { get; private set; }

        public FakePreviewWindowHost CurrentHost { get; private set; } = null!;

        public ITextPreviewWindowHost Create(TextPreviewWindowViewModel viewModel)
        {
            CreateCount++;
            CurrentHost = new FakePreviewWindowHost
            {
                DataContext = viewModel,
            };
            return CurrentHost;
        }
    }

    private sealed class FakePreviewWindowHost : ITextPreviewWindowHost
    {
        public object? DataContext { get; set; }

        public bool IsVisible { get; private set; }

        public int ActivateCount { get; private set; }

        public event EventHandler? Closed;

        public bool Activate()
        {
            ActivateCount++;
            return true;
        }

        public void Show()
        {
            IsVisible = true;
        }

        public void RaiseClosed()
        {
            IsVisible = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class StubClipboardService : IClipboardService
    {
        public bool TryGetText(out string text)
        {
            text = string.Empty;
            return false;
        }

        public void SetText(string text)
        {
        }
    }

    private sealed class StubExportService : ITextResultExportService
    {
        public Task<string?> SaveAsync(TextConversionResult result, CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class StubDialogService : IUserDialogService
    {
        public bool Confirm(string title, string message) => true;

        public void ShowError(string title, string message)
        {
        }

        public void ShowInfo(string title, string message)
        {
        }
    }
}
