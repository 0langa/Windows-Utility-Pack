using System.IO;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.FileDataTools.FileHashCalculator;

public class FileHashCalculatorViewModel : ViewModelBase, IDisposable, IViewLifetime
{
    private string _filePath      = string.Empty;
    private string _md5Hash       = string.Empty;
    private string _sha1Hash      = string.Empty;
    private string _sha256Hash    = string.Empty;
    private string _sha512Hash    = string.Empty;
    private string _verifyHash    = string.Empty;
    private string _verifyResult  = string.Empty;
    private bool   _isComputing;
    private double _progress;
    private string _statusMessage = string.Empty;
    private bool   _hasFile;
    private CancellationTokenSource? _computation;
    private bool _disposed;

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (!SetProperty(ref _filePath, value)) return;
            _computation?.Cancel();
            HasFile = !string.IsNullOrWhiteSpace(value);
            ClearResults();
            StatusMessage = string.Empty;
        }
    }

    public bool HasFile
    {
        get => _hasFile;
        private set => SetProperty(ref _hasFile, value);
    }

    public string Md5Hash
    {
        get => _md5Hash;
        private set => SetProperty(ref _md5Hash, value);
    }

    public string Sha1Hash
    {
        get => _sha1Hash;
        private set => SetProperty(ref _sha1Hash, value);
    }

    public string Sha256Hash
    {
        get => _sha256Hash;
        private set => SetProperty(ref _sha256Hash, value);
    }

    public string Sha512Hash
    {
        get => _sha512Hash;
        private set => SetProperty(ref _sha512Hash, value);
    }

    public string VerifyHash
    {
        get => _verifyHash;
        set { if (SetProperty(ref _verifyHash, value)) VerifyResult = string.Empty; }
    }

    public string VerifyResult
    {
        get => _verifyResult;
        private set
        {
            SetProperty(ref _verifyResult, value);
            OnPropertyChanged(nameof(HasVerifyResult));
        }
    }

    public bool HasVerifyResult => !string.IsNullOrEmpty(_verifyResult);

    public bool IsComputing
    {
        get => _isComputing;
        private set => SetProperty(ref _isComputing, value);
    }

    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public AsyncRelayCommand BrowseCommand    { get; }
    public AsyncRelayCommand ComputeCommand   { get; }
    public RelayCommand      VerifyCommand    { get; }
    public RelayCommand      CancelCommand    { get; }
    public RelayCommand      CopyMd5Command    { get; }
    public RelayCommand      CopySha1Command   { get; }
    public RelayCommand      CopySha256Command { get; }
    public RelayCommand      CopySha512Command { get; }

    private readonly IClipboardService _clipboard;

    public FileHashCalculatorViewModel(IClipboardService clipboard)
    {
        _clipboard       = clipboard;
        BrowseCommand     = new AsyncRelayCommand(BrowseAsync);
        ComputeCommand    = new AsyncRelayCommand(ComputeAsync);
        CancelCommand     = new RelayCommand(() => _computation?.Cancel());
        VerifyCommand     = new RelayCommand(Verify);
        CopyMd5Command    = new RelayCommand(() => CopyHash(Md5Hash));
        CopySha1Command   = new RelayCommand(() => CopyHash(Sha1Hash));
        CopySha256Command = new RelayCommand(() => CopyHash(Sha256Hash));
        CopySha512Command = new RelayCommand(() => CopyHash(Sha512Hash));
    }

    private async Task BrowseAsync()
    {
        var dlg = new OpenFileDialog { Title = "Select a file to hash", Filter = "All Files (*.*)|*.*" };
        if (dlg.ShowDialog() != true) return;

        FilePath = dlg.FileName;
        await ComputeAsync();
    }

    public async Task ComputeAsync()
    {
        if (_disposed || IsComputing) return;
        var path = FilePath;
        ClearResults();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusMessage = "File not found.";
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _computation = cancellation;
        IsComputing = true;
        Progress = 0;
        StatusMessage = "Computing hashes…";
        try
        {
            var result = await Task.Run(async () =>
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                return await StreamingHashService.ComputeAsync(stream, cancellation.Token);
            }, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (_disposed || FilePath != path) return;
            Md5Hash = result.Md5;
            Sha1Hash = result.Sha1;
            Sha256Hash = result.Sha256;
            Sha512Hash = result.Sha512;
            Progress = 100;
            StatusMessage = "Hashes computed.";
        }
        catch (OperationCanceledException) { if (FilePath == path) StatusMessage = "Hashing canceled."; }
        catch (Exception ex) { if (FilePath == path) StatusMessage = $"Error: {ex.Message}"; }
        finally
        {
            _computation = null;
            IsComputing = false;
        }
    }

    private void ClearResults()
    {
        Md5Hash = Sha1Hash = Sha256Hash = Sha512Hash = VerifyResult = string.Empty;
        Progress = 0;
    }

    public void Activate() { }
    public void Deactivate() => _computation?.Cancel();
    public void Dispose() { _disposed = true; Deactivate(); }

    private void Verify()
    {
        var input = VerifyHash.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(input))
        {
            VerifyResult = string.Empty;
            return;
        }

        if (IsComputing || string.IsNullOrEmpty(Sha256Hash))
        {
            VerifyResult = "Compute a file hash first.";
            return;
        }

        bool match = input == Md5Hash    ||
                     input == Sha1Hash   ||
                     input == Sha256Hash ||
                     input == Sha512Hash;

        VerifyResult = match ? "Match - Hash verified!" : "Mismatch - Hash does not match.";
    }

    private void CopyHash(string hash)
    {
        if (!string.IsNullOrEmpty(hash))
        {
            _clipboard.SetText(hash);
            StatusMessage = "Hash copied to clipboard.";
        }
    }
}
