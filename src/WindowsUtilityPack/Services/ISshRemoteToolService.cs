using System.Net.Sockets;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Provides SSH profile management and connectivity checks.
/// </summary>
public interface ISshRemoteToolService
{
    Task<IReadOnlyList<SshConnectionProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);

    Task SaveProfileAsync(SshConnectionProfile profile, CancellationToken cancellationToken = default);

    Task<bool> DeleteProfileAsync(string name, CancellationToken cancellationToken = default);

    string BuildSshCommand(SshConnectionProfile profile);

    Task<(bool IsReachable, string Message)> TestConnectionAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default SSH remote service backed by local SQLite profile persistence.
/// </summary>
public sealed class SshRemoteToolService : ISshRemoteToolService
{
    private readonly IAppDataStoreService _store;

    public SshRemoteToolService(IAppDataStoreService store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _store.EnsureInitialized();
    }

    public async Task<IReadOnlyList<SshConnectionProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT name, host, port, username, private_key_path, created_utc, updated_utc
FROM ssh_profiles
ORDER BY updated_utc DESC, name ASC;";

        var profiles = new List<SshConnectionProfile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            profiles.Add(new SshConnectionProfile
            {
                Name = reader.GetString(0),
                Host = reader.GetString(1),
                Port = reader.GetInt32(2),
                Username = reader.GetString(3),
                PrivateKeyPath = reader.GetString(4),
                CreatedUtc = DateTime.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                UpdatedUtc = DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind),
            });
        }

        return profiles;
    }

    public async Task SaveProfileAsync(SshConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.Name) || string.IsNullOrWhiteSpace(profile.Host) || string.IsNullOrWhiteSpace(profile.Username))
        {
            throw new InvalidOperationException("Profile name, host, and username are required.");
        }

        if (profile.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Port must be between 1 and 65535.");
        }

        var now = DateTime.UtcNow;
        var created = profile.CreatedUtc == default ? now : profile.CreatedUtc;

        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO ssh_profiles(name, host, port, username, private_key_path, created_utc, updated_utc)
VALUES($name, $host, $port, $username, $privateKey, $created, $updated)
ON CONFLICT(name) DO UPDATE SET
    host = excluded.host,
    port = excluded.port,
    username = excluded.username,
    private_key_path = excluded.private_key_path,
    updated_utc = excluded.updated_utc;";

        command.Parameters.AddWithValue("$name", profile.Name.Trim());
        command.Parameters.AddWithValue("$host", profile.Host.Trim());
        command.Parameters.AddWithValue("$port", profile.Port);
        command.Parameters.AddWithValue("$username", profile.Username.Trim());
        command.Parameters.AddWithValue("$privateKey", profile.PrivateKeyPath ?? string.Empty);
        command.Parameters.AddWithValue("$created", created.ToString("O"));
        command.Parameters.AddWithValue("$updated", now.ToString("O"));

        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ssh_profiles WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name.Trim());
        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    public string BuildSshCommand(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var keyPart = string.IsNullOrWhiteSpace(profile.PrivateKeyPath)
            ? string.Empty
            : $" -i \"{profile.PrivateKeyPath}\"";

        return $"ssh -p {profile.Port}{keyPart} {profile.Username}@{profile.Host}";
    }

    public async Task<(bool IsReachable, string Message)> TestConnectionAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return (false, "Host is required.");
        }

        if (port is < 1 or > 65535)
        {
            return (false, "Port must be between 1 and 65535.");
        }

        using var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
            return (true, $"Connected to {host}:{port}.");
        }
        catch (OperationCanceledException)
        {
            return (false, $"Connection to {host}:{port} timed out.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}