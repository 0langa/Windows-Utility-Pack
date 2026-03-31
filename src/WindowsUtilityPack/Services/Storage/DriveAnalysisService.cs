using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Drive analysis service implementation.
///
/// Media type detection strategy:
///   - Removable drives are identified by DriveType.Removable.
///   - CD/DVD drives are identified by DriveType.CDRom.
///   - For fixed drives, we attempt to detect SSD vs HDD via the
///     StorageDevice Windows API (DeviceIoControl IOCTL_STORAGE_QUERY_PROPERTY).
///   - If the API call fails (common in non-admin contexts), we fall back
///     to reporting StorageMediaType.Unknown.
///
/// Note: Reliable SSD/HDD detection on Windows typically requires admin privileges.
/// The service degrades gracefully when not elevated.
/// </summary>
public class DriveAnalysisService : IDriveAnalysisService
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<DriveInfoExtended>> GetAllDrivesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<DriveInfoExtended>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => BuildExtended(d))
                .ToList();

            return drives;
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<DriveInfoExtended?> GetDriveInfoExtendedAsync(
        string driveLetter,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<DriveInfoExtended?>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var drive = DriveInfo.GetDrives()
                .FirstOrDefault(d => d.IsReady &&
                    string.Equals(d.Name, driveLetter, StringComparison.OrdinalIgnoreCase));

            return drive is null ? null : BuildExtended(drive);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<long> GetFolderSizeAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(path))
                return 0L;

            return Directory
                .EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch (IOException) { return 0L; }
                    catch (UnauthorizedAccessException) { return 0L; }
                });
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<(string FolderPath, long SizeBytes)>> GetTopFoldersBySize(
        string rootPath,
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("Root path must not be empty.", nameof(rootPath));

        return Task.Run<IReadOnlyList<(string, long)>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(rootPath))
                return Array.Empty<(string, long)>();

            var results = Directory
                .EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly)
                .Select(dir =>
                {
                    long size = 0;
                    try
                    {
                        size = Directory
                            .EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                            .Sum(f =>
                            {
                                try { return new FileInfo(f).Length; }
                                catch { return 0L; }
                            });
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                    return (FolderPath: dir, SizeBytes: size);
                })
                .OrderByDescending(t => t.SizeBytes)
                .Take(topN)
                .ToList();

            return results;
        }, cancellationToken);
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private static DriveInfoExtended BuildExtended(DriveInfo d)
    {
        string mediaType = StorageDeviceHelper.GetMediaType(d.Name);

        return new DriveInfoExtended
        {
            RootPath    = d.RootDirectory.FullName,
            VolumeLabel = SafeGet(() => d.VolumeLabel, string.Empty),
            FileSystem  = SafeGet(() => d.DriveFormat, string.Empty),
            DriveType   = d.DriveType,
            TotalBytes  = SafeGet(() => d.TotalSize, 0L),
            FreeBytes   = SafeGet(() => d.AvailableFreeSpace, 0L),
            MediaType   = mediaType
        };
    }

    private static StorageMediaType DetectMediaType(System.IO.DriveInfo drive)
    {
        // Removable drives (USB sticks, SD cards)
        if (drive.DriveType == DriveType.Removable) return StorageMediaType.Removable;

        // Virtual / RAM drives / network
        if (drive.DriveType == DriveType.Network)   return StorageMediaType.Virtual;
        if (drive.DriveType == DriveType.CDRom)      return StorageMediaType.Removable;
        if (drive.DriveType != DriveType.Fixed)      return StorageMediaType.Unknown;

        // For fixed drives, attempt SSD/HDD detection via WMI-free approach
        // using the drive letter to query StorageDevice properties
        return TryDetectFixedDriveType(drive.RootDirectory.FullName);
    }

    /// <summary>
    /// Attempts to detect SSD vs HDD using StorageDeviceProperty query.
    /// This is a best-effort approach — returns Unknown on failure (e.g. when not elevated).
    /// </summary>
    private static StorageMediaType TryDetectFixedDriveType(string rootPath)
    {
        // Use a simple heuristic: check if the drive reports as non-rotational
        // via the Windows DeviceIoControl call. This requires P/Invoke; we
        // keep it optional and return Unknown if unavailable.
        try
        {
            return StorageDeviceHelper.IsRotational(rootPath)
                ? StorageMediaType.HDD
                : StorageMediaType.SSD;
        }
        catch
        {
            return StorageMediaType.Unknown;
        }
    }

    private static T SafeGet<T>(Func<T> getter, T fallback)
    {
        try { return getter(); } catch { return fallback; }
    }
}
