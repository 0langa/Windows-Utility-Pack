using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.StructuredData;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.JsonYamlValidator;

/// <summary>
/// ViewModel for a dedicated JSON/YAML validator and formatter.
/// </summary>
public sealed class JsonYamlValidatorViewModel : ViewModelBase
{
    private readonly IStructuredDataValidationService _validationService;
    private readonly IClipboardService _clipboardService;

    private string _inputText = string.Empty;
    private string _outputText = string.Empty;
    private string _mode = "JSON";
    private string _statusMessage = "Paste JSON or YAML and click Validate.";
    private bool _isValid;

    public IReadOnlyList<string> Modes { get; } = ["JSON", "YAML"];

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    public string OutputText
    {
        get => _outputText;
        private set => SetProperty(ref _outputText, value);
    }

    public string Mode
    {
        get => _mode;
        set => SetProperty(ref _mode, value);
    }

    public bool IsValid
    {
        get => _isValid;
        private set => SetProperty(ref _isValid, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand ValidateCommand { get; }
    public RelayCommand FormatCommand { get; }
    public RelayCommand CopyOutputCommand { get; }
    public RelayCommand ClearCommand { get; }

    public JsonYamlValidatorViewModel(
        IStructuredDataValidationService validationService,
        IClipboardService clipboardService)
    {
        _validationService = validationService;
        _clipboardService = clipboardService;

        ValidateCommand = new RelayCommand(_ => Validate(format: false));
        FormatCommand = new RelayCommand(_ => Validate(format: true));
        CopyOutputCommand = new RelayCommand(_ => _clipboardService.SetText(OutputText), _ => !string.IsNullOrWhiteSpace(OutputText));
        ClearCommand = new RelayCommand(_ => Clear());
    }

    private void Validate(bool format)
    {
        var type = Mode.Equals("YAML", StringComparison.OrdinalIgnoreCase)
            ? StructuredDocumentType.Yaml
            : StructuredDocumentType.Json;

        var result = _validationService.Validate(InputText, type);
        IsValid = result.IsValid;

        if (result.IsValid)
        {
            OutputText = format ? result.NormalizedText : InputText;
            StatusMessage = $"{Mode} is valid.";
            return;
        }

        OutputText = string.Empty;
        var location = result.ErrorLine.HasValue
            ? $" (line {result.ErrorLine}, col {result.ErrorColumn ?? 0})"
            : string.Empty;
        StatusMessage = $"Invalid {Mode}: {result.ErrorMessage}{location}";
    }

    private void Clear()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
        StatusMessage = "Paste JSON or YAML and click Validate.";
        IsValid = false;
    }
}
