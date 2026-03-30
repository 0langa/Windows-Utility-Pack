using System.Windows.Documents;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.TextConversion;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;

/// <summary>
/// ViewModel for the modeless pop-out preview window.
/// </summary>
public sealed class TextPreviewWindowViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboardService;
    private readonly ITextResultExportService _resultExportService;
    private readonly IUserDialogService _dialogService;
    private readonly TextConversionResult _result;
    private string _statusMessage = string.Empty;

    public TextPreviewWindowViewModel(
        TextConversionResult result,
        ITextPreviewDocumentBuilder previewDocumentBuilder,
        IClipboardService clipboardService,
        ITextResultExportService resultExportService,
        IUserDialogService dialogService)
    {
        _result = result;
        _clipboardService = clipboardService;
        _resultExportService = resultExportService;
        _dialogService = dialogService;

        PreviewDocument = previewDocumentBuilder.Build(
            result.TargetFormat,
            TextConversionResultUtilities.GetClipboardText(result)).Document;

        WindowTitle = $"Text Preview · {result.TargetFormat.ToDisplayName()}";
        Summary = $"{result.TargetFormat.ToDisplayName()} · {TextConversionResultUtilities.FormatFileSize(result.OutputBytes.LongLength)}";
        StatusMessage = result.StatusMessage;

        CopyCommand = new RelayCommand(_ => CopyResultText());
        SaveCommand = new AsyncRelayCommand(_ => SaveResultAsync());
    }

    public string WindowTitle { get; }

    public string Summary { get; }

    public FlowDocument PreviewDocument { get; }

    public RelayCommand CopyCommand { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private void CopyResultText()
    {
        try
        {
            _clipboardService.SetText(TextConversionResultUtilities.GetClipboardText(_result));
            StatusMessage = _result.TargetFormat.IsBinaryDocument()
                ? $"Copied {_result.TargetFormat.ToDisplayName()} preview text to the clipboard."
                : $"Copied {_result.TargetFormat.ToDisplayName()} output to the clipboard.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Clipboard Error", ex.Message);
            StatusMessage = "Copying the preview content failed.";
        }
    }

    private async Task SaveResultAsync()
    {
        try
        {
            var filePath = await _resultExportService.SaveAsync(_result, CancellationToken.None);
            StatusMessage = string.IsNullOrWhiteSpace(filePath)
                ? "Save cancelled."
                : $"Saved {_result.TargetFormat.ToDisplayName()} output to {filePath}.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Save Failed", ex.Message);
            StatusMessage = "Saving the preview result failed.";
        }
    }
}
