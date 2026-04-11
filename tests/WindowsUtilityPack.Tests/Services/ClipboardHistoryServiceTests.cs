using System.IO;
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
            var service = new ClipboardHistoryService(store);

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
            var service = new ClipboardHistoryService(store);

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
            var service = new ClipboardHistoryService(store);

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
}