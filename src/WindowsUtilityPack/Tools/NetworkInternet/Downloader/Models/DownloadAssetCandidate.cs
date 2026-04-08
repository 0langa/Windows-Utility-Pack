using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

/// <summary>Selectable asset candidate discovered from a page scan or crawl run.</summary>
public sealed class DownloadAssetCandidate : ViewModelBase
{
    private bool _isSelected = true;
    private bool _isReachable = true;
    private string _warning = string.Empty;
    private long? _sizeBytes;

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public string SourcePage { get; set; } = string.Empty;

    public string PackageTitle { get; set; } = string.Empty;

    public string TypeLabel { get; set; } = "Other";

    public long? SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (SetProperty(ref _sizeBytes, value))
            {
                OnPropertyChanged(nameof(SizeLabel));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsReachable
    {
        get => _isReachable;
        set => SetProperty(ref _isReachable, value);
    }

    public string Warning
    {
        get => _warning;
        set => SetProperty(ref _warning, value ?? string.Empty);
    }

    public string SizeLabel => SizeBytes is > 0
        ? FormatBytes(SizeBytes.Value)
        : "Unknown";

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (1024d * 1024d * 1024d):F2} GB",
        >= 1L << 20 => $"{bytes / (1024d * 1024d):F1} MB",
        >= 1L << 10 => $"{bytes / 1024d:F1} KB",
        _ => $"{bytes} B",
    };
}
