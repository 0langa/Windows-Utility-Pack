using System.IO;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class SshRemoteToolServiceTests
{
    [Fact]
    public async Task Command_PowerShellParsesMetacharactersAsLiteralArguments()
    {
        var path = GetTempDatabasePath();
        try
        {
            var service = new SshRemoteToolService(new AppDataStoreService(path));
            var profile = new SshConnectionProfile { Host = "example.test", Port = 22, Username = "u; Write-Error bad", PrivateKeyPath = @"C:\keys\O'Brien’s\$(literal).key" };
            var command = service.BuildSshCommand(profile);
            var script = "$ProgressPreference = 'SilentlyContinue'; function ssh { ConvertTo-Json -InputObject ([string[]]$args) -Compress }; " + command;
            var start = new System.Diagnostics.ProcessStartInfo("powershell.exe") { StandardOutputEncoding = System.Text.Encoding.UTF8 };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes("[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); " + script)) }) start.ArgumentList.Add(arg);
            var result = await new ProcessRunner().RunAsync(start);
            Assert.Equal(0, result.ExitCode);
            var args = System.Text.Json.JsonSerializer.Deserialize<string[]>(result.StdOut)!;
            Assert.Contains(profile.PrivateKeyPath, args); Assert.Contains(profile.Username, args); Assert.Contains("example.test", args);
            Assert.Empty(result.StdErr);
        }
        finally { TryDelete(path); }
    }

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
        Assert.Contains("-i 'C:\\keys\\id_rsa'", command);
        Assert.Contains("-l 'ubuntu' -- 'example.com'", command);
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
