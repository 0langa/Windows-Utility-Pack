using System.Text.Json.Serialization;

namespace WindowsUtilityPack.Models;

/// <summary>
/// A saved snapshot of a storage scan, persisted to JSON for historical comparison.
/// Snapshots store a lightweight summary (not the full tree) to keep files small.
/// </summary>
public class StorageSnapshot
{
    /// <summary>Unique ID for this snapshot (generated on save).</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>User-provided or auto-generated label for this snapshot.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The root path that was scanned.</summary>
    public string RootPath { get; init; } = string.Empty;

    /// <summary>When this snapshot was taken.</summary>
    public DateTime TakenAt { get; init; } = DateTime.Now;

    /// <summary>Total bytes counted in the scanned path.</summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>Total number of files found.</summary>
    public int FileCount { get; init; }

    /// <summary>Total number of directories found.</summary>
    public int DirectoryCount { get; init; }

    /// <summary>
    /// Top-level folder summaries for comparison (name → total bytes).
    /// Serialised as a flat list to avoid JSON serialisation complexity.
    /// </summary>
    public List<SnapshotFolderEntry> TopFolders { get; init; } = [];

    /// <summary>Extension breakdown for this snapshot (extension → total bytes).</summary>
    public List<SnapshotExtensionEntry> ExtensionBreakdown { get; init; } = [];

    // ── Display ───────────────────────────────────────────────────────────────

    [JsonIgnore]
    public string TotalSizeFormatted => StorageItem.FormatBytes(TotalSizeBytes);

    [JsonIgnore]
    public string DisplayLabel => string.IsNullOrEmpty(Label)
        ? $"Snapshot {TakenAt:yyyy-MM-dd HH:mm} — {TotalSizeFormatted}"
        : Label;
}

/// <summary>A single folder entry in a snapshot (for comparison).</summary>
public class SnapshotFolderEntry
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

/// <summary>A single extension entry in a snapshot (for comparison).</summary>
public class SnapshotExtensionEntry
{
    public string Extension { get; init; } = string.Empty;
    public long TotalBytes { get; init; }
    public int FileCount { get; init; }
}

/// <summary>Result of comparing two snapshots.</summary>
public class SnapshotComparison
{
    public StorageSnapshot Baseline { get; init; } = null!;
    public StorageSnapshot Current  { get; init; } = null!;

    public long SizeDeltaBytes  => Current.TotalSizeBytes - Baseline.TotalSizeBytes;
    public int  FileDeltaCount  => Current.FileCount      - Baseline.FileCount;

    public string SizeDeltaFormatted =>
        $"{(SizeDeltaBytes >= 0 ? "+" : "")}{StorageItem.FormatBytes(Math.Abs(SizeDeltaBytes))}";

    /// <summary>Folders that grew between the two snapshots (sorted by growth desc).</summary>
    public List<FolderGrowthEntry> FolderGrowth { get; init; } = [];
}

/// <summary>Growth data for a single folder between two snapshots.</summary>
public class FolderGrowthEntry
{
    public string FolderPath    { get; init; } = string.Empty;
    public string FolderName    { get; init; } = string.Empty;
    public long   BaselineBytes { get; init; }
    public long   CurrentBytes  { get; init; }
    public long   DeltaBytes    => CurrentBytes - BaselineBytes;
    public string DeltaFormatted =>
        $"{(DeltaBytes >= 0 ? "+" : "")}{StorageItem.FormatBytes(Math.Abs(DeltaBytes))}";
}
