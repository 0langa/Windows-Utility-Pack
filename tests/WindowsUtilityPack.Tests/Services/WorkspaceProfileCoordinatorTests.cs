using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class WorkspaceProfileCoordinatorTests
{
    [Fact]
    public async Task CaptureCurrentAsync_SavesNormalizedProfile()
    {
        var stubProfiles = new StubWorkspaceProfileService();
        var settings = new StubSettingsService();
        var coordinator = new WorkspaceProfileCoordinator(stubProfiles, settings);

        var profile = await coordinator.CaptureCurrentAsync(
            " Dev ",
            " Developer mode ",
            "http-request-tester",
            ["http-request-tester", "regex-tester", "regex-tester"]);

        Assert.Equal("Dev", profile.Name);
        Assert.Equal("Developer mode", profile.Description);
        Assert.Equal(2, profile.PinnedToolKeys.Count);
        Assert.Equal("http-request-tester", profile.StartupToolKey);
    }

    [Fact]
    public async Task ApplyProfileAsync_UpdatesSettings()
    {
        var stubProfiles = new StubWorkspaceProfileService
        {
            Stored = new WorkspaceProfile
            {
                Name = "Security",
                Description = "Security mode",
                StartupToolKey = "local-secret-vault",
                PinnedToolKeys = ["local-secret-vault", "certificate-inspector"],
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            },
        };
        var settings = new StubSettingsService();
        var coordinator = new WorkspaceProfileCoordinator(stubProfiles, settings);

        var applied = await coordinator.ApplyProfileAsync("Security");

        Assert.True(applied);
        Assert.Equal("local-secret-vault", settings.Current.StartupPage);
        Assert.Equal(2, settings.Current.FavoriteToolKeys.Count);
    }

    private sealed class StubWorkspaceProfileService : IWorkspaceProfileService
    {
        public WorkspaceProfile? Stored { get; set; }

        public Task<bool> DeleteProfileAsync(string name, CancellationToken cancellationToken = default)
        {
            var removed = Stored is not null && Stored.Name.Equals(name, StringComparison.OrdinalIgnoreCase);
            if (removed)
            {
                Stored = null;
            }

            return Task.FromResult(removed);
        }

        public Task<WorkspaceProfile?> GetProfileAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Stored is not null && Stored.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    ? Stored
                    : null);
        }

        public Task<IReadOnlyList<WorkspaceProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<WorkspaceProfile> result = Stored is null ? [] : [Stored];
            return Task.FromResult(result);
        }

        public Task SaveProfileAsync(WorkspaceProfile profile, CancellationToken cancellationToken = default)
        {
            Stored = profile;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSettingsService : ISettingsService
    {
        public AppSettings Current { get; private set; } = new();

        public AppSettings Load() => Current;

        public void Save(AppSettings settings)
        {
            Current = settings;
        }
    }
}