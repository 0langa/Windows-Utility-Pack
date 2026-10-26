using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SecurityPrivacy.HashGenerator;

/// <summary>
/// ViewModel for the Hash Generator tool.
/// Computes MD5, SHA-1, SHA-256, SHA-512, and CRC-32 from text or a file.
/// </summary>
public class HashGeneratorViewModel : ViewModelBase, IDisposable, IViewLifetime
{
    private readonly IClipboardService _clipboard;
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _computeCts;
    private bool _disposed;
    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private string _inputText      = string.Empty;
    private string _inputMode      = "Text";
    private string _filePath       = string.Empty;
    private string _md5Result      = string.Empty;
    private string _sha1Result     = string.Empty;
    private string _sha256Result   = string.Empty;
    private string _sha512Result   = string.Empty;
    private string _crcResult      = string.Empty;
    private bool   _isComputing;

    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value) && InputMode == "Text")
                { _computeCts?.Cancel(); ClearResults(); ScheduleAutoCompute(); }
        }
    }

    public string InputMode
    {
        get => _inputMode;
        set
        {
            if (SetProperty(ref _inputMode, value))
            {
                _computeCts?.Cancel(); ClearResults();
                OnPropertyChanged(nameof(IsTextMode));
                OnPropertyChanged(nameof(IsFileMode));
            }
        }
    }

    public bool IsTextMode
    {
        get => _inputMode == "Text";
        set { if (value) InputMode = "Text"; }
    }

    public bool IsFileMode
    {
        get => _inputMode == "File";
        set { if (value) InputMode = "File"; }
    }

    public string FilePath
    {
        get => _filePath;
        set { if (SetProperty(ref _filePath, value)) { _computeCts?.Cancel(); ClearResults(); } }
    }

    public string Md5Result
    {
        get => _md5Result;
        set => SetProperty(ref _md5Result, value);
    }

    public string Sha1Result
    {
        get => _sha1Result;
        set => SetProperty(ref _sha1Result, value);
    }

    public string Sha256Result
    {
        get => _sha256Result;
        set => SetProperty(ref _sha256Result, value);
    }

    public string Sha512Result
    {
        get => _sha512Result;
        set => SetProperty(ref _sha512Result, value);
    }

    public string CrcResult
    {
        get => _crcResult;
        set => SetProperty(ref _crcResult, value);
    }

    public bool IsComputing
    {
        get => _isComputing;
        set => SetProperty(ref _isComputing, value);
    }

    public AsyncRelayCommand ComputeCommand  { get; }
    public RelayCommand      BrowseFileCommand { get; }
    public RelayCommand      CopyMd5Command   { get; }
    public RelayCommand      CopySha1Command  { get; }
    public RelayCommand      CopySha256Command { get; }
    public RelayCommand      CopySha512Command { get; }
    public RelayCommand CopyCrcCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand      ClearCommand     { get; }

    public HashGeneratorViewModel(IClipboardService clipboard)
    {
        _clipboard  = clipboard;

        ComputeCommand   = new AsyncRelayCommand(_ => ComputeHashesAsync(), _ => !IsComputing);
        BrowseFileCommand = new RelayCommand(_ => BrowseFile());
        CopyMd5Command   = new RelayCommand(_ => _clipboard.SetText(Md5Result),    _ => !string.IsNullOrEmpty(Md5Result));
        CopySha1Command  = new RelayCommand(_ => _clipboard.SetText(Sha1Result),   _ => !string.IsNullOrEmpty(Sha1Result));
        CopySha256Command = new RelayCommand(_ => _clipboard.SetText(Sha256Result), _ => !string.IsNullOrEmpty(Sha256Result));
        CopySha512Command = new RelayCommand(_ => _clipboard.SetText(Sha512Result), _ => !string.IsNullOrEmpty(Sha512Result));
        CopyCrcCommand = new RelayCommand(() => _clipboard.SetText(CrcResult), () => !string.IsNullOrEmpty(CrcResult));
        CancelCommand = new RelayCommand(() => _computeCts?.Cancel(), () => IsComputing);
        ClearCommand     = new RelayCommand(_ => ClearAll());
    }

    private async void ScheduleAutoCompute()
    {
        if (_disposed) return;
        _debounceCts?.Cancel(); _debounceCts?.Dispose();
        var cts = new CancellationTokenSource(); _debounceCts = cts;
        var token = cts.Token;
        try { await Task.Delay(300, token); if (IsTextMode) await ComputeHashesAsync(); }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_debounceCts, cts)) _debounceCts = null; cts.Dispose(); }
    }
    internal async Task ComputeHashesAsync()
    {
        if (_disposed) return;
        _computeCts?.Cancel();
        using var cts = new CancellationTokenSource(); _computeCts = cts;
        var token = cts.Token;
        var fileMode = IsFileMode; var path = FilePath; var text = InputText;
        IsComputing = true; ClearResults(); StatusMessage = "Computing…";
        try
        {
            if (!fileMode && text.Length > 1_000_000) throw new InvalidDataException("Text input is limited to 1,000,000 characters. Use File mode for larger data.");
            var results = await Task.Run(async () =>
            {
                using Stream stream = fileMode ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan)
                    : new MemoryStream(Encoding.UTF8.GetBytes(text));
                return await StreamingHashService.ComputeAsync(stream, token).ConfigureAwait(false);
            }, token);
            if (token.IsCancellationRequested || _disposed) return;
            Md5Result = results.Md5; Sha1Result = results.Sha1; Sha256Result = results.Sha256;
            Sha512Result = results.Sha512; CrcResult = results.Crc32; StatusMessage = "Hashes computed.";
        }
        catch (OperationCanceledException) { if (ReferenceEquals(_computeCts, cts) && !_disposed) StatusMessage = "Cancelled."; }
        catch (Exception ex) { if (ReferenceEquals(_computeCts, cts) && !_disposed) StatusMessage = $"Hashing failed: {ex.Message}"; }
        finally { if (ReferenceEquals(_computeCts, cts)) { _computeCts = null; IsComputing = false; } }
    }
    public void Activate() { }
    public void Deactivate() { _debounceCts?.Cancel(); _computeCts?.Cancel(); }
    public void Dispose() { _disposed = true; Deactivate(); }
    private void BrowseFile()
    {
        var dlg = new OpenFileDialog { Title = "Select File", Filter = "All Files (*.*)|*.*" };
        if (dlg.ShowDialog() == true)
        {
            FilePath  = dlg.FileName;
            InputMode = "File";
        }
    }

    private void ClearResults()
    {
        Md5Result    = string.Empty;
        Sha1Result   = string.Empty;
        Sha256Result = string.Empty;
        Sha512Result = string.Empty;
        CrcResult    = string.Empty;
    }

    private void ClearAll()
    {
        _debounceCts?.Cancel(); _computeCts?.Cancel();
        InputText = string.Empty;
        FilePath  = string.Empty;
        ClearResults();
    }
}
