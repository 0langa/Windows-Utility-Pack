using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace WindowsUtilityPack.NativeTests;

internal sealed partial class Cases
{
    private void Dns()
    {
        SandboxOnly(); using var server = new DnsFixture();
        var backup = Path.Combine(work, "dns-before.xml");
        PowerShell($"$adapters=@(Get-NetAdapter | Where-Object Status -eq 'Up'); $state=@($adapters | ForEach-Object {{ Get-DnsClientServerAddress -InterfaceIndex $_.ifIndex }}); $state | Export-Clixml -LiteralPath '{backup}'; $adapters | ForEach-Object {{ Set-DnsClientServerAddress -InterfaceIndex $_.ifIndex -ServerAddresses '127.0.0.1' }}");
        try
        {
            var probe = PowerShell("Resolve-DnsName native.wup.test -Type A -Server 127.0.0.1 -DnsOnly | ConvertTo-Json -Compress");
            Assert(probe.Contains("127.0.0.42"), "Independent Windows DNS query rejected the fixture response");
            ui.Navigate("System Info Dashboard"); ui.Navigate("DNS Lookup");
            ui.Set("Host", "native.wup.test"); ui.Click("LookupCommand"); ui.Expect("StatusMessage", x => x.StartsWith("Found 5 record"), "controlled A/AAAA/CNAME/MX/TXT lookup", 25);
            ui.Click("CopyResultsCommand"); var copy = ClipboardText(); Assert(copy.Contains("127.0.0.42") && copy.Contains("alias.wup.test") && copy.Contains("mail.wup.test") && copy.Contains("native-dns-fixture!"), "DNS records lost during copy");
            ui.Set("Host", "missing.wup.test"); ui.Click("LookupCommand"); ui.Expect("StatusMessage", x => x.Contains("failed") || x.StartsWith("No records"), "controlled NXDOMAIN", 25);
            ui.Click("ClearCommand");
        }
        finally
        {
            File.WriteAllLines(Path.Combine(NativeApp.EvidenceDirectory!, "dns-fixture-queries.txt"), server.Queries);
            PowerShell($"Import-Clixml -LiteralPath '{backup}' | Group-Object InterfaceIndex | ForEach-Object {{ $addresses=@($_.Group | ForEach-Object ServerAddresses); if ($addresses.Count) {{ Set-DnsClientServerAddress -InterfaceIndex ([int]$_.Name) -ServerAddresses $addresses }} else {{ Set-DnsClientServerAddress -InterfaceIndex ([int]$_.Name) -ResetServerAddresses }} }}");
        }
    }
    private void HttpTester()
    {
        using var server = new HttpFixture();
        ui.Set("Url", server.Url + "/echo"); ui.Select("Method", "POST");
        ui.Set("RequestHeaders", "X-Test: native-header"); ui.Set("RequestBody", "Grüße 🧪"); ui.Click("SendCommand");
        ui.Expect("ResponseBody", x => x.Contains("native-header"), "HTTP response from owned listener");
        using (var json = JsonDocument.Parse(ui.Text("ResponseBody"))) Assert(json.RootElement.GetProperty("Body").GetString() == "Grüße 🧪", "HTTP request body changed");
        ui.Set("Url", server.Url + "/delay"); ui.Click("SendCommand"); ui.Click("CancelCommand");
        ui.Expect("ResponseStatus", x => x.Contains("cancel", StringComparison.OrdinalIgnoreCase), "HTTP cancellation");
        ui.Set("Url", server.Url + "/echo"); ui.Click("SendCommand");
        ui.Expect("ResponseBody", x => x.Contains("native-header"), "HTTP restart");
    }
    private void PortScan()
    {
        using var server = new HttpFixture();
        ui.Set("Host", "127.0.0.1"); ui.Set("PortRangeText", $"{server.Port},1"); ui.Click("ScanCommand");
        ui.Expect("StatusMessage", x => x.Contains("1 open port(s) found of 2 scanned"), "one open and one closed local port");
        ui.Click("CopyOpenPortsCommand"); Assert(ClipboardText().Contains(server.Port.ToString()), "Open-port copy missing owned listener");
        ui.Set("PortRangeText", "not-a-port"); ui.Click("ScanCommand");
        ui.Expect("StatusMessage", x => x.Contains("No valid ports"), "invalid port input");
        ui.Set("PortRangeText", "1-65535"); ui.Set("Concurrency", "1"); ui.Click("ScanCommand"); ui.Click("StopCommand");
        ui.Expect("StatusMessage", x => x.StartsWith("Scan stopped"), "scan cancellation");
    }
    private void Ping()
    {
        ui.Set("Host", "127.0.0.1"); ui.Set("PingCountBox", "3"); ui.Click("PingCommand");
        ui.Expect("Summary", x => x.Contains("3") && x.Contains("0%"), "three loopback ICMP replies", 20);
        ui.Set("PingCountBox", "100"); ui.Click("PingCommand"); ui.Click("StopCommand");
        NativeApp.Wait(() => ui.Get("PingCommand").Current.IsEnabled, "ping cancellation");
        ui.Set("PingCountBox", "1"); ui.Click("PingCommand");
        ui.Expect("Summary", x => x.Contains("1") && x.Contains("0%"), "ping restart", 20);
    }
    private void Speed()
    {
        using var server = new HttpFixture();
        ui.Set("ServerUrl", server.Url + "/bytes"); ui.Set("UploadUrl", server.Url + "/upload"); ui.Click("RunTestCommand");
        ui.Expect("StatusMessage", x => x.Contains("Test complete"), "local download/upload speed workflow", 30);
        Assert(server.Requests.Contains("GET /bytes") && server.Requests.Contains("POST /upload"), "Both speed phases must use the owned listener");
        Assert(ui.Text("DownloadSpeed").Contains("Mbps") && ui.Text("UploadSpeed").Contains("Mbps"), "Throughput missing");
        ui.Set("ServerUrl", server.Url + "/delay"); ui.Click("RunTestCommand");
        NativeApp.Wait(() => ui.Get("StopCommand").Current.IsEnabled, "speed test active"); ui.Click("StopCommand");
        ui.Expect("StatusMessage", x => x.Contains("cancel", StringComparison.OrdinalIgnoreCase) || x.Contains("stopped", StringComparison.OrdinalIgnoreCase), "speed cancellation");
    }
    private void RequestBuilder()
    {
        using var server = new HttpFixture();
        ui.Set("Url", server.Url + "/echo"); ui.Select("SelectedMethod", "POST");
        ui.Set("RequestBody", "{\"native\":42}"); ui.Click("SendCommand");
        ui.Expect("ResponseBody", x => x.Contains("native"), "request builder echo");
        ui.Click("CopyResponseCommand"); Assert(ClipboardText().Contains("native"), "Response copy missing body");
        ui.Set("Url", server.Url + "/delay"); ui.Click("SendCommand"); ui.Click("CancelCommand");
        ui.Expect("StatusText", x => x.Contains("cancel", StringComparison.OrdinalIgnoreCase), "request builder cancellation");
        ui.Set("Url", server.Url + "/echo"); ui.Click("SendCommand");
        ui.Expect("ResponseBody", x => x.Contains("native"), "request builder restart");
    }
    private void HeaderAnalyzer()
    {
        ui.Click("ImportLatestHeadersCommand");
        ui.Expect("RawHeaders", x => x.Contains("X-Native-Fixture", StringComparison.OrdinalIgnoreCase), "shared response header import");
        ui.Set("RawHeaders", "HTTP/1.1 200 OK\r\nX-Test: one\r\nX-Test: two\r\nSet-Cookie: a=1; HttpOnly\r\nSet-Cookie: b=2; Secure; SameSite=None\r\nMalformed header\r\n"); ui.Click("AnalyzeCommand");
        ui.Expect("StatusText", x => x.Contains("header", StringComparison.OrdinalIgnoreCase) && !x.Contains("Paste"), "duplicate/malformed header analysis");
        var headers = ui.GridRows("ParsedHeaders");
        Assert(headers.Count(row => row[0] == "X-Test") == 2 &&
            headers.Any(row => row[0] == "X-Test" && row[1] == "one") &&
            headers.Any(row => row[0] == "X-Test" && row[1] == "two"), "Parsed output lost duplicate header values");
        var cookies = ui.GridRows("Cookies");
        Assert(cookies.Length == 2 &&
            cookies.Any(row => row[0] == "a" && row[1] == "False" && row[2] == "True") &&
            cookies.Any(row => row[0] == "b" && row[1] == "True" && row[2] == "False" && row[3] == "None"),
            "Parsed cookies lost names or Secure/HttpOnly/SameSite attributes");
        ui.Click("ClearCommand");
    }
    private void ResponseDiff()
    {
        ui.Click("LoadLatestToLeftCommand"); ui.Expect("LeftRaw", x => x.Contains("native"), "shared response handoff");
        ui.Set("LeftRaw", "HTTP/1.1 200 OK\r\nX-Test: a\r\n\r\nfirst"); ui.Set("RightRaw", "HTTP/1.1 404 Missing\r\nX-Test: b\r\n\r\nsecond"); ui.Click("CompareCommand");
        ui.Expect("StatusText", x => x.Contains("differences detected") && !x.StartsWith("No"), "status/header/body differences");
        ui.Set("RightRaw", ui.Text("LeftRaw")); ui.Click("CompareCommand"); ui.Expect("StatusText", x => x == "No differences detected.", "identical responses");
        ui.Click("ClearCommand");
    }
    private void CertificateInspect()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=WUP Native Fixture", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
        var path = Path.Combine(work, "native.cer"); File.WriteAllBytes(path, cert.Export(X509ContentType.Cert));
        ui.Select("InputMode", "File"); ui.Click("BrowseFileCommand"); ui.FileDialog(path); ui.Click("InspectCommand");
        ui.Expect("Certificate.Subject", x => x.Contains("WUP Native Fixture"), "certificate file inspection");
        ui.Expect("Certificate.Thumbprint", x => x.Replace(" ", "").Equals(cert.Thumbprint, StringComparison.OrdinalIgnoreCase), "certificate thumbprint");
        ui.Select("InputMode", "PEM"); ui.Set("PemText", "invalid PEM"); ui.Click("InspectCommand");
        ui.Expect("StatusMessage", x => x.StartsWith("Error"), "malformed PEM error");
        ui.Set("PemText", cert.ExportCertificatePem()); ui.Click("InspectCommand");
        ui.Expect("Certificate.Subject", x => x.Contains("WUP Native Fixture"), "PEM recovery");
    }
}
