using System.IO;
using WindowsUtilityPack.Services.Downloader.Engines;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public sealed class GalleryDownloadEngineTests
{
    [Fact]
    public void OutputEvent_ReportsExistingNestedUnicodeFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), "wup-gallery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "photo ü (1).jpg");
            File.WriteAllText(path, "fixture");
            Assert.True(GalleryDownloadEngine.TryGetOutputPath("WUP_OUTPUT:" + path, out var actual));
            Assert.Equal(path, actual);
            Assert.False(GalleryDownloadEngine.TryGetOutputPath("# " + path, out _));
            Assert.False(GalleryDownloadEngine.TryGetOutputPath(path, out _));
            File.Delete(path);
            Assert.False(GalleryDownloadEngine.TryGetOutputPath("WUP_OUTPUT:" + path, out _));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Theory]
    [InlineData("WUP_OUTPUT:relative.jpg")]
    [InlineData("[error] download failed")]
    [InlineData("WUP_OUTPUT:")]
    public void NonOutputOrInvalidPath_DoesNotBecomeHistory(string line)
    {
        Assert.False(GalleryDownloadEngine.TryGetOutputPath(line, out var path));
        Assert.Empty(path);
    }
}
