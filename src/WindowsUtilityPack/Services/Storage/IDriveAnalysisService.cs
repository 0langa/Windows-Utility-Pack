using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Provides extended drive information including media type detection
/// (SSD/HDD/removable) for use in the Storage Master drive overview.
/// </summary>
public interface IDriveAnalysisService
{
    /// <summary>Returns extended information for all ready drives on the system.</summary>
    IReadOnlyList<DriveInfoExtended> GetAllDrives();

    /// <summary>
    /// Returns extended information for a specific drive root path.
    /// Returns null if the drive is not ready or doesn't exist.
    /// </summary>
    DriveInfoExtended? GetDrive(string rootPath);
}
