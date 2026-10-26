using WindowsUtilityPack.Services.Downloader;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class DependencyManagerServiceTests
{
    [Theory]
    [InlineData("SHA2-256SUMS")]
    [InlineData("SHA256SUMS")]
    [InlineData("checksums.sha256")]
    public void ReleaseWithDetachedSignature_SelectsChecksumManifestInsteadOfSignature(string name)
    {
        var assets = Newtonsoft.Json.Linq.JArray.Parse($$"""
            [{"name":"{{name}}","browser_download_url":"https://example.test/checksums"},
             {"name":"{{name}}.sig","browser_download_url":"https://example.test/signature"}]
            """);
        Assert.Equal("https://example.test/checksums", DependencyManagerService.SelectChecksumManifestUrl(assets, name));
    }

    [Fact]
    public void ReleaseWithOnlyDetachedSignature_DoesNotTreatItAsChecksums()
    {
        var assets = Newtonsoft.Json.Linq.JArray.Parse("""
            [{"name":"SHA2-256SUMS.sig","browser_download_url":"https://example.test/SHA2-256SUMS.sig"}]
            """);
        Assert.Null(DependencyManagerService.SelectChecksumManifestUrl(assets, "SHA2-256SUMS"));
    }
    [Fact]
    public void ExtractFfmpeg_MissingBinaryIsAnErrorAndKeepsInstalledFile()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wup-tools-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        try
        {
            var zip = System.IO.Path.Combine(folder, "fixture.zip");
            var destination = System.IO.Path.Combine(folder, "ffmpeg.exe");
            System.IO.File.WriteAllText(destination, "existing fixture");
            using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
                archive.CreateEntry("bin/unrelated.exe");
            Assert.Throws<System.IO.InvalidDataException>(() => DependencyManagerService.ExtractFfmpeg(zip, destination, default));
            Assert.Equal("existing fixture", System.IO.File.ReadAllText(destination));
        }
        finally { System.IO.Directory.Delete(folder, true); }
    }

    [Fact]
    public void ExtractFfmpeg_CancellationKeepsInstalledFile()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wup-tools-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        try
        {
            var zip = System.IO.Path.Combine(folder, "fixture.zip");
            var destination = System.IO.Path.Combine(folder, "ffmpeg.exe");
            System.IO.File.WriteAllText(destination, "existing fixture");
            using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
            using (var writer = new System.IO.StreamWriter(archive.CreateEntry("ffmpeg/bin/ffmpeg.exe").Open())) writer.Write("new fixture");
            Assert.ThrowsAny<OperationCanceledException>(() => DependencyManagerService.ExtractFfmpeg(zip, destination, new CancellationToken(true)));
            Assert.Equal("existing fixture", System.IO.File.ReadAllText(destination));
        }
        finally { System.IO.Directory.Delete(folder, true); }
    }

    [Fact]
    public void ExtractSha256FromManifest_ParsesUnixStyleChecksums()
    {
        const string manifest = """
            ffaabbccddeeff00112233445566778899aabbccddeeff001122334455667788  yt-dlp.exe
            aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  other.exe
            """;

        var hash = DependencyManagerService.ExtractSha256FromManifest(manifest, "yt-dlp.exe");

        Assert.Equal("ffaabbccddeeff00112233445566778899aabbccddeeff001122334455667788", hash);
    }

    [Fact]
    public void ExtractSha256FromManifest_ParsesOpenSslStyleChecksums()
    {
        const string manifest = """
            SHA256 (ffmpeg-master-latest-win64-gpl.zip) = 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
            """;

        var hash = DependencyManagerService.ExtractSha256FromManifest(manifest, "ffmpeg-master-latest-win64-gpl.zip");

        Assert.Equal("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", hash);
    }

    [Fact]
    public void ExtractSha256FromManifest_ReturnsNull_WhenAssetMissing()
    {
        const string manifest = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff  something-else.exe";

        var hash = DependencyManagerService.ExtractSha256FromManifest(manifest, "gallery-dl.exe");

        Assert.Null(hash);
    }
}
