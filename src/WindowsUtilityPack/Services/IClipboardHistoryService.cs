using WindowsUtilityPack.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Persists clipboard history entries for the Clipboard Manager tool.
/// </summary>
public interface IClipboardHistoryService
{
    Task<IReadOnlyList<ClipboardHistoryEntry>> GetRecentAsync(int limit = 200, CancellationToken cancellationToken = default);

    Task<long> AddEntryAsync(string content, string contentKind = "Text", CancellationToken cancellationToken = default);

    Task<bool> DeleteEntryAsync(long id, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// SQLite-backed clipboard history service.
/// </summary>
public sealed class ClipboardHistoryService : IClipboardHistoryService
{
    private readonly IAppDataStoreService _store;
    private readonly ISettingsService _settings;

    private static readonly Regex JwtLikePattern = new(@"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    private static readonly Regex ApiKeyLikePattern = new(@"(?i)(api[_-]?key|token|secret|password)\s*[:=]\s*['""]?[A-Za-z0-9_\-]{8,}['""]?", RegexOptions.Compiled);

    private const string EncryptedPrefix = "enc:v1:";

    public ClipboardHistoryService(IAppDataStoreService store, ISettingsService settings)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _store.EnsureInitialized();
    }

    public async Task<IReadOnlyList<ClipboardHistoryEntry>> GetRecentAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 2000);
        await PurgeExpiredEntriesAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, captured_utc, content, content_kind
FROM clipboard_history
ORDER BY captured_utc DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        var entries = new List<ClipboardHistoryEntry>(limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new ClipboardHistoryEntry
            {
                Id = reader.GetInt64(0),
                CapturedUtc = ParseDate(reader.GetString(1)),
                Content = TryUnprotect(reader.GetString(2)),
                ContentKind = reader.GetString(3),
            });
        }

        return entries;
    }

    public async Task<long> AddEntryAsync(string content, string contentKind = "Text", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        var settings = _settings.Load();
        content = content.Trim();
        await PurgeExpiredEntriesAsync(cancellationToken).ConfigureAwait(false);

        if (!settings.ClipboardCaptureSensitiveContent && IsLikelySensitive(content))
        {
            return 0;
        }

        if (content.Length > 32_000)
        {
            content = content[..32_000];
        }

        await using var connection = _store.CreateConnection();

        // Avoid storing adjacent duplicate entries.
        await using (var latestCommand = connection.CreateCommand())
        {
            latestCommand.CommandText = "SELECT content FROM clipboard_history ORDER BY captured_utc DESC LIMIT 1;";
            var latestRaw = await latestCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            var latest = TryUnprotect(latestRaw ?? string.Empty);
            if (string.Equals(latest, content, StringComparison.Ordinal))
            {
                return 0;
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO clipboard_history(captured_utc, content, content_kind)
VALUES($captured, $content, $kind);
SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$captured", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$content", Protect(content));
        command.Parameters.AddWithValue("$kind", string.IsNullOrWhiteSpace(contentKind) ? "Text" : contentKind);

        var id = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(id);
    }

    public async Task<bool> DeleteEntryAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return false;
        }

        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM clipboard_history WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM clipboard_history;";
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DateTime ParseDate(string value)
    {
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? (parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime())
            : DateTime.UtcNow;
    }

    private async Task PurgeExpiredEntriesAsync(CancellationToken cancellationToken)
    {
        var retentionDays = Math.Clamp(_settings.Load().ClipboardHistoryRetentionDays, 0, 3650);
        if (retentionDays <= 0)
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays).ToString("O");
        await using var connection = _store.CreateConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM clipboard_history WHERE captured_utc < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", cutoff);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static bool IsLikelySensitive(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var trimmed = content.Trim();
        if (JwtLikePattern.IsMatch(trimmed))
        {
            return true;
        }

        if (ApiKeyLikePattern.IsMatch(trimmed))
        {
            return true;
        }

        if (trimmed.StartsWith("-----BEGIN ", StringComparison.OrdinalIgnoreCase) && trimmed.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string Protect(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return EncryptedPrefix + Convert.ToBase64String(protectedBytes);
    }

    private static string TryUnprotect(string stored)
    {
        if (!stored.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            return stored;
        }

        try
        {
            var payload = stored[EncryptedPrefix.Length..];
            var protectedBytes = Convert.FromBase64String(payload);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
