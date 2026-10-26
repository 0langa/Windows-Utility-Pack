using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsUtilityPack.NativeTests;

internal sealed partial class Cases
{
    private string ImageFixture(string name = "fixture.png")
    {
        var bytes = new byte[400 * 240 * 4];
        for (var y = 0; y < 240; y++) for (var x = 0; x < 400; x++)
        { var i = (y * 400 + x) * 4; bytes[i] = x >= 200 ? (byte)255 : (byte)0; bytes[i + 1] = y >= 120 ? (byte)255 : (byte)0; bytes[i + 2] = x < 200 ? (byte)255 : (byte)0; bytes[i + 3] = 255; }
        var bitmap = BitmapSource.Create(400, 240, 300, 300, PixelFormats.Bgra32, null, bytes, 1600);
        var metadata = new BitmapMetadata("png"); metadata.SetQuery("/tEXt/{str=Comment}", "WUP private fixture metadata");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap, null, metadata, null));
        var path = Path.Combine(work, name); using var file = File.Create(path); encoder.Save(file); return path;
    }
    private static BitmapFrame DecodeImage(string path)
    {
        using var stream = File.OpenRead(path);
        return BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
    }
    private void FileHash()
    {
        var path = Fixture("hash-abc.txt", "abc"); ui.Click("BrowseCommand"); ui.FileDialog(path);
        var expected = Convert.ToHexString(SHA256.HashData("abc"u8));
        ui.Expect("Sha256Hash", x => x.Equals(expected, StringComparison.OrdinalIgnoreCase), "file SHA256");
        ui.Set("VerifyHash", expected); ui.Click("VerifyCommand"); ui.Expect("VerifyResult", x => x.Contains("match", StringComparison.OrdinalIgnoreCase) && !x.Contains("not", StringComparison.OrdinalIgnoreCase), "hash match");
        ui.Set("VerifyHash", new string('0', 64)); ui.Click("VerifyCommand"); ui.Expect("VerifyResult", x => x.Contains("not", StringComparison.OrdinalIgnoreCase) || x.Contains("mismatch", StringComparison.OrdinalIgnoreCase), "hash mismatch");
    }
    private void Rename()
    {
        var folder = Path.Combine(work, "rename"); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "original.txt"), "native contents");
        ui.Click("BrowseFolderCommand"); ui.FileDialog(folder, folder: true);
        ui.Expect("SelectedFolder", x => x == folder, "chosen rename fixture directory");
        ui.Set("Prefix", "new-"); ui.Click("ApplyRenameCommand"); ui.Confirm("No"); Assert(File.Exists(Path.Combine(folder, "original.txt")), "Cancelled rename changed input");
        ui.Click("ApplyRenameCommand"); ui.Confirm();
        NativeApp.Wait(() => File.Exists(Path.Combine(folder, "new-original.txt")), "renamed file");
        Assert(File.ReadAllText(Path.Combine(folder, "new-original.txt")) == "native contents", "Rename changed contents");
        ui.Set("Prefix", "../");
        ui.Expect("NewName", x => x == "../new-original.txt", "invalid rename preview");
        ui.Click("ApplyRenameCommand");
        Assert(ui.Dump(ui.Dialog()).Contains("1 conflicts will be skipped"), "Invalid prefix was not marked as a conflict");
        ui.Confirm();
        ui.Expect("OriginalName", x => x == "new-original.txt", "invalid rename retained its original path");
        Assert(File.ReadAllText(Path.Combine(folder, "new-original.txt")) == "native contents", "Invalid rename changed or removed input");
        Assert(!File.Exists(Path.Combine(work, "new-original.txt")), "Invalid prefix escaped folder");
    }
    private void SplitJoin()
    {
        var original = RandomNumberGenerator.GetBytes(2_500_000); var path = Path.Combine(work, "split.bin"); File.WriteAllBytes(path, original);
        var parts = Path.Combine(work, "parts"); Directory.CreateDirectory(parts);
        ui.Click("BrowseSplitInputCommand"); ui.FileDialog(path); ui.Click("BrowseSplitOutputCommand"); ui.FileDialog(parts, true); ui.Set("ChunkSizeMb", "1"); ui.Click("SplitCommand");
        ui.Expect("StatusMessage", x => x.StartsWith("Split complete. 3 parts"), "three split parts");
        ui.Click("BrowseJoinPartCommand"); ui.FileDialog(Directory.GetFiles(parts, "*.part0001").Single()); var joined = Path.Combine(work, "joined.bin"); ui.Click("BrowseJoinOutputCommand"); ui.FileDialog(joined); ui.Click("JoinCommand");
        ui.Expect("StatusMessage", x => x.StartsWith("Join complete. Checksum verified"), "joined checksum");
        Assert(File.ReadAllBytes(joined).SequenceEqual(original), "Split/join changed bytes");
        ui.Click("SplitCommand"); ui.Expect("StatusMessage", x => x.StartsWith("Split failed") && x.Contains("already exists"), "part collision preserved");
        Assert(File.ReadAllBytes(joined).SequenceEqual(original), "Collision changed existing output");
    }
    private void Shred()
    {
        SandboxOnly(); var path = Fixture("discard-only.txt", "disposable native shredding fixture");
        ui.Click("AddFilesCommand"); ui.FileDialog(path); ui.Expect("StatusMessage", x => x.Contains("1 file(s) queued"), "shred queue");
        ui.Click("ShredCommand"); ui.Confirm("No"); Assert(File.Exists(path), "Cancelled shred removed file");
        ui.Click("ShredCommand"); ui.Confirm(); ui.Expect("StatusMessage", x => x.StartsWith("Deleted 1/1") && x.Contains("0 failed"), "shred fixture deletion");
        Assert(!File.Exists(path), "Shredded file remains");
    }
    private void Metadata()
    {
        var source = ImageFixture("metadata.png"); var before = SHA256.HashData(File.ReadAllBytes(source));
        ui.Click("BrowseCommand"); ui.FileDialog(source); ui.Expect("StatusMessage", x => x.StartsWith("Loaded"), "metadata read");
        ui.Click("StripMetadataCommand"); var output = Path.Combine(work, "stripped.png"); ui.FileDialog(output);
        NativeApp.Wait(() => File.Exists(output), "stripped image output"); var frame = DecodeImage(output);
        Assert(frame.PixelWidth == 400 && frame.PixelHeight == 240, "Metadata stripping changed image dimensions");
        Assert(SHA256.HashData(File.ReadAllBytes(source)).SequenceEqual(before), "Metadata stripping changed source");
        Assert(!(frame.Metadata as BitmapMetadata)?.ContainsQuery("/tEXt/{str=Comment}") ?? true, "Private PNG comment remains");
    }
    private void LogAnalyzer()
    {
        var path = Fixture("native.log", "2026-10-26 INFO hello\n2026-10-26 WARN warning\n2026-10-26 ERROR native-failure\n");
        ui.Click("BrowseCommand"); ui.FileDialog(path); ui.Click("AnalyzeCommand");
        ui.Expect("StatusMessage", x => x.StartsWith("Analyzed 3 lines"), "log line count");
        ui.Set("SearchText", "native-failure"); ui.Click("AnalyzeCommand"); ui.Expect("MatchedLines", x => x == "Matched: 1", "log filtering");
        ui.Click("CopySummaryCommand"); Assert(ClipboardText().Contains("1"), "Log summary empty");
    }
    private void Markdown()
    {
        const string input = "# Native title\n\nGrüße **bold** and [link](https://example.com).";
        ui.Set("MarkdownText", input); ui.Click("RenderCommand"); ui.Expect("RenderedHtml", x => x.Contains("<h1") && x.Contains("<strong>bold</strong>"), "Markdown HTML");
        ui.Click("SaveAsCommand"); var path = Path.Combine(work, "native.md"); ui.FileDialog(path);
        NativeApp.Wait(() => File.Exists(path), "Markdown save"); Assert(File.ReadAllText(path) == input, "Markdown save changed contents");
        ui.Set("MarkdownText", "unsaved edits"); ui.Click("NewDocumentCommand"); ui.Confirm("No"); ui.Expect("MarkdownText", x => x == "unsaved edits", "dirty new cancelled");
        ui.Click("NewDocumentCommand"); ui.Confirm(); ui.Click("OpenCommand"); ui.FileDialog(path); ui.Expect("MarkdownText", x => x == input, "Markdown reopen");
    }
    private void TextConvert()
    {
        ui.Set("DirectInputText", "{\"native\":42,\"text\":\"Grüße\"}"); ui.Select("SelectedTargetFormat", "XML"); ui.Click("ConvertCommand");
        ui.Expect("ResultStatusMessage", x => !x.Contains("Load a file") && !x.Contains("begin") && !x.Contains("failed", StringComparison.OrdinalIgnoreCase), "JSON to XML conversion");
        NativeApp.Wait(() => ui.Get("CopyResultCommand").Current.IsEnabled, "converted result available");
        ui.Click("CopyResultCommand"); var xml = System.Xml.Linq.XDocument.Parse(ClipboardText()); Assert(xml.Descendants("native").Single().Value == "42" && xml.Descendants("text").Single().Value == "Grüße", "Converted XML missing content");
        ui.Click("SaveResultCommand"); var output = Path.Combine(work, "converted.xml"); ui.FileDialog(output);
        NativeApp.Wait(() => File.Exists(output), "converted document saved"); Assert(File.ReadAllText(output).Contains("native"), "Saved converted file empty");
        ui.Click("ClearAllCommand");
    }
}
