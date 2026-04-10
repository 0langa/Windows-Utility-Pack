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
public class HashGeneratorViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboard;
    private CancellationTokenSource? _debounceCts;

    private string _inputText      = string.Empty;
    private string _inputMode      = "Text";
    private string _filePath       = string.Empty;
    private string _md5Result      = string.Empty;
    private string _sha1Result     = string.Empty;
    private string _sha256Result   = string.Empty;
    private string _sha512Result   = string.Empty;
    private string _crcResult      = string.Empty;
    private string _blake2bResult  = "Not supported (library required)";
    private bool   _isComputing;

    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value) && InputMode == "Text")
                ScheduleAutoCompute();
        }
    }

    public string InputMode
    {
        get => _inputMode;
        set
        {
            if (SetProperty(ref _inputMode, value))
            {
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
        set => SetProperty(ref _filePath, value);
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

    public string Blake2bResult
    {
        get => _blake2bResult;
        set => SetProperty(ref _blake2bResult, value);
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
        ClearCommand     = new RelayCommand(_ => ClearAll());
    }

    private void ScheduleAutoCompute()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var ct = _debounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, ct);
                if (!ct.IsCancellationRequested)
                    await Application.Current.Dispatcher.InvokeAsync(() => ComputeCommand.Execute(null));
            }
            catch (TaskCanceledException) { }
        });
    }

    private async Task ComputeHashesAsync()
    {
        IsComputing = true;
        ClearResults();

        try
        {
            byte[] data;

            if (InputMode == "File")
            {
                if (!File.Exists(FilePath))
                {
                    Md5Result = "File not found.";
                    return;
                }
                data = await File.ReadAllBytesAsync(FilePath);
            }
            else
            {
                data = Encoding.UTF8.GetBytes(InputText ?? string.Empty);
            }

            await Task.Run(() =>
            {
                Md5Result    = Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
                Sha1Result   = Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();
                Sha256Result = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
                Sha512Result = Convert.ToHexString(SHA512.HashData(data)).ToLowerInvariant();
                CrcResult    = $"{ComputeCrc32(data):X8}".ToLowerInvariant();
            });
        }
        catch (Exception ex)
        {
            Md5Result = $"Error: {ex.Message}";
        }
        finally
        {
            IsComputing = false;
        }
    }

    /// <summary>CRC-32 implementation using polynomial 0xEDB88320 (IEEE 802.3).</summary>
    private static uint ComputeCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

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
        Blake2bResult = "Not supported (library required)";
    }

    private void ClearAll()
    {
        InputText = string.Empty;
        FilePath  = string.Empty;
        ClearResults();
    }
}
