namespace WindowsUtilityPack.Models;

/// <summary>
/// Composable filter and sort parameters for the Storage Master file list.
/// All filters are combined with AND logic (a file must pass every active filter).
/// </summary>
public class StorageFilter
{
    // ── Text search ───────────────────────────────────────────────────────────

    /// <summary>Name/path search text (case-insensitive substring or wildcard).</summary>
    public string SearchText { get; set; } = string.Empty;

    // ── Type filters ──────────────────────────────────────────────────────────

    /// <summary>Filter by file extension (lower-case, e.g. ".mp4"). Empty = all.</summary>
    public string ExtensionFilter { get; set; } = string.Empty;

    /// <summary>Show files in results.</summary>
    public bool ShowFiles { get; set; } = true;

    /// <summary>Show directories in results.</summary>
    public bool ShowDirectories { get; set; } = true;

    /// <summary>Show hidden items.</summary>
    public bool ShowHidden { get; set; } = false;

    /// <summary>Show system items.</summary>
    public bool ShowSystem { get; set; } = false;

    // ── Size filters ──────────────────────────────────────────────────────────

    /// <summary>Minimum file size in bytes (0 = no minimum).</summary>
    public long MinSizeBytes { get; set; } = 0;

    /// <summary>Maximum file size in bytes (0 = no maximum).</summary>
    public long MaxSizeBytes { get; set; } = 0;

    // ── Date filters ──────────────────────────────────────────────────────────

    /// <summary>Only include items older than this many days (0 = no filter).</summary>
    public int OlderThanDays { get; set; } = 0;

    /// <summary>Only include items newer than this many days (0 = no filter).</summary>
    public int NewerThanDays { get; set; } = 0;

    // ── Sort ──────────────────────────────────────────────────────────────────

    public StorageSortField SortField { get; set; } = StorageSortField.Size;
    public bool SortDescending { get; set; } = true;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <paramref name="item"/> passes all active filters.
    /// </summary>
    public bool Matches(StorageItem item)
    {
        if (!ShowFiles      && !item.IsDirectory) return false;
        if (!ShowDirectories && item.IsDirectory) return false;
        if (!ShowHidden     && item.IsHidden)     return false;
        if (!ShowSystem     && item.IsSystem)     return false;

        long effectiveSize = item.IsDirectory ? item.TotalSizeBytes : item.SizeBytes;

        if (MinSizeBytes > 0 && effectiveSize < MinSizeBytes) return false;
        if (MaxSizeBytes > 0 && effectiveSize > MaxSizeBytes) return false;

        if (OlderThanDays > 0 && item.AgeDays < OlderThanDays) return false;
        if (NewerThanDays > 0 && item.AgeDays > NewerThanDays) return false;

        if (!string.IsNullOrEmpty(ExtensionFilter) &&
            !item.Extension.Equals(ExtensionFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(SearchText) &&
            !item.FullPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}

/// <summary>Fields by which storage items can be sorted.</summary>
public enum StorageSortField
{
    Name,
    Size,
    LastModified,
    Extension,
    FileCount
}
