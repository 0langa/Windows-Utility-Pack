namespace WindowsUtilityPack.Models;

/// <summary>
/// Progress snapshot reported by the scan engine during an active scan.
/// </summary>
public class ScanProgress
{
    /// <summary>Total number of items (files + directories) enumerated so far.</summary>
    public int ItemsEnumerated { get; init; }

    /// <summary>Number of files enumerated.</summary>
    public int FilesFound { get; init; }

    /// <summary>Number of directories enumerated.</summary>
    public int DirsFound { get; init; }

    /// <summary>Total bytes counted so far.</summary>
    public long BytesCounted { get; init; }

    /// <summary>The path currently being scanned (last directory entered).</summary>
    public string CurrentPath { get; init; } = string.Empty;

    /// <summary>Elapsed time since scan start.</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>Human-readable bytes counted string.</summary>
    public string BytesFormatted => StorageItem.FormatBytes(BytesCounted);
}
