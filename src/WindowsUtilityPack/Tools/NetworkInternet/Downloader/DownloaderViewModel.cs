using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Data;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader;

/// <summary>Represents one download entry in the downloads list.</summary>
public class DownloadItem : ViewModelBase
{
    private double _progress;
    private string _status = "Queued";
    private string _speed = string.Empty;
    private string _engine = string.Empty;
    private string _title = string.Empty;
    private string _selectedFormat = "Best (auto)";
    private string _eta = string.Empty;

    /// <summary>Display file name.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Source URL.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Target save directory or file path.</summary>
    public string SavePath { get; init; } = string.Empty;

    /// <summary>Formatted total size string.</summary>
    public string TotalSize { get; set; } = "—";

    /// <summary>Download progress 0–100.</summary>
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    /// <summary>Current status text (Queued, Downloading, Complete, Failed, Cancelled, etc.).</summary>
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>Formatted download speed (e.g. "2.4 MB/s").</summary>
    public string Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    /// <summary>Detected download engine: "yt-dlp", "gallery-dl", or "Scraper".</summary>
    public string Engine
    {
        get => _engine;
        set => SetProperty(ref _engine, value);
    }

    /// <summary>Title resolved from the URL (e.g. video title or hostname).</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>Selected format label for yt-dlp downloads.</summary>
    public string SelectedFormat
    {
        get => _selectedFormat;
        set => SetProperty(ref _selectedFormat, value);
    }

    /// <summary>Estimated time remaining.</summary>
    public string Eta
    {
        get => _eta;
        set => SetProperty(ref _eta, value);
    }

    /// <summary>Cancellation source for this individual download.</summary>
    public CancellationTokenSource Cts { get; } = new();
}

/// <summary>
/// ViewModel for the Downloader tool.
/// Provides URL-based file downloading with progress tracking,
/// engine detection (yt-dlp, gallery-dl, scraper), and asset selection.
/// </summary>
public class DownloaderViewModel : ViewModelBase
{
    private static readonly HttpClient SharedClient = new();

    private readonly IFolderPickerService _folderPicker;
    private readonly IDependencyManagerService _depManager;
    private readonly IDownloadEngineService _engine;
    private readonly IWebScraperService _scraper;

    private string _url = string.Empty;
    private string _saveFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";
    private bool _isDownloading;
    private CancellationTokenSource? _cts;

    // Settings
    private bool _crawlSubdirectories;
    private int _maxDepth = 2;
    private int _maxPages = 50;
    private int _maxConcurrentDownloads = 2;
    private bool _autoStartOnAdd;
    private string _selectedFormat = "Best (auto)";
    private bool _showSettings;

    // Dependency status
    private bool _dependenciesReady;
    private string _statusMessage = string.Empty;

    // Scraper panel
    private bool _showScraperPanel;
    private string _filterText = string.Empty;
    private string _selectedTypeFilter = "All";
    private DownloadItem? _pendingScrapeItem;
    private bool _isScanning;
    private string _scanStatusMessage = string.Empty;

    /// <summary>URL(s) to download (supports multi-line).</summary>
    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    /// <summary>Folder where downloads are saved.</summary>
    public string SaveFolder
    {
        get => _saveFolder;
        set => SetProperty(ref _saveFolder, value);
    }

    /// <summary>True while any download is in progress.</summary>
    public bool IsDownloading
    {
        get => _isDownloading;
        set => SetProperty(ref _isDownloading, value);
    }

    /// <summary>Whether to crawl sub-pages on the same host.</summary>
    public bool CrawlSubdirectories
    {
        get => _crawlSubdirectories;
        set => SetProperty(ref _crawlSubdirectories, value);
    }

    /// <summary>Maximum crawl depth (1–10).</summary>
    public int MaxDepth
    {
        get => _maxDepth;
        set => SetProperty(ref _maxDepth, Math.Clamp(value, 1, 10));
    }

    /// <summary>Maximum pages to crawl (1–500).</summary>
    public int MaxPages
    {
        get => _maxPages;
        set => SetProperty(ref _maxPages, Math.Clamp(value, 1, 500));
    }

    /// <summary>Maximum concurrent downloads (1–8).</summary>
    public int MaxConcurrentDownloads
    {
        get => _maxConcurrentDownloads;
        set => SetProperty(ref _maxConcurrentDownloads, Math.Clamp(value, 1, 8));
    }

    /// <summary>Automatically start downloads when items are added.</summary>
    public bool AutoStartOnAdd
    {
        get => _autoStartOnAdd;
        set => SetProperty(ref _autoStartOnAdd, value);
    }

    /// <summary>Selected video format label.</summary>
    public string SelectedFormat
    {
        get => _selectedFormat;
        set => SetProperty(ref _selectedFormat, value);
    }

    /// <summary>Whether the settings panel is expanded.</summary>
    public bool ShowSettings
    {
        get => _showSettings;
        set => SetProperty(ref _showSettings, value);
    }

    /// <summary>Whether all external tool dependencies are installed.</summary>
    public bool DependenciesReady
    {
        get => _dependenciesReady;
        set => SetProperty(ref _dependenciesReady, value);
    }

    /// <summary>General status or log message displayed in the tools bar.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Whether the scraped-assets selection panel is visible.</summary>
    public bool ShowScraperPanel
    {
        get => _showScraperPanel;
        set => SetProperty(ref _showScraperPanel, value);
    }

    /// <summary>Whether a page scan is currently in progress.</summary>
    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    /// <summary>Status message shown during scanning (e.g. "Scanning... 3 pages, 42 assets").</summary>
    public string ScanStatusMessage
    {
        get => _scanStatusMessage;
        set => SetProperty(ref _scanStatusMessage, value);
    }

    /// <summary>Filter text applied to the scraped assets list.</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                ScrapedAssetsView.Refresh();
            }
        }
    }

    /// <summary>Type filter for scraped assets ("All" or an asset type name).</summary>
    public string SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
            {
                ScrapedAssetsView.Refresh();
            }
        }
    }

    /// <summary>Available video format labels derived from the engine.</summary>
    public IReadOnlyList<string> FormatLabels { get; }

    /// <summary>List of all download items.</summary>
    public ObservableCollection<DownloadItem> Downloads { get; } = [];

    /// <summary>Raw scraped assets from the scraper engine.</summary>
    public ObservableCollection<ScrapedAsset> ScrapedAssets { get; } = [];

    /// <summary>Filtered view over <see cref="ScrapedAssets"/>.</summary>
    public ICollectionView ScrapedAssetsView { get; }

    // Existing commands
    /// <summary>Adds URL(s) to the queue and detects engines.</summary>
    public AsyncRelayCommand DownloadCommand { get; }

    /// <summary>Opens a folder picker to choose the save directory.</summary>
    public RelayCommand BrowseFolderCommand { get; }

    /// <summary>Cancels all active downloads.</summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>Removes completed, failed, and cancelled items from the list.</summary>
    public RelayCommand ClearCompletedCommand { get; }

    // New commands
    /// <summary>Downloads and installs missing external tools.</summary>
    public AsyncRelayCommand InstallToolsCommand { get; }

    /// <summary>Updates yt-dlp to the latest version.</summary>
    public AsyncRelayCommand UpdateYtDlpCommand { get; }

    /// <summary>Starts all queued items with bounded concurrency.</summary>
    public AsyncRelayCommand StartAllCommand { get; }

    /// <summary>Re-queues a failed download item.</summary>
    public RelayCommand RetryItemCommand { get; }

    /// <summary>Opens the save folder in Explorer.</summary>
    public RelayCommand OpenFolderCommand { get; }

    /// <summary>Opens a completed item's file location.</summary>
    public RelayCommand OpenFileCommand { get; }

    /// <summary>Cancels a single download item.</summary>
    public RelayCommand CancelItemCommand { get; }

    /// <summary>Selects all scraped assets.</summary>
    public RelayCommand SelectAllScrapedCommand { get; }

    /// <summary>Deselects all scraped assets.</summary>
    public RelayCommand SelectNoneScrapedCommand { get; }

    /// <summary>Downloads all selected scraped assets.</summary>
    public AsyncRelayCommand DownloadScrapedCommand { get; }

    /// <summary>Scans the current URL for all downloadable assets without starting a download.</summary>
    public AsyncRelayCommand ScanPageCommand { get; }

    /// <summary>Closes the scraper panel.</summary>
    public RelayCommand CloseScanPanelCommand { get; }

    /// <summary>Toggles the settings panel visibility.</summary>
    public RelayCommand ToggleSettingsCommand { get; }

    /// <summary>
    /// Initialises a new <see cref="DownloaderViewModel"/>.
    /// </summary>
    /// <param name="folderPicker">Folder picker service for browse dialogs.</param>
    /// <param name="depManager">Manages external tool dependencies.</param>
    /// <param name="engine">Download engine orchestrator.</param>
    /// <param name="scraper">Web scraper for discovering downloadable assets on pages.</param>
    public DownloaderViewModel(
        IFolderPickerService folderPicker,
        IDependencyManagerService depManager,
        IDownloadEngineService engine,
        IWebScraperService scraper)
    {
        _folderPicker = folderPicker;
        _depManager = depManager;
        _engine = engine;
        _scraper = scraper;

        FormatLabels = engine.VideoFormats.Select(f => f.Label).ToList();

        ScrapedAssetsView = CollectionViewSource.GetDefaultView(ScrapedAssets);
        ScrapedAssetsView.Filter = FilterScrapedAsset;

        DownloadCommand = new AsyncRelayCommand(_ => AddUrlsAsync(), _ => !string.IsNullOrWhiteSpace(Url));
        BrowseFolderCommand = new RelayCommand(_ =>
        {
            var path = _folderPicker.PickFolder("Select download folder");
            if (!string.IsNullOrEmpty(path))
            {
                SaveFolder = path;
            }
        });
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsDownloading);
        ClearCompletedCommand = new RelayCommand(_ =>
        {
            for (var i = Downloads.Count - 1; i >= 0; i--)
            {
                if (Downloads[i].Status is "Complete" or "Failed" or "Cancelled")
                {
                    Downloads.RemoveAt(i);
                }
            }
        });

        InstallToolsCommand = new AsyncRelayCommand(_ => InstallToolsAsync());
        UpdateYtDlpCommand = new AsyncRelayCommand(_ => UpdateYtDlpAsync());
        StartAllCommand = new AsyncRelayCommand(_ => StartAllAsync());
        ToggleSettingsCommand = new RelayCommand(_ => ShowSettings = !ShowSettings);

        RetryItemCommand = new RelayCommand(param =>
        {
            if (param is DownloadItem item && item.Status == "Failed")
            {
                item.Status = "Queued";
                item.Progress = 0;
                item.Speed = string.Empty;
            }
        });

        OpenFolderCommand = new RelayCommand(_ =>
        {
            if (Directory.Exists(SaveFolder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = SaveFolder,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });
            }
        });

        OpenFileCommand = new RelayCommand(param =>
        {
            if (param is DownloadItem item && item.Status == "Complete" && Directory.Exists(item.SavePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = item.SavePath,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });
            }
        });

        CancelItemCommand = new RelayCommand(param =>
        {
            if (param is DownloadItem item)
            {
                item.Cts.Cancel();
                item.Status = "Cancelled";
            }
        });

        SelectAllScrapedCommand = new RelayCommand(_ =>
        {
            foreach (var asset in ScrapedAssets)
            {
                asset.IsSelected = true;
            }

            ScrapedAssetsView.Refresh();
        });

        SelectNoneScrapedCommand = new RelayCommand(_ =>
        {
            foreach (var asset in ScrapedAssets)
            {
                asset.IsSelected = false;
            }

            ScrapedAssetsView.Refresh();
        });

        DownloadScrapedCommand = new AsyncRelayCommand(_ => DownloadScrapedAsync());

        ScanPageCommand = new AsyncRelayCommand(_ => ScanPageAsync(), _ => !string.IsNullOrWhiteSpace(Url) && !IsScanning);
        CloseScanPanelCommand = new RelayCommand(_ =>
        {
            ShowScraperPanel = false;
            ScrapedAssets.Clear();
            ScanStatusMessage = string.Empty;
        });

        _ = CheckDepsAsync();
    }

    private async Task CheckDepsAsync()
    {
        try
        {
            var status = _depManager.Check();
            DependenciesReady = status.AllOk;
            StatusMessage = status.AllOk
                ? "All tools ready."
                : "Some tools are missing. Click Install Tools.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Dependency check failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task InstallToolsAsync()
    {
        StatusMessage = "Installing tools...";
        try
        {
            await _depManager.EnsureAllAsync(msg =>
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = msg);
            });

            DependenciesReady = _depManager.Check().AllOk;
            StatusMessage = "All tools installed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Install failed: {ex.Message}";
        }
    }

    private async Task UpdateYtDlpAsync()
    {
        StatusMessage = "Updating yt-dlp...";
        try
        {
            var result = await _depManager.UpdateYtDlpAsync();
            StatusMessage = result;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Update failed: {ex.Message}";
        }
    }

    private async Task AddUrlsAsync()
    {
        var rawText = Url;
        var lines = rawText.Split(['\r', '\n', ' '], StringSplitOptions.RemoveEmptyEntries);
        var existingUrls = new HashSet<string>(
            Downloads.Select(d => d.Url), StringComparer.OrdinalIgnoreCase);

        var newUrls = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out _) && existingUrls.Add(trimmed))
            {
                newUrls.Add(trimmed);
            }
        }

        Url = string.Empty;

        foreach (var url in newUrls)
        {
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "download";
            }

            var item = new DownloadItem
            {
                FileName = fileName,
                Url = url,
                SavePath = SaveFolder,
                SelectedFormat = SelectedFormat,
            };

            Downloads.Insert(0, item);

            try
            {
                await _engine.DetectEngineAsync(item, item.Cts.Token);
            }
            catch (Exception ex)
            {
                item.Status = "Failed";
                item.Speed = ex.Message;
            }
        }

        if (AutoStartOnAdd)
        {
            await StartAllAsync();
        }
    }

    private async Task StartAllAsync()
    {
        IsDownloading = true;
        _cts = new CancellationTokenSource();

        var semaphore = new SemaphoreSlim(MaxConcurrentDownloads);
        var tasks = new List<Task>();

        foreach (var item in Downloads.Where(d => d.Status == "Queued").ToList())
        {
            await semaphore.WaitAsync(_cts.Token);

            var task = Task.Run(async () =>
            {
                try
                {
                    var result = await _engine.DownloadAsync(item, item.Cts.Token);

                    if (result is not null)
                    {
                        // Scraper returned assets — show selection panel on UI thread.
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ScrapedAssets.Clear();
                            foreach (var asset in result)
                            {
                                ScrapedAssets.Add(asset);
                            }

                            _pendingScrapeItem = item;
                            ShowScraperPanel = true;
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    item.Status = "Cancelled";
                }
                catch (Exception ex)
                {
                    item.Status = "Failed";
                    item.Speed = ex.Message;
                }
                finally
                {
                    semaphore.Release();
                }
            }, _cts.Token);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        IsDownloading = false;
        _cts.Dispose();
        _cts = null;
    }

    private async Task DownloadScrapedAsync()
    {
        ShowScraperPanel = false;
        var selected = ScrapedAssets.Where(a => a.IsSelected).ToList();

        if (selected.Count == 0)
        {
            return;
        }

        if (_pendingScrapeItem is not null)
        {
            _pendingScrapeItem.Status = "Downloading";
        }

        try
        {
            await _engine.DownloadScrapedAssetsAsync(
                selected,
                SaveFolder,
                CrawlSubdirectories,
                MaxDepth,
                MaxPages,
                progress =>
                {
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (_pendingScrapeItem is not null)
                        {
                            _pendingScrapeItem.Progress = progress;
                        }
                    });
                });

            if (_pendingScrapeItem is not null)
            {
                _pendingScrapeItem.Progress = 100;
                _pendingScrapeItem.Status = "Complete";
            }
        }
        catch (OperationCanceledException)
        {
            if (_pendingScrapeItem is not null)
            {
                _pendingScrapeItem.Status = "Cancelled";
            }
        }
        catch (Exception ex)
        {
            if (_pendingScrapeItem is not null)
            {
                _pendingScrapeItem.Status = "Failed";
                _pendingScrapeItem.Speed = ex.Message;
            }
        }
        finally
        {
            ScrapedAssets.Clear();
            _pendingScrapeItem = null;
        }
    }

    private bool FilterScrapedAsset(object obj)
    {
        if (obj is not ScrapedAsset asset)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(SelectedTypeFilter)
            && SelectedTypeFilter != "All"
            && !asset.TypeLabel.Equals(SelectedTypeFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(FilterText))
        {
            var filter = FilterText;
            return asset.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || asset.Url.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || asset.ExtensionLabel.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private async Task ScanPageAsync()
    {
        var rawUrl = Url.Trim().Split(['\r', '\n', ' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rawUrl) || !Uri.TryCreate(rawUrl, UriKind.Absolute, out _))
        {
            StatusMessage = "Enter a valid URL to scan.";
            return;
        }

        IsScanning = true;
        ScanStatusMessage = "Scanning...";
        ShowScraperPanel = true;
        ScrapedAssets.Clear();

        try
        {
            var assets = await _scraper.ScrapeAsync(
                rawUrl,
                CrawlSubdirectories,
                MaxDepth,
                MaxPages,
                progress =>
                {
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ScanStatusMessage = $"Scanning... {progress.pagesScraped} page(s), {progress.assetsFound} asset(s) found";
                    });
                });

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ScrapedAssets.Clear();
                foreach (var asset in assets)
                {
                    ScrapedAssets.Add(asset);
                }

                ScanStatusMessage = $"Scan complete — {assets.Count} asset(s) found";
                ScrapedAssetsView.Refresh();
            });
        }
        catch (OperationCanceledException)
        {
            ScanStatusMessage = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            ScanStatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB",
        >= 1L << 20 => $"{bytes / (1024.0 * 1024.0):F1} MB",
        >= 1L << 10 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B",
    };
}
