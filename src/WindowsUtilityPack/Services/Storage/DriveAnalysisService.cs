using System.IO;
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
    public IReadOnlyList<DriveInfoExtended> GetAllDrives()
    {
        var results = new List<DriveInfoExtended>();

        foreach (var drive in System.IO.DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            results.Add(BuildDriveInfo(drive));
        }

        return results;
    }

    /// <inheritdoc/>
    public DriveInfoExtended? GetDrive(string rootPath)
    {
        try
        {
            var drive = new System.IO.DriveInfo(rootPath);
            return drive.IsReady ? BuildDriveInfo(drive) : null;
        }
        catch
        {
            return null;
        }
    }

    private static DriveInfoExtended BuildDriveInfo(System.IO.DriveInfo drive)
    {
        var extended = new DriveInfoExtended
        {
            RootPath    = drive.RootDirectory.FullName,
            VolumeLabel = SafeGet(() => drive.VolumeLabel, string.Empty),
            FileSystem  = SafeGet(() => drive.DriveFormat, string.Empty),
            DriveType   = drive.DriveType,
            TotalBytes  = SafeGet(() => drive.TotalSize, 0L),
            FreeBytes   = SafeGet(() => drive.AvailableFreeSpace, 0L),
        };

        // Detect media type
        extended.MediaType = DetectMediaType(drive);

        return extended;
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
