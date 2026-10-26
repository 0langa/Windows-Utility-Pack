using System.IO;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wup-settings-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TrySave_LockedFile_PreservesPreviousSettingsAndReportsFailure()
    {
        var path = Path.Combine(_directory, "settings.json");
        var service = new SettingsService(path); var settings = service.Load();
        Assert.True(service.TrySave(settings, out _));
        var original = File.ReadAllBytes(path); settings.Theme = AppTheme.Aurora;
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        { Assert.False(service.TrySave(settings, out var error)); Assert.False(string.IsNullOrWhiteSpace(error)); }
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(_directory, ".wup-write-*"));
    }

    [Fact]
    public void Load_MigratesLegacyPlaintextWithoutLeavingPlaintextBackup()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{\"Theme\":1,\"PentestLastRequestHeaders\":\"Authorization: private-legacy\"}");
        var settings = new SettingsService(path).Load();
        Assert.Contains("private-legacy", settings.PentestLastRequestHeaders);
        Assert.DoesNotContain("private-legacy", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public void Save_StaleSnapshotDoesNotEraseChangesToOtherFields()
    {
        var service = new SettingsService(Path.Combine(_directory, "settings.json"));
        var theme = service.Load();
        var favorites = service.Load();
        theme.Theme = AppTheme.Aurora;
        service.Save(theme);
        favorites.FavoriteToolKeys = ["storage-master"];
        service.Save(favorites);
        var saved = service.Load();
        Assert.Equal(AppTheme.Aurora, saved.Theme);
        Assert.Equal(new[] { "storage-master" }, saved.FavoriteToolKeys);
    }

    [Fact]
    public void Save_RequestCredentialsAreProtectedAtRestAndRoundTripForCurrentUser()
    {
        var path = Path.Combine(_directory, "settings.json");
        var service = new SettingsService(path);
        var settings = new AppSettings
        {
            PentestLastRequestUrl = "https://example.test/?token=private-token",
            PentestLastRequestHeaders = "Authorization: Bearer private-bearer",
            PentestLastRequestBody = "private-body"
        };
        settings.DownloaderSettings.Connections.CustomHeaders = "Cookie: session=private-cookie";
        service.Save(settings);
        var raw = File.ReadAllText(path);
        Assert.DoesNotContain("private-", raw);
        var loaded = service.Load();
        Assert.Equal(settings.PentestLastRequestUrl, loaded.PentestLastRequestUrl);
        Assert.Equal(settings.PentestLastRequestHeaders, loaded.PentestLastRequestHeaders);
        Assert.Equal(settings.PentestLastRequestBody, loaded.PentestLastRequestBody);
        Assert.Equal(settings.DownloaderSettings.Connections.CustomHeaders, loaded.DownloaderSettings.Connections.CustomHeaders);
    }

    [Fact]
    public void Save_FirstRunSettings_PreservesPreferencesAndUnsetCoordinates()
    {
        var path = Path.Combine(_directory, "settings.json");
        var service = new SettingsService(path);
        var settings = new AppSettings { Theme = AppTheme.Aurora, FavoriteToolKeys = ["json-yaml-validator"] };

        service.Save(settings);

        Assert.True(File.Exists(path), "First-run preferences were not written to disk.");
        var restored = new SettingsService(path).Load();
        Assert.Equal(AppTheme.Aurora, restored.Theme);
        Assert.Equal(settings.FavoriteToolKeys, restored.FavoriteToolKeys);
        Assert.True(double.IsNaN(restored.WindowLeft));
        Assert.True(double.IsNaN(restored.WindowTop));
    }

    [Fact]
    public void Load_CorruptSettings_PreservesOriginalForRecovery()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{ broken");

        _ = new SettingsService(path).Load();

        Assert.Equal("{ broken", File.ReadAllText(Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"))));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
