using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace WindowsUtilityPack.NativeTests;

internal sealed partial class Cases
{
    private static string PowerShell(string script)
    {
        SandboxOnly();
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-EncodedCommand"); start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; " + script)));
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { process.Kill(true); throw new TimeoutException("Guest fixture PowerShell timed out."); }
        if (process.ExitCode != 0) throw new InvalidOperationException(stderr.Result);
        return stdout.Result;
    }
    private void SystemInfo()
    {
        ui.Click("LoadCommand"); ui.Expect("ComputerName", x => x == Environment.MachineName, "actual guest computer name", 30);
        ui.Expect("CpuLogicalProcessors", x => int.TryParse(x, out var n) && n == Environment.ProcessorCount, "actual logical CPU count");
        ui.Click("ExportJsonCommand"); var path = Path.Combine(work, "system-info.json"); ui.FileDialog(path);
        NativeApp.Wait(() => File.Exists(path), "system info export"); using var json = JsonDocument.Parse(File.ReadAllText(path)); Assert(json.RootElement.ValueKind == JsonValueKind.Object, "Invalid system export");
    }
    private void EnvironmentEditor()
    {
        SandboxOnly(); var name = "WUP_NATIVE_" + Guid.NewGuid().ToString("N");
        try
        {
            ui.Select("ScopeFilter", "User"); ui.Click("NewCommand"); ui.Set("EditName", name); ui.Set("EditValue", "Grüße | space value"); ui.Click("SaveCommand");
            NativeApp.Wait(() => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) == "Grüße | space value", "user environment save");
            ui.Set("SearchText", name); ui.Row("SelectedEntry", name); ui.Click("CopyValueCommand"); Assert(ClipboardText() == "Grüße | space value", "Environment value copy changed content");
            ui.Set("EditName", name + "_RENAMED"); ui.Click("SaveCommand");
            NativeApp.Wait(() => Environment.GetEnvironmentVariable(name + "_RENAMED", EnvironmentVariableTarget.User) == "Grüße | space value" && Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) == null, "environment rename");
            ui.Row("SelectedEntry", name + "_RENAMED"); ui.Click("DeleteCommand"); ui.Confirm("No"); Assert(Environment.GetEnvironmentVariable(name + "_RENAMED", EnvironmentVariableTarget.User) != null, "Cancelled delete changed environment");
            ui.Click("DeleteCommand"); ui.Confirm(); NativeApp.Wait(() => Environment.GetEnvironmentVariable(name + "_RENAMED", EnvironmentVariableTarget.User) == null, "environment delete");
            ui.Set("SearchText", ""); ui.Select("ScopeFilter", "Machine"); ui.Click("NewCommand"); ui.Set("EditName", name); ui.Set("EditValue", "guest-machine-only"); ui.Click("SaveCommand");
            NativeApp.Wait(() => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) == "guest-machine-only", "Machine scope writes machine environment");
            Assert(Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User) == null, "Machine scope leaked into User scope");
            ui.Set("SearchText", name); ui.Row("SelectedEntry", name); ui.Click("DeleteCommand"); ui.Confirm(); NativeApp.Wait(() => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) == null, "machine fixture delete");
        }
        finally { Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.User); Environment.SetEnvironmentVariable(name + "_RENAMED", null, EnvironmentVariableTarget.User); Environment.SetEnvironmentVariable(name, null, EnvironmentVariableTarget.Machine); }
    }
    private void RegistryEditor()
    {
        Assert(ui.Get("DeleteValueCommand").Current.BoundingRectangle.Height <= ui.Get("SaveValueCommand").Current.BoundingRectangle.Height * 1.25,
            "Registry delete action stretches to fill the editor panel");
        SandboxOnly(); var subkey = @"Software\WupNativeAcceptance\" + Guid.NewGuid().ToString("N");
        using var key = Registry.CurrentUser.CreateSubKey(subkey);
        try
        {
            ui.Set("KeyPath", @"HKCU\" + subkey); ui.Click("LoadKeyCommand"); ui.Set("EditValueName", "NativeValue"); ui.Set("EditValueData", "Grüße | retained"); ui.Click("SaveValueCommand");
            NativeApp.Wait(() => Equals(key.GetValue("NativeValue"), "Grüße | retained"), "registry value save");
            ui.Click("BackupCommand"); var backup = Path.Combine(work, "registry-backup.json"); ui.FileDialog(backup); NativeApp.Wait(() => File.Exists(backup), "registry backup");
            ui.Row("SelectedValue", "NativeValue"); ui.Set("EditValueData", "changed"); ui.Click("SaveValueCommand"); NativeApp.Wait(() => Equals(key.GetValue("NativeValue"), "changed"), "registry edit");
            ui.Click("RestoreCommand"); ui.Confirm(); ui.FileDialog(backup); NativeApp.Wait(() => Equals(key.GetValue("NativeValue"), "Grüße | retained"), "registry restore");
            ui.Row("SelectedValue", "NativeValue"); ui.Click("DeleteValueCommand"); ui.Confirm(); NativeApp.Wait(() => key.GetValue("NativeValue") == null, "registry delete");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(subkey, false); }
    }
    private void Hosts()
    {
        SandboxOnly(); var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts"); var original = File.ReadAllBytes(path);
        try
        {
            ui.Click("LoadCommand"); ui.Expect("StatusMessage", x => x.StartsWith("Loaded"), "hosts load");
            ui.Set("NewIp", "invalid"); ui.Set("NewHostname", "native.wup.test"); ui.Click("AddEntryCommand"); ui.Expect("StatusMessage", x => x.Contains("valid IP"), "invalid hosts input");
            ui.Set("NewIp", "127.0.0.1"); ui.Click("AddEntryCommand"); ui.Click("SaveCommand"); ui.Expect("StatusMessage", x => x == "Hosts file saved successfully.", "hosts save");
            Assert(File.ReadAllText(path).Contains("native.wup.test"), "Saved host entry missing");
            ui.Click("RestoreBackupCommand"); ui.Confirm(); ui.Expect("StatusMessage", x => x == "Hosts file restored from backup.", "hosts restore");
            Assert(File.ReadAllBytes(path).SequenceEqual(original), "Hosts restore changed original bytes");
        }
        finally { File.WriteAllBytes(path, original); }
    }
    private void Startup()
    {
        SandboxOnly(); const string run = @"Software\Microsoft\Windows\CurrentVersion\Run"; const string disabled = @"Software\Microsoft\Windows\CurrentVersion\Run_Disabled";
        var name = "WupNative_" + Guid.NewGuid().ToString("N"); const string command = "%WINDIR%\\System32\\cmd.exe /c exit";
        using var key = Registry.CurrentUser.CreateSubKey(run); key.SetValue(name, command, RegistryValueKind.ExpandString);
        try
        {
            ui.Click("RefreshCommand"); ui.Row("SelectedEntry", name); ui.Click("ToggleCommand");
            NativeApp.Wait(() => key.GetValue(name) == null, "startup disabled");
            using (var target = Registry.CurrentUser.OpenSubKey(disabled)) Assert(target?.GetValueKind(name) == RegistryValueKind.ExpandString && Equals(target.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), command), "Startup disable lost value type/data");
            ui.Click("ToggleCommand"); NativeApp.Wait(() => key.GetValue(name) != null, "startup enabled");
            ui.Click("ExportCsvCommand"); var path = Path.Combine(work, "startup.csv"); ui.FileDialog(path); NativeApp.Wait(() => File.Exists(path), "startup CSV"); Assert(File.ReadAllText(path).Contains(name), "Startup export lost fixture");
            ui.Click("RemoveCommand"); NativeApp.Wait(() => key.GetValue(name) == null, "startup removal");
        }
        finally { key.DeleteValue(name, false); using var target = Registry.CurrentUser.OpenSubKey(disabled, true); target?.DeleteValue(name, false); }
    }
    private void Processes()
    {
        SandboxOnly(); var exe = Path.Combine(work, "WupNativeChild.exe"); File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), exe);
        using var child = Process.Start(new ProcessStartInfo(exe, "-t 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true })!;
        try
        {
            ui.Check("AutoRefreshEnabled", false); ui.Set("SearchQuery", "WupNativeChild"); ui.Click("RefreshCommand"); ui.Row("SelectedProcess", "WupNativeChild");
            ui.Expect("SelectedProcess.ProcessId", x => x == "PID: " + child.Id, "owned process selection");
            ui.Click("CopyDetailsCommand"); Assert(ClipboardText().Contains(child.Id.ToString()), "Process copy missing PID");
            ui.Click("TerminateCommand"); ui.Confirm("No"); Assert(!child.HasExited, "Cancelled termination killed fixture");
            ui.Click("TerminateCommand"); ui.Confirm(); NativeApp.Wait(() => child.HasExited, "owned process termination");
        }
        finally { if (!child.HasExited) child.Kill(); }
    }
    private void ScheduledTask()
    {
        SandboxOnly(); var name = "WupNative" + Guid.NewGuid().ToString("N"); var marker = Path.Combine(work, "task-ran.txt");
        var action = Convert.ToBase64String(Encoding.Unicode.GetBytes($"[IO.File]::WriteAllText('{marker}', 'native task ran')"));
        PowerShell($"$action=New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -EncodedCommand {action}'; Register-ScheduledTask -TaskName '{name}' -Action $action -Force | Out-Null");
        try
        {
            ui.Set("SearchQuery", name); ui.Click("RefreshCommand"); ui.Row("SelectedTask", "\\" + name);
            Assert(!ui.Dump().Contains("Details unavailable"), "A task with no scheduled next run lost its details");
            ui.Click("RunSelectedCommand"); ui.Confirm("No"); Assert(!File.Exists(marker), "Cancelled scheduled task ran");
            ui.Click("RunSelectedCommand"); ui.Confirm(); NativeApp.Wait(() => File.Exists(marker), "scheduled task marker", 30); Assert(File.ReadAllText(marker) == "native task ran", "Task output changed");
        }
        finally { PowerShell($"Unregister-ScheduledTask -TaskName '{name}' -Confirm:$false"); }
    }
    private void Events()
    {
        SandboxOnly(); const string source = "WupNativeAcceptance";
        PowerShell($"if (-not [Diagnostics.EventLog]::SourceExists('{source}')) {{ New-EventLog -LogName Application -Source '{source}' }}; Write-EventLog -LogName Application -Source '{source}' -EntryType Information -EventId 4242 -Message 'WUP native event fixture'");
        ui.Select("SelectedLogName", "Application"); ui.Set("SourceFilter", source); ui.Set("EventIdFilterText", "4242"); ui.Click("RefreshCommand");
        ui.Expect("StatusMessage", x => x.StartsWith("Loaded"), "Windows event query", 30); ui.Click("CopyCsvCommand"); Assert(ClipboardText().Contains("WUP native event fixture"), "Event export missing actual guest event");
        ui.Set("EventIdFilterText", "invalid"); ui.Click("RefreshCommand"); ui.Expect("StatusMessage", x => x.Contains("Event ID must"), "invalid event ID feedback");
    }
    private void Activity()
    {
        ui.Click("RefreshCommand"); ui.Click("CopyCsvCommand"); var csv = ClipboardText(); Assert(csv.Contains("Timestamp", StringComparison.OrdinalIgnoreCase) || csv.Contains("Category"), "Activity CSV has no header");
        ui.Set("CategoryFilter", "no-such-native-category"); ui.Click("RefreshCommand"); ui.Expect("StatusMessage", x => x.Contains("No ") || x.Contains("0"), "activity filter empty result");
        ui.Set("CategoryFilter", ""); ui.Click("RefreshCommand"); ui.Click("ClearCommand"); ui.Confirm("No"); ui.Click("CopyCsvCommand"); Assert(ClipboardText().Length >= csv.Length, "Cancelled clear removed activity");
    }
    private void Hotkeys()
    {
        SandboxOnly(); ui.Check("HotkeysEnabled", true); ui.Click("SaveCommand"); ui.Confirm("OK"); ui.Expect("StatusMessage", x => x == "Hotkey configuration saved.", "hotkeys save");
        using (var foreground = new System.Windows.Forms.Form { Text = "Owned hotkey foreground fixture" })
        {
            foreground.Show(); foreground.Activate(); System.Windows.Forms.Application.DoEvents();
            System.Windows.Forms.SendKeys.SendWait("^k");
            var palette = ui.Dialog(); Assert(palette.Current.Name == "Command Palette", "Global shortcut did not open the palette from another process");
            ui.Get("QueryBox", palette).SetFocus(); System.Windows.Forms.SendKeys.SendWait("{ESC}");
            NativeApp.Wait(() => { try { return palette.Current.IsOffscreen; } catch (System.Windows.Automation.ElementNotAvailableException) { return true; } }, "global palette dismiss");
        }
        ui.Click("ExportProfileCommand"); var path = Path.Combine(work, "hotkeys.json"); ui.FileDialog(path); ui.Confirm("OK"); NativeApp.Wait(() => File.Exists(path), "hotkey export");
        ui.Check("HotkeysEnabled", false); ui.Click("SaveCommand"); ui.Confirm("OK"); ui.Click("ImportProfileCommand"); ui.FileDialog(path); ui.Confirm("OK"); ui.Expect("StatusMessage", x => x.StartsWith("Imported"), "hotkey import");
        ui.Check("HotkeysEnabled", false); ui.Click("SaveCommand"); ui.Confirm("OK");
        var registered = RegisterHotKey(IntPtr.Zero, 0x5755, 2, 0x4B);
        try { Assert(registered, "Disabled app retained its global Ctrl+K registration"); }
        finally { if (registered) UnregisterHotKey(IntPtr.Zero, 0x5755); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    private void Automation()
    {
        ui.Click("AddCommand"); ui.Click("SaveCommand"); ui.Expect("StatusMessage", x => x.StartsWith("Saved rule"), "automation rule save");
        ui.Set("SimulationDiskFreeGb", "0"); ui.Click("RunDryRunCommand"); ui.Expect("StatusMessage", x => x.Contains("dry", StringComparison.OrdinalIgnoreCase) || x.Contains("simulat", StringComparison.OrdinalIgnoreCase), "automation dry run");
        ui.Click("RefreshCommand"); ui.Row("SelectedRule", "New rule"); ui.Click("DeleteCommand"); ui.Confirm(); ui.Expect("StatusMessage", x => x.StartsWith("Deleted rule"), "automation rule delete");
    }
    private void Workspace()
    {
        var fieldHeight = ui.Get("ProfileName").Current.BoundingRectangle.Height;
        Assert(new[] { "SaveProfileCommand", "ApplyProfileCommand", "DeleteProfileCommand" }
            .All(id => ui.Get(id).Current.BoundingRectangle.Height <= fieldHeight * 2),
            "Workspace actions stretch to fill the details panel");
        ui.Set("ProfileName", "Native workspace"); ui.Set("ProfileDescription", "Grüße profile"); ui.Set("PinnedToolsCsv", "file-hash-calculator,system-info-dashboard"); ui.Click("SaveProfileCommand"); ui.Expect("StatusMessage", x => x.Contains("Saved profile"), "workspace save");
        ui.Row("SelectedProfile", "Native workspace"); ui.Click("ApplyProfileCommand"); ui.Confirm("OK"); ui.Expect("StatusMessage", x => x.Contains("Applied profile"), "workspace apply");
        ui.Navigate("System Info Dashboard"); ui.Navigate("Workspace Profiles"); ui.Row("SelectedProfile", "Native workspace"); ui.Expect("ProfileDescription", x => x == "Grüße profile", "workspace reopen");
        ui.Click("DeleteProfileCommand"); ui.Confirm(); ui.Expect("StatusMessage", x => x.Contains("Deleted"), "workspace delete");
    }
    private void Storage()
    {
        SandboxOnly();
        var folder = Path.Combine(work, "storage"); Directory.CreateDirectory(folder); Directory.CreateDirectory(Path.Combine(folder, "empty")); File.WriteAllText(Path.Combine(folder, "a.txt"), new string('a', 8192)); File.Copy(Path.Combine(folder, "a.txt"), Path.Combine(folder, "b.txt"));
        ui.Click("BrowseFolderCommand"); ui.FileDialog(folder, true); ui.Click("ScanCommand");
        ui.Expect("ScanStatusText", x => x.StartsWith("Scan complete") && x.Contains("2 files"), "storage scan count", 30);
        NativeApp.Wait(() => ui.Get("ScanDuplicatesCommand").Current.IsEnabled, "storage scan finished"); ui.Click("ScanDuplicatesCommand");
        ui.Expect("ScanStatusText", x => x.Contains("1 groups, 8.0 KB wasted"), "duplicate bytes", 30);
        ui.Click("SaveSnapshotCommand"); ui.Expect("ScanStatusText", x => x.StartsWith("Snapshot saved"), "storage snapshot save");
        ui.Tab("Reports"); ui.Click("ExportFileCsvCommand"); var path = Path.Combine(work, "storage.csv"); ui.FileDialog(path); NativeApp.Wait(() => File.Exists(path), "storage file report"); Assert(File.ReadAllText(path).Contains("a.txt") && File.ReadAllText(path).Contains("b.txt"), "Storage export missing files");
        ui.Click("AnalyseCleanupCommand"); ui.Expect("PolicyPreviewSummary", x => x.Contains("of 2 recommendations"), "cleanup recommendations");
        var buttons = new[] { "PreviewCleanupPolicyCommand", "ApplyCleanupPolicyCommand", "ExecuteCleanupPolicyRecycleCommand", "SelectAllCleanupCommand", "DeselectAllCleanupCommand", "RecycleSelectedCleanupCommand", "DeleteSelectedCleanupCommand" }.Select(id => ui.Get(id).Current.BoundingRectangle).ToArray();
        Assert(buttons.All(rect => ui.Window.Current.BoundingRectangle.Contains(rect)), "Cleanup toolbar controls extend beyond the app window");
        Assert(!buttons.SelectMany((rect, index) => buttons.Skip(index + 1).Select(other => rect.IntersectsWith(other))).Any(overlap => overlap), "Cleanup toolbar controls overlap");
        ui.Click("SelectAllCleanupCommand");
        ui.Click("RecycleSelectedCleanupCommand"); ui.Confirm("No"); Assert(Directory.GetFiles(folder).Length == 2 && Directory.Exists(Path.Combine(folder, "empty")), "Cancelled cleanup changed fixtures");
        ui.Click("RecycleSelectedCleanupCommand"); ui.Confirm(); ui.Expect("ScanStatusText", x => x == "Recycled 2 items.", "duplicate and empty directory recycled");
        Assert(Directory.GetFiles(folder).Length == 1 && File.ReadAllText(Directory.GetFiles(folder).Single()) == new string('a', 8192), "Cleanup did not preserve the duplicate survivor");
        PowerShell($"$shell=New-Object -ComObject Shell.Application; $items=@($shell.NameSpace(10).Items() | Where-Object {{ $_.ExtendedProperty('System.Recycle.DeletedFrom') -eq '{folder}' }}); if ($items.Count -ne 2) {{ throw 'Expected two owned Recycle Bin entries' }}; $items | ForEach-Object {{ $_.InvokeVerb('undelete') }}");
        NativeApp.Wait(() => File.Exists(Path.Combine(folder, "a.txt")) && File.Exists(Path.Combine(folder, "b.txt")) && Directory.Exists(Path.Combine(folder, "empty")), "Windows Recycle Bin fixture recovery");
        Assert(File.ReadAllBytes(Path.Combine(folder, "a.txt")).SequenceEqual(File.ReadAllBytes(Path.Combine(folder, "b.txt"))), "Recycle Bin recovery changed bytes");
    }
    private void BackgroundTasks()
    {
        ui.Click("RefreshCommand"); ui.Expect("StatusMessage", x => x.Contains("task", StringComparison.OrdinalIgnoreCase), "background task refresh");
        var tree = ui.Dump(); Assert(tree.Contains("Completed") && tree.Contains("Storage", StringComparison.OrdinalIgnoreCase), "Storage task completion missing from monitor");
        Assert(!ui.Get("CancelSelectedCommand").Current.IsEnabled, "Unselected completed task should not be cancellable");
        ui.Button("Open current tool in detached window"); var monitor = ui.Dialog();
        try
        {
            ui.Navigate("Port Scanner"); ui.Set("Host", "127.0.0.1"); ui.Set("PortRangeText", "1-65535"); ui.Set("Concurrency", "1"); ui.Click("ScanCommand");
            ui.TargetWindow = monitor; ui.Row("SelectedTask", "Port scan"); ui.Click("CancelSelectedCommand");
            ui.TargetWindow = null; ui.Expect("StatusMessage", x => x.StartsWith("Scan stopped"), "monitor cancellation stopped the actual port worker");
        }
        finally { ui.TargetWindow = null; ((System.Windows.Automation.WindowPattern)monitor.GetCurrentPattern(System.Windows.Automation.WindowPattern.Pattern)).Close(); }
        ui.Navigate("Background Task Monitor"); Assert(ui.Dump().Contains("Cancelled from task monitor."), "Monitor lost canceled task state");
    }
}
