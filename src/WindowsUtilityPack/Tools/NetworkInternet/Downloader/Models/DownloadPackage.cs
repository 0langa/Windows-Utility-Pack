using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

/// <summary>Aggregates a group of related download jobs.</summary>
public sealed class DownloadPackage : ViewModelBase
{
    private string _title = string.Empty;
    private string _outputFolder = string.Empty;
    private int _assetCount;
    private int _completedCount;
    private double _progressPercent;

    public string PackageId { get; init; } = Guid.NewGuid().ToString("N");

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value ?? string.Empty);
    }

    public string OutputFolder
    {
        get => _outputFolder;
        set => SetProperty(ref _outputFolder, value ?? string.Empty);
    }

    public int AssetCount
    {
        get => _assetCount;
        set => SetProperty(ref _assetCount, Math.Max(0, value));
    }

    public int CompletedCount
    {
        get => _completedCount;
        set => SetProperty(ref _completedCount, Math.Clamp(value, 0, AssetCount));
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, Math.Clamp(value, 0, 100));
    }
}
