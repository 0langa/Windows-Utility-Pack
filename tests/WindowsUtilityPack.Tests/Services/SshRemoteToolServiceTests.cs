using System.IO;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class SshRemoteToolServiceTests
{
    [Fact]
    public async Task SaveAndGetProfiles_RoundTrips()
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new SshRemoteToolService(new AppDataStoreService(path));
            await service.SaveProfileAsync(new SshConnectionProfile
            {
                Name = "Lab",
                Host = "127.0.0.1",
                Port = 22,
                Username = "dev",
                PrivateKeyPath = "",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            });

            var profiles = await service.GetProfilesAsync();
            Assert.Single(profiles);
            Assert.Equal("Lab", profiles[0].Name);
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Fact]
    public void BuildSshCommand_IncludesPrivateKeyWhenProvided()
    {
        var service = new SshRemoteToolService(new AppDataStoreService(GetTempDatabasePath()));
        var command = service.BuildSshCommand(new SshConnectionProfile
        {
            Name = "A",
            Host = "example.com",
            Port = 2222,
            Username = "ubuntu",
            PrivateKeyPath = "C:\\keys\\id_rsa",
        });

        Assert.Contains("-p 2222", command);
        Assert.Contains("-i \"C:\\keys\\id_rsa\"", command);
        Assert.Contains("ubuntu@example.com", command);
    }

    private static string GetTempDatabasePath()
        => Path.Combine(Path.GetTempPath(), $"wup-tests-{Guid.NewGuid():N}.db");

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }
}