using System.IO;
using System.Text.Json;

namespace WindowsUtilityPack.NativeTests;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: NativeTests <published exe> <isolated work directory> <report.json> [--filter key] [--inspect palette title]");
            return 2;
        }
        var work = Path.GetFullPath(args[1]); Directory.CreateDirectory(work);
        var report = Path.GetFullPath(args[2]); Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        NativeApp.EvidenceDirectory = Path.GetDirectoryName(report);
        if (args.Contains("--installer")) return InstallerAcceptance.Run(Path.GetFullPath(args[0]), work, report);
        if (Option(args, "--legacy") is { } legacy) return LegacyMigrationAcceptance.Run(Path.GetFullPath(args[0]), Path.GetFullPath(legacy), work, report);
        var filter = Option(args, "--filter"); var inspect = Option(args, "--inspect");
        if (filter == null && inspect == null && Environment.UserName != "WDAGUtilityAccount")
            throw new InvalidOperationException("The complete suite must run inside Windows Sandbox; it changes disposable guest OS state.");
        var results = new List<CaseResult>();
        var appAssembly = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0]))!, "WindowsUtilityPack.dll");
        var appSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(appAssembly)));
        using var app = new NativeApp(Path.GetFullPath(args[0]), Path.Combine(work, "profile"));
        if (inspect != null)
        {
            app.Navigate(inspect); File.WriteAllText(report, app.Dump()); return 0;
        }
        var context = new Cases(app, work);
        var cases = context.All().ToArray();
        if (filter == null && cases.Select(t => t.Key).Distinct().Count() != 52)
            throw new InvalidOperationException("The full native suite must contain exactly the 52 registered tools.");
        var selected = filter?.Split(',').ToHashSet(StringComparer.Ordinal);
        if (selected != null)
        {
            if (selected.Any(key => !cases.Any(test => test.Key == key))) throw new ArgumentException("The filter contains an unknown tool key.");
            if (selected.Contains("background-task-monitor")) selected.Add("storage-master");
            if (selected.Contains("pentest-header-cookie-analyzer") || selected.Contains("pentest-response-diff-viewer")) selected.Add("pentest-http-request-builder");
        }
        var planned = cases.Where(t => selected == null || selected.Contains(t.Key)).ToArray();
        void SaveReport()
        {
            File.WriteAllText(report + ".tmp", JsonSerializer.Serialize(new { StartedUtc = results[0].StartedUtc, Executable = Path.GetFullPath(args[0]), AppAssemblySha256 = appSha256, NativeProcessId = app.Process.Id, Guest = Environment.UserName == "WDAGUtilityAccount", ExpectedToolCount = planned.Length, Completed = results.Count == planned.Length && results.All(r => r.Completed && r.EvidenceError == null), Cases = results }, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(report + ".tmp", report, true);
        }
        foreach (var test in planned)
        {
            var result = new CaseResult(test.Key, test.Title, DateTimeOffset.UtcNow);
            results.Add(result);
            NativeApp.Actions.Clear();
            SaveReport();
            try
            {
                app.Navigate(test.Title); test.Run();
                result.Completed = true;
            }
            catch (Exception ex)
            {
                result.Error = ex.ToString();
                Console.Error.WriteLine($"{test.Key}: {ex.Message}");
            }
            finally
            {
                result.FinishedUtc = DateTimeOffset.UtcNow;
                result.Actions = NativeApp.Actions.ToArray();
                try
                {
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(report)!, test.Key + ".tree.txt"), app.Dump());
                    app.SaveScreenshot(Path.Combine(Path.GetDirectoryName(report)!, test.Key + ".png"));
                }
                catch (Exception ex) { result.EvidenceError = ex.Message; }
                SaveReport();
            }
            if (!result.Completed) break; // retain the first failing state and avoid cascading modal-dialog failures
        }
        return results.Count == planned.Length && results.All(r => r.Completed && r.EvidenceError == null) ? 0 : 1;
    }
    private static string? Option(string[] args, string key) { var i = Array.IndexOf(args, key); return i >= 0 ? args[i + 1] : null; }
    private sealed record CaseResult(string Key, string Title, DateTimeOffset StartedUtc)
    {
        public DateTimeOffset FinishedUtc { get; set; }
        public bool Completed { get; set; }
        public string? Error { get; set; }
        public string? EvidenceError { get; set; }
        public object[] Actions { get; set; } = [];
    }
}
