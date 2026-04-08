using System.Text.Json;
using System.IO;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>JSON-backed history persistence for completed and failed download jobs.</summary>
public sealed class DownloadHistoryService : IDownloadHistoryService
{
    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsUtilityPack",
        "downloader-history.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<IReadOnlyList<DownloadHistoryEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(HistoryPath))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(HistoryPath, cancellationToken);
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

            Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
            var json = JsonSerializer.Serialize(items, JsonOptions);
            await File.WriteAllTextAsync(HistoryPath, json, cancellationToken);
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
            if (File.Exists(HistoryPath))
            {
                File.Delete(HistoryPath);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
