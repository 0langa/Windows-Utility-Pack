using System.Collections.ObjectModel;
using System.IO;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.DiskInfo;

public class DriveInfoItem
{
    public string Name { get; init; } = string.Empty;
    public string DriveType { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
    public double TotalSizeGb { get; init; }
    public double FreeSpaceGb { get; init; }
    public double UsedSpaceGb => TotalSizeGb - FreeSpaceGb;
    public double UsedPercent => TotalSizeGb > 0 ? (UsedSpaceGb / TotalSizeGb) * 100 : 0;
    public string Label { get; init; } = string.Empty;
    public string DisplayLabel => string.IsNullOrEmpty(Label) ? Name : $"{Label} ({Name})";
}

public class DiskInfoViewModel : ViewModelBase
{
    private bool _isLoading;

    public ObservableCollection<DriveInfoItem> Drives { get; } = [];
    public AsyncRelayCommand RefreshCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public DiskInfoViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(_ => LoadDrivesAsync());
        _ = LoadDrivesAsync();
    }

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
                    Name = drive.Name,
                    Label = drive.VolumeLabel,
                    DriveType = drive.DriveType.ToString(),
                    Format = drive.DriveFormat,
                    TotalSizeGb = Math.Round(drive.TotalSize / 1_073_741_824.0, 1),
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
