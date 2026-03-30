using System.Collections.ObjectModel;
using System.IO;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.FileDataTools.BulkFileRenamer;

/// <summary>Represents a single file in the rename preview list.</summary>
public class RenamePreviewItem
{
    public string OriginalName { get; init; } = string.Empty;
    public string NewName      { get; init; } = string.Empty;

    /// <summary>
    /// True when the new name would clash with an existing file or with another
    /// rename target in the same batch.  Conflicting items are skipped on apply.
    /// </summary>
    public bool HasConflict    { get; init; }
}

/// <summary>
/// ViewModel for the Bulk File Renamer tool.
///
/// Workflow:
/// <list type="number">
///   <item>User selects a folder via <see cref="BrowseFolderCommand"/>.</item>
///   <item><see cref="RefreshPreview"/> builds a live preview of the renamed files
///         based on prefix, suffix, and find/replace settings.</item>
///   <item>User reviews the preview list, then clicks Apply (<see cref="ApplyRenameCommand"/>).</item>
///   <item>Files without conflicts are renamed; conflicts are skipped with a summary shown.</item>
/// </list>
///
/// The preview refreshes automatically whenever any setting property changes,
/// so the user always sees an up-to-date before/after comparison.
/// </summary>
public class BulkFileRenamerViewModel : ViewModelBase
{
    private readonly IFolderPickerService _folderPicker;
    private readonly IUserDialogService   _dialogs;

    private string _selectedFolder = string.Empty;
    private string _prefix         = string.Empty;
    private string _suffix         = string.Empty;
    private string _findText       = string.Empty;
    private string _replaceText    = string.Empty;
    private bool   _isBusy;

    /// <summary>Path of the folder whose files will be renamed.</summary>
    public string SelectedFolder
    {
        get => _selectedFolder;
        set { if (SetProperty(ref _selectedFolder, value)) RefreshPreview(); }
    }

    /// <summary>Text prepended to every filename (excluding extension).</summary>
    public string Prefix
    {
        get => _prefix;
        set { if (SetProperty(ref _prefix, value)) RefreshPreview(); }
    }

    /// <summary>Text appended to every filename (before the extension).</summary>
    public string Suffix
    {
        get => _suffix;
        set { if (SetProperty(ref _suffix, value)) RefreshPreview(); }
    }

    /// <summary>Text to search for and replace within each filename.</summary>
    public string FindText
    {
        get => _findText;
        set { if (SetProperty(ref _findText, value)) RefreshPreview(); }
    }

    /// <summary>Replacement text for <see cref="FindText"/> occurrences.</summary>
    public string ReplaceText
    {
        get => _replaceText;
        set { if (SetProperty(ref _replaceText, value)) RefreshPreview(); }
    }

    /// <summary>True while files are being renamed (disables the Apply button).</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Live preview of the rename operation (original → new name pairs).</summary>
    public ObservableCollection<RenamePreviewItem> PreviewItems { get; } = [];

    /// <summary>Opens a folder picker dialog and sets <see cref="SelectedFolder"/>.</summary>
    public RelayCommand BrowseFolderCommand { get; }

    /// <summary>
    /// Applies the rename to all non-conflicting files.
    /// Enabled only when the preview has items and no rename is in progress.
    /// </summary>
    public RelayCommand ApplyRenameCommand { get; }

    public BulkFileRenamerViewModel()
        : this(new FolderPickerService(), new UserDialogService()) { }

    /// <summary>Constructor used in tests or custom wiring with injected services.</summary>
    public BulkFileRenamerViewModel(IFolderPickerService folderPicker, IUserDialogService dialogs)
    {
        _folderPicker = folderPicker;
        _dialogs      = dialogs;
        BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
        ApplyRenameCommand  = new RelayCommand(
            _ => ApplyRename(),
            _ => PreviewItems.Count > 0 && !IsBusy);
    }

    private void BrowseFolder()
    {
        var folder = _folderPicker.PickFolder("Select folder to rename files in");
        if (folder is not null)
            SelectedFolder = folder;
    }

    /// <summary>
    /// Rebuilds <see cref="PreviewItems"/> from the files in <see cref="SelectedFolder"/>
    /// applying the current prefix, suffix, and find/replace settings.
    /// Detects name conflicts both against existing files and within the batch itself.
    /// </summary>
    private void RefreshPreview()
    {
        PreviewItems.Clear();
        if (string.IsNullOrEmpty(SelectedFolder) || !Directory.Exists(SelectedFolder))
            return;

        var files = Directory.GetFiles(SelectedFolder);
        var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var original = Path.GetFileNameWithoutExtension(file);
            var ext      = Path.GetExtension(file);
            var modified = original;

            if (!string.IsNullOrEmpty(FindText))
                modified = modified.Replace(FindText, ReplaceText, StringComparison.Ordinal);

            var newName  = Prefix + modified + Suffix + ext;

            // Strip any path-separator characters from the computed name so that
            // user-controlled inputs (Prefix, Suffix, ReplaceText) cannot inject a
            // directory traversal sequence (e.g. "../") into the target filename.
            newName = newName
                .Replace(Path.DirectorySeparatorChar, '_')
                .Replace(Path.AltDirectorySeparatorChar, '_');

            // Conflict: another item in this batch already uses the same new name,
            // or the target name already exists on disk (and we're not just keeping it).
            var conflict = seen.Contains(newName) ||
                           (newName != Path.GetFileName(file) &&
                            File.Exists(Path.Combine(SelectedFolder, newName)));
            seen.Add(newName);

            PreviewItems.Add(new RenamePreviewItem
            {
                OriginalName = Path.GetFileName(file),
                NewName      = newName,
                HasConflict  = conflict,
            });
        }
    }

    private void ApplyRename()
    {
        if (PreviewItems.Count == 0) return;

        var conflicts = PreviewItems.Where(p => p.HasConflict).ToList();
        var msg       = conflicts.Count > 0
            ? $"Rename {PreviewItems.Count} files? ({conflicts.Count} conflicts will be skipped)"
            : $"Rename {PreviewItems.Count} files?";

        if (!_dialogs.Confirm("Confirm Rename", msg))
            return;

        IsBusy = true;
        try
        {
            var resolvedFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(SelectedFolder))
                                 + Path.DirectorySeparatorChar;
            foreach (var item in PreviewItems.Where(p => !p.HasConflict && p.OriginalName != p.NewName))
            {
                var src = Path.Combine(SelectedFolder, item.OriginalName);
                var dst = Path.Combine(SelectedFolder, item.NewName);

                // Defense-in-depth: ensure the resolved destination is still inside the
                // selected folder even after any path normalisation by the OS.
                if (!Path.GetFullPath(dst).StartsWith(resolvedFolder, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (File.Exists(src) && !File.Exists(dst))
                    File.Move(src, dst);
            }
            RefreshPreview();
            _dialogs.ShowInfo("Done", "Files renamed successfully.");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Error", $"Error renaming files: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
