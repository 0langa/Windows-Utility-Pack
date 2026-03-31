using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Provides extended drive information including media type detection
/// (SSD/HDD/removable) for use in the Storage Master drive overview.
/// </summary>
public interface IDriveAnalysisService
{
    Task<IReadOnlyList<DriveInfoExtended>> GetAllDrivesAsync(
        CancellationToken cancellationToken = default);

    Task<DriveInfoExtended?> GetDriveInfoExtendedAsync(
        string driveLetter,
        CancellationToken cancellationToken = default);

    Task<long> GetFolderSizeAsync(
        string path,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(string FolderPath, long SizeBytes)>> GetTopFoldersBySize(
        string rootPath,
        int topN = 10,
        CancellationToken cancellationToken = default);
}
