using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Service that discovers duplicate files within a scanned storage tree.
///
/// Detection strategy (staged, performance-aware):
///   Stage 1 — Group by exact file size (free, eliminates most files immediately).
///   Stage 2 — For same-size candidates: compare SHA-256 of first 8 KB (quick partial hash).
///   Stage 3 — For matching quick-hash groups: compute full SHA-256 and confirm.
///
/// This staged approach avoids hashing the majority of files while providing
/// high-confidence duplicate detection on the final results.
/// </summary>
public interface IDuplicateDetectionService
{
    /// <summary>
    /// Finds all duplicate file groups in the given scan tree.
    /// </summary>
    /// <param name="root">Root of the storage scan tree.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Groups of duplicate files, sorted by wasted bytes descending.</returns>
    Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(
        StorageItem root,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}
