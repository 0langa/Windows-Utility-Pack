using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WindowsUtilityPack.NativeTests;

internal sealed partial class Cases(NativeApp ui, string work)
{
    internal sealed record Test(string Key, string Title, Action Run);
    internal IEnumerable<Test> All()
    {
        yield return new("base64-url-encoder", "Base64 / URL Encoder-Decoder", Base64);
        yield return new("json-yaml-validator", "JSON / YAML Validator", StructuredData);
        yield return new("regex-tester", "Regex Tester", RegexTool);
        yield return new("diff-tool", "Diff Tool", Diff);
        yield return new("timestamp-converter", "Timestamp Converter", Timestamp);
        yield return new("uuid-generator", "UUID / ULID Generator", Identifiers);
        yield return new("hash-generator", "Hash Generator", Hashes);
        yield return new("password-generator", "Password Generator", Passwords);
        yield return new("pentest-jwt-inspector", "JWT Inspector", Jwt);
        yield return new("http-request-tester", "HTTP Request Tester", HttpTester);
        yield return new("port-scanner", "Port Scanner", PortScan);
        yield return new("ping-tool", "Ping Tool", Ping);
        yield return new("network-speed-test", "Network Speed Test", Speed);
        yield return new("pentest-http-request-builder", "HTTP Request Builder", RequestBuilder);
        yield return new("pentest-header-cookie-analyzer", "Header & Cookie Analyzer", HeaderAnalyzer);
        yield return new("pentest-response-diff-viewer", "Response Diff Viewer", ResponseDiff);
        yield return new("certificate-inspector", "Certificate Inspector", CertificateInspect);
        yield return new("file-hash-calculator", "File Hash Calculator", FileHash);
        yield return new("bulk-renamer", "Bulk File Renamer", Rename);
        yield return new("file-splitter-joiner", "File Splitter / Joiner", SplitJoin);
        yield return new("secure-file-shredder", "Secure File Shredder", Shred);
        yield return new("metadata-editor", "Metadata Viewer/Editor", Metadata);
        yield return new("log-file-analyzer", "Log File Analyzer", LogAnalyzer);
        yield return new("markdown-editor", "Markdown Editor", Markdown);
        yield return new("text-format-converter", "Text Format Converter", TextConvert);
        yield return new("system-info-dashboard", "System Info Dashboard", SystemInfo);
        yield return new("env-vars-editor", "Environment Variables Editor", EnvironmentEditor);
        yield return new("registry-editor", "Registry Editor", RegistryEditor);
        yield return new("hosts-file-editor", "Hosts File Editor", Hosts);
        yield return new("startup-manager", "Startup Manager", Startup);
        yield return new("process-explorer", "Process Explorer", Processes);
        yield return new("task-scheduler-ui", "Task Scheduler UI", ScheduledTask);
        yield return new("event-log-viewer", "Event Log Viewer", Events);
        yield return new("activity-log", "Activity Log", Activity);
        yield return new("hotkey-manager", "Hotkey Manager", Hotkeys);
        yield return new("automation-rules", "Automation Rules", Automation);
        yield return new("workspace-profiles", "Workspace Profiles", Workspace);
        yield return new("storage-master", "Storage Master", Storage);
        yield return new("background-task-monitor", "Background Task Monitor", BackgroundTasks);
        yield return new("local-secret-vault", "Local Secret Vault", Vault);
        yield return new("certificate-manager", "Certificate Manager", Certificates);
        yield return new("ssh-remote-tool", "SSH Remote Tool", Ssh);
        yield return new("downloader", "Downloader Studio", Downloader);
        yield return new("pentest-site-map-explorer", "Site Map Explorer", SiteMap);
        yield return new("api-mock-server", "API Mock Server", MockServer);
        yield return new("qr-code-generator", "QR Code Generator", Qr);
        yield return new("clipboard-manager", "Clipboard Manager", ClipboardManager);
        yield return new("color-picker", "Color Picker", Color);
        yield return new("image-format-converter", "Image Format Converter", ImageConvert);
        yield return new("image-resizer", "Image Resizer & Compressor", ImageResize);
        yield return new("screenshot-annotator", "Screenshot Annotator", Annotate);
        yield return new("dns-lookup", "DNS Lookup", Dns);
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private string Fixture(string name, string text) { var path = Path.Combine(work, name); File.WriteAllText(path, text, new UTF8Encoding(false)); return path; }
    private static string ClipboardText()
    {
        string? text = null;
        NativeApp.Wait(() => {
            System.Windows.Forms.Application.DoEvents();
            text = NativeApp.ReadClipboardText(); return text != null;
        }, "clipboard available", 3);
        return text!;
    }
    private static void SandboxOnly() { if (Environment.UserName != "WDAGUtilityAccount") throw new InvalidOperationException("This case requires the disposable Windows Sandbox guest."); }

    private void Base64()
    {
        const string input = "Grüße 🧪 + & < >";
        ui.Set("InputText", input); ui.Click("EncodeCommand");
        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        ui.Expect("OutputText", x => x == expected, "UTF-8 Base64 output");
        ui.Click("SwapCommand"); ui.Click("DecodeCommand");
        ui.Expect("OutputText", x => x == input, "Unicode round trip");
        ui.Set("InputText", "%%%invalid"); ui.Click("DecodeCommand");
        ui.Expect("StatusMessage", x => x.Contains("Error"), "malformed Base64 error");
        ui.Select("Mode", "URL Encode"); ui.Set("InputText", "a+b & Grüße"); ui.Click("EncodeCommand");
        ui.Expect("OutputText", x => x.Contains("%2B", StringComparison.OrdinalIgnoreCase), "literal plus encoded");
        ui.Click("SwapCommand"); ui.Click("DecodeCommand");
        ui.Expect("OutputText", x => x == "a+b & Grüße", "URL round trip");
        ui.Click("ClearCommand"); ui.Expect("InputText", x => x == "", "clear input");
    }
    private void StructuredData()
    {
        ui.Set("InputText", "{\"name\":\"Grüße\",\"n\":42}"); ui.Click("FormatCommand");
        ui.Expect("OutputText", x => x.Contains("42"), "formatted JSON");
        using (var json = JsonDocument.Parse(ui.Text("OutputText"))) Assert(json.RootElement.GetProperty("name").GetString() == "Grüße", "JSON Unicode changed");
        ui.Set("InputText", "{bad"); ui.Click("ValidateCommand");
        ui.Expect("StatusMessage", x => x.Contains("invalid", StringComparison.OrdinalIgnoreCase) || x.Contains("error", StringComparison.OrdinalIgnoreCase), "invalid JSON feedback");
        ui.Select("Mode", "YAML"); ui.Set("InputText", "name: native\nn: 42"); ui.Click("FormatCommand");
        ui.Expect("OutputText", x => x.Contains("native"), "YAML recovery"); ui.Click("ClearCommand");
    }
    private void RegexTool()
    {
        ui.Set("Pattern", "[A-Z]+\\d+"); ui.Set("InputText", "AB12 xx CD34");
        ui.Expect("StatusMessage", x => x.StartsWith("2 match", StringComparison.OrdinalIgnoreCase), "two regex matches");
        ui.Set("Pattern", "["); ui.Expect("StatusMessage", x => x.Contains("invalid", StringComparison.OrdinalIgnoreCase) || x.Contains("error", StringComparison.OrdinalIgnoreCase), "invalid regex feedback");
        ui.Set("Pattern", "(?i)hello"); ui.Set("InputText", "HELLO hello");
        ui.Expect("StatusMessage", x => x.StartsWith("2 match", StringComparison.OrdinalIgnoreCase), "regex error recovery");
    }
    private void Diff()
    {
        ui.Set("LeftText", "alpha\nbeta\nGrüße"); ui.Set("RightText", "alpha\ngamma\nGrüße");
        ui.Expect("StatusMessage", x => x == "1 added, 1 removed, 2 unchanged", "known diff");
        ui.Check("IgnoreCase", true); ui.Set("LeftText", "ABC"); ui.Set("RightText", "abc");
        ui.Expect("StatusMessage", x => x == "0 added, 0 removed, 1 unchanged", "ignore case");
        ui.Click("ClearCommand"); ui.Expect("LeftText", x => x == "", "diff clear");
    }
    private void Timestamp()
    {
        ui.Set("UnixSeconds", "0"); ui.Expect("HumanReadableUtc", x => x.Contains("1970"), "Unix epoch");
        ui.Set("UnixSeconds", "-1"); ui.Expect("HumanReadableUtc", x => x.Contains("1969"), "negative Unix timestamp");
        ui.Set("UnixSeconds", "999999999999999999999999"); ui.Expect("CustomFormatResult", x => x == "", "invalid timestamp clears result");
        ui.Set("DateInput", "2000-01-01T00:00:00Z"); ui.Click("ParseDateCommand");
        ui.Expect("ParsedUnixSeconds", x => x == "946684800", "ISO UTC date");
    }
    private void Identifiers()
    {
        Assert(Guid.TryParse(ui.Text("SingleId"), out _), "Initial UUID cannot parse");
        ui.Select("Format", "No Hyphens"); ui.Expect("SingleId", x => x.Length == 32, "UUID format changes existing value");
        ui.Select("IdType", "ULID"); ui.Click("GenerateSingleCommand");
        ui.Expect("SingleId", x => Regex.IsMatch(x, "^[0-7][0-9A-HJKMNP-TV-Z]{25}$"), "ULID alphabet/range");
        ui.Set("GenerateCount", "25"); ui.Click("GenerateBulkCommand"); ui.Click("CopyAllCommand");
        var values = ClipboardText().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Assert(values.Length == 25 && values.Distinct().Count() == 25, "Bulk ULIDs missing/duplicated");
        ui.Click("ClearCommand");
    }
    private void Hashes()
    {
        ui.Set("InputText", "abc"); ui.Click("ComputeCommand");
        ui.Expect("Sha256Result", x => x.Equals(Convert.ToHexString(SHA256.HashData("abc"u8)), StringComparison.OrdinalIgnoreCase), "SHA256 known vector");
        ui.Expect("Md5Result", x => x.Equals("900150983cd24fb0d6963f7d28e17f72", StringComparison.OrdinalIgnoreCase), "MD5 known vector");
        ui.Click("ClearCommand"); ui.Expect("Sha256Result", x => x == "", "hash clear");
    }
    private void Passwords()
    {
        var first = ui.Text("GeneratedPassword"); Assert(first.Length >= 8, "Password missing");
        ui.Check("UseUppercase", false); ui.Check("UseLowercase", false); ui.Check("UseSymbols", false); ui.Check("UseDigits", true); ui.Click("GenerateCommand");
        ui.Expect("GeneratedPassword", x => x.Length >= 8 && x.All(char.IsAsciiDigit), "digits-only password");
        ui.Check("UseUppercase", true); ui.Check("UseLowercase", true); ui.Check("UseSymbols", true); ui.Click("GenerateCommand");
        ui.Expect("GeneratedPassword", x => x.Any(char.IsUpper) && x.Any(char.IsLower) && x.Any(char.IsDigit) && x.Any(c => !char.IsLetterOrDigit(c)), "all enabled classes included");
    }
    private void Jwt()
    {
        static string B64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        ui.Set("TokenInput", B64("{\"alg\":\"none\",\"typ\":\"JWT\"}") + "." + B64("{\"sub\":\"native-fixture\",\"exp\":1}") + "."); ui.Click("InspectCommand");
        ui.Expect("PayloadJson", x => x.Contains("native-fixture"), "decoded JWT payload");
        Assert(ui.Text("Algorithm").Contains("none"), "Unsigned algorithm not displayed");
        ui.Set("TokenInput", "malformed.token"); ui.Click("InspectCommand");
        ui.Expect("StatusText", x => x.Contains("invalid", StringComparison.OrdinalIgnoreCase) || x.Contains("failed", StringComparison.OrdinalIgnoreCase) || x.Contains("three", StringComparison.OrdinalIgnoreCase), "malformed JWT feedback");
        ui.Click("ClearCommand");
    }
}
