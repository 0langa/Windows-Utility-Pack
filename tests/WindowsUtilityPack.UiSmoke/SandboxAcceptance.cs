using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.SystemUtilities.HostsFileEditor;
using WindowsUtilityPack.Tools.SystemUtilities.StartupManager;

// Destructive acceptance fixtures are restricted to the disposable Windows Sandbox guest.
// These exercise actual OS services and commands, not mouse/keyboard interaction.
internal static class SandboxAcceptance
{
    private sealed record Result(string Name, bool Completed, string? Error);

    internal static int Run(string reportPath)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!string.Equals(Environment.UserName, "WDAGUtilityAccount", StringComparison.OrdinalIgnoreCase)
            || !new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)
            || !Directory.Exists(@"C:\WupResults") || !Directory.Exists(@"C:\WupApp"))
        {
            File.WriteAllText(reportPath, "{\"refused\":\"Requires the configured elevated Windows Sandbox guest\"}");
            return 3;
        }

        var profile = @"C:\WupAcceptance\service-profile-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(profile);
        AppContext.SetData("WindowsUtilityPack.DataDirectory", profile);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var results = new List<Result>();
        using var watchdog = new System.Threading.Timer(_ =>
        {
            File.WriteAllText(reportPath + ".timeout.txt", "Sandbox service acceptance exceeded five minutes.");
            Environment.Exit(2);
        }, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
        app.Startup += async (_, _) =>
        {
            foreach (var (name, action) in new (string, Func<Task>)[]
            {
                ("actual-hosts-save-backup-restore", () => HostsAsync(profile)),
                ("machine-environment-rename-collision", EnvironmentAsync),
                ("startup-run-entry-disable-enable-remove", StartupAsync),
                ("registry-backup-restore-typed-values", () => RegistryAsync(profile)),
                ("scheduled-task-query-run", () => ScheduledTaskAsync(profile)),
            })
            {
                try { await action(); results.Add(new(name, true, null)); }
                catch (Exception ex) { results.Add(new(name, false, ex.ToString())); }
                File.WriteAllText(reportPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            }
            app.Shutdown(results.Any(r => !r.Completed) ? 1 : 0);
        };
        return app.Run();
    }

    private static async Task HostsAsync(string profile)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
        var original = File.ReadAllBytes(path);
        var backup = Path.Combine(profile, "hosts.backup");
        try
        {
            var vm = new HostsFileEditorViewModel(path, backup, false, new FixtureDialogs());
            vm.LoadCommand.Execute(null);
            await UntilAsync(() => !vm.IsBusy && vm.StatusMessage.StartsWith("Loaded", StringComparison.Ordinal));
            vm.NewIp = "127.0.0.1";
            vm.NewHostname = "wup-acceptance-" + Guid.NewGuid().ToString("N") + ".invalid";
            var name = vm.NewHostname;
            vm.NewComment = "Disposable Windows Sandbox fixture";
            vm.AddEntryCommand.Execute(null);
            vm.SaveCommand.Execute(null);
            await UntilAsync(() => !vm.IsBusy);
            Require(!vm.IsModified && File.ReadAllText(path).Contains(name, StringComparison.Ordinal), vm.StatusMessage);
            Require(File.ReadAllBytes(backup).SequenceEqual(original), "Hosts backup changed original bytes.");
            var modified = File.ReadAllBytes(path);
            vm.RestoreBackupCommand.Execute(null);
            await UntilAsync(() => !vm.IsBusy);
            Require(File.ReadAllBytes(path).SequenceEqual(original), vm.StatusMessage);
            Require(File.ReadAllBytes(backup + ".before-restore").SequenceEqual(modified), "Restore did not preserve the edited version.");
        }
        finally { AtomicFile.WriteAllBytes(path, original); }
    }

    private static Task EnvironmentAsync()
    {
        var store = new EnvironmentVariableStore();
        var first = "WUP_ACCEPTANCE_" + Guid.NewGuid().ToString("N");
        var second = first + "_RENAMED";
        var target = EnvironmentVariableTarget.Machine;
        try
        {
            EnvironmentVariableEditor.Save(store, target, null, null, first, "Grüße 🌍");
            EnvironmentVariableEditor.Save(store, target, null, null, second, "occupied");
            var rejected = false;
            try { EnvironmentVariableEditor.Save(store, target, first, "Grüße 🌍", second, "updated"); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && store.Get(first, target) == "Grüße 🌍" && store.Get(second, target) == "occupied", "Rename overwrote a collision.");
            store.Set(second, null, target);
            EnvironmentVariableEditor.Save(store, target, first, "Grüße 🌍", second, "renamed 🌍");
            Require(store.Get(first, target) is null && store.Get(second, target) == "renamed 🌍", "Machine environment rename did not persist.");
        }
        finally { store.Set(first, null, target); store.Set(second, null, target); }
        return Task.CompletedTask;
    }

    private static async Task StartupAsync()
    {
        const string run = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string disabled = @"Software\Microsoft\Windows\CurrentVersion\Run_Disabled";
        var name = "WupAcceptance-" + Guid.NewGuid().ToString("N");
        const string command = @"%SystemRoot%\System32\notepad.exe";
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(run)) key.SetValue(name, command, RegistryValueKind.ExpandString);
            var vm = new StartupManagerViewModel(new ClipboardService(), new StartupDiagnosticsService());
            await UntilAsync(() => !vm.IsLoading);
            vm.SelectedEntry = vm.Entries.Single(e => e.Name == name);
            vm.ToggleCommand.Execute(null);
            using (var key = Registry.CurrentUser.OpenSubKey(disabled))
            {
                Require(key?.GetValueKind(name) == RegistryValueKind.ExpandString, vm.StatusMessage);
                Require(Equals(key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), command), "Startup command was expanded.");
            }
            vm.ToggleCommand.Execute(null);
            using (var key = Registry.CurrentUser.OpenSubKey(run)) Require(Equals(key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), command), vm.StatusMessage);
            vm.RemoveCommand.Execute(null);
            using (var key = Registry.CurrentUser.OpenSubKey(run)) Require(key?.GetValue(name) is null, vm.StatusMessage);
        }
        finally
        {
            foreach (var path in new[] { run, disabled })
            { using var key = Registry.CurrentUser.OpenSubKey(path, true); key?.DeleteValue(name, false); }
        }
    }

    private static async Task RegistryAsync(string profile)
    {
        var path = @"Software\WupAcceptance-" + Guid.NewGuid().ToString("N");
        var values = new[] { " a|b ", "Grüße 🌍" };
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(path);
            key.SetValue("multi", values, RegistryValueKind.MultiString);
            key.SetValue("expand", "%TEMP%\\fixture", RegistryValueKind.ExpandString);
            var service = new RegistryEditorService();
            var backup = Path.Combine(profile, "registry.json");
            await service.BackupAsync(@"HKCU\" + path, backup);
            key.SetValue("multi", new[] { "changed" }, RegistryValueKind.MultiString);
            await service.RestoreAsync(backup);
            Require(((string[])key.GetValue("multi")!).SequenceEqual(values), "Multi-string backup lost data.");
            Require(Equals(key.GetValue("expand", null, RegistryValueOptions.DoNotExpandEnvironmentNames), "%TEMP%\\fixture"), "ExpandString backup changed raw data.");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path, false); }
    }

    private static async Task ScheduledTaskAsync(string profile)
    {
        var name = "WupAcceptance-" + Guid.NewGuid().ToString("N");
        var output = Path.Combine(profile, "scheduled-result.txt");
        var payload = Convert.ToBase64String(Encoding.Unicode.GetBytes($"[IO.File]::WriteAllText('{output}', 'fixture ran')"));
        try
        {
            await PowerShellAsync($"$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -NonInteractive -EncodedCommand {payload}'; Register-ScheduledTask -TaskName '{name}' -Action $action | Out-Null");
            var service = new TaskSchedulerService();
            var row = (await service.GetTasksAsync(name)).Single();
            Require(row.TaskName == "\\" + name, "Scheduler query changed task identity.");
            Require(await service.RunTaskAsync(row.TaskName), "Scheduler refused the fixture task.");
            await UntilAsync(() => File.Exists(output));
            Require(File.ReadAllText(output) == "fixture ran", "Scheduled action output was incorrect.");
        }
        finally { await PowerShellAsync($"Unregister-ScheduledTask -TaskName '{name}' -Confirm:$false -ErrorAction SilentlyContinue"); }
    }

    private static async Task PowerShellAsync(string script)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; " + script)) }) start.ArgumentList.Add(argument);
        var result = await new ProcessRunner(TimeSpan.FromSeconds(45)).RunAsync(start);
        Require(result.ExitCode == 0, result.StdErr);
    }

    private static async Task UntilAsync(Func<bool> predicate)
    {
        var deadline = Stopwatch.StartNew();
        while (!predicate())
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Sandbox command did not reach its expected state.");
            await Task.Delay(50);
        }
    }

    private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    private sealed class FixtureDialogs : IUserDialogService
    {
        public bool Confirm(string title, string message) => true;
        public void ShowInfo(string title, string message) { }
        public void ShowError(string title, string message) => throw new InvalidOperationException(title + ": " + message);
    }
}
