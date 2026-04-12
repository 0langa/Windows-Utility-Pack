using WindowsUtilityPack.Services.Downloader;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class DependencyManagerServiceTests
{
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
