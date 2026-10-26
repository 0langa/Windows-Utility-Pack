using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;

namespace WindowsUtilityPack.NativeTests;

internal sealed partial class Cases
{
    private void Vault()
    {
        ui.Set("MasterPasswordBox", "Native-only fixture password 42!"); ui.Click("UnlockCommand"); ui.Expect("StatusMessage", x => x.StartsWith("Vault unlocked"), "vault create");
        ui.Click("AddSecretCommand"); ui.Set("EditName", "Native secret"); ui.Set("EditCategory", "fixture"); ui.Set("EditValuePasswordBox", "private-Grüße-42"); ui.Set("EditNotes", "Fixture notes"); ui.Click("SaveSecretCommand"); ui.Expect("StatusMessage", x => x == "Secret saved.", "vault save");
        ui.Click("LockCommand"); ui.Set("MasterPasswordBox", "wrong password"); ui.Click("UnlockCommand"); ui.Expect("StatusMessage", x => x.StartsWith("Incorrect master password"), "vault wrong password");
        // The first failed attempt has a short backoff; wait on the actual unlock control/state.
        Thread.Sleep(1100); ui.Set("MasterPasswordBox", "Native-only fixture password 42!"); ui.Click("UnlockCommand"); ui.Expect("StatusMessage", x => x.StartsWith("Vault unlocked"), "vault unlock persisted entry");
        ui.Row("SelectedSecret", "Native secret"); ui.Click("CopyValueCommand"); Assert(ClipboardText() == "private-Grüße-42", "Vault round trip changed secret");
        var files = Directory.GetFiles(Path.Combine(work, "profile"), "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".log"));
        Assert(!files.Any(p => File.ReadAllBytes(p).AsSpan().IndexOf("private-Grüße-42"u8) >= 0), "Vault secret persisted in plaintext");
        ui.Click("DeleteSecretCommand"); ui.Confirm(); ui.Expect("StatusMessage", x => x == "Secret deleted.", "vault delete"); ui.Click("LockCommand");
    }
    private void Certificates()
    {
        SandboxOnly(); using var rsa = RSA.Create(2048); var request = new CertificateRequest("CN=WUP Native Store Fixture", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(3));
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser); store.Open(OpenFlags.ReadWrite); store.Add(cert);
        try
        {
            ui.Set("SearchQuery", "WUP Native Store Fixture"); ui.Click("RefreshCommand"); ui.Expect("StatusMessage", x => x == "Loaded 1 certificates.", "certificate store fixture");
            ui.Row("SelectedCertificate", cert.Subject); ui.Click("CopyPemCommand"); ui.Expect("StatusMessage", x => x == "Certificate PEM copied.", "certificate PEM copy");
            using var exported = X509Certificate2.CreateFromPem(ClipboardText()); Assert(exported.Thumbprint == cert.Thumbprint, "Certificate PEM changed identity");
            ui.Set("SearchQuery", "no-such-native-certificate"); ui.Click("RefreshCommand"); ui.Expect("StatusMessage", x => x == "Loaded 0 certificates.", "certificate empty filter");
        }
        finally { store.Remove(cert); }
    }
    private void Ssh()
    {
        using var server = new HttpFixture(); // This feature tests TCP reachability, not SSH authentication.
        ui.Click("NewProfileCommand"); ui.Set("Name", "Native SSH fixture"); ui.Set("Host", "127.0.0.1"); ui.Set("Port", server.Port.ToString()); ui.Set("Username", "nativeuser"); ui.Click("SaveProfileCommand"); ui.Expect("StatusMessage", x => x.StartsWith("Saved SSH profile"), "SSH profile save");
        ui.Click("TestConnectionCommand"); ui.Expect("StatusMessage", x => x.Contains("reachable", StringComparison.OrdinalIgnoreCase) || x.Contains("connected", StringComparison.OrdinalIgnoreCase), "owned TCP reachability");
        ui.Click("CopyCommandCommand"); var command = ClipboardText(); Assert(command.Contains(server.Port.ToString()) && command.Contains("nativeuser") && command.Contains("127.0.0.1"), "SSH command missing profile fields");
        ui.Navigate("System Info Dashboard"); ui.Navigate("SSH Remote Tool"); ui.Row("SelectedProfile", "Native SSH fixture"); ui.Expect("Port", x => x == server.Port.ToString(), "SSH profile reopen");
        ui.Click("DeleteProfileCommand"); ui.Confirm(); ui.Expect("StatusMessage", x => x.Contains("deleted"), "SSH profile delete");
    }
    private void Downloader()
    {
        using var server = new HttpFixture(); var output = Path.Combine(work, "downloads"); Directory.CreateDirectory(output);
        ui.Tab("Settings, Rules & Help"); ui.Set("Settings.General.DefaultDownloadFolder", output); ui.Check("Settings.General.AutoStartOnAdd", false); ui.Click("SaveSettingsCommand");
        ui.Tab("Quick Download"); ui.Set("QuickInput", server.Url + "/download.bin"); ui.Click("AnalyzeQuickInputCommand"); ui.Click("AddToQueueCommand");
        ui.Tab("Queue Manager"); ui.Row("QueueGrid", "download.bin"); ui.Click("StartSelectedCommand"); ui.Expect("CompletedCount", x => x == "Completed: 1", "staged direct download completion", 30);
        var files = Directory.GetFiles(output, "download*", SearchOption.AllDirectories); Assert(files.Length == 1 && File.ReadAllText(files[0]) == "WUP native download fixture\n", "Native downloader output missing/changed");
        ui.Tab("History & Diagnostics"); Assert(ui.Dump().Contains("download.bin"), "Native download history missing file");
        ui.Navigate("System Info Dashboard"); ui.Navigate("Downloader Studio"); ui.Tab("History & Diagnostics"); Assert(ui.Dump().Contains("download.bin"), "Download history lost on reopen");
    }
    private void SiteMap()
    {
        using var server = new HttpFixture(); ui.Set("TargetUrl", server.Url); ui.Check("EnableCrawl", true); ui.Set("MaxPages", "5"); ui.Set("MaxDepth", "2"); ui.Set("DelayMs", "0"); ui.Click("StartCommand");
        ui.Expect("StatusText", x => x.StartsWith("Discovered"), "site map crawl", 30); ui.Click("ExportUrlsCommand");
        var export = Path.Combine(work, "site-map.csv"); ui.FileDialog(export); NativeApp.Wait(() => File.Exists(export), "site map CSV export"); var urls = File.ReadAllText(export);
        Assert(urls.Contains(server.Url + "/child") && urls.Contains(server.Url + "/Case") && urls.Contains(server.Url + "/case"), "Site map lost case-sensitive child paths");
        ui.Set("TargetUrl", server.Url + "/delay"); ui.Click("StartCommand"); NativeApp.Wait(() => ui.Get("CancelCommand").Current.IsEnabled, "crawler active"); ui.Click("CancelCommand"); ui.Expect("StatusText", x => x.Contains("canceled"), "crawler cancel"); ui.Click("ClearCommand");
    }
    private void MockServer()
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        ui.Set("Port", port.ToString()); ui.Click("AddEndpointCommand"); ui.Click("SaveEndpointsCommand"); ui.Expect("StatusMessage", x => x.Contains("configuration saved"), "mock endpoint save"); ui.Click("StartCommand"); ui.Expect("StatusMessage", x => x.Contains("started at"), "mock server start");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) }; var baseUrl = ui.Text("BaseUrl").TrimEnd('/');
        Assert(client.GetStringAsync(baseUrl + "/new").Result.Contains("\"ok\":true"), "Mock response body missing"); Assert(client.GetAsync(baseUrl + "/missing").Result.StatusCode == HttpStatusCode.NotFound, "Mock missing route is not 404");
        ui.Click("RefreshLogCommand"); Assert(ui.Dump().Contains("/new") && ui.Dump().Contains("/missing"), "Mock request log missing requests"); ui.Click("StopCommand"); ui.Expect("StatusMessage", x => x.Contains("stopped"), "mock stop");
        ui.Click("StartCommand"); ui.Expect("StatusMessage", x => x.Contains("started at"), "mock restart"); Assert(client.GetStringAsync(baseUrl + "/new").Result.Contains("ok"), "Mock restart response missing"); ui.Click("StopCommand");
    }
    private void Qr()
    {
        const string url = "https://example.com/native?value=42"; ui.Set("UrlInput", url); ui.Click("GenerateCommand"); ui.Set("ExportSizePixels", "512"); ui.Click("SaveCommand"); var path = Path.Combine(work, "native-qr.png"); ui.FileDialog(path); NativeApp.Wait(() => File.Exists(path), "QR export");
        var pixels = Pixels(DecodeImage(path), out var width, out var height);
        var result = new BarcodeReaderGeneric().Decode(pixels, width, height, RGBLuminanceSource.BitmapFormat.BGRA32); Assert(result?.Text == url, "Independent QR decoding did not recover URL");
        ui.Click("ClearCommand"); ui.Set("UrlInput", "not a url !"); ui.Click("GenerateCommand"); ui.Expect("ValidationMessage", x => !string.IsNullOrWhiteSpace(x), "invalid QR URL feedback");
    }
    private void ClipboardManager()
    {
        SandboxOnly();
        // WPF's Toggle provider holds its automation call open during this modal
        // consent, preventing another UIA call from answering it. Use real input.
        var bounds = ui.Get("IsMonitoring").Current.BoundingRectangle;
        NativeApp.ClickAt((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
        ui.Confirm(); ui.Expect("StatusMessage", x => x.Contains("enabled"), "clipboard consent/monitoring");
        const string fixture = "WUP clipboard native fixture Grüße"; System.Windows.Forms.Clipboard.SetText(fixture); ui.Expect("StatusMessage", x => x.Contains("Captured"), "real clipboard capture");
        ui.Check("IsMonitoring", false); ui.Set("SearchText", "WUP clipboard native"); ui.Click("RefreshCommand"); ui.Row("SelectedEntry", fixture); ui.Click("CopySelectedCommand"); Assert(ClipboardText() == fixture, "Clipboard content round trip changed");
        ui.Click("DeleteSelectedCommand"); NativeApp.Wait(() => !ui.Dump().Contains(fixture), "clipboard entry delete");
    }
    private void Color()
    {
        ui.Set("HexColor", "#336699"); ui.Expect("R", x => x == "51", "HEX red component"); ui.Expect("G", x => x == "102", "HEX green component"); ui.Expect("B", x => x == "153", "HEX blue component");
        ui.Set("PaletteName", "Native swatch"); ui.Click("AddToPaletteCommand"); Assert(ui.Dump().Contains("Native swatch"), "Palette add missing");
        using var swatch = new System.Windows.Forms.Form { BackColor = System.Drawing.Color.FromArgb(17, 85, 153), FormBorderStyle = System.Windows.Forms.FormBorderStyle.None, StartPosition = System.Windows.Forms.FormStartPosition.Manual, Bounds = new System.Drawing.Rectangle(30, 30, 150, 150), TopMost = true };
        swatch.Show(); swatch.Refresh(); System.Windows.Forms.Application.DoEvents();
        ui.Click("PickFromScreenCommand"); ui.Dialog(); NativeApp.ClickAt(90, 90); ui.Expect("HexColor", x => x == "#115599", "actual screen pixel pick");
        ui.Click("PickFromScreenCommand"); var picker = ui.Dialog(); ui.Key("{ESC}", picker);
        NativeApp.Wait(() => { try { return picker.Current.IsOffscreen; } catch (System.Windows.Automation.ElementNotAvailableException) { return true; } }, "Escape closes the color picker");
        ui.Expect("HexColor", x => x == "#115599", "cancelled color pick preserves value");
        ui.Click("ClearPaletteCommand");
    }
    private void ImageConvert()
    {
        var source = ImageFixture(); var directory = Path.Combine(work, "image-convert"); Directory.CreateDirectory(directory);
        ui.Click("AddFilesCommand"); ui.FileDialog(source); ui.Select("TargetFormat", "JPEG"); ui.Click("BrowseOutputDirectoryCommand"); ui.FileDialog(directory, true); ui.Click("ConvertCommand");
        NativeApp.Wait(() => Directory.GetFiles(directory, "*.jpg").Length == 1, "JPEG conversion"); var first = Directory.GetFiles(directory, "*.jpg").Single(); var originalHash = SHA256.HashData(File.ReadAllBytes(first));
        var frame = DecodeImage(first); Assert(frame.PixelWidth == 400 && frame.PixelHeight == 240, "Converted dimensions changed");
        NativeApp.Wait(() => ui.Get("ConvertCommand").Current.IsEnabled, "converter idle"); ui.Click("ConvertCommand"); NativeApp.Wait(() => Directory.GetFiles(directory, "*.jpg").Length == 2, "conversion collision suffix"); Assert(SHA256.HashData(File.ReadAllBytes(first)).SequenceEqual(originalHash), "Repeat conversion replaced original");
    }
    private void ImageResize()
    {
        var source = ImageFixture("resize.png"); var directory = Path.Combine(work, "image-resize"); Directory.CreateDirectory(directory);
        ui.Click("AddFilesCommand"); ui.FileDialog(source); ui.Set("Width", "200"); ui.Set("Height", "120"); ui.Click("BrowseOutputDirectoryCommand"); ui.FileDialog(directory, true); ui.Click("ResizeCommand");
        NativeApp.Wait(() => Directory.GetFiles(directory).Any(), "resized image"); var frame = DecodeImage(Directory.GetFiles(directory).Single()); Assert(frame.PixelWidth == 200 && frame.PixelHeight == 120, "Native resize dimensions wrong");
        ui.Click("ClearFilesCommand"); ui.Click("ResizeCommand"); ui.Expect("StatusMessage", x => x.Contains("at least one image"), "empty image batch feedback");
        Assert(Directory.GetFiles(directory).Length == 1, "Empty resize batch produced additional output");
    }
    private static byte[] Pixels(BitmapSource source, out int width, out int height)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); width = converted.PixelWidth; height = converted.PixelHeight; var bytes = new byte[width * height * 4]; converted.CopyPixels(bytes, width * 4, 0); return bytes;
    }
    private void Annotate()
    {
        var source = ImageFixture("annotate.png"); ui.Click("LoadImageCommand"); ui.FileDialog(source); ui.Select("AnnotationType", "Rectangle"); ui.Set("X", "20"); ui.Set("Y", "20"); ui.Set("Width", "80"); ui.Set("Height", "60"); ui.Click("AddAnnotationCommand");
        var path = Path.Combine(work, "annotated.png"); ui.Click("BrowseOutputCommand"); ui.FileDialog(path); ui.Click("SaveAnnotatedImageCommand"); NativeApp.Wait(() => File.Exists(path), "annotation export");
        var before = Pixels(DecodeImage(source), out _, out _); var after = Pixels(DecodeImage(path), out var width, out var height); Assert(width == 400 && height == 240 && !after.SequenceEqual(before), "Annotation missing or image dimensions changed");
        ui.Click("UndoCommand"); var undone = Path.Combine(work, "annotation-undone.png"); ui.Click("BrowseOutputCommand"); ui.FileDialog(undone); ui.Click("SaveAnnotatedImageCommand"); NativeApp.Wait(() => File.Exists(undone), "undo export"); Assert(Pixels(DecodeImage(undone), out _, out _).SequenceEqual(before), "Undo after export did not restore original pixels");
    }
}
