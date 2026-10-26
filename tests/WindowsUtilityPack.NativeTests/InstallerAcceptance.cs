using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;

namespace WindowsUtilityPack.NativeTests;

internal static class InstallerAcceptance
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{CA86909A-47C7-43D5-9E5C-52C74B8E98B7}_is1";
    internal static int Run(string packages, string work, string reportPath)
    {
        if (Environment.UserName != "WDAGUtilityAccount") throw new InvalidOperationException("Installer lifecycle is guest-only.");
        Directory.CreateDirectory(work); var steps = new List<object>(); var completed = false; string? error = null;
        var install = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WindowsUtilityPack");
        var exe = Path.Combine(install, "WindowsUtilityPack.exe");
        var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsUtilityPack");
        var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Windows Utility Pack.lnk");
        var oldSetup = Path.Combine(packages, "WindowsUtilityPack-0.9.0-win-x64-setup.exe");
        var newSetup = Path.Combine(packages, "WindowsUtilityPack-1.0.0-win-x64-setup.exe");
        string? oldSetupHash = null, newSetupHash = null, installedAppHash = null;
        var evidence = Path.GetDirectoryName(reportPath)!;
        try
        {
            oldSetupHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(oldSetup)));
            newSetupHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(newSetup)));
            Check(!File.Exists(exe) && Version() == null, "Expected a clean installation state");
            using (var setup = Process.Start(new ProcessStartInfo(oldSetup) { UseShellExecute = false })!)
            {
                try
                {
                AutomationElement? cancel = null;
                NativeApp.Wait(() => (cancel = ButtonForProcess(setup.Id, "Cancel")) != null, "installer cancel button", 30);
                NativeApp.Invoke(cancel!);
                AutomationElement? yes = null;
                NativeApp.Wait(() => setup.HasExited || (yes = ButtonForProcess(setup.Id, "Yes")) != null, "cancel setup confirmation");
                if (yes != null) NativeApp.Invoke(yes);
                Check(setup.WaitForExit(30000), "Cancelled installer did not exit");
                Check(!File.Exists(exe) && Version() == null, "Cancelled setup left an installed app");
                steps.Add(new { Name = "interactive-cancel", ExitCode = setup.ExitCode });
                }
                finally { if (!setup.HasExited) setup.Kill(entireProcessTree: true); }
            }
            var oldCode = Execute(oldSetup, $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=\"{Path.Combine(work, "install-old.log")}\"");
            Check(oldCode == 0 && Version() == "0.9.0" && File.Exists(shortcut), "Initial per-user install/registration/shortcut missing");
            Check(File.Exists(Path.Combine(install, "coreclr.dll")), "Installer lacks bundled runtime");
            Directory.CreateDirectory(profile);
            File.WriteAllText(Path.Combine(profile, "settings.json"), "{\"HotkeysEnabled\":false,\"TrayModeEnabled\":false,\"CloseToTray\":false,\"ClipboardMonitoringEnabled\":false}");
            File.WriteAllText(Path.Combine(profile, "user-retained.txt"), "preserve user data");
            using (var app = new NativeApp(exe, null))
            {
                app.Navigate("Workspace Profiles"); app.Set("ProfileName", "Upgrade fixture"); app.Set("ProfileDescription", "Preserved Grüße workspace"); app.Click("SaveProfileCommand"); app.Expect("StatusMessage", x => x.Contains("Saved profile"), "old install saved state");
                var before = SHA256.HashData(File.ReadAllBytes(Path.Combine(install, "WindowsUtilityPack.dll")));
                var busy = Execute(newSetup, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART");
                Check(busy != 0 && Version() == "0.9.0" && SHA256.HashData(File.ReadAllBytes(Path.Combine(install, "WindowsUtilityPack.dll"))).SequenceEqual(before), "Running-app update was not rejected without changing payload");
                steps.Add(new { Name = "install-and-running-update-guard", InstallExitCode = oldCode, BlockedExitCode = busy });
            }
            var upgrade = Execute(newSetup, $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=\"{Path.Combine(work, "upgrade.log")}\"");
            Check(upgrade == 0 && Version() == "1.0.0", "Upgrade did not register current version");
            installedAppHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(install, "WindowsUtilityPack.dll"))));
            using (var app = new NativeApp(exe, null))
            {
                app.Navigate("Workspace Profiles"); app.Row("SelectedProfile", "Upgrade fixture"); app.Expect("ProfileDescription", x => x == "Preserved Grüße workspace", "upgrade preserved saved app state");
                File.WriteAllText(Path.Combine(evidence, "upgraded-workspace.tree.txt"), app.Dump());
                app.SaveScreenshot(Path.Combine(evidence, "upgraded-workspace.png"));
                var busy = Execute(Path.Combine(install, "unins000.exe"), "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART");
                Check(busy != 0 && File.Exists(exe), "Running-app uninstall guard failed");
                steps.Add(new { Name = "upgrade-and-running-uninstall-guard", UpgradeExitCode = upgrade, BlockedExitCode = busy });
            }
            File.WriteAllText(Path.Combine(install, "user-kept.txt"), "unknown user file");
            var uninstall = Execute(Path.Combine(install, "unins000.exe"), $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=\"{Path.Combine(work, "uninstall.log")}\"");
            NativeApp.Wait(() => !File.Exists(exe) && Version() == null, "uninstalled payload and registration", 30);
            Check(uninstall == 0 && !File.Exists(shortcut) && File.Exists(Path.Combine(install, "user-kept.txt")) && File.ReadAllText(Path.Combine(profile, "user-retained.txt")) == "preserve user data", "Uninstall removed user data or retained app shortcut");
            steps.Add(new { Name = "uninstall-preserves-profile-and-unknown-files", ExitCode = uninstall });
            Check(Execute(newSetup, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") == 0, "Reinstall failed");
            using (var app = new NativeApp(exe, null))
            {
                app.Navigate("Workspace Profiles"); app.Row("SelectedProfile", "Upgrade fixture"); app.Expect("ProfileDescription", x => x == "Preserved Grüße workspace", "reinstall reused retained data");
                app.SaveScreenshot(Path.Combine(evidence, "reinstalled-workspace.png"));
            }
            Check(Execute(Path.Combine(install, "unins000.exe"), "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") == 0, "Final uninstall failed");
            steps.Add(new { Name = "reinstall-reopens-retained-workspace" }); completed = true;
        }
        catch (Exception ex) { error = ex.ToString(); Console.Error.WriteLine(ex.Message); }
        foreach (var log in Directory.GetFiles(work, "*.log")) File.Copy(log, Path.Combine(evidence, Path.GetFileName(log)), true);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Completed = completed, FinishedUtc = DateTimeOffset.UtcNow, Guest = Environment.UserName, InstallDirectory = install, PriorVersionIsGeneratedFixture = true, OldSetupSha256 = oldSetupHash, NewSetupSha256 = newSetupHash, InstalledAppAssemblySha256 = installedAppHash, Steps = steps, Actions = NativeApp.Actions.ToArray(), Error = error }, new JsonSerializerOptions { WriteIndented = true }));
        return completed ? 0 : 1;
    }
    private static int Execute(string executable, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = false })!;
        if (!process.WaitForExit(120000)) { process.Kill(true); throw new TimeoutException("Installer/uninstaller exceeded two minutes."); }
        return process.ExitCode;
    }
    private static string? Version() { using var key = Registry.CurrentUser.OpenSubKey(UninstallKey); return key?.GetValue("DisplayVersion") as string; }
    private static void Check(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
    private static AutomationElement? ButtonForProcess(int pid, string name)
    {
        var owned = Descendants(pid);
        foreach (var window in AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition).Cast<AutomationElement>().Where(w => owned.Contains(w.Current.ProcessId)))
            foreach (var button in window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)).Cast<AutomationElement>())
                if (button.Current.IsEnabled && (button.Current.Name.Replace("&", "") == name
                    || (name == "Yes" && button.Current.AutomationId == "6" && button.Current.ClassName == "Button"))) return button;
        return null;
    }
    private static HashSet<int> Descendants(int root)
    {
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == (IntPtr)(-1)) throw new System.ComponentModel.Win32Exception();
        var parents = new Dictionary<int, int>();
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            if (Process32First(snapshot, ref entry))
                do { parents[(int)entry.ProcessId] = (int)entry.ParentProcessId; } while (Process32Next(snapshot, ref entry));
        }
        finally { CloseHandle(snapshot); }
        var result = new HashSet<int> { root }; bool changed;
        do { changed = false; foreach (var pair in parents) if (result.Contains(pair.Value)) changed |= result.Add(pair.Key); } while (changed);
        return result;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        internal uint Size, Usage, ProcessId; internal IntPtr DefaultHeapId;
        internal uint ModuleId, Threads, ParentProcessId; internal int BasePriority; internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string ExeFile;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode)] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode)] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
