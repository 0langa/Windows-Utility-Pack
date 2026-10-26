namespace WindowsUtilityPack.Models;

/// <summary>
/// Configuration parameters for a storage scan.
/// Passed to <c>IScanEngine.ScanAsync</c> to control scan behaviour.
/// </summary>
public class ScanOptions
{
    /// <summary>Include hidden files and directories in the scan.</summary>
    public bool IncludeHidden { get; init; } = false;

    /// <summary>Include system files and directories in the scan.</summary>
    public bool IncludeSystem { get; init; } = false;

    /// <summary>
    /// Minimum file size in bytes to include in results.
    /// Smaller files are excluded from the displayed tree and its aggregates;
    /// affected directories are marked incomplete and cannot be recommended as empty.
    /// Default 0 = include all.
    /// </summary>
    public long MinFileSizeBytes { get; init; } = 0;

    /// <summary>
    /// Maximum scan depth relative to the root path.
    /// 0 = the safety ceiling of 256 levels. Higher values improve performance for shallow scans.
    /// </summary>
    public int MaxDepth { get; init; } = 0;

    /// <summary>
    /// How frequently (in items enumerated) to fire a progress update.
    /// Lower values give smoother progress but increase overhead.
    /// </summary>
    public int ProgressIntervalItems { get; init; } = 200;

    /// <summary>Default safe options suitable for most users (no hidden/system files).</summary>
    public static ScanOptions Default => new();

    /// <summary>Elevated options for admin mode (includes hidden and system files).</summary>
    public static ScanOptions Elevated => new() { IncludeHidden = true, IncludeSystem = true };
}
