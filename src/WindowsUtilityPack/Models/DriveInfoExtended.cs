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

    public override string ToString()
        => $"{RootPath} [{VolumeLabel}] {FileSystem} | {MediaType} | "
         + $"Free {FreeBytes:N0} / {TotalBytes:N0} bytes";
}
