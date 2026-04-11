using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Scans a directory tree for files with identical content (SHA-256).
/// </summary>
public interface IDuplicateDetectionService
{
    /// <summary>
    /// Asynchronously finds all duplicate file groups under <paramref name="rootPath"/>.
    /// </summary>
    /// <param name="rootPath">Root of the directory tree to scan.</param>
    /// <param name="progress">
    ///   Optional progress callback — reports 0-100 as scanning progresses.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the scan.</param>
    /// <returns>
    ///   A list of <see cref="DuplicateGroup"/> items, one per set of identical files.
    ///   Empty when no duplicates are found.
    /// </returns>
    Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(
        string rootPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
