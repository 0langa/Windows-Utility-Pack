using System.IO;

namespace WindowsUtilityPack.Models;

/// <summary>
/// Extended drive information including type detection (SSD/HDD/removable).
/// Wraps <see cref="DriveInfo"/> with additional metadata for the Storage Master overview.
/// </summary>
public class DriveInfoExtended
{
    /// <summary>Drive root (e.g. "C:\").</summary>
    public string RootPath { get; init; } = string.Empty;

    /// <summary>Volume label (may be empty).</summary>
    public string VolumeLabel { get; init; } = string.Empty;

    /// <summary>Filesystem format (NTFS, FAT32, exFAT, etc.).</summary>
    public string FileSystem { get; init; } = string.Empty;

    /// <summary>Windows DriveType enum value.</summary>
    public DriveType DriveType { get; init; }

    /// <summary>Total capacity in bytes.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Available free space in bytes.</summary>
    public long FreeBytes { get; init; }

    /// <summary>Used space in bytes.</summary>
    public long UsedBytes => TotalBytes - FreeBytes;

    /// <summary>Percentage of space used (0–100).</summary>
    public double UsedPercent => TotalBytes > 0 ? (UsedBytes / (double)TotalBytes) * 100 : 0;

    /// <summary>Detected storage media type (SSD, HDD, removable, etc.).</summary>
    public StorageMediaType MediaType { get; set; } = StorageMediaType.Unknown;

    /// <summary>Whether the drive is likely an SSD (affects optimization recommendations).</summary>
    public bool IsSsd => MediaType == StorageMediaType.SSD;

    // ── Display helpers ───────────────────────────────────────────────────────

    public string DisplayLabel => string.IsNullOrEmpty(VolumeLabel)
        ? RootPath
        : $"{VolumeLabel} ({RootPath})";

    public string TotalFormatted => StorageItem.FormatBytes(TotalBytes);
    public string FreeFormatted  => StorageItem.FormatBytes(FreeBytes);
    public string UsedFormatted  => StorageItem.FormatBytes(UsedBytes);

    public string DriveTypeIcon => DriveType switch
    {
        DriveType.Fixed     => MediaType == StorageMediaType.SSD ? "⚡" : "💾",
        DriveType.Removable => "🔌",
        DriveType.Network   => "🌐",
        DriveType.CDRom     => "💿",
        _                   => "🖥"
    };

    public string MediaTypeDisplay => MediaType switch
    {
        StorageMediaType.SSD      => "SSD",
        StorageMediaType.HDD      => "HDD",
        StorageMediaType.Removable=> "Removable",
        StorageMediaType.Virtual  => "Virtual",
        _                         => DriveType.ToString()
    };

    public string OptimizationAdvice => MediaType switch
    {
        StorageMediaType.SSD =>
            "SSD detected — avoid defragmentation. Ensure TRIM is enabled for optimal performance.",
        StorageMediaType.HDD =>
            "HDD detected — periodic defragmentation may improve performance. Keep at least 15% free.",
        StorageMediaType.Removable =>
            "Removable drive — safely eject before removal. Avoid interrupting write operations.",
        _ =>
            "Monitor free space and remove unused files to maintain performance."
    };
}

/// <summary>Storage media type classification.</summary>
public enum StorageMediaType
{
    Unknown,
    SSD,
    HDD,
    Removable,
    Virtual
}
