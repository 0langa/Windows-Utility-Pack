using System.Collections.ObjectModel;
using System.IO;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.DiskInfo;

/// <summary>Represents a single physical or logical drive displayed in the Disk Info list.</summary>
public class DriveInfoItem
{
    public string Name       { get; init; } = string.Empty;
    public string DriveType  { get; init; } = string.Empty;
    public string Format     { get; init; } = string.Empty;
    public double TotalSizeGb  { get; init; }
    public double FreeSpaceGb  { get; init; }

    /// <summary>Calculated: total minus free space.</summary>
    public double UsedSpaceGb  => TotalSizeGb - FreeSpaceGb;

    /// <summary>Calculated: percentage of space used (0–100).  Returns 0 for empty/zero-size drives.</summary>
    public double UsedPercent  => TotalSizeGb > 0 ? (UsedSpaceGb / TotalSizeGb) * 100 : 0;

    public string Label        { get; init; } = string.Empty;

    /// <summary>
    /// Friendly display label: "Volume Label (Drive Letter)" or just the drive letter
    /// if no volume label is set.
    /// </summary>
    public string DisplayLabel => string.IsNullOrEmpty(Label) ? Name : $"{Label} ({Name})";
}

/// <summary>
/// ViewModel for the Disk Info Viewer tool.
/// Loads all ready drives on construction and exposes a refresh command.
/// Drives are presented as a flat list with a usage progress bar in the View.
/// </summary>
public class DiskInfoViewModel : ViewModelBase
{
    private bool _isLoading;

    /// <summary>The list of ready drives, updated by <see cref="RefreshCommand"/>.</summary>
    public ObservableCollection<DriveInfoItem> Drives { get; } = [];

    /// <summary>Reloads all drive information asynchronously.</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>True while the drive list is being loaded (used to show a loading indicator).</summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public DiskInfoViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(_ => LoadDrivesAsync());
        // Load drives immediately when the ViewModel is first created.
        _ = LoadDrivesAsync();
    }

    /// <summary>
    /// Enumerates all ready drives via <see cref="DriveInfo.GetDrives()"/> and
    /// populates the <see cref="Drives"/> collection.
    /// Runs synchronously on the UI thread (drive enumeration is fast);
    /// wrapped in a Task for consistency with the async command pattern.
    /// </summary>
    private Task LoadDrivesAsync()
    {
        IsLoading = true;
        Drives.Clear();
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                Drives.Add(new DriveInfoItem
                {
                    Name        = drive.Name,
                    Label       = drive.VolumeLabel,
                    DriveType   = drive.DriveType.ToString(),
                    Format      = drive.DriveFormat,
                    TotalSizeGb = Math.Round(drive.TotalSize          / 1_073_741_824.0, 1),
                    FreeSpaceGb = Math.Round(drive.AvailableFreeSpace / 1_073_741_824.0, 1),
                });
            }
        }
        finally
        {
            IsLoading = false;
        }
        return Task.CompletedTask;
    }
}
