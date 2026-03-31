using System.IO;
using System.Text.Json;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// JSON-based snapshot persistence service.
/// Each snapshot is a separate .json file in %LOCALAPPDATA%\WindowsUtilityPack\Snapshots\.
/// </summary>
public class SnapshotService : ISnapshotService
{
    private static readonly string SnapshotDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsUtilityPack",
        "Snapshots");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <inheritdoc/>
    public async Task<StorageSnapshot> SaveSnapshotAsync(StorageItem root, string? label = null)
    {
        EnsureDirectory();

        var snapshot = BuildSnapshot(root, label);
        var filePath = Path.Combine(SnapshotDirectory, $"snapshot_{snapshot.Id}.json");

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);

        return snapshot;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StorageSnapshot>> LoadSnapshotsAsync(string rootPath)
    {
        var all = await LoadAllSnapshotsAsync();
        return all
            .Where(s => s.RootPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StorageSnapshot>> LoadAllSnapshotsAsync()
    {
        EnsureDirectory();

        var files   = Directory.GetFiles(SnapshotDirectory, "snapshot_*.json");
        var results = new List<StorageSnapshot>(files.Length);

        foreach (var file in files)
        {
            try
            {
                var json     = await File.ReadAllTextAsync(file);
                var snapshot = JsonSerializer.Deserialize<StorageSnapshot>(json);
                if (snapshot != null) results.Add(snapshot);
            }
            catch { /* skip corrupted snapshot files */ }
        }

        return results.OrderByDescending(s => s.TakenAt).ToList();
    }

    /// <inheritdoc/>
    public async Task DeleteSnapshotAsync(string snapshotId)
    {
        EnsureDirectory();

        var files = Directory.GetFiles(SnapshotDirectory, $"snapshot_{snapshotId}.json");
        foreach (var file in files)
        {
            try { File.Delete(file); }
            catch { /* ignore deletion failures */ }
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public SnapshotComparison Compare(StorageSnapshot baseline, StorageSnapshot current)
    {
        // Build lookup maps for folder comparison
        var baselineFolders = baseline.TopFolders.ToDictionary(
            f => f.Path, f => f.SizeBytes, StringComparer.OrdinalIgnoreCase);

        var growthEntries = new List<FolderGrowthEntry>();

        foreach (var folder in current.TopFolders)
        {
            baselineFolders.TryGetValue(folder.Path, out var baselineSize);
            growthEntries.Add(new FolderGrowthEntry
            {
                FolderPath    = folder.Path,
                FolderName    = folder.Name,
                BaselineBytes = baselineSize,
                CurrentBytes  = folder.SizeBytes,
            });
        }

        // Also add folders that existed in baseline but not current
        foreach (var baselineFolder in baseline.TopFolders)
        {
            if (!current.TopFolders.Any(f =>
                f.Path.Equals(baselineFolder.Path, StringComparison.OrdinalIgnoreCase)))
            {
                growthEntries.Add(new FolderGrowthEntry
                {
                    FolderPath    = baselineFolder.Path,
                    FolderName    = baselineFolder.Name,
                    BaselineBytes = baselineFolder.SizeBytes,
                    CurrentBytes  = 0,
                });
            }
        }

        // Sort by delta (largest growth first)
        growthEntries.Sort((a, b) => Math.Abs(b.DeltaBytes).CompareTo(Math.Abs(a.DeltaBytes)));

        return new SnapshotComparison
        {
            Baseline    = baseline,
            Current     = current,
            FolderGrowth = growthEntries,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static StorageSnapshot BuildSnapshot(StorageItem root, string? label)
    {
        // Build top-folder summaries (immediate children of root)
        var topFolders = root.Children
            .OrderByDescending(c => c.TotalSizeBytes)
            .Take(50)
            .Select(c => new SnapshotFolderEntry
            {
                Path      = c.FullPath,
                Name      = c.Name,
                SizeBytes = c.TotalSizeBytes,
            })
            .ToList();

        // Build extension breakdown from all files
        var extensionMap = new Dictionary<string, (long bytes, int count)>(StringComparer.OrdinalIgnoreCase);
        CollectExtensions(root, extensionMap);

        var extBreakdown = extensionMap
            .OrderByDescending(kv => kv.Value.bytes)
            .Take(30)
            .Select(kv => new SnapshotExtensionEntry
            {
                Extension = kv.Key,
                TotalBytes = kv.Value.bytes,
                FileCount  = kv.Value.count,
            })
            .ToList();

        return new StorageSnapshot
        {
            RootPath           = root.FullPath,
            Label              = label ?? string.Empty,
            TakenAt            = DateTime.Now,
            TotalSizeBytes     = root.TotalSizeBytes,
            FileCount          = root.FileCount,
            DirectoryCount     = root.DirectoryCount,
            TopFolders         = topFolders,
            ExtensionBreakdown = extBreakdown,
        };
    }

    private static void CollectExtensions(
        StorageItem node,
        Dictionary<string, (long bytes, int count)> map)
    {
        foreach (var child in node.Children)
        {
            if (!child.IsDirectory)
            {
                var ext = string.IsNullOrEmpty(child.Extension) ? "(no extension)" : child.Extension;
                map.TryGetValue(ext, out var existing);
                map[ext] = (existing.bytes + child.SizeBytes, existing.count + 1);
            }
            else
            {
                CollectExtensions(child, map);
            }
        }
    }

    private static void EnsureDirectory()
    {
        if (!Directory.Exists(SnapshotDirectory))
            Directory.CreateDirectory(SnapshotDirectory);
    }
}
