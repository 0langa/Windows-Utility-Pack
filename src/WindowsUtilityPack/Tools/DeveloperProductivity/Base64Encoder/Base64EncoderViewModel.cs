using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using System.Threading;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.Base64Encoder;

public sealed class Base64EncoderViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboardService;
    private CancellationTokenSource? _debounceCts;
    private const int DebounceMs = 300;
    private bool _updating;

    private string _mode = "Base64";
    private string _inputText = string.Empty;
    private string _outputText = string.Empty;
    private string _direction = "Encode";
    private string _encoding = "UTF-8";
    private string _statusMessage = string.Empty;

    public ObservableCollection<string> Modes { get; } = ["Base64", "URL Encode", "HTML Encode"];
    public ObservableCollection<string> Encodings { get; } = ["UTF-8", "ASCII"];

    public string Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
                ScheduleConvert();
        }
    }

    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value) && !_updating)
                ScheduleConvert();
        }
    }

    public string OutputText
    {
        get => _outputText;
        private set => SetProperty(ref _outputText, value);
    }

    public string Direction
    {
        get => _direction;
        set
        {
            if (SetProperty(ref _direction, value))
                ScheduleConvert();
        }
    }

    public string Encoding
    {
        get => _encoding;
        set
        {
            if (SetProperty(ref _encoding, value))
                ScheduleConvert();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBase64Mode => Mode == "Base64";

    public RelayCommand EncodeCommand { get; }
    public RelayCommand DecodeCommand { get; }
    public RelayCommand SwapCommand { get; }
    public RelayCommand CopyOutputCommand { get; }
    public RelayCommand ClearCommand { get; }

    public Base64EncoderViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;

        EncodeCommand = new RelayCommand(_ => { Direction = "Encode"; Convert(); });
        DecodeCommand = new RelayCommand(_ => { Direction = "Decode"; Convert(); });
        SwapCommand = new RelayCommand(_ => SwapInputOutput());
        CopyOutputCommand = new RelayCommand(_ => _clipboardService.SetText(OutputText),
                                             _ => !string.IsNullOrEmpty(OutputText));
        ClearCommand = new RelayCommand(_ => { InputText = string.Empty; OutputText = string.Empty; StatusMessage = string.Empty; });
    }

    private async void ScheduleConvert()
    {
        if (_updating) return;
        _debounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        try
        {
            await Task.Delay(DebounceMs, cts.Token);
            Convert();
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    private void Convert()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            StatusMessage = string.Empty;
            return;
        }

        try
        {
            OutputText = Mode switch
            {
                "URL Encode"  => Direction == "Encode" ? Uri.EscapeDataString(InputText)   : Uri.UnescapeDataString(InputText),
                "HTML Encode" => Direction == "Encode" ? WebUtility.HtmlEncode(InputText)  : WebUtility.HtmlDecode(InputText),
                _             => Direction == "Encode" ? EncodeBase64(InputText)             : DecodeBase64(InputText)
            };

            var inBytes  = System.Text.Encoding.UTF8.GetByteCount(InputText);
            var outBytes = System.Text.Encoding.UTF8.GetByteCount(OutputText);
            StatusMessage = $"Input: {inBytes} bytes  →  Output: {outBytes} bytes";
        }
        catch (Exception ex)
        {
            OutputText    = string.Empty;
            StatusMessage = $"Error: {ex.Message}";
        }

        OnPropertyChanged(nameof(IsBase64Mode));
    }

    private string EncodeBase64(string input)
    {
        var enc = Encoding == "ASCII" ? System.Text.Encoding.ASCII : System.Text.Encoding.UTF8;
        return System.Convert.ToBase64String(enc.GetBytes(input));
    }

    private string DecodeBase64(string input)
    {
        var bytes = System.Convert.FromBase64String(input);
        var enc = Encoding == "ASCII" ? System.Text.Encoding.ASCII : System.Text.Encoding.UTF8;
        return enc.GetString(bytes);
    }

    private void SwapInputOutput()
    {
        _updating = true;
        try
        {
            var tmp = InputText;
            InputText = OutputText;
            OutputText = tmp;
            Direction = Direction == "Encode" ? "Decode" : "Encode";
        }
        finally
        {
            _updating = false;
        }
        Convert();
    }
}
