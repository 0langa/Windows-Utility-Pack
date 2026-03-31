namespace WindowsUtilityPack.Models;

/// <summary>
/// A group of files that are considered duplicates of each other.
/// </summary>
public class DuplicateGroup
{
    /// <summary>The hash key that identifies this group (SHA-256 hex string, or size+quickhash).</summary>
    public string GroupKey { get; init; } = string.Empty;

    /// <summary>All files in this duplicate group.</summary>
    public List<StorageItem> Files { get; init; } = [];

    /// <summary>Size of one copy in bytes (all copies have the same size).</summary>
    public long FileSizeBytes => Files.FirstOrDefault()?.SizeBytes ?? 0;

    /// <summary>
    /// Wasted space in bytes: size × (count - 1).
    /// The "original" is assumed to be the oldest file by creation time.
    /// </summary>
    public long WastedBytes => FileSizeBytes * (Files.Count - 1);

    /// <summary>Human-readable wasted space.</summary>
    public string WastedFormatted => StorageItem.FormatBytes(WastedBytes);

    /// <summary>Human-readable single-file size.</summary>
    public string FileSizeFormatted => StorageItem.FormatBytes(FileSizeBytes);

    /// <summary>
    /// Confidence level of the duplicate match.
    /// </summary>
    public DuplicateConfidence Confidence { get; init; } = DuplicateConfidence.FullHash;

    /// <summary>The "original" file (earliest creation date); the rest are candidates for deletion.</summary>
    public StorageItem? Original => Files.OrderBy(f => f.CreatedAt).FirstOrDefault();
}

/// <summary>Confidence level of a duplicate detection match.</summary>
public enum DuplicateConfidence
{
    /// <summary>Matched only by file size (low confidence — not yet verified).</summary>
    SizeOnly,

    /// <summary>Matched by file size and a quick partial hash of the first 8 KB.</summary>
    QuickHash,

    /// <summary>Matched by full SHA-256 hash of the entire file (highest confidence).</summary>
    FullHash
}
