using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader;

/// <summary>Represents one download entry in the downloads list.</summary>
public class DownloadItem : ViewModelBase
{
    private double _progress;
    private string _status = "Queued";
    private string _speed = string.Empty;

    public string FileName { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string SavePath { get; init; } = string.Empty;
    public string TotalSize { get; set; } = "—";

    /// <summary>Download progress 0–100.</summary>
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    /// <summary>Current status text (Queued, Downloading, Complete, Failed).</summary>
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
}

/// <summary>
/// ViewModel for the Downloader tool.
/// Provides URL-based file downloading with progress tracking.
/// </summary>
public class DownloaderViewModel : ViewModelBase
{
    private static readonly HttpClient SharedClient = new();

    private readonly IFolderPickerService _folderPicker;

    private string _url = string.Empty;
    private string _saveFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";
    private bool _isDownloading;
    private CancellationTokenSource? _cts;

    /// <summary>URL to download.</summary>
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

    /// <summary>List of all download items.</summary>
    public ObservableCollection<DownloadItem> Downloads { get; } = [];

    public AsyncRelayCommand DownloadCommand { get; }
    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ClearCompletedCommand { get; }

    public DownloaderViewModel(IFolderPickerService folderPicker)
    {
        _folderPicker = folderPicker;

        DownloadCommand = new AsyncRelayCommand(_ => StartDownloadAsync(), _ => !string.IsNullOrWhiteSpace(Url));
        BrowseFolderCommand = new RelayCommand(_ =>
        {
            var path = _folderPicker.PickFolder("Select download folder");
            if (!string.IsNullOrEmpty(path))
                SaveFolder = path;
        });
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsDownloading);
        ClearCompletedCommand = new RelayCommand(_ =>
        {
            for (var i = Downloads.Count - 1; i >= 0; i--)
            {
                if (Downloads[i].Status is "Complete" or "Failed" or "Cancelled")
                    Downloads.RemoveAt(i);
            }
        });
    }

    private async Task StartDownloadAsync()
    {
        var url = Url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "download";

        var savePath = Path.Combine(SaveFolder, fileName);

        var item = new DownloadItem
        {
            FileName = fileName,
            Url = url,
            SavePath = savePath,
        };

        Downloads.Insert(0, item);
        Url = string.Empty;
        IsDownloading = true;
        _cts = new CancellationTokenSource();

        try
        {
            using var response = await SharedClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            item.TotalSize = totalBytes.HasValue ? FormatBytes(totalBytes.Value) : "Unknown";
            item.Status = "Downloading";

            await using var contentStream = await response.Content.ReadAsStreamAsync(_cts.Token);
            await using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long totalRead = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), _cts.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cts.Token);
                totalRead += bytesRead;

                if (totalBytes > 0)
                    item.Progress = (double)totalRead / totalBytes.Value * 100;

                if (sw.ElapsedMilliseconds > 500)
                {
                    item.Speed = FormatBytes((long)(totalRead / sw.Elapsed.TotalSeconds)) + "/s";
                    sw.Restart();
                    totalRead = 0;
                }
            }

            item.Progress = 100;
            item.Speed = string.Empty;
            item.Status = "Complete";
        }
        catch (OperationCanceledException)
        {
            item.Status = "Cancelled";
            item.Speed = string.Empty;
        }
        catch (Exception ex)
        {
            item.Status = "Failed";
            item.Speed = ex.Message;
        }
        finally
        {
            IsDownloading = false;
            _cts?.Dispose();
            _cts = null;
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
