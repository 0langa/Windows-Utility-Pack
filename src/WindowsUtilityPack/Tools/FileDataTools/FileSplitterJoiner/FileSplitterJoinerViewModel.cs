using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.FileTools;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.FileDataTools.FileSplitterJoiner;

/// <summary>
/// ViewModel for splitting files into part sets and joining them back.
/// </summary>
public sealed class FileSplitterJoinerViewModel : ViewModelBase
{
    private readonly IFileSplitJoinService _fileSplitJoinService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IClipboardService _clipboardService;

    private CancellationTokenSource? _operationCts;

    private string _splitInputFilePath = string.Empty;
    private string _splitOutputDirectory = string.Empty;
    private int _chunkSizeMb = 100;
    private string _joinPartFilePath = string.Empty;
    private string _joinOutputFilePath = string.Empty;
    private bool _verifyChecksum = true;
    private bool _isBusy;
    private double _progressPercent;
    private string _statusMessage = "Select a file to split or a part file to join.";

    public ObservableCollection<string> RecentOutputFiles { get; } = [];

    public string SplitInputFilePath
    {
        get => _splitInputFilePath;
        set => SetProperty(ref _splitInputFilePath, value);
    }

    public string SplitOutputDirectory
    {
        get => _splitOutputDirectory;
        set => SetProperty(ref _splitOutputDirectory, value);
    }

    public int ChunkSizeMb
    {
        get => _chunkSizeMb;
        set => SetProperty(ref _chunkSizeMb, Math.Clamp(value, 1, 10240));
    }

    public string JoinPartFilePath
    {
        get => _joinPartFilePath;
        set => SetProperty(ref _joinPartFilePath, value);
    }

    public string JoinOutputFilePath
    {
        get => _joinOutputFilePath;
        set => SetProperty(ref _joinOutputFilePath, value);
    }

    public bool VerifyChecksum
    {
        get => _verifyChecksum;
        set => SetProperty(ref _verifyChecksum, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand BrowseSplitInputCommand { get; }
    public RelayCommand BrowseSplitOutputCommand { get; }
    public AsyncRelayCommand SplitCommand { get; }
    public RelayCommand BrowseJoinPartCommand { get; }
    public RelayCommand BrowseJoinOutputCommand { get; }
    public AsyncRelayCommand JoinCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand CopyStatusCommand { get; }

    public FileSplitterJoinerViewModel(
        IFileSplitJoinService fileSplitJoinService,
        IFolderPickerService folderPickerService,
        IClipboardService clipboardService)
    {
        _fileSplitJoinService = fileSplitJoinService;
        _folderPickerService = folderPickerService;
        _clipboardService = clipboardService;

        BrowseSplitInputCommand = new RelayCommand(_ => BrowseSplitInput());
        BrowseSplitOutputCommand = new RelayCommand(_ => BrowseSplitOutput());
        SplitCommand = new AsyncRelayCommand(_ => SplitAsync(), _ => !IsBusy);
        BrowseJoinPartCommand = new RelayCommand(_ => BrowseJoinPartFile());
        BrowseJoinOutputCommand = new RelayCommand(_ => BrowseJoinOutput());
        JoinCommand = new AsyncRelayCommand(_ => JoinAsync(), _ => !IsBusy);
        CancelCommand = new RelayCommand(_ => _operationCts?.Cancel(), _ => IsBusy);
        CopyStatusCommand = new RelayCommand(_ => _clipboardService.SetText(StatusMessage));
    }

    private void BrowseSplitInput()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select file to split",
            Filter = "All Files (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            SplitInputFilePath = dialog.FileName;
            if (string.IsNullOrWhiteSpace(SplitOutputDirectory))
            {
                SplitOutputDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }
        }
    }

    private void BrowseSplitOutput()
    {
        var selected = _folderPickerService.PickFolder("Select split output folder");
        if (!string.IsNullOrWhiteSpace(selected))
            SplitOutputDirectory = selected;
    }

    private async Task SplitAsync()
    {
        if (string.IsNullOrWhiteSpace(SplitInputFilePath) || !File.Exists(SplitInputFilePath))
        {
            StatusMessage = "Please select a valid source file.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SplitOutputDirectory))
        {
            StatusMessage = "Please select an output directory.";
            return;
        }

        _operationCts = new CancellationTokenSource();
        IsBusy = true;
        ProgressPercent = 0;
        StatusMessage = "Splitting file...";

        try
        {
            var progress = new Progress<FileSplitJoinProgress>(p =>
            {
                ProgressPercent = p.TotalBytes > 0 ? p.ProcessedBytes * 100d / p.TotalBytes : 0;
                StatusMessage = $"Splitting part {p.CurrentPart}/{p.TotalParts} ({ProgressPercent:F1}%)";
            });

            var result = await _fileSplitJoinService.SplitAsync(
                SplitInputFilePath,
                SplitOutputDirectory,
                ChunkSizeMb * 1024L * 1024L,
                _operationCts.Token,
                progress);

            StatusMessage = $"Split complete. {result.PartFiles.Count} parts created. SHA-256: {result.Sha256}";
            AddRecent(result.ManifestPath);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Split operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Split failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _operationCts?.Dispose();
            _operationCts = null;
        }
    }

    private void BrowseJoinPartFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select any part file",
            Filter = "Part Files (*.part*)|*.part*|All Files (*.*)|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            JoinPartFilePath = dialog.FileName;
            if (string.IsNullOrWhiteSpace(JoinOutputFilePath))
            {
                var baseName = Path.GetFileNameWithoutExtension(dialog.FileName);
                JoinOutputFilePath = Path.Combine(Path.GetDirectoryName(dialog.FileName) ?? string.Empty, $"{baseName}.joined");
            }
        }
    }

    private void BrowseJoinOutput()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Select joined output file",
            Filter = "All Files (*.*)|*.*",
            FileName = Path.GetFileName(JoinOutputFilePath),
            InitialDirectory = string.IsNullOrWhiteSpace(JoinOutputFilePath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                : Path.GetDirectoryName(JoinOutputFilePath),
        };

        if (dialog.ShowDialog() == true)
            JoinOutputFilePath = dialog.FileName;
    }

    private async Task JoinAsync()
    {
        if (string.IsNullOrWhiteSpace(JoinPartFilePath) || !File.Exists(JoinPartFilePath))
        {
            StatusMessage = "Please select a valid part file.";
            return;
        }

        if (string.IsNullOrWhiteSpace(JoinOutputFilePath))
        {
            StatusMessage = "Please select an output file path.";
            return;
        }

        _operationCts = new CancellationTokenSource();
        IsBusy = true;
        ProgressPercent = 0;
        StatusMessage = "Joining parts...";

        try
        {
            var progress = new Progress<FileSplitJoinProgress>(p =>
            {
                ProgressPercent = p.TotalBytes > 0 ? p.ProcessedBytes * 100d / p.TotalBytes : 0;
                StatusMessage = $"Joining part {p.CurrentPart}/{p.TotalParts} ({ProgressPercent:F1}%)";
            });

            var result = await _fileSplitJoinService.JoinAsync(
                JoinPartFilePath,
                JoinOutputFilePath,
                VerifyChecksum,
                _operationCts.Token,
                progress);

            StatusMessage = result.ChecksumVerified
                ? $"Join complete. Checksum verified. SHA-256: {result.Sha256}"
                : $"Join complete. SHA-256: {result.Sha256}";
            AddRecent(result.OutputPath);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Join operation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Join failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _operationCts?.Dispose();
            _operationCts = null;
        }
    }

    private void AddRecent(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (RecentOutputFiles.Contains(path))
            RecentOutputFiles.Remove(path);

        RecentOutputFiles.Insert(0, path);
        while (RecentOutputFiles.Count > 5)
            RecentOutputFiles.RemoveAt(RecentOutputFiles.Count - 1);
    }
}
