using System.Collections.Specialized;
using System.ComponentModel;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader;

public sealed partial class DownloaderViewModel
{
    private void SetAssetSelection(Func<bool, bool> selector)
    {
        foreach (var asset in DiscoveredAssets)
        {
            asset.IsSelected = selector(asset.IsSelected);
        }

        RaiseDiscoverySummaryChanged();
    }

    private void SetVisibleAssetSelection(bool selected)
    {
        foreach (var item in DiscoveredAssetsView)
        {
            if (item is DownloadAssetCandidate asset)
            {
                asset.IsSelected = selected;
            }
        }

        RaiseDiscoverySummaryChanged();
    }

    private void SelectReachableAssets()
    {
        foreach (var item in DiscoveredAssetsView)
        {
            if (item is DownloadAssetCandidate asset)
            {
                asset.IsSelected = asset.IsReachable;
            }
        }

        RaiseDiscoverySummaryChanged();
    }

    private void ClearDiscoveredAssets()
    {
        DiscoveredAssets.Clear();
        DiscoveredAssetsView.Refresh();
        RaiseDiscoverySummaryChanged();
        ScanStatus = "Discovery staging cleared.";
    }

    private bool FilterAsset(object obj)
    {
        if (obj is not DownloadAssetCandidate asset)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(AssetSearchText)
            && !asset.Name.Contains(AssetSearchText, StringComparison.OrdinalIgnoreCase)
            && !asset.Url.Contains(AssetSearchText, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return SelectedAssetFilter switch
        {
            AssetFilterType.Images => asset.TypeLabel.Equals("Image", StringComparison.OrdinalIgnoreCase),
            AssetFilterType.Video => asset.TypeLabel.Equals("Video", StringComparison.OrdinalIgnoreCase),
            AssetFilterType.Audio => asset.TypeLabel.Equals("Audio", StringComparison.OrdinalIgnoreCase),
            AssetFilterType.Archives => asset.TypeLabel.Equals("Archive", StringComparison.OrdinalIgnoreCase),
            AssetFilterType.Documents => asset.TypeLabel is "Document" or "Spreadsheet" or "Presentation",
            AssetFilterType.Executables => asset.TypeLabel.Equals("Executable", StringComparison.OrdinalIgnoreCase),
            AssetFilterType.CodeTextData => asset.TypeLabel is "Code" or "Text" or "Database",
            AssetFilterType.Fonts => asset.TypeLabel.Equals("Font", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
    }

    private bool FilterJob(object obj)
    {
        if (obj is not DownloadJob job)
        {
            return false;
        }

        return DownloaderJobFilterMatcher.Matches(job, QueueSearchText, SelectedQueueStatusFilter);
    }

    private IEnumerable<DownloadJob> GetSelection() => SelectedJobs.Count > 0 ? SelectedJobs : SelectedJob is null ? [] : [SelectedJob];

    private void OnJobsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is DownloadJob oldJob)
                {
                    oldJob.PropertyChanged -= OnJobPropertyChanged;
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is DownloadJob newJob)
                {
                    newJob.PropertyChanged += OnJobPropertyChanged;
                }
            }
        }

        RefreshStatistics();
        JobsView.Refresh();
    }

    private void OnJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DownloadJob.Status)
            or nameof(DownloadJob.DisplayTitle)
            or nameof(DownloadJob.SourceUrl)
            or nameof(DownloadJob.Category)
            or nameof(DownloadJob.EngineType)
            or nameof(DownloadJob.EffectivePlan))
        {
            RefreshStatistics();
            JobsView.Refresh();
        }
    }

    private void OnEventRecorded(object? sender, DownloadEventRecord eventRecord)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.Invoke(() =>
        {
            RecentEvents.Insert(0, eventRecord);
            const int MaxEvents = 250;
            var excess = RecentEvents.Count - MaxEvents;
            for (var i = 0; i < excess; i++)
            {
                RecentEvents.RemoveAt(RecentEvents.Count - 1);
            }

            StatusMessage = eventRecord.Message;
        });
    }

    private void OnDiscoveredAssetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is DownloadAssetCandidate oldAsset)
                {
                    oldAsset.PropertyChanged -= OnDiscoveredAssetPropertyChanged;
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is DownloadAssetCandidate newAsset)
                {
                    newAsset.PropertyChanged += OnDiscoveredAssetPropertyChanged;
                }
            }
        }

        RaiseDiscoverySummaryChanged();
    }

    private void OnDiscoveredAssetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadAssetCandidate.IsSelected)
            || e.PropertyName == nameof(DownloadAssetCandidate.IsReachable))
        {
            RaiseDiscoverySummaryChanged();
        }
    }

    private void RaiseDiscoverySummaryChanged()
    {
        OnPropertyChanged(nameof(DiscoveredAssetCount));
        OnPropertyChanged(nameof(SelectedAssetCount));
        OnPropertyChanged(nameof(ReachableAssetCount));
        OnPropertyChanged(nameof(DiscoverySummary));
    }

    private void RefreshStatistics()
    {
        _coordinator.RecomputeStatistics();
        OnPropertyChanged(nameof(QueuedCount));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(PausedCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(IsQueueRunning));
    }
}
