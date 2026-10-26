using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsUtilityPack.Services.FileTools;
using WindowsUtilityPack.Services.QrCode;
using Xunit;
using ZXing;

namespace WindowsUtilityPack.Tests.Services;

public sealed class ExportWorkflowTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "wup-export-" + Guid.NewGuid().ToString("N"));
    public ExportWorkflowTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    [Theory]
    [InlineData(QrCodeExportFormat.Png, QrCodeErrorCorrectionLevel.Low, false)]
    [InlineData(QrCodeExportFormat.Png, QrCodeErrorCorrectionLevel.High, true)]
    [InlineData(QrCodeExportFormat.Jpeg, QrCodeErrorCorrectionLevel.Quartile, false)]
    [InlineData(QrCodeExportFormat.Bmp, QrCodeErrorCorrectionLevel.Medium, false)]
    public async Task QrExport_IndependentReaderRecoversExactPayload(QrCodeExportFormat format, QrCodeErrorCorrectionLevel ecc, bool transparent)
    {
        const string payload = "https://example.com/fixture?text=Gr%C3%BC%C3%9Fe&value=42";
        var path = Path.Combine(_directory, "qr." + format.ToString().ToLowerInvariant());
        await new QrCodeService().ExportAsync(new()
        {
            NormalizedUrl = payload, FilePath = path, Format = format, ExportSizePixels = 720,
            Style = new() { ErrorCorrectionLevel = ecc, TransparentBackground = transparent }
        }, default);
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var pixels = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        var bgra = new byte[pixels.PixelWidth * pixels.PixelHeight * 4];
        pixels.CopyPixels(bgra, pixels.PixelWidth * 4, 0);
        // Composite transparent modules onto a white background before independent decoding.
        for (var i = 0; i < bgra.Length; i += 4)
        {
            for (var c = 0; c < 3; c++) bgra[i + c] = (byte)((bgra[i + c] * bgra[i + 3] + 255 * (255 - bgra[i + 3])) / 255);
            bgra[i + 3] = 255;
        }
        var result = new BarcodeReaderGeneric().Decode(bgra, pixels.PixelWidth, pixels.PixelHeight, RGBLuminanceSource.BitmapFormat.BGRA32);
        Assert.NotNull(result);
        Assert.Equal(payload, result.Text);
    }

    [Fact]
    public async Task QrExport_InvalidFormatPreservesExistingDestination()
    {
        var path = Path.Combine(_directory, "existing.bin");
        File.WriteAllText(path, "keep existing content");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new QrCodeService().ExportAsync(new()
        {
            NormalizedUrl = "https://example.com/", FilePath = path,
            Format = (QrCodeExportFormat)999, Style = new()
        }, default));
        Assert.Equal("keep existing content", File.ReadAllText(path));
    }

    [Fact]
    public async Task AudioMetadataStrip_PreservesPcmSamplesAndRemovesTitle()
    {
        var path = Path.Combine(_directory, "sample.wav");
        var samples = Enumerable.Range(0, 16000).Select(i => (byte)(i * 17)).ToArray();
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length); writer.Write(samples);
        }
        using (var tagged = TagLib.File.Create(path)) { tagged.Tag.Title = "Synthetic fixture title"; tagged.Save(); }
        using (var tagged = TagLib.File.Create(path)) Assert.Equal("Synthetic fixture title", tagged.Tag.Title);
        await AudioMetadataService.StripAsync(path, path);
        using (var stripped = TagLib.File.Create(path)) Assert.True(string.IsNullOrEmpty(stripped.Tag.Title));
        using var reader = new BinaryReader(File.OpenRead(path));
        reader.BaseStream.Position = 12;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            var id = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var length = reader.ReadInt32();
            if (id == "data") { Assert.Equal(samples, reader.ReadBytes(length)); return; }
            reader.BaseStream.Position += length + (length & 1);
        }
        Assert.Fail("The audio data chunk was lost.");
    }

    [Fact]
    public void Shredder_RejectsHardLinksWithoutChangingEitherPath()
    {
        var original = Path.Combine(_directory, "original.txt");
        var link = Path.Combine(_directory, "linked.txt");
        File.WriteAllText(original, "preserve this shared content");
        Assert.True(CreateHardLinkW(link, original, 0), $"Could not create fixture hard link: {Marshal.GetLastWin32Error()}");
        Assert.Throws<IOException>(() => FileOverwriteService.OverwriteAndDelete(link, 1));
        Assert.Equal("preserve this shared content", File.ReadAllText(original));
        Assert.Equal(File.ReadAllBytes(original), File.ReadAllBytes(link));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string link, string target, nint security);
}
