using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Persists and queries application activity events.
/// </summary>
public interface IActivityLogService
{
    /// <summary>
    /// Writes one activity event to the local store.
    /// </summary>
    Task LogAsync(
        string category,
        string action,
        string details = "",
        bool isSensitive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns recent events ordered newest first.
    /// </summary>
    Task<IReadOnlyList<ActivityLogEntry>> GetRecentAsync(
        int limit = 100,
        string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes activity log records, optionally scoped to a category.
    /// </summary>
    Task<int> ClearAsync(string? category = null, CancellationToken cancellationToken = default);
}