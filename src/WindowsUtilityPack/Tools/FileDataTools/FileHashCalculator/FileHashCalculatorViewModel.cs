using System.IO;
using System.Security.Cryptography;
using System.Windows;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.FileDataTools.FileHashCalculator;

public class FileHashCalculatorViewModel : ViewModelBase
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

    public string FilePath
    {
        get => _filePath;
        set
        {
            SetProperty(ref _filePath, value);
            HasFile = !string.IsNullOrEmpty(value);
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
        set => SetProperty(ref _verifyHash, value);
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
        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
        {
            StatusMessage = "File not found.";
            return;
        }

        IsComputing   = true;
        Progress      = 0;
        VerifyResult  = string.Empty;
        Md5Hash       = string.Empty;
        Sha1Hash      = string.Empty;
        Sha256Hash    = string.Empty;
        Sha512Hash    = string.Empty;
        StatusMessage = "Computing hashes…";

        try
        {
            var (md5, sha1, sha256, sha512) = await Task.Run(() => ComputeAllHashes(FilePath));

            Application.Current.Dispatcher.Invoke(() =>
            {
                Md5Hash    = md5;
                Sha1Hash   = sha1;
                Sha256Hash = sha256;
                Sha512Hash = sha512;
                Progress   = 100;
                StatusMessage = "Hashes computed successfully.";
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsComputing = false;
        }
    }

    private (string md5, string sha1, string sha256, string sha512) ComputeAllHashes(string path)
    {
        const int bufferSize = 4096;
        var fileInfo = new FileInfo(path);
        long totalBytes = fileInfo.Length;
        long bytesRead  = 0;

        using var md5Alg    = MD5.Create();
        using var sha1Alg   = SHA1.Create();
        using var sha256Alg = SHA256.Create();
        using var sha512Alg = SHA512.Create();

        using var stream = File.OpenRead(path);
        var buffer = new byte[bufferSize];
        int read;

        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            md5Alg.TransformBlock(buffer,    0, read, null, 0);
            sha1Alg.TransformBlock(buffer,   0, read, null, 0);
            sha256Alg.TransformBlock(buffer, 0, read, null, 0);
            sha512Alg.TransformBlock(buffer, 0, read, null, 0);

            bytesRead += read;

            if (totalBytes > 0)
            {
                double pct = bytesRead / (double)totalBytes * 100.0;
                Application.Current.Dispatcher.Invoke(() => Progress = pct);
            }
        }

        md5Alg.TransformFinalBlock(buffer,    0, 0);
        sha1Alg.TransformFinalBlock(buffer,   0, 0);
        sha256Alg.TransformFinalBlock(buffer, 0, 0);
        sha512Alg.TransformFinalBlock(buffer, 0, 0);

        return (
            HexString(md5Alg.Hash!),
            HexString(sha1Alg.Hash!),
            HexString(sha256Alg.Hash!),
            HexString(sha512Alg.Hash!)
        );
    }

    private static string HexString(byte[] bytes) =>
        Convert.ToHexString(bytes).ToLowerInvariant();

    private void Verify()
    {
        var input = VerifyHash.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(input))
        {
            VerifyResult = string.Empty;
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
