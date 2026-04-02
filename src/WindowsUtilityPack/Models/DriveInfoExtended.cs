using System;
using System.IO;

namespace WindowsUtilityPack.Models;

// ── StorageMediaType ──────────────────────────────────────────────────────────

/// <summary>
/// Classifies the physical storage medium of a drive,
/// as detected by <c>DriveAnalysisService</c>.
/// </summary>
public enum StorageMediaType
{
    /// <summary>Detection failed or media type is indeterminate.</summary>
    Unknown = 0,

    /// <summary>Solid-state drive (no seek penalty).</summary>
    SSD,

    /// <summary>Hard disk drive (rotational media, has seek penalty).</summary>
    HDD,

    /// <summary>Removable media (USB, SD card, optical, etc.).</summary>
    Removable,

    /// <summary>Virtual, RAM, or network-mapped drive.</summary>
    Virtual
}

// ── DriveInfoExtended ─────────────────────────────────────────────────────────

/// <summary>
/// Immutable snapshot of a drive's identity and capacity metrics.
/// Populated via object-initializer syntax inside DriveAnalysisService.BuildExtended.
/// </summary>
public sealed class DriveInfoExtended
{
    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>Root path, e.g. "C:\".</summary>
    public string RootPath    { get; init; } = string.Empty;

    /// <summary>User-assigned volume label.</summary>
    public string VolumeLabel { get; init; } = string.Empty;

    /// <summary>File-system name, e.g. "NTFS", "FAT32".</summary>
    public string FileSystem  { get; init; } = string.Empty;

    /// <summary>BCL drive-type classification.</summary>
    public DriveType DriveType { get; init; } = DriveType.Unknown;

    // ── Capacity ─────────────────────────────────────────────────────────────

    /// <summary>Total drive capacity in bytes.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Available free space in bytes.</summary>
    public long FreeBytes  { get; init; }

    /// <summary>Consumed space in bytes (derived).</summary>
    public long UsedBytes  => TotalBytes - FreeBytes;

    /// <summary>Percentage of drive space currently used (0–100).</summary>
    public double UsedPercent
        => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100.0 : 0.0;

    // ── Media detection ───────────────────────────────────────────────────────

    /// <summary>
    /// Human-readable media-type string, e.g. "SSD", "HDD", "Removable".
    /// Populated by <see cref="Storage.StorageDeviceHelper.GetMediaType"/>.
    /// </summary>
    public string MediaType { get; init; } = "Unknown";

    // ── Computed display properties (bound by Overview tab XAML) ──────────

    /// <summary>Label shown next to drive icon, e.g. "C: [Windows]".</summary>
    public string DisplayLabel
        => string.IsNullOrWhiteSpace(VolumeLabel)
            ? RootPath.TrimEnd('\\')
            : $"{RootPath.TrimEnd('\\')} [{VolumeLabel}]";

    /// <summary>MDL2 icon glyph for the drive type.</summary>
    public string DriveTypeIcon => DriveType switch
    {
        DriveType.Fixed     => "\uEDA2",  // HardDrive
        DriveType.Removable => "\uE88E",  // USB
        DriveType.CDRom     => "\uE958",  // CD
        DriveType.Network   => "\uE968",  // NetworkFolder
        _                   => "\uE8B7",  // Page (generic)
    };

    /// <summary>Human-readable free space, e.g. "42.3 GB".</summary>
    public string FreeFormatted  => FormatBytes(FreeBytes);

    /// <summary>Human-readable total capacity, e.g. "256.0 GB".</summary>
    public string TotalFormatted => FormatBytes(TotalBytes);

    /// <summary>Cleaned-up media type for display, e.g. "SSD · NTFS".</summary>
    public string MediaTypeDisplay
        => string.IsNullOrWhiteSpace(FileSystem)
            ? MediaType
            : $"{MediaType} · {FileSystem}";

    /// <summary>Quick optimisation hint based on usage percentage.</summary>
    public string OptimizationAdvice => UsedPercent switch
    {
        >= 95 => "Critical — drive almost full!",
        >= 85 => "Consider freeing space soon.",
        >= 70 => "Usage is moderate.",
        _     => string.Empty,
    };

    public override string ToString()
        => $"{RootPath} [{VolumeLabel}] {FileSystem} | {MediaType} | "
         + $"Free {FreeBytes:N0} / {TotalBytes:N0} bytes";

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 40 => $"{bytes / (double)(1L << 40):F1} TB",
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F1} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F1} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F1} KB",
        _           => $"{bytes} B",
    };
}
