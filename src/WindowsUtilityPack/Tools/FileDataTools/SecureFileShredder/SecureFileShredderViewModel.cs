using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
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

public class SecureFileShredderViewModel : ViewModelBase
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

    public SecureFileShredderViewModel(IUserDialogService dialog)
    {
        _dialog              = dialog;
        AddFilesCommand      = new AsyncRelayCommand(AddFilesAsync);
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
        if (Files.Count == 0) return;

        var passLabel = PassCount switch
        {
            1 => "1-pass (Quick)",
            3 => "3-pass (Standard)",
            7 => "7-pass (DoD 5220.22-M)",
            _ => $"{PassCount}-pass"
        };

        var confirmed = _dialog.Confirm(
            "Confirm Shredding",
            $"This will permanently destroy {Files.Count} file(s) using {passLabel} overwrite.\n\nThis action CANNOT be undone. Continue?");

        if (!confirmed) return;

        IsShredding   = true;
        Progress      = 0;
        StatusMessage = $"Shredding {Files.Count} file(s)…";

        var filesToShred = Files.ToList();
        int total        = filesToShred.Count;
        int done         = 0;

        try
        {
            await Task.Run(() =>
            {
                foreach (var entry in filesToShred)
                {
                    RunOnUi(() =>
                    {
                        entry.Status = "Shredding";
                        CurrentFile = entry.FileName;
                    });

                    try
                    {
                        ShredFile(entry.FilePath, PassCount);

                        RunOnUi(() =>
                        {
                            entry.Status = "Done";
                        });
                    }
                    catch (Exception ex)
                    {
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

            StatusMessage = $"Shredding complete. {done}/{total} file(s) processed.";
            CurrentFile   = string.Empty;
        }
        finally
        {
            IsShredding = false;
        }
    }

    private static void ShredFile(string path, int passes)
    {
        if (!File.Exists(path)) return;

        var fileInfo = new FileInfo(path);
        long length  = fileInfo.Length;

        // Rename before deletion for additional obscurity
        var activePath = path;
        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var randomName = Path.Combine(dir, Path.GetRandomFileName());
        try
        {
            File.Move(path, randomName);
            activePath = randomName;
        }
        catch
        {
            // Keep original path if rename is not possible (locked/permission/cross-volume edge case).
            activePath = path;
        }

        using (var stream = new FileStream(activePath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            for (int pass = 0; pass < passes; pass++)
            {
                stream.Seek(0, SeekOrigin.Begin);
                long remaining = length;
                const int bufSize = 65536;
                var buffer = new byte[bufSize];

                while (remaining > 0)
                {
                    int toWrite = (int)Math.Min(bufSize, remaining);
                    RandomNumberGenerator.Fill(buffer.AsSpan(0, toWrite));
                    stream.Write(buffer, 0, toWrite);
                    remaining -= toWrite;
                }

                stream.Flush();
            }

            // Final zero-out pass
            stream.Seek(0, SeekOrigin.Begin);
            var zeros = new byte[Math.Min(65536, length)];
            long rem  = length;
            while (rem > 0)
            {
                int toWrite = (int)Math.Min(zeros.Length, rem);
                stream.Write(zeros, 0, toWrite);
                rem -= toWrite;
            }

            stream.Flush();
        }

        File.Delete(activePath);
    }

    private static void RunOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
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
