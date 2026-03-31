using System.IO;

namespace WindowsUtilityPack.Models;

/// <summary>
/// Represents a single file or directory node in the storage scan tree.
/// This is the central data model for Storage Master.
///
/// Design notes:
///   - Files: SizeBytes is the actual file size; TotalSizeBytes == SizeBytes.
///   - Directories: SizeBytes is 0; TotalSizeBytes is the recursive sum of all descendants.
///   - AllocatedBytes reflects cluster-aligned allocation (may exceed SizeBytes for small files).
///   - Children are populated during the scan and are only present when IsDirectory is true.
/// </summary>
public class StorageItem
{
    /// <summary>Full absolute path (e.g. C:\Windows\System32).</summary>
    public string FullPath { get; init; } = string.Empty;

    /// <summary>File or directory name without path.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>True if this node represents a directory.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>Actual file size in bytes (0 for directories; use TotalSizeBytes instead).</summary>
    public long SizeBytes { get; init; }

    /// <summary>Disk allocation in bytes (cluster-aligned; 0 when unavailable).</summary>
    public long AllocatedBytes { get; init; }

    /// <summary>Last modification time.</summary>
    public DateTime LastModified { get; init; }

    /// <summary>Creation time.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>File attributes flags (hidden, system, read-only, etc.).</summary>
    public FileAttributes Attributes { get; init; }

    /// <summary>Scan tree depth (0 = root).</summary>
    public int Depth { get; init; }

    // ── Derived attribute checks ──────────────────────────────────────────────

    public bool IsHidden   => (Attributes & FileAttributes.Hidden)   != 0;
    public bool IsSystem   => (Attributes & FileAttributes.System)   != 0;
    public bool IsReadOnly => (Attributes & FileAttributes.ReadOnly) != 0;

    /// <summary>Lower-case file extension including dot (e.g. ".txt"); empty for directories.</summary>
    public string Extension => IsDirectory
        ? string.Empty
        : Path.GetExtension(Name).ToLowerInvariant();

    // ── Aggregated values (populated after scan completes) ────────────────────

    /// <summary>
    /// Recursive total size for directories; equals SizeBytes for files.
    /// Set by the ScanEngine after the full tree is built.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Recursive total allocated bytes for directories; equals AllocatedBytes for files.
    /// Set by the ScanEngine after the full tree is built.
    /// </summary>
    public long TotalAllocatedBytes { get; set; }

    /// <summary>Total number of files under this node (recursive). 1 for files.</summary>
    public int FileCount { get; set; }

    /// <summary>Total number of subdirectories under this node (recursive). 0 for files.</summary>
    public int DirectoryCount { get; set; }

    // ── Tree links ────────────────────────────────────────────────────────────

    /// <summary>Parent node reference (null for the scan root).</summary>
    public StorageItem? Parent { get; set; }

    /// <summary>Direct child nodes (files and directories at the next level).</summary>
    public List<StorageItem> Children { get; } = [];

    // ── Display helpers ───────────────────────────────────────────────────────

    /// <summary>Human-readable size string ("1.2 GB", "345 KB", etc.).</summary>
    public string DisplaySize => FormatBytes(TotalSizeBytes > 0 || IsDirectory ? TotalSizeBytes : SizeBytes);

    /// <summary>Age of the file in days since last modification.</summary>
    public int AgeDays => (int)(DateTime.Now - LastModified).TotalDays;

    /// <summary>True if the file has not been modified in over 365 days.</summary>
    public bool IsStale => AgeDays > 365;

    /// <summary>Formats a byte count to a human-readable string.</summary>
    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_099_511_627_776L => $"{bytes / 1_099_511_627_776.0:F1} TB",
        >= 1_073_741_824L     => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576L         => $"{bytes / 1_048_576.0:F1} MB",
        >= 1_024L             => $"{bytes / 1_024.0:F1} KB",
        _                     => $"{bytes} B"
    };

    public override string ToString() => $"{(IsDirectory ? "DIR" : "FILE")} {FullPath} ({DisplaySize})";
}
