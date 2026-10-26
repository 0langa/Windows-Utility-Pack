using System.IO;
using WindowsUtilityPack.Tools.SystemUtilities.HostsFileEditor;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class HostsFileEditorViewModelTests
{
    [Fact]
    public async Task Save_PreservesUnicodeAliasesAndUnchangedLines()
    {
        using var sandbox = HostsSandbox.Create();
        var original = "# Grüß dich 🌍\r\n\r\n127.0.0.1 localhost alias.test # literal\r\n";
        await File.WriteAllTextAsync(sandbox.HostsPath, original, new System.Text.UTF8Encoding(true));
        var bytes = await File.ReadAllBytesAsync(sandbox.HostsPath);
        var vm = new HostsFileEditorViewModel(sandbox.HostsPath, sandbox.BackupPath, false);
        vm.LoadCommand.Execute(null);
        await WaitUntilAsync(() => vm.StatusMessage.StartsWith("Loaded"));
        vm.NewIp = "0.0.0.0"; vm.NewHostname = "added.test"; vm.AddEntryCommand.Execute(null);
        vm.SaveCommand.Execute(null);
        await WaitUntilAsync(() => vm.StatusMessage.Contains("saved successfully"));
        Assert.StartsWith(original, await File.ReadAllTextAsync(sandbox.HostsPath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(sandbox.BackupPath));
        Assert.Equal(new byte[] { 239, 187, 191 }, (await File.ReadAllBytesAsync(sandbox.HostsPath)).Take(3));
    }

    [Fact]
    public async Task Save_WhenBackupFails_PreservesOriginalAndDraft()
    {
        using var sandbox = HostsSandbox.Create();
        await File.WriteAllTextAsync(sandbox.HostsPath, "127.0.0.1 localhost");
        Directory.CreateDirectory(sandbox.BackupPath); // A directory cannot be replaced by the backup file.
        var vm = new HostsFileEditorViewModel(sandbox.HostsPath, sandbox.BackupPath, false);
        vm.LoadCommand.Execute(null); await WaitUntilAsync(() => vm.StatusMessage.StartsWith("Loaded"));
        vm.NewIp = "0.0.0.0"; vm.NewHostname = "added.test"; vm.AddEntryCommand.Execute(null);
        vm.SaveCommand.Execute(null);
        await WaitUntilAsync(() => vm.StatusMessage.Contains("failed", StringComparison.OrdinalIgnoreCase) || !vm.IsModified);
        Assert.True(vm.IsModified);
        Assert.Equal("127.0.0.1 localhost", await File.ReadAllTextAsync(sandbox.HostsPath));
    }

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
        vm.LoadCommand.Execute(null);
        await WaitUntilAsync(() => vm.StatusMessage.StartsWith("Loaded", StringComparison.Ordinal));
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

        var vm = new HostsFileEditorViewModel(sandbox.HostsPath, sandbox.BackupPath, autoLoad: false, new ConfirmDialogs());

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

    private sealed class ConfirmDialogs : WindowsUtilityPack.Services.IUserDialogService
    {
        public bool Confirm(string title, string message) => true;
        public void ShowInfo(string title, string message) { }
        public void ShowError(string title, string message) { }
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
