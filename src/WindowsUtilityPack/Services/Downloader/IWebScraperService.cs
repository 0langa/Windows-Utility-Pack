using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>
/// Scrapes web pages for downloadable assets and handles downloading them to disk.
/// </summary>
public interface IWebScraperService
{
    /// <summary>
    /// Scrapes a page (and optionally sub-pages) for downloadable assets.
    /// </summary>
    /// <param name="pageUrl">Starting page URL.</param>
    /// <param name="crawlSubdirectories">Whether to follow same-host links under the starting path.</param>
    /// <param name="maxDepth">Maximum link depth when crawling.</param>
    /// <param name="maxPages">Maximum total pages to visit.</param>
    /// <param name="onProgress">Optional callback reporting (pagesScraped, assetsFound).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<List<ScrapedAsset>> ScrapeAsync(
        string pageUrl,
        bool crawlSubdirectories,
        int maxDepth,
        int maxPages,
        Action<(int pagesScraped, int assetsFound)>? onProgress,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads a single scraped asset to the specified output directory.
    /// </summary>
    /// <param name="asset">The asset to download.</param>
    /// <param name="outputDir">Target directory.</param>
    /// <param name="progress">Optional progress reporter (0–100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Absolute path of the saved file.</returns>
    Task<string> DownloadAssetAsync(
        ScrapedAsset asset,
        string outputDir,
        IProgress<double>? progress,
        CancellationToken ct = default);
}
