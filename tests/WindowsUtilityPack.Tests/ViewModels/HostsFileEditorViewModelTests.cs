using System.IO;
using WindowsUtilityPack.Tools.SystemUtilities.HostsFileEditor;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class HostsFileEditorViewModelTests
{
    [Fact]
    public void AddEntryCommand_RejectsInvalidIpAddress()
    {
        using var sandbox = HostsSandbox.Create();
        var vm = new HostsFileEditorViewModel(sandbox.HostsPath, sandbox.BackupPath, autoLoad: false)
        {
            NewIp = "999.999.0.1",
            NewHostname = "example.test"
        };

        vm.AddEntryCommand.Execute(null);

        Assert.Empty(vm.Entries);
        Assert.False(vm.IsModified);
        Assert.Contains("valid IP address", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddEntryCommand_RejectsDuplicateEntry()
    {
        using var sandbox = HostsSandbox.Create();
        var vm = new HostsFileEditorViewModel(sandbox.HostsPath, sandbox.BackupPath, autoLoad: false)
        {
            NewIp = "127.0.0.1",
            NewHostname = "localhost"
        };

        vm.AddEntryCommand.Execute(null);

        vm.NewIp = "127.0.0.1";
        vm.NewHostname = "localhost";
        vm.AddEntryCommand.Execute(null);

        Assert.Single(vm.Entries);
        Assert.Contains("already exists", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveCommand_WritesHostsFileAndBackup()
    {
        using var sandbox = HostsSandbox.Create();
        File.WriteAllText(sandbox.HostsPath, "127.0.0.1 localhost");

        var vm = new HostsFileEditorViewModel(sandbox.HostsPath, sandbox.BackupPath, autoLoad: false)
        {
            NewIp = "0.0.0.0",
            NewHostname = "ads.example.test",
            NewComment = "block"
        };
        vm.AddEntryCommand.Execute(null);

        vm.SaveCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsModified);

        var hostsContent = File.ReadAllText(sandbox.HostsPath);
        var backupContent = File.ReadAllText(sandbox.BackupPath);

        Assert.Contains("0.0.0.0\tads.example.test\t# block", hostsContent);
        Assert.Equal("127.0.0.1 localhost", backupContent);
        Assert.Contains("saved successfully", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreBackupCommand_RestoresSavedBackup()
    {
        using var sandbox = HostsSandbox.Create();
        File.WriteAllText(sandbox.HostsPath, "0.0.0.0 bad.test");
        File.WriteAllText(sandbox.BackupPath, "127.0.0.1 localhost");

        var vm = new HostsFileEditorViewModel(sandbox.HostsPath, sandbox.BackupPath, autoLoad: false);

        vm.RestoreBackupCommand.Execute(null);
        await WaitUntilAsync(() => vm.StatusMessage.Contains("restored", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("127.0.0.1 localhost", File.ReadAllText(sandbox.HostsPath));
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, int timeoutMs = 4000)
    {
        var start = DateTime.UtcNow;
        while (!predicate())
        {
            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Condition was not met before timeout.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class HostsSandbox : IDisposable
    {
        private readonly string _root;
        public string HostsPath { get; }
        public string BackupPath { get; }

        private HostsSandbox(string root)
        {
            _root = root;
            HostsPath = Path.Combine(root, "hosts");
            BackupPath = Path.Combine(root, "backup", "hosts.backup");
            Directory.CreateDirectory(Path.GetDirectoryName(BackupPath)!);
        }

        public static HostsSandbox Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "wup-hosts-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new HostsSandbox(root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup in tests.
            }
        }
    }
}
