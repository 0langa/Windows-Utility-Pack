namespace WindowsUtilityPack.Models;

/// <summary>
/// A single cleanup recommendation produced by the cleanup analysis engine.
/// Recommendations always require user review before any action is taken.
/// </summary>
public class CleanupRecommendation
{
    /// <summary>Category of this recommendation for grouping in the UI.</summary>
    public CleanupCategory Category { get; init; }

    /// <summary>The storage item this recommendation applies to.</summary>
    public StorageItem Item { get; init; } = null!;

    /// <summary>Human-readable explanation of why this is recommended.</summary>
    public string Rationale { get; init; } = string.Empty;

    /// <summary>Estimated space that would be freed by acting on this recommendation.</summary>
    public long PotentialSavingsBytes => Item.IsDirectory ? Item.TotalSizeBytes : Item.SizeBytes;

    /// <summary>Human-readable savings estimate.</summary>
    public string PotentialSavingsFormatted => StorageItem.FormatBytes(PotentialSavingsBytes);

    /// <summary>Risk level — used to inform warnings shown before destructive actions.</summary>
    public CleanupRisk Risk { get; init; } = CleanupRisk.Low;

    /// <summary>Whether this recommendation is selected for review (default true for low risk).</summary>
    public bool IsSelected { get; set; } = true;
}

/// <summary>Categories of cleanup recommendations.</summary>
public enum CleanupCategory
{
    TemporaryFiles,
    LargeStaleFiles,
    DuplicateFiles,
    EmptyFolders,
    CacheLikeFiles,
    Unknown
}

/// <summary>Risk level for a cleanup recommendation.</summary>
public enum CleanupRisk
{
    /// <summary>Safe to clean; well-known temporary/junk location.</summary>
    Low,

    /// <summary>Probably safe but verify before deleting.</summary>
    Medium,

    /// <summary>Potentially important — requires careful review.</summary>
    High
}
