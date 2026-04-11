using Microsoft.Data.Sqlite;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Default activity logger implementation backed by the shared local SQLite store.
/// </summary>
public sealed class ActivityLogService : IActivityLogService
{
    private readonly IAppDataStoreService _store;

    public ActivityLogService(IAppDataStoreService store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _store.EnsureInitialized();
    }

    /// <inheritdoc />
    public async Task LogAsync(
        string category,
        string action,
        string details = "",
        bool isSensitive = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category must be provided.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action must be provided.", nameof(action));
        }

        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO activity_log(timestamp_utc, category, action, details, is_sensitive)
VALUES($timestamp, $category, $action, $details, $isSensitive);";

        command.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$category", category.Trim());
        command.Parameters.AddWithValue("$action", action.Trim());
        command.Parameters.AddWithValue("$details", details ?? string.Empty);
        command.Parameters.AddWithValue("$isSensitive", isSensitive ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityLogEntry>> GetRecentAsync(
        int limit = 100,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 2000);

        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();

        var hasCategory = !string.IsNullOrWhiteSpace(category);
        command.CommandText = hasCategory
            ? @"
SELECT id, timestamp_utc, category, action, details, is_sensitive
FROM activity_log
WHERE category = $category
ORDER BY timestamp_utc DESC
LIMIT $limit;"
            : @"
SELECT id, timestamp_utc, category, action, details, is_sensitive
FROM activity_log
ORDER BY timestamp_utc DESC
LIMIT $limit;";

        if (hasCategory)
        {
            command.Parameters.AddWithValue("$category", category!.Trim());
        }

        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<ActivityLogEntry>(limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new ActivityLogEntry
            {
                Id = reader.GetInt64(0),
                TimestampUtc = ParseUtc(reader.GetString(1)),
                Category = reader.GetString(2),
                Action = reader.GetString(3),
                Details = reader.GetString(4),
                IsSensitive = reader.GetInt64(5) == 1,
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<int> ClearAsync(string? category = null, CancellationToken cancellationToken = default)
    {
        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();

        if (string.IsNullOrWhiteSpace(category))
        {
            command.CommandText = "DELETE FROM activity_log;";
        }
        else
        {
            command.CommandText = "DELETE FROM activity_log WHERE category = $category;";
            command.Parameters.AddWithValue("$category", category.Trim());
        }

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DateTime ParseUtc(string value)
    {
        if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
        }

        return DateTime.UtcNow;
    }
}