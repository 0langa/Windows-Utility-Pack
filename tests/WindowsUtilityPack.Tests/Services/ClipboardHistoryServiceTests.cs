using System.IO;
using Microsoft.Data.Sqlite;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class ClipboardHistoryServiceTests
{
    [Fact]
    public async Task AddEntryAsync_PersistsAndReturnsRecentEntries()
    {
            var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new ClipboardHistoryService(store, new FakeSettingsService());

            _ = await service.AddEntryAsync("first");
            _ = await service.AddEntryAsync("second");

            var recent = await service.GetRecentAsync(10);

            Assert.Equal(2, recent.Count);
            Assert.Equal("second", recent[0].Content);
            Assert.Equal("first", recent[1].Content);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task AddEntryAsync_SkipsAdjacentDuplicates()
    {
            var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new ClipboardHistoryService(store, new FakeSettingsService());

            var first = await service.AddEntryAsync("same");
            var second = await service.AddEntryAsync("same");

            Assert.True(first > 0);
            Assert.Equal(0, second);

            var recent = await service.GetRecentAsync(10);
            Assert.Single(recent);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
            var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new ClipboardHistoryService(store, new FakeSettingsService());

            _ = await service.AddEntryAsync("one");
            _ = await service.AddEntryAsync("two");

            await service.ClearAsync();
            var recent = await service.GetRecentAsync(10);

            Assert.Empty(recent);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task AddEntryAsync_SkipsSensitiveContent_WhenPrivacyFilterEnabled()
    {
        var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new ClipboardHistoryService(store, new FakeSettingsService(new AppSettings
            {
                ClipboardCaptureSensitiveContent = false,
                ClipboardHistoryRetentionDays = 30,
            }));

            var id = await service.AddEntryAsync("apiKey=supersecretvalue");
            var recent = await service.GetRecentAsync(10);

            Assert.Equal(0, id);
            Assert.Empty(recent);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public async Task AddEntryAsync_StoresEncryptedPayloadAtRest()
    {
        var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new ClipboardHistoryService(store, new FakeSettingsService());

            _ = await service.AddEntryAsync("top-secret-token");

            await using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT content FROM clipboard_history LIMIT 1;";
            var raw = Convert.ToString(await command.ExecuteScalarAsync());

            Assert.NotNull(raw);
            Assert.StartsWith("enc:v1:", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("top-secret-token", raw, StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static string GetTempDatabasePath()
        => Path.Combine(Path.GetTempPath(), $"wup-tests-{Guid.NewGuid():N}.db");

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private AppSettings _settings;

        public FakeSettingsService(AppSettings? seed = null)
        {
            _settings = seed ?? new AppSettings
            {
                ClipboardCaptureSensitiveContent = true,
                ClipboardHistoryRetentionDays = 30,
            };
        }

        public AppSettings Load() => _settings;

        public void Save(AppSettings settings) => _settings = settings;
    }
}
