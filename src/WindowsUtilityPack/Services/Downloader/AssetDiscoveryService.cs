using System.Net.Http;
using System.IO;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>Performs page scan/crawl discovery and maps scraper output to queue-ready asset candidates.</summary>
public sealed class AssetDiscoveryService : IAssetDiscoveryService
{
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
    });

    private readonly IWebScraperService _webScraperService;

    public AssetDiscoveryService(IWebScraperService webScraperService)
    {
        _webScraperService = webScraperService;
    }

    public async Task<IReadOnlyList<DownloadAssetCandidate>> DiscoverAsync(
        string url,
        bool deepCrawl,
        DownloaderSettings settings,
        IProgress<(int pages, int assets)>? progress,
        CancellationToken cancellationToken)
    {
        var maxDepth = Math.Clamp(settings.Scan.MaxDepth, 1, 10);
        var maxPages = Math.Clamp(settings.Scan.MaxPages, 1, 2000);

        var scraped = await _webScraperService.ScrapeAsync(
            pageUrl: url,
            crawlSubdirectories: deepCrawl,
            maxDepth: maxDepth,
            maxPages: maxPages,
            onProgress: value => progress?.Report(value),
            ct: cancellationToken);

        var candidates = new List<DownloadAssetCandidate>(scraped.Count);
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in scraped)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (settings.Scan.UniqueAssetsOnly && !unique.Add(asset.Url))
            {
                continue;
            }

            var candidate = new DownloadAssetCandidate
            {
                Name = string.IsNullOrWhiteSpace(asset.FileName)
                    ? InferName(asset.Url)
                    : asset.FileName,
                Url = asset.Url,
                Extension = asset.FileExtension,
                SourcePage = asset.SourcePageUrl,
                Host = ExtractHost(asset.Url),
                PackageTitle = BuildPackageTitle(asset.SourcePageUrl),
                TypeLabel = asset.TypeLabel,
                SizeBytes = asset.FileSizeBytes > 0 ? asset.FileSizeBytes : null,
                IsReachable = true,
            };

            candidates.Add(candidate);
        }

        if (settings.Scan.ProbeContentType)
        {
            await ProbeAssetMetadataAsync(candidates, cancellationToken);
        }

        return candidates;
    }

    private static async Task ProbeAssetMetadataAsync(IReadOnlyList<DownloadAssetCandidate> assets, CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(
            assets,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 8,
            },
            async (asset, ct) =>
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head, asset.Url);
                    using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                    asset.IsReachable = response.IsSuccessStatusCode;
                    if (!asset.SizeBytes.HasValue && response.Content.Headers.ContentLength is > 0)
                    {
                        asset.SizeBytes = response.Content.Headers.ContentLength;
                    }

                    if (!asset.IsReachable)
                    {
                        asset.Warning = $"HTTP {(int)response.StatusCode}";
                    }
                }
                catch
                {
                    asset.IsReachable = false;
                    asset.Warning = "Unavailable";
                }
            });
    }

    private static string InferName(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var name = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return uri.Host;
        }

        return "asset";
    }

    private static string BuildPackageTitle(string sourcePage)
    {
        if (Uri.TryCreate(sourcePage, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return "Asset Package";
    }

    private static string ExtractHost(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.Host
            : string.Empty;
    }
}
