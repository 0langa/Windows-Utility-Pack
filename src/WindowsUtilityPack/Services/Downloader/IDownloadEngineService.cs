using WindowsUtilityPack.Models;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>
/// Orchestrates downloads using yt-dlp, gallery-dl, or the built-in web scraper
/// depending on URL compatibility.
/// </summary>
public interface IDownloadEngineService
{
    /// <summary>
    /// Detects which download engine can handle the given item's URL.
    /// Sets <see cref="DownloadItem.Engine"/> and <see cref="DownloadItem.Title"/>.
    /// </summary>
    Task DetectEngineAsync(DownloadItem item, CancellationToken ct = default);

    /// <summary>
    /// Downloads the item using its detected engine. Returns a list of <see cref="ScrapedAsset"/>
    /// when the scraper engine is used (user must select assets), or <c>null</c> for yt-dlp/gallery-dl.
    /// </summary>
    Task<List<ScrapedAsset>?> DownloadAsync(DownloadItem item, CancellationToken ct = default);

    /// <summary>
    /// Downloads a set of scraped assets to the output directory.
    /// </summary>
    Task DownloadScrapedAssetsAsync(
        IEnumerable<ScrapedAsset> assets,
        string outputDir,
        bool crawlSubdirectories,
        int maxDepth,
        int maxPages,
        Action<double> onProgress,
        CancellationToken ct = default);

    /// <summary>Available video format presets for yt-dlp.</summary>
    IReadOnlyList<(string Label, string Format)> VideoFormats { get; }
}
