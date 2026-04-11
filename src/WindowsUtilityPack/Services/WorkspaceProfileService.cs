using System.Text.Json;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// SQLite-backed workspace profile persistence service.
/// </summary>
public sealed class WorkspaceProfileService : IWorkspaceProfileService
{
    private readonly IAppDataStoreService _store;

    public WorkspaceProfileService(IAppDataStoreService store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _store.EnsureInitialized();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkspaceProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT name, description, startup_tool_key, pinned_tool_keys_json, created_utc, updated_utc
FROM workspace_profiles
ORDER BY updated_utc DESC, name ASC;";

        var results = new List<WorkspaceProfile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(Map(reader));
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<WorkspaceProfile?> GetProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT name, description, startup_tool_key, pinned_tool_keys_json, created_utc, updated_utc
FROM workspace_profiles
WHERE name = $name
LIMIT 1;";
        command.Parameters.AddWithValue("$name", name.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return Map(reader);
    }

    /// <inheritdoc />
    public async Task SaveProfileAsync(WorkspaceProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new ArgumentException("Profile name must be provided.", nameof(profile));
        }

        var now = DateTime.UtcNow;
        var created = profile.CreatedUtc == default ? now : profile.CreatedUtc;

        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO workspace_profiles(name, description, startup_tool_key, pinned_tool_keys_json, created_utc, updated_utc)
VALUES($name, $description, $startup, $pinned, $created, $updated)
ON CONFLICT(name) DO UPDATE SET
    description = excluded.description,
    startup_tool_key = excluded.startup_tool_key,
    pinned_tool_keys_json = excluded.pinned_tool_keys_json,
    updated_utc = excluded.updated_utc;";

        command.Parameters.AddWithValue("$name", profile.Name.Trim());
        command.Parameters.AddWithValue("$description", profile.Description ?? string.Empty);
        command.Parameters.AddWithValue("$startup", string.IsNullOrWhiteSpace(profile.StartupToolKey) ? "home" : profile.StartupToolKey);
        command.Parameters.AddWithValue("$pinned", JsonSerializer.Serialize(profile.PinnedToolKeys ?? []));
        command.Parameters.AddWithValue("$created", created.ToString("O"));
        command.Parameters.AddWithValue("$updated", now.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM workspace_profiles WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name.Trim());

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    private static WorkspaceProfile Map(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new WorkspaceProfile
        {
            Name = reader.GetString(0),
            Description = reader.GetString(1),
            StartupToolKey = reader.GetString(2),
            PinnedToolKeys = ParseKeys(reader.GetString(3)),
            CreatedUtc = ParseDate(reader.GetString(4)),
            UpdatedUtc = ParseDate(reader.GetString(5)),
        };
    }

    private static IReadOnlyList<string> ParseKeys(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(json);
            return values ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static DateTime ParseDate(string value)
    {
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? (parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime())
            : DateTime.UtcNow;
    }
}