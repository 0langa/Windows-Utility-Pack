using System.Collections.ObjectModel;
using System.IO;
using WindowsUtilityPack.Services.FileTools;
using System.Windows;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.FileDataTools.SecureFileShredder;

public class ShredderFileEntry : ViewModelBase
{
    private string _filePath    = string.Empty;
    private string _fileName    = string.Empty;
    private string _sizeDisplay = string.Empty;
    private string _status      = "Queued";

    public string FilePath    { get => _filePath;    set => SetProperty(ref _filePath, value); }
    public string FileName    { get => _fileName;    set => SetProperty(ref _fileName, value); }
    public string SizeDisplay { get => _sizeDisplay; set => SetProperty(ref _sizeDisplay, value); }
    public string Status      { get => _status;      set => SetProperty(ref _status, value); } // Queued, Shredding, Done, Error
}

public class SecureFileShredderViewModel : ViewModelBase, INavigationGuard
{
    private ObservableCollection<ShredderFileEntry> _files = [];
    private ShredderFileEntry? _selectedFile;
    private int    _passCount    = 3;
    private bool   _isShredding;
    private double _progress;
    private string _statusMessage = string.Empty;
    private string _currentFile   = string.Empty;

    public ObservableCollection<ShredderFileEntry> Files
    {
        get => _files;
        private set => SetProperty(ref _files, value);
    }

    public ShredderFileEntry? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public int PassCount
    {
        get => _passCount;
        set => SetProperty(ref _passCount, value);
    }

    public bool IsShredding
    {
        get => _isShredding;
        private set => SetProperty(ref _isShredding, value);
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

    public string CurrentFile
    {
        get => _currentFile;
        private set => SetProperty(ref _currentFile, value);
    }

    public AsyncRelayCommand AddFilesCommand      { get; }
    public RelayCommand      RemoveSelectedCommand { get; }
    public RelayCommand      ClearAllCommand       { get; }
    public AsyncRelayCommand ShredCommand          { get; }

    private readonly IUserDialogService _dialog;
    private CancellationTokenSource? _shredCancellation;
    public RelayCommand CancelCommand { get; }
    public bool CanNavigateAway() => !IsShredding;

    public SecureFileShredderViewModel(IUserDialogService dialog)
    {
        _dialog              = dialog;
        AddFilesCommand      = new AsyncRelayCommand(AddFilesAsync, () => !IsShredding, ex => _dialog.ShowError("Cannot add files", ex.Message));
        CancelCommand = new RelayCommand(() => _shredCancellation?.Cancel(), () => IsShredding);
        RemoveSelectedCommand = new RelayCommand(RemoveSelected, () => SelectedFile != null && !IsShredding);
        ClearAllCommand      = new RelayCommand(ClearAll,       () => Files.Count > 0 && !IsShredding);
        ShredCommand         = new AsyncRelayCommand(ShredAsync, () => Files.Count > 0 && !IsShredding);
    }

    private async Task AddFilesAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title     = "Select files to shred",
            Filter    = "All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dlg.ShowDialog() != true) return;

        await Task.Run(() =>
        {
            foreach (var path in dlg.FileNames)
            {
                var info = new FileInfo(path);
                var entry = new ShredderFileEntry
                {
                    FilePath    = path,
                    FileName    = info.Name,
                    SizeDisplay = FormatSize(info.Length),
                    Status      = "Queued"
                };

                RunOnUi(() => Files.Add(entry));
            }
        });

        StatusMessage = $"{Files.Count} file(s) queued.";
    }

    private void RemoveSelected()
    {
        if (SelectedFile != null)
        {
            Files.Remove(SelectedFile);
            SelectedFile = null;
        }
    }

    private void ClearAll()
    {
        Files.Clear();
        SelectedFile  = null;
        Progress      = 0;
        StatusMessage = string.Empty;
        CurrentFile   = string.Empty;
    }

    private async Task ShredAsync()
    {
        if (Files.Count == 0 || IsShredding) return;
        if (PassCount is < 1 or > 7) { _dialog.ShowError("Invalid passes", "Choose 1 to 7 overwrite passes."); return; }
        var passes = PassCount;

        var passLabel = PassCount switch
        {
            1 => "1-pass (Quick)",
            3 => "3-pass (Standard)",
            7 => "7-pass",
            _ => $"{PassCount}-pass"
        };

        var confirmed = _dialog.Confirm(
            "Confirm Shredding",
            $"This will overwrite and delete {Files.Count} file(s) using {passLabel} overwrite.\n\nThis action cannot be undone. SSD remnants, snapshots and backups may survive. Continue?");

        if (!confirmed) return;

        IsShredding   = true;
        Progress      = 0;
        StatusMessage = $"Shredding {Files.Count} file(s)…";

        var filesToShred = Files.ToList();
        int total        = filesToShred.Count;
        int done         = 0;
        int failed = 0;
        using var cancellation = new CancellationTokenSource();
        _shredCancellation = cancellation;

        try
        {
            await Task.Run(() =>
            {
                foreach (var entry in filesToShred)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    RunOnUi(() =>
                    {
                        entry.Status = "Shredding";
                        CurrentFile = entry.FileName;
                    });

                    try
                    {
                        FileOverwriteService.OverwriteAndDelete(entry.FilePath, passes, cancellation.Token);

                        RunOnUi(() =>
                        {
                            entry.Status = "Done";
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        RunOnUi(() => entry.Status = "Canceled; file may be partially overwritten" );
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        RunOnUi(() =>
                        {
                            entry.Status = $"Error: {ex.Message}";
                        });
                    }

                    done++;
                    var pct = done / (double)total * 100.0;
                    RunOnUi(() => Progress = pct);
                }
            });

            StatusMessage = $"Deleted {done - failed}/{total} file(s); {failed} failed. See each file for details.";
            CurrentFile   = string.Empty;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Canceled. Deleted {done - failed}/{total} files; the current file may be partially overwritten.";
        }
        finally
        {
            _shredCancellation = null;
            CurrentFile = string.Empty;
            IsShredding = false;
        }
    }

    private static void ShredFile(string path, int passes) => FileOverwriteService.OverwriteAndDelete(path, passes);

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) { action(); return; }
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)           return $"{bytes} B";
        if (bytes < 1024 * 1024)   return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
