using System.IO;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class ActivityLogServiceTests
{
    [Fact]
    public async Task LogAsync_PersistsAndReturnsRecentRows()
    {
        var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new ActivityLogService(store);

            await service.LogAsync("Test", "ActionA", "detail-a");
            await service.LogAsync("Test", "ActionB", "detail-b", isSensitive: true);

            var items = await service.GetRecentAsync(limit: 10, category: "Test");

            Assert.Equal(2, items.Count);
            Assert.Equal("ActionB", items[0].Action);
            Assert.True(items[0].IsSensitive);
            Assert.Equal("ActionA", items[1].Action);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task GetRecentAsync_FiltersByCategory()
    {
        var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new ActivityLogService(store);

            await service.LogAsync("Navigation", "OpenTool", "storage-master");
            await service.LogAsync("CommandPalette", "Execute", "shell:settings");

            var navOnly = await service.GetRecentAsync(limit: 10, category: "Navigation");

            Assert.Single(navOnly);
            Assert.Equal("OpenTool", navOnly[0].Action);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task ClearAsync_RemovesOnlyScopedCategory_WhenProvided()
    {
        var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new ActivityLogService(store);

            await service.LogAsync("Navigation", "OpenTool", "storage-master");
            await service.LogAsync("CommandPalette", "Execute", "shell:settings");

            var removed = await service.ClearAsync("Navigation");
            var remaining = await service.GetRecentAsync(limit: 10);

            Assert.Equal(1, removed);
            Assert.Single(remaining);
            Assert.Equal("CommandPalette", remaining[0].Category);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static string GetTempDatabasePath()
        => Path.Combine(Path.GetTempPath(), $"wup-tests-{Guid.NewGuid():N}.db");

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // SQLite may still hold a short-lived file lock during finalizer cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore best-effort temp cleanup failures in tests.
        }
    }
}