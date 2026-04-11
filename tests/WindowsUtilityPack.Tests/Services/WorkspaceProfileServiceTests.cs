using System.IO;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class WorkspaceProfileServiceTests
{
    [Fact]
    public async Task SaveProfileAsync_CreatesAndReturnsProfile()
    {
        var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new WorkspaceProfileService(store);

            var profile = new WorkspaceProfile
            {
                Name = "Dev Mode",
                Description = "Developer workflow",
                StartupToolKey = "http-request-tester",
                PinnedToolKeys = ["http-request-tester", "regex-tester"],
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            };

            await service.SaveProfileAsync(profile);
            var saved = await service.GetProfileAsync("Dev Mode");

            Assert.NotNull(saved);
            Assert.Equal("http-request-tester", saved!.StartupToolKey);
            Assert.Equal(2, saved.PinnedToolKeys.Count);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task SaveProfileAsync_UpdatesExistingProfile()
    {
        var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new WorkspaceProfileService(store);

            await service.SaveProfileAsync(new WorkspaceProfile
            {
                Name = "Security",
                Description = "Initial",
                StartupToolKey = "local-secret-vault",
                PinnedToolKeys = ["local-secret-vault"],
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            });

            await service.SaveProfileAsync(new WorkspaceProfile
            {
                Name = "Security",
                Description = "Updated",
                StartupToolKey = "certificate-inspector",
                PinnedToolKeys = ["certificate-inspector", "local-secret-vault"],
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            });

            var saved = await service.GetProfileAsync("Security");

            Assert.NotNull(saved);
            Assert.Equal("Updated", saved!.Description);
            Assert.Equal("certificate-inspector", saved.StartupToolKey);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task DeleteProfileAsync_RemovesStoredProfile()
    {
        var path = GetTempDatabasePath();
        try
        {
            var store = new AppDataStoreService(path);
            var service = new WorkspaceProfileService(store);

            await service.SaveProfileAsync(new WorkspaceProfile
            {
                Name = "Cleanup",
                Description = "Disk cleanup profile",
                StartupToolKey = "storage-master",
                PinnedToolKeys = ["storage-master"],
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            });

            var removed = await service.DeleteProfileAsync("Cleanup");
            var loaded = await service.GetProfileAsync("Cleanup");

            Assert.True(removed);
            Assert.Null(loaded);
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