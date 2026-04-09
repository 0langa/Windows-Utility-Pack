using System.Text.Json;
using System.IO;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>JSON-backed history persistence for completed and failed download jobs.</summary>
public sealed class DownloadHistoryService : IDownloadHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _historyPath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>Creates a service that stores history in the default <c>%LOCALAPPDATA%\WindowsUtilityPack</c> folder.</summary>
    public DownloadHistoryService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsUtilityPack"))
    {
    }

    /// <summary>
    /// Creates a service that stores history in <paramref name="directory"/>.
    /// Used by unit tests to isolate file I/O to a temp directory.
    /// </summary>
    public DownloadHistoryService(string directory)
    {
        _historyPath = Path.Combine(directory, "downloader-history.json");
    }

    public async Task<IReadOnlyList<DownloadHistoryEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_historyPath))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(_historyPath, cancellationToken);
            var entries = JsonSerializer.Deserialize<List<DownloadHistoryEntry>>(json, JsonOptions);
            return entries ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task AppendAsync(DownloadHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var items = (await LoadAsync(cancellationToken)).ToList();
            items.Insert(0, entry);
            if (items.Count > 1000)
            {
                items = items.Take(1000).ToList();
            }

            var dir = Path.GetDirectoryName(_historyPath)!;
            Directory.CreateDirectory(dir);

            // Fix Issue 11: write to a temp file then atomically move to prevent
            // a partial write from corrupting the history file on crash.
            var tmpPath = _historyPath + ".tmp";
            var json = JsonSerializer.Serialize(items, JsonOptions);
            await File.WriteAllTextAsync(tmpPath, json, cancellationToken);
            File.Move(tmpPath, _historyPath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_historyPath))
            {
                File.Delete(_historyPath);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
