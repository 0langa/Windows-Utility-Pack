using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.FileDataTools.BulkFileRenamer;

public class RenamePreviewItem
{
    public string OriginalName { get; init; } = string.Empty;
    public string NewName { get; init; } = string.Empty;
    public bool HasConflict { get; init; }
}

public class BulkFileRenamerViewModel : ViewModelBase
{
    private string _selectedFolder = string.Empty;
    private string _prefix = string.Empty;
    private string _suffix = string.Empty;
    private string _findText = string.Empty;
    private string _replaceText = string.Empty;
    private bool _isBusy;

    public string SelectedFolder
    {
        get => _selectedFolder;
        set { if (SetProperty(ref _selectedFolder, value)) RefreshPreview(); }
    }

    public string Prefix
    {
        get => _prefix;
        set { if (SetProperty(ref _prefix, value)) RefreshPreview(); }
    }

    public string Suffix
    {
        get => _suffix;
        set { if (SetProperty(ref _suffix, value)) RefreshPreview(); }
    }

    public string FindText
    {
        get => _findText;
        set { if (SetProperty(ref _findText, value)) RefreshPreview(); }
    }

    public string ReplaceText
    {
        get => _replaceText;
        set { if (SetProperty(ref _replaceText, value)) RefreshPreview(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<RenamePreviewItem> PreviewItems { get; } = [];

    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand ApplyRenameCommand { get; }

    public BulkFileRenamerViewModel()
    {
        BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
        ApplyRenameCommand = new RelayCommand(_ => ApplyRename(),
            _ => PreviewItems.Count > 0 && !IsBusy);
    }

    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select folder to rename files in" };
        if (dialog.ShowDialog() == true)
            SelectedFolder = dialog.FolderName;
    }

    private void RefreshPreview()
    {
        PreviewItems.Clear();
        if (string.IsNullOrEmpty(SelectedFolder) || !Directory.Exists(SelectedFolder))
            return;

        var files = Directory.GetFiles(SelectedFolder);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var original = Path.GetFileNameWithoutExtension(file);
            var ext = Path.GetExtension(file);
            var modified = original;

            if (!string.IsNullOrEmpty(FindText))
                modified = modified.Replace(FindText, ReplaceText, StringComparison.Ordinal);

            var newName = Prefix + modified + Suffix + ext;
            var conflict = seen.Contains(newName) || (newName != Path.GetFileName(file) &&
                           File.Exists(Path.Combine(SelectedFolder, newName)));
            seen.Add(newName);

            PreviewItems.Add(new RenamePreviewItem
            {
                OriginalName = Path.GetFileName(file),
                NewName = newName,
                HasConflict = conflict,
            });
        }
    }

    private void ApplyRename()
    {
        if (PreviewItems.Count == 0) return;

        var conflicts = PreviewItems.Where(p => p.HasConflict).ToList();
        var msg = conflicts.Count > 0
            ? $"Rename {PreviewItems.Count} files? ({conflicts.Count} conflicts will be skipped)"
            : $"Rename {PreviewItems.Count} files?";

        if (MessageBox.Show(msg, "Confirm Rename",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        try
        {
            foreach (var item in PreviewItems.Where(p => !p.HasConflict && p.OriginalName != p.NewName))
            {
                var src = Path.Combine(SelectedFolder, item.OriginalName);
                var dst = Path.Combine(SelectedFolder, item.NewName);
                if (File.Exists(src) && !File.Exists(dst))
                    File.Move(src, dst);
            }
            RefreshPreview();
            MessageBox.Show("Files renamed successfully.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error renaming files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
