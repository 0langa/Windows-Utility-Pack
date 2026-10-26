using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Automation;

namespace WindowsUtilityPack.NativeTests;

internal static class LegacyMigrationAcceptance
{
    internal static int Run(string currentExe, string legacyExe, string work, string reportPath)
    {
        if (Environment.UserName != "WDAGUtilityAccount") throw new InvalidOperationException("Legacy migration acceptance is guest-only.");
        const string expectedLegacyHash = "A09559B799425B827A1749E7476A2E55948AD6AEA65D1B52EAA80A65B8EB60AF";
        const string password = "Generated alpha migration fixture 42!";
        const string secret = "alpha-private-Grüße-42";
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsUtilityPack");
        var roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsUtilityPack");
        var vault = Path.Combine(roaming, "vault.json");
        var migratedVault = Path.Combine(local, "vault.json");
        var evidence = Path.GetDirectoryName(reportPath)!;
        var steps = new List<string>();
        string? error = null;
        var complete = false;
        try
        {
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(legacyExe))) == expectedLegacyHash, "Unexpected published alpha binary");
            Check(!File.Exists(vault) && !File.Exists(Path.Combine(local, "settings.json")), "Migration requires a fresh guest profile");
            Directory.CreateDirectory(local);
            // Disable machine integrations, and avoid the alpha's NaN window-state serialization bug.
            File.WriteAllText(Path.Combine(local, "settings.json"), "{\"RememberWindowPosition\":false,\"WindowLeft\":0,\"WindowTop\":0,\"HotkeysEnabled\":false,\"TrayModeEnabled\":false,\"CloseToTray\":false,\"ClipboardMonitoringEnabled\":false}");
            using (var old = new NativeApp(legacyExe, null))
            {
                old.Set("SearchBox", "Local Secret Vault");
                var title = old.Named("Local Secret Vault", ControlType.Text);
                var button = title;
                while (button.Current.ControlType != ControlType.Button)
                    button = TreeWalker.ControlViewWalker.GetParent(button) ?? throw new InvalidOperationException("Alpha vault card has no button");
                NativeApp.Invoke(button);
                old.Set("MasterPasswordBox", password); old.Button("Unlock Vault");
                NativeApp.Wait(() => old.Dump().Contains("unlocked", StringComparison.OrdinalIgnoreCase), "alpha vault created");
                old.Button("+ Add Secret");
                SetNamed(old, "Secret name", "Alpha migration fixture");
                SetNamed(old, "Secret category", "migration");
                old.Set("EditValuePasswordBox", secret);
                SetNamed(old, "Secret notes", "Retained from the published v0.1.0-alpha binary");
                old.Button("Save"); NativeApp.Wait(() => old.Dump().Contains("Secret saved."), "alpha secret persisted");
                old.Button("Lock Vault");
                old.SaveScreenshot(Path.Combine(evidence, "alpha-vault.png"));
                File.WriteAllText(Path.Combine(evidence, "alpha-vault.tree.txt"), old.Dump());
            }
            var original = File.ReadAllBytes(vault);
            Check(!JsonDocument.Parse(original).RootElement.TryGetProperty("Version", out var version) || version.GetInt32() < 2, "Alpha did not write a legacy vault");
            File.Copy(vault, Path.Combine(work, "original-alpha-vault.json"));
            steps.Add("Published alpha created an encrypted vault through native controls");
            using (var current = new NativeApp(currentExe, null))
            {
                current.Navigate("Local Secret Vault");
                current.Set("MasterPasswordBox", "incorrect fixture password"); current.Click("UnlockCommand");
                current.Expect("StatusMessage", x => x.StartsWith("Incorrect master password"), "legacy wrong-password rejection");
                Check(File.ReadAllBytes(vault).SequenceEqual(original), "Wrong password changed the legacy vault");
                Thread.Sleep(1100);
                current.Set("MasterPasswordBox", password); current.Click("UnlockCommand");
                current.Expect("StatusMessage", x => x.StartsWith("Vault unlocked"), "published-alpha vault migration");
                current.Row("SelectedSecret", "Alpha migration fixture"); current.Click("CopyValueCommand");
                NativeApp.Wait(() => NativeApp.ReadClipboardText() == secret, "migrated secret value");
                current.Expect("EditNotes", x => x.Contains("v0.1.0-alpha"), "legacy secret notes");
                Check(JsonDocument.Parse(File.ReadAllText(migratedVault)).RootElement.GetProperty("Version").GetInt32() == 2, "Vault was not migrated to version 2");
                Check(File.ReadAllBytes(migratedVault + ".bak").SequenceEqual(original), "Encrypted alpha backup changed");
                Check(File.ReadAllBytes(vault).SequenceEqual(original), "Original roaming-profile alpha vault changed");
                current.Click("LockCommand");
                current.SaveScreenshot(Path.Combine(evidence, "migrated-vault.png"));
            }
            steps.Add("Current app rejected a wrong password, migrated values and retained an identical encrypted backup");
            using (var restarted = new NativeApp(currentExe, null))
            {
                restarted.Navigate("Local Secret Vault"); restarted.Set("MasterPasswordBox", password); restarted.Click("UnlockCommand");
                restarted.Expect("StatusMessage", x => x.StartsWith("Vault unlocked"), "migrated vault after process restart");
                restarted.Row("SelectedSecret", "Alpha migration fixture"); restarted.Click("CopyValueCommand");
                NativeApp.Wait(() => NativeApp.ReadClipboardText() == secret, "migrated secret after process restart");
                restarted.Click("LockCommand");
            }
            steps.Add("Migrated vault reopened in a new process with the same secret");
            complete = true;
        }
        catch (Exception ex) { error = ex.ToString(); Console.Error.WriteLine(error); }
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Completed = complete, FinishedUtc = DateTimeOffset.UtcNow, LegacyRelease = "v0.1.0-alpha", LegacyExecutableSha256 = expectedLegacyHash, AppAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(currentExe)!, "WindowsUtilityPack.dll")))), Steps = steps, Actions = NativeApp.Actions.ToArray(), Error = error }, new JsonSerializerOptions { WriteIndented = true }));
        return complete ? 0 : 1;
    }
    private static void SetNamed(NativeApp app, string name, string value) =>
        ((ValuePattern)app.Named(name, ControlType.Edit).GetCurrentPattern(ValuePattern.Pattern)).SetValue(value);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
