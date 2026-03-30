using System.Collections.ObjectModel;
using System.Windows.Documents;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.TextConversion;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.TextFormatConverter;

/// <summary>
/// ViewModel for the Text Format Converter &amp; Formatter tool.
/// </summary>
public sealed class TextFormatConverterViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboardService;
    private readonly IFileDialogService _fileDialogService;
    private readonly ITextFormatConversionService _conversionService;
    private readonly ITextPreviewDocumentBuilder _previewDocumentBuilder;
    private readonly ITextPreviewWindowService _previewWindowService;
    private readonly ITextResultExportService _resultExportService;
    private readonly IUserDialogService _dialogService;
    private readonly Dictionary<TextFormatKind, TextFormatOption> _formatOptions;

    private TextLoadedFile? _loadedFile;
    private TextConversionResult? _latestResult;
    private CancellationTokenSource? _operationCancellationTokenSource;
    private string _busyMessage = string.Empty;
    private string _directInputText = string.Empty;
    private bool _autoDetectSource = true;
    private TextFormatKind _selectedSourceFormat = TextFormatKind.Json;
    private TextFormatKind _selectedTargetFormat = TextFormatKind.Json;
    private TextFormatKind? _detectedSourceFormat;
    private FlowDocument _resultPreviewDocument = new();
    private string _resultStatusMessage = "Load a file or enter direct text to begin.";
    private TextNoticeSeverity _resultStatusSeverity = TextNoticeSeverity.Info;
    private string _directionMessage = "Load a file or enter direct text to see supported conversion directions.";
    private TextNoticeSeverity _directionSeverity = TextNoticeSeverity.Info;
    private bool _isBusy;
    private bool _wasDirectInputTrimmed;

    public TextFormatConverterViewModel(
        IClipboardService clipboardService,
        IFileDialogService fileDialogService,
        ITextFormatConversionService conversionService,
        ITextPreviewDocumentBuilder previewDocumentBuilder,
        ITextPreviewWindowService previewWindowService,
        ITextResultExportService resultExportService,
        IUserDialogService dialogService)
    {
        _clipboardService = clipboardService;
        _fileDialogService = fileDialogService;
        _conversionService = conversionService;
        _previewDocumentBuilder = previewDocumentBuilder;
        _previewWindowService = previewWindowService;
        _resultExportService = resultExportService;
        _dialogService = dialogService;

        _formatOptions = TextFormatKindExtensions.GetAllFormats()
            .ToDictionary(kind => kind, kind => new TextFormatOption(kind));

        SourceFormats = _formatOptions.Values.ToArray();
        TargetFormats = [];
        SourceNotices = [];
        ResultWarnings = [];

        LoadFileCommand = new AsyncRelayCommand(_ => LoadFileAsync(), _ => !IsBusy);
        ClearFileCommand = new RelayCommand(_ => ClearLoadedFile(), _ => HasLoadedFile && !IsBusy);
        PasteCommand = new RelayCommand(_ => PasteDirectInput(), _ => !HasLoadedFile && !IsBusy);
        SwapFormatsCommand = new RelayCommand(_ => SwapFormats(), _ => !IsBusy);
        ClearAllCommand = new RelayCommand(_ => ClearAll(), _ => !IsBusy);
        ConvertCommand = new AsyncRelayCommand(_ => ConvertAsync(isFormatOnly: false), _ => CanConvert());
        FormatCommand = new AsyncRelayCommand(_ => ConvertAsync(isFormatOnly: true), _ => CanFormat());
        CancelCommand = new RelayCommand(_ => CancelOperation(), _ => IsBusy);
        CopyResultCommand = new RelayCommand(_ => CopyResultText(), _ => HasResult && !IsBusy);
        SaveResultCommand = new AsyncRelayCommand(_ => SaveResultAsync(), _ => HasResult && !IsBusy);
        OpenPreviewCommand = new RelayCommand(_ => OpenPreviewWindow(), _ => HasResult && !IsBusy);

        RefreshState(clearResult: false);
    }

    public IReadOnlyList<TextFormatOption> SourceFormats { get; }

    public ObservableCollection<TextFormatOption> TargetFormats { get; }

    public ObservableCollection<TextNotice> SourceNotices { get; }

    public ObservableCollection<TextNotice> ResultWarnings { get; }

    public AsyncRelayCommand LoadFileCommand { get; }

    public RelayCommand ClearFileCommand { get; }

    public RelayCommand PasteCommand { get; }

    public RelayCommand SwapFormatsCommand { get; }

    public RelayCommand ClearAllCommand { get; }

    public AsyncRelayCommand ConvertCommand { get; }

    public AsyncRelayCommand FormatCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand CopyResultCommand { get; }

    public AsyncRelayCommand SaveResultCommand { get; }

    public RelayCommand OpenPreviewCommand { get; }

    public string DirectInputText
    {
        get => _directInputText;
        set
        {
            var normalizedValue = value ?? string.Empty;
            var wasTrimmed = normalizedValue.Length > TextFormatKindExtensions.MaxDirectInputCharacters;
            if (wasTrimmed)
            {
                normalizedValue = normalizedValue[..TextFormatKindExtensions.MaxDirectInputCharacters];
            }

            var wasTrimmedChanged = _wasDirectInputTrimmed != wasTrimmed;
            _wasDirectInputTrimmed = wasTrimmed;

            if (SetProperty(ref _directInputText, normalizedValue) || wasTrimmedChanged)
            {
                OnPropertyChanged(nameof(DirectInputCharacterCount));
                OnPropertyChanged(nameof(DirectInputRemainingCharacters));
                RefreshState(clearResult: true);
            }
        }
    }

    public bool AutoDetectSource
    {
        get => _autoDetectSource;
        set
        {
            if (SetProperty(ref _autoDetectSource, value))
            {
                OnPropertyChanged(nameof(IsManualSourceSelectionEnabled));
                RefreshState(clearResult: true);
            }
        }
    }

    public TextFormatKind SelectedSourceFormat
    {
        get => _selectedSourceFormat;
        set
        {
            if (SetProperty(ref _selectedSourceFormat, value))
            {
                RefreshState(clearResult: true);
            }
        }
    }

    public TextFormatKind SelectedTargetFormat
    {
        get => _selectedTargetFormat;
        set
        {
            if (SetProperty(ref _selectedTargetFormat, value))
            {
                RefreshState(clearResult: true);
            }
        }
    }

    public TextFormatKind? DetectedSourceFormat
    {
        get => _detectedSourceFormat;
        private set
        {
            if (_detectedSourceFormat != value)
            {
                _detectedSourceFormat = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveSourceFormatLabel));
            }
        }
    }

    public FlowDocument ResultPreviewDocument
    {
        get => _resultPreviewDocument;
        private set => SetProperty(ref _resultPreviewDocument, value);
    }

    public string BusyMessage
    {
        get => _busyMessage;
        private set => SetProperty(ref _busyMessage, value);
    }

    public string ResultStatusMessage
    {
        get => _resultStatusMessage;
        private set => SetProperty(ref _resultStatusMessage, value);
    }

    public TextNoticeSeverity ResultStatusSeverity
    {
        get => _resultStatusSeverity;
        private set => SetProperty(ref _resultStatusSeverity, value);
    }

    public string DirectionMessage
    {
        get => _directionMessage;
        private set => SetProperty(ref _directionMessage, value);
    }

    public TextNoticeSeverity DirectionSeverity
    {
        get => _directionSeverity;
        private set => SetProperty(ref _directionSeverity, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsDirectInputEnabled));
                RelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasLoadedFile => _loadedFile is not null;

    public bool HasResult => _latestResult is not null;

    public bool HasActiveInput => HasLoadedFile || !string.IsNullOrWhiteSpace(DirectInputText);

    public bool IsDirectInputEnabled => !HasLoadedFile && !IsBusy;

    public bool IsManualSourceSelectionEnabled => !AutoDetectSource && !IsBusy;

    public int DirectInputCharacterCount => DirectInputText.Length;

    public int DirectInputRemainingCharacters =>
        TextFormatKindExtensions.MaxDirectInputCharacters - DirectInputCharacterCount;

    public string DirectInputCounterText =>
        $"{DirectInputCharacterCount:N0}/{TextFormatKindExtensions.MaxDirectInputCharacters:N0} characters";

    public string LoadedFileName => _loadedFile?.FileName ?? "No file loaded";

    public string LoadedFileSummary => _loadedFile is null
        ? "Load an HTML, XML, Markdown, RTF, PDF, DOCX, or JSON file."
        : $"{_loadedFile.Format.ToDisplayName()} · {TextConversionResultUtilities.FormatFileSize(_loadedFile.FileSizeBytes)} · {_loadedFile.CharacterCount:N0} characters";

    public string LoadedFilePreviewText => _loadedFile?.PreviewText ?? string.Empty;

    public string EffectiveSourceFormatLabel
    {
        get
        {
            if (TryGetEffectiveSourceFormat(out var format))
            {
                return format.ToDisplayName();
            }

            return AutoDetectSource ? "Detecting…" : SelectedSourceFormat.ToDisplayName();
        }
    }

    public string ResultSummary => _latestResult is null
        ? "No result yet."
        : $"{_latestResult.TargetFormat.ToDisplayName()} · {TextConversionResultUtilities.FormatFileSize(_latestResult.OutputBytes.LongLength)} · {TextConversionResultUtilities.GetClipboardText(_latestResult).Length:N0} preview characters";

    private async Task LoadFileAsync()
    {
        var filePath = _fileDialogService.OpenTextFormatFile();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        using var cancellationTokenSource = BeginOperation("Loading file…");

        try
        {
            var loadedFile = await _conversionService.LoadFileAsync(filePath, cancellationTokenSource.Token);
            _loadedFile = loadedFile;
            InvalidateResult();
            RefreshState(clearResult: false);
            SetResultStatus($"Loaded {loadedFile.FileName}.", TextNoticeSeverity.Success);
        }
        catch (OperationCanceledException)
        {
            SetResultStatus("File loading cancelled.", TextNoticeSeverity.Info);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Unable to Load File", ex.Message);
            SetResultStatus("The selected file could not be loaded.", TextNoticeSeverity.Error);
        }
        finally
        {
            EndOperation(cancellationTokenSource);
        }
    }

    private void ClearLoadedFile()
    {
        _loadedFile = null;
        InvalidateResult();
        RefreshState(clearResult: false);
        SetResultStatus("File input cleared. Direct text input is active.", TextNoticeSeverity.Info);
    }

    private void PasteDirectInput()
    {
        if (!_clipboardService.TryGetText(out var clipboardText))
        {
            SetResultStatus("Clipboard does not currently contain readable text.", TextNoticeSeverity.Warning);
            return;
        }

        DirectInputText = clipboardText;
        SetResultStatus("Pasted clipboard text into the direct input editor.", TextNoticeSeverity.Success);
    }

    private void SwapFormats()
    {
        var effectiveSource = TryGetEffectiveSourceFormat(out var currentSourceFormat)
            ? currentSourceFormat
            : SelectedSourceFormat;

        AutoDetectSource = false;

        var previousTarget = SelectedTargetFormat;
        _selectedSourceFormat = previousTarget;
        _selectedTargetFormat = effectiveSource;

        OnPropertyChanged(nameof(SelectedSourceFormat));
        OnPropertyChanged(nameof(SelectedTargetFormat));

        RefreshState(clearResult: true);
        SetResultStatus("Source and target formats were swapped. Auto-detect was turned off for explicit control.", TextNoticeSeverity.Info);
    }

    private void ClearAll()
    {
        _loadedFile = null;
        _latestResult = null;
        _wasDirectInputTrimmed = false;
        _directInputText = string.Empty;
        _selectedSourceFormat = TextFormatKind.Json;
        _selectedTargetFormat = TextFormatKind.Json;
        _autoDetectSource = true;
        _detectedSourceFormat = null;
        SourceNotices.Clear();
        ResultWarnings.Clear();
        ResultPreviewDocument = new FlowDocument();
        BusyMessage = string.Empty;

        OnPropertyChanged(nameof(DirectInputText));
        OnPropertyChanged(nameof(SelectedSourceFormat));
        OnPropertyChanged(nameof(SelectedTargetFormat));
        OnPropertyChanged(nameof(AutoDetectSource));
        OnPropertyChanged(nameof(DetectedSourceFormat));
        OnPropertyChanged(nameof(DirectInputCharacterCount));
        OnPropertyChanged(nameof(DirectInputRemainingCharacters));
        OnPropertyChanged(nameof(IsManualSourceSelectionEnabled));

        RefreshState(clearResult: false);
        SetResultStatus("All source, result, and format selections were reset.", TextNoticeSeverity.Info);
    }

    private async Task ConvertAsync(bool isFormatOnly)
    {
        if (!TryGetEffectiveSourceFormat(out var sourceFormat))
        {
            return;
        }

        var targetFormat = isFormatOnly ? sourceFormat : SelectedTargetFormat;
        var request = new TextConversionRequest
        {
            SourceFormat = sourceFormat,
            TargetFormat = targetFormat,
            InputText = HasLoadedFile ? _loadedFile!.ConversionText : DirectInputText,
            SourceName = HasLoadedFile ? _loadedFile!.FileName : "direct-input",
            IsFileSource = HasLoadedFile,
        };

        using var cancellationTokenSource = BeginOperation(isFormatOnly ? "Formatting…" : "Converting…");

        try
        {
            var result = await _conversionService.ConvertAsync(request, cancellationTokenSource.Token);
            _latestResult = result;
            ResultPreviewDocument = _previewDocumentBuilder.Build(
                result.TargetFormat,
                TextConversionResultUtilities.GetClipboardText(result)).Document;
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(ResultSummary));

            UpdateResultWarnings(sourceFormat, targetFormat);
            SetResultStatus(result.StatusMessage, TextNoticeSeverity.Success);
            RelayCommand.RaiseCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
            SetResultStatus("The current operation was cancelled.", TextNoticeSeverity.Info);
        }
        catch (Exception ex)
        {
            InvalidateResult();
            _dialogService.ShowError(isFormatOnly ? "Formatting Failed" : "Conversion Failed", ex.Message);
            SetResultStatus(ex.Message, TextNoticeSeverity.Error);
        }
        finally
        {
            EndOperation(cancellationTokenSource);
        }
    }

    private void CopyResultText()
    {
        if (_latestResult is null)
        {
            return;
        }

        try
        {
            _clipboardService.SetText(TextConversionResultUtilities.GetClipboardText(_latestResult));
            SetResultStatus(
                _latestResult.TargetFormat.IsBinaryDocument()
                    ? $"Copied {_latestResult.TargetFormat.ToDisplayName()} preview text to the clipboard."
                    : $"Copied {_latestResult.TargetFormat.ToDisplayName()} output to the clipboard.",
                TextNoticeSeverity.Success);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Clipboard Error", ex.Message);
            SetResultStatus("Copying the result to the clipboard failed.", TextNoticeSeverity.Error);
        }
    }

    private async Task SaveResultAsync()
    {
        if (_latestResult is null)
        {
            return;
        }

        using var cancellationTokenSource = BeginOperation("Saving result…");

        try
        {
            var savedPath = await _resultExportService.SaveAsync(_latestResult, cancellationTokenSource.Token);
            if (string.IsNullOrWhiteSpace(savedPath))
            {
                SetResultStatus("Save cancelled.", TextNoticeSeverity.Info);
                return;
            }

            SetResultStatus($"Saved {_latestResult.TargetFormat.ToDisplayName()} output to {savedPath}.", TextNoticeSeverity.Success);
        }
        catch (OperationCanceledException)
        {
            SetResultStatus("Save cancelled.", TextNoticeSeverity.Info);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Save Failed", ex.Message);
            SetResultStatus("The result could not be saved.", TextNoticeSeverity.Error);
        }
        finally
        {
            EndOperation(cancellationTokenSource);
        }
    }

    private void OpenPreviewWindow()
    {
        if (_latestResult is null)
        {
            return;
        }

        var previewViewModel = new TextPreviewWindowViewModel(
            _latestResult,
            _previewDocumentBuilder,
            _clipboardService,
            _resultExportService,
            _dialogService);

        _previewWindowService.ShowOrUpdate(previewViewModel);
        SetResultStatus("Opened the pop-out preview window.", TextNoticeSeverity.Info);
    }

    private void CancelOperation()
    {
        _operationCancellationTokenSource?.Cancel();
    }

    private CancellationTokenSource BeginOperation(string busyMessage)
    {
        CancelOperation();
        _operationCancellationTokenSource?.Dispose();
        _operationCancellationTokenSource = new CancellationTokenSource();
        BusyMessage = busyMessage;
        IsBusy = true;
        return _operationCancellationTokenSource;
    }

    private void EndOperation(CancellationTokenSource cancellationTokenSource)
    {
        if (ReferenceEquals(_operationCancellationTokenSource, cancellationTokenSource))
        {
            _operationCancellationTokenSource = null;
        }

        BusyMessage = string.Empty;
        IsBusy = false;
    }

    private bool CanConvert()
    {
        return CanRunOperation(requireSameFormat: false);
    }

    private bool CanFormat()
    {
        return CanRunOperation(requireSameFormat: true);
    }

    private bool CanRunOperation(bool requireSameFormat)
    {
        if (IsBusy || !HasActiveInput || !TryGetEffectiveSourceFormat(out var sourceFormat))
        {
            return false;
        }

        if (requireSameFormat && SelectedTargetFormat != sourceFormat)
        {
            return false;
        }

        var targetFormat = requireSameFormat ? sourceFormat : SelectedTargetFormat;
        if (!requireSameFormat && targetFormat == sourceFormat)
        {
            return false;
        }

        return _conversionService.GetConversionSupport(sourceFormat, targetFormat).IsSupported;
    }

    private void RefreshState(bool clearResult)
    {
        if (clearResult)
        {
            InvalidateResult();
        }

        RefreshDetectedSourceFormat();
        RefreshTargetFormats();
        RefreshDirectionState();
        RefreshSourceNotices();
        OnPropertyChanged(nameof(HasActiveInput));
        OnPropertyChanged(nameof(HasLoadedFile));
        OnPropertyChanged(nameof(IsDirectInputEnabled));
        OnPropertyChanged(nameof(LoadedFileName));
        OnPropertyChanged(nameof(LoadedFileSummary));
        OnPropertyChanged(nameof(LoadedFilePreviewText));
        OnPropertyChanged(nameof(EffectiveSourceFormatLabel));
        OnPropertyChanged(nameof(DirectInputCounterText));
        RelayCommand.RaiseCanExecuteChanged();
    }

    private void RefreshDetectedSourceFormat()
    {
        if (!HasActiveInput)
        {
            DetectedSourceFormat = null;
            return;
        }

        if (HasLoadedFile)
        {
            DetectedSourceFormat = _loadedFile!.Format;
            return;
        }

        DetectedSourceFormat = _conversionService.DetectFormat(DirectInputText);
    }

    private void RefreshTargetFormats()
    {
        var referenceFormat = GetReferenceSourceFormat();
        var supportedTargets = referenceFormat.GetSupportedTargets()
            .Select(kind => _formatOptions[kind])
            .ToArray();

        ReplaceCollection(TargetFormats, supportedTargets);

        if (!supportedTargets.Any(option => option.Kind == _selectedTargetFormat))
        {
            _selectedTargetFormat = supportedTargets[0].Kind;
            OnPropertyChanged(nameof(SelectedTargetFormat));
        }
    }

    private void RefreshDirectionState()
    {
        if (!HasActiveInput)
        {
            DirectionMessage = "Load a file or enter direct text to see supported conversion directions.";
            DirectionSeverity = TextNoticeSeverity.Info;
            return;
        }

        if (!TryGetEffectiveSourceFormat(out var sourceFormat))
        {
            DirectionMessage = "Auto-detect could not determine the source format. Disable auto-detect to choose it manually.";
            DirectionSeverity = TextNoticeSeverity.Error;
            return;
        }

        var targetFormat = SelectedTargetFormat;
        var support = _conversionService.GetConversionSupport(sourceFormat, targetFormat);

        DirectionMessage = support.Reason;
        DirectionSeverity = !support.IsSupported
            ? TextNoticeSeverity.Error
            : support.IsBestEffort
                ? TextNoticeSeverity.Warning
                : TextNoticeSeverity.Info;
    }

    private void RefreshSourceNotices()
    {
        var notices = new List<TextNotice>();

        if (HasLoadedFile)
        {
            notices.Add(new TextNotice
            {
                Message = "Loaded file input is active. Clear the file to use direct text input again.",
                Severity = TextNoticeSeverity.Info,
            });

            notices.AddRange(_loadedFile!.Warnings.Select(message => new TextNotice
            {
                Message = message,
                Severity = TextNoticeSeverity.Warning,
            }));
        }

        if (_wasDirectInputTrimmed)
        {
            notices.Add(new TextNotice
            {
                Message = $"Direct input was truncated to {TextFormatKindExtensions.MaxDirectInputCharacters:N0} characters.",
                Severity = TextNoticeSeverity.Warning,
            });
        }

        if (HasActiveInput)
        {
            if (AutoDetectSource)
            {
                notices.Add(DetectedSourceFormat is null
                    ? new TextNotice
                    {
                        Message = "Auto-detect is enabled but the source format is not currently recognizable.",
                        Severity = TextNoticeSeverity.Error,
                    }
                    : new TextNotice
                    {
                        Message = $"Auto-detect selected {DetectedSourceFormat.Value.ToDisplayName()} as the effective source format.",
                        Severity = TextNoticeSeverity.Info,
                    });
            }
            else
            {
                notices.Add(new TextNotice
                {
                    Message = $"Using manually selected source format: {SelectedSourceFormat.ToDisplayName()}.",
                    Severity = TextNoticeSeverity.Info,
                });
            }
        }

        ReplaceCollection(SourceNotices, notices);
    }

    private void UpdateResultWarnings(TextFormatKind sourceFormat, TextFormatKind targetFormat)
    {
        var notices = new List<TextNotice>();
        var support = _conversionService.GetConversionSupport(sourceFormat, targetFormat);

        if (support.IsBestEffort)
        {
            notices.Add(new TextNotice
            {
                Message = support.Reason,
                Severity = TextNoticeSeverity.Warning,
            });
        }

        if (_latestResult is not null)
        {
            notices.AddRange(_latestResult.Warnings.Select(message => new TextNotice
            {
                Message = message,
                Severity = TextNoticeSeverity.Warning,
            }));
        }

        ReplaceCollection(ResultWarnings, notices);
    }

    private void InvalidateResult()
    {
        _latestResult = null;
        ResultWarnings.Clear();
        ResultPreviewDocument = new FlowDocument();
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(ResultSummary));
        RelayCommand.RaiseCanExecuteChanged();
    }

    private void SetResultStatus(string message, TextNoticeSeverity severity)
    {
        ResultStatusMessage = message;
        ResultStatusSeverity = severity;
    }

    private bool TryGetEffectiveSourceFormat(out TextFormatKind sourceFormat)
    {
        if (HasLoadedFile)
        {
            if (AutoDetectSource)
            {
                sourceFormat = _loadedFile!.Format;
                return true;
            }

            sourceFormat = SelectedSourceFormat;
            return true;
        }

        if (AutoDetectSource)
        {
            if (DetectedSourceFormat is TextFormatKind detectedSourceFormat)
            {
                sourceFormat = detectedSourceFormat;
                return true;
            }

            sourceFormat = default;
            return false;
        }

        sourceFormat = SelectedSourceFormat;
        return true;
    }

    private TextFormatKind GetReferenceSourceFormat()
    {
        if (HasLoadedFile && AutoDetectSource)
        {
            return _loadedFile!.Format;
        }

        return DetectedSourceFormat ?? SelectedSourceFormat;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();

        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
