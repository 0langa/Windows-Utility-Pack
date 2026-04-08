using System.Windows.Documents;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.TextConversion;
using WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="TextFormatConverterViewModel"/>.
/// </summary>
public sealed class TextFormatConverterViewModelTests
{
    [Fact]
    public async Task LoadFileCommand_GivesFilePrecedenceUntilCleared()
    {
        var services = new TestServices
        {
            OpenFilePath = "sample.json",
            LoadedFile = new TextLoadedFile
            {
                FilePath = "sample.json",
                FileName = "sample.json",
                Format = TextFormatKind.Json,
                ConversionText = "{\"name\":\"alex\"}",
                PreviewText = "{\"name\":\"alex\"}",
                FileSizeBytes = 16,
                CharacterCount = 16,
            },
        };
        var viewModel = services.CreateViewModel();
        viewModel.DirectInputText = "<html></html>";

        viewModel.LoadFileCommand.Execute(null);
        await Task.Delay(20);

        Assert.True(viewModel.HasLoadedFile);
        Assert.False(viewModel.IsDirectInputEnabled);
        Assert.Contains("file input is active", viewModel.SourceNotices[0].Message, StringComparison.OrdinalIgnoreCase);

        viewModel.ClearFileCommand.Execute(null);

        Assert.False(viewModel.HasLoadedFile);
        Assert.True(viewModel.IsDirectInputEnabled);
    }

    [Fact]
    public void ConvertCommand_IsDisabledWhenAutoDetectCannotResolveSource()
    {
        var services = new TestServices();
        var viewModel = services.CreateViewModel();
        viewModel.DirectInputText = "plain text without a recognizable format";
        viewModel.AutoDetectSource = true;
        viewModel.SelectedTargetFormat = TextFormatKind.Json;

        Assert.False(viewModel.ConvertCommand.CanExecute(null));
        Assert.Equal(TextNoticeSeverity.Error, viewModel.DirectionSeverity);
    }

    [Fact]
    public void ConvertCommand_IsEnabledForManualSupportedDirection()
    {
        var services = new TestServices();
        var viewModel = services.CreateViewModel();
        viewModel.AutoDetectSource = false;
        viewModel.SelectedSourceFormat = TextFormatKind.Json;
        viewModel.SelectedTargetFormat = TextFormatKind.Xml;
        viewModel.DirectInputText = "{\"name\":\"alex\"}";

        Assert.True(viewModel.ConvertCommand.CanExecute(null));
        Assert.False(viewModel.FormatCommand.CanExecute(null));
    }

    [Fact]
    public async Task ConvertCommand_PopulatesResultStateAndPreview()
    {
        var services = new TestServices
        {
            ConvertResult = new TextConversionResult
            {
                SourceFormat = TextFormatKind.Json,
                TargetFormat = TextFormatKind.Xml,
                OutputText = "<root />",
                PreviewText = "<root />",
                OutputBytes = [1, 2, 3],
                SuggestedFileName = "result.xml",
                Warnings = ["Best-effort warning"],
                StatusMessage = "Converted successfully.",
            },
        };
        var viewModel = services.CreateViewModel();
        viewModel.AutoDetectSource = false;
        viewModel.SelectedSourceFormat = TextFormatKind.Json;
        viewModel.SelectedTargetFormat = TextFormatKind.Xml;
        viewModel.DirectInputText = "{\"name\":\"alex\"}";

        viewModel.ConvertCommand.Execute(null);
        await Task.Delay(20);

        Assert.True(viewModel.HasResult);
        Assert.Equal("Converted successfully.", viewModel.ResultStatusMessage);
        Assert.Single(viewModel.ResultWarnings);
        Assert.NotNull(viewModel.ResultPreviewDocument);
    }

    [Fact]
    public async Task SaveAndOpenPreviewCommands_AreEnabledOnlyAfterConversion()
    {
        var services = new TestServices
        {
            ConvertResult = new TextConversionResult
            {
                SourceFormat = TextFormatKind.Json,
                TargetFormat = TextFormatKind.Json,
                OutputText = "{\"name\":\"alex\"}",
                PreviewText = "{\"name\":\"alex\"}",
                OutputBytes = [1, 2, 3],
                SuggestedFileName = "result.json",
                StatusMessage = "Formatted.",
            },
            SavedFilePath = "result.json",
        };
        var viewModel = services.CreateViewModel();
        viewModel.AutoDetectSource = false;
        viewModel.SelectedSourceFormat = TextFormatKind.Json;
        viewModel.SelectedTargetFormat = TextFormatKind.Json;
        viewModel.DirectInputText = "{\"name\":\"alex\"}";

        Assert.False(viewModel.SaveResultCommand.CanExecute(null));
        Assert.False(viewModel.OpenPreviewCommand.CanExecute(null));

        viewModel.FormatCommand.Execute(null);
        await Task.Delay(20);

        Assert.True(viewModel.SaveResultCommand.CanExecute(null));
        Assert.True(viewModel.OpenPreviewCommand.CanExecute(null));

        viewModel.OpenPreviewCommand.Execute(null);
        viewModel.SaveResultCommand.Execute(null);
        await Task.Delay(20);

        Assert.Equal(1, services.PreviewWindowService.ShowCount);
        Assert.Equal(1, services.ExportService.SaveCount);
    }

    [Fact]
    public async Task CopyCommand_UsesPreviewTextForBinaryOutputs()
    {
        var services = new TestServices
        {
            ConvertResult = new TextConversionResult
            {
                SourceFormat = TextFormatKind.Markdown,
                TargetFormat = TextFormatKind.Pdf,
                OutputText = "binary-output",
                PreviewText = "preview-text",
                OutputBytes = [1, 2, 3],
                SuggestedFileName = "result.pdf",
                StatusMessage = "Converted.",
            },
        };
        var viewModel = services.CreateViewModel();
        viewModel.AutoDetectSource = false;
        viewModel.SelectedSourceFormat = TextFormatKind.Markdown;
        viewModel.SelectedTargetFormat = TextFormatKind.Pdf;
        viewModel.DirectInputText = "# Heading";

        viewModel.ConvertCommand.Execute(null);
        await Task.Delay(20);
        viewModel.CopyResultCommand.Execute(null);

        Assert.Equal("preview-text", services.ClipboardService.LastText);
    }

    private sealed class TestServices
    {
        public TestClipboardService ClipboardService { get; } = new();

        public TestFileDialogService FileDialogService { get; } = new();

        public TestConversionService ConversionService { get; } = new();

        public TestPreviewDocumentBuilder PreviewDocumentBuilder { get; } = new();

        public TestPreviewWindowService PreviewWindowService { get; } = new();

        public TestResultExportService ExportService { get; } = new();

        public TestDialogService DialogService { get; } = new();

        public string? OpenFilePath
        {
            set => FileDialogService.OpenFilePath = value;
        }

        public string? SavedFilePath
        {
            set => ExportService.SavedFilePath = value;
        }

        public TextLoadedFile? LoadedFile
        {
            set => ConversionService.LoadedFile = value;
        }

        public TextConversionResult? ConvertResult
        {
            set => ConversionService.ConvertResult = value;
        }

        public TextFormatConverterViewModel CreateViewModel()
        {
            return new TextFormatConverterViewModel(
                ClipboardService,
                FileDialogService,
                ConversionService,
                PreviewDocumentBuilder,
                PreviewWindowService,
                ExportService,
                DialogService);
        }
    }

    private sealed class TestClipboardService : IClipboardService
    {
        public string? LastText { get; private set; }

        public bool TryGetText(out string text)
        {
            text = "clipboard text";
            return true;
        }

        public void SetText(string text)
        {
            LastText = text;
        }

        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => true;
    }

    private sealed class TestFileDialogService : IFileDialogService
    {
        public string? OpenFilePath { get; set; }

        public string? OpenTextFormatFile() => OpenFilePath;

        public string? SaveTextFormatFile(TextFormatKind format, string suggestedFileName) => suggestedFileName;
    }

    private sealed class TestConversionService : ITextFormatConversionService
    {
        public TextLoadedFile? LoadedFile { get; set; }

        public TextConversionResult? ConvertResult { get; set; }

        public TextFormatKind? DetectFormat(string text, string? fileName = null)
        {
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return TextFormatKindExtensions.FromFilePath(fileName);
            }

            return text.TrimStart().StartsWith("{", StringComparison.Ordinal)
                ? TextFormatKind.Json
                : text.TrimStart().StartsWith("#", StringComparison.Ordinal)
                    ? TextFormatKind.Markdown
                    : null;
        }

        public TextConversionSupport GetConversionSupport(TextFormatKind source, TextFormatKind target)
        {
            if (source == target)
            {
                return new TextConversionSupport(true, false, "Format-only normalization is supported.");
            }

            return source switch
            {
                TextFormatKind.Json when target == TextFormatKind.Xml => new TextConversionSupport(true, false, "Directly supported."),
                TextFormatKind.Markdown when target == TextFormatKind.Pdf => new TextConversionSupport(true, true, "Best-effort conversion."),
                _ => new TextConversionSupport(false, false, "Blocked."),
            };
        }

        public Task<TextLoadedFile> LoadFileAsync(string filePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(LoadedFile!);
        }

        public Task<TextConversionResult> ConvertAsync(TextConversionRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ConvertResult!);
        }
    }

    private sealed class TestPreviewDocumentBuilder : ITextPreviewDocumentBuilder
    {
        public TextPreviewDocument Build(TextFormatKind format, string text)
        {
            var document = new FlowDocument(new Paragraph(new Run(text)));
            return new TextPreviewDocument
            {
                Document = document,
                Mode = format.IsBinaryDocument() ? TextPreviewMode.Document : TextPreviewMode.Syntax,
            };
        }
    }

    private sealed class TestPreviewWindowService : ITextPreviewWindowService
    {
        public int ShowCount { get; private set; }

        public bool HasOpenPreview { get; private set; }

        public void ShowOrUpdate(TextPreviewWindowViewModel viewModel)
        {
            ShowCount++;
            HasOpenPreview = true;
        }
    }

    private sealed class TestResultExportService : ITextResultExportService
    {
        public int SaveCount { get; private set; }

        public string? SavedFilePath { get; set; }

        public Task<string?> SaveAsync(TextConversionResult result, CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(SavedFilePath);
        }
    }

    private sealed class TestDialogService : IUserDialogService
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
