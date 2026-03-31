using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Manages saving, loading, and comparing storage scan snapshots.
/// Snapshots are stored as JSON in %LOCALAPPDATA%\WindowsUtilityPack\Snapshots\.
/// </summary>
public interface ISnapshotService
{
    /// <summary>Saves a new snapshot created from the provided scan tree.</summary>
    Task<StorageSnapshot> SaveSnapshotAsync(StorageItem root, string? label = null);

    /// <summary>Loads all saved snapshots for a given root path, newest first.</summary>
    Task<IReadOnlyList<StorageSnapshot>> LoadSnapshotsAsync(string rootPath);

    /// <summary>Loads all saved snapshots (all paths), newest first.</summary>
    Task<IReadOnlyList<StorageSnapshot>> LoadAllSnapshotsAsync();

    /// <summary>Deletes a saved snapshot by ID.</summary>
    Task DeleteSnapshotAsync(string snapshotId);

    /// <summary>Compares two snapshots and returns a comparison report.</summary>
    SnapshotComparison Compare(StorageSnapshot baseline, StorageSnapshot current);
}
