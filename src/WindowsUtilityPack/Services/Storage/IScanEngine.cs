using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Core storage scan engine interface.
///
/// Architecture notes:
///   - Scan is fully async and cancellable via CancellationToken.
///   - Progress is reported via IProgress[ScanProgress] so it is automatically
///     marshalled to the UI thread by WPF.
///   - The scan returns a virtual root StorageItem whose Children are
///     the top-level items of the scanned path.
///   - For future optimization, implementations may use NTFS-specific
///     enumeration APIs (e.g. USN journal) — the interface is designed to
///     accommodate that without changes to callers.
/// </summary>
public interface IScanEngine
{
    /// <summary>
    /// Scans the specified root path and returns the scan tree.
    /// </summary>
    /// <param name="rootPath">Directory to scan (must exist and be readable).</param>
    /// <param name="options">Scan configuration.</param>
    /// <param name="progress">Optional progress reporter (marshalled to UI thread).</param>
    /// <param name="cancellationToken">Token to cancel the scan.</param>
    /// <returns>A <see cref="StorageItem"/> representing the root of the scan tree.</returns>
    Task<StorageItem> ScanAsync(
        string rootPath,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken);
}
