using WindowsUtilityPack.Services.Downloader;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

public class DownloadCategoryServiceTests
{
    [Fact]
    public void ResolveCategory_Matches_ByExtension()
    {
        var service = new DownloadCategoryService();
        var settings = new DownloaderSettings();

        var category = service.ResolveCategory("https://cdn.example.com/movie.mp4", ".mp4", settings);

        Assert.Equal("Videos", category.Name);
    }

    [Fact]
    public void ResolveCategory_Matches_ByDomain_WhenExtensionMissing()
    {
        var service = new DownloadCategoryService();
        var settings = new DownloaderSettings();

        var category = service.ResolveCategory("https://youtube.com/watch?v=abc", string.Empty, settings);

        Assert.Equal("Videos", category.Name);
    }

    [Fact]
    public void ResolveCategory_FallsBack_ToMixed()
    {
        var service = new DownloadCategoryService();
        var settings = new DownloaderSettings();

        var category = service.ResolveCategory("https://example.com/path/unknown.custom", ".custom", settings);

        Assert.Equal("Mixed", category.Name);
    }
}
