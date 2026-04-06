using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>
/// Scrapes web pages for downloadable assets using HtmlAgilityPack and regex-based
/// extraction from inline scripts, styles, and embedded JSON data.
/// </summary>
public partial class WebScraperService : IWebScraperService
{
    /// <summary>Null-safe wrapper around <see cref="HtmlNode.SelectNodes"/>.</summary>
    private static IEnumerable<HtmlNode> SafeSelect(HtmlNode node, string xpath)
    {
        return node.SelectNodes(xpath) ?? Enumerable.Empty<HtmlNode>();
    }

    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
    })
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36" }
        }
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".avif", ".bmp",
        ".ico", ".tiff", ".tif", ".heic", ".heif", ".jxl", ".raw", ".cr2",
        ".nef", ".arw", ".dng", ".psd", ".ai", ".eps",
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".mov", ".avi", ".flv", ".m3u8", ".mpd",
        ".wmv", ".m4v", ".3gp", ".ts", ".vob", ".ogv", ".divx", ".asf",
        ".rm", ".rmvb", ".f4v", ".mts", ".m2ts",
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".ogg", ".wav", ".aac", ".m4a",
        ".wma", ".opus", ".alac", ".aiff", ".aif", ".mid", ".midi",
        ".ape", ".mka", ".ac3", ".dts", ".pcm",
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".rtf", ".odt", ".epub", ".mobi",
        ".djvu", ".xps", ".oxps", ".pages",
    };

    private static readonly HashSet<string> SpreadsheetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xls", ".ods", ".csv", ".tsv", ".numbers",
    };

    private static readonly HashSet<string> PresentationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pptx", ".ppt", ".odp", ".key",
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".zst",
        ".tar.gz", ".tgz", ".tar.bz2", ".tar.xz", ".cab", ".lz",
        ".lzma", ".lzo", ".z", ".arj", ".ace",
    };

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".dmg", ".pkg", ".deb", ".rpm", ".appimage",
        ".apk", ".ipa", ".snap", ".flatpak", ".bat", ".cmd", ".sh",
        ".ps1", ".jar", ".app", ".run", ".bin", ".com",
    };

    private static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf", ".otf", ".woff", ".woff2", ".eot", ".fon",
    };

    private static readonly HashSet<string> DatabaseExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sql", ".db", ".sqlite", ".sqlite3", ".mdb", ".accdb", ".bak",
    };

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf",
        ".cs", ".java", ".py", ".js", ".ts", ".cpp", ".c", ".h", ".hpp",
        ".rb", ".go", ".rs", ".swift", ".kt", ".lua", ".r", ".m", ".pl",
        ".php", ".html", ".htm", ".css", ".scss", ".less", ".sass",
        ".wasm", ".ipynb",
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".log", ".nfo", ".readme", ".changelog",
        ".license", ".srt", ".sub", ".ass", ".vtt", ".ssa",
    };

    private static readonly HashSet<string> DiskExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".iso", ".img", ".vhd", ".vhdx", ".vmdk", ".ova", ".qcow2",
        ".torrent", ".nrg", ".cue",
    };

    /// <summary>All recognised downloadable extensions (union of all category sets).</summary>
    private static readonly HashSet<string> AllDownloadableExtensions = BuildAllExtensions();

    private static HashSet<string> BuildAllExtensions()
    {
        var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        all.UnionWith(ImageExtensions);
        all.UnionWith(VideoExtensions);
        all.UnionWith(AudioExtensions);
        all.UnionWith(DocumentExtensions);
        all.UnionWith(SpreadsheetExtensions);
        all.UnionWith(PresentationExtensions);
        all.UnionWith(ArchiveExtensions);
        all.UnionWith(ExecutableExtensions);
        all.UnionWith(FontExtensions);
        all.UnionWith(DatabaseExtensions);
        all.UnionWith(CodeExtensions);
        all.UnionWith(TextExtensions);
        all.UnionWith(DiskExtensions);
        return all;
    }

    private static readonly string[] JsonFieldNames =
    [
        "url", "src", "file", "source", "videoUrl", "imageUrl",
        "hls_url", "dash_url", "mp4", "webm", "poster",
        "thumbnail", "image", "cover",
    ];

    private readonly IDependencyManagerService _depManager;

    /// <summary>Initialises a new <see cref="WebScraperService"/>.</summary>
    public WebScraperService(IDependencyManagerService depManager)
    {
        _depManager = depManager;
    }

    /// <inheritdoc/>
    public async Task<List<ScrapedAsset>> ScrapeAsync(
        string pageUrl,
        bool crawlSubdirectories,
        int maxDepth,
        int maxPages,
        Action<(int pagesScraped, int assetsFound)>? onProgress,
        CancellationToken ct = default)
    {
        var baseUri = new Uri(pageUrl);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assets = new List<ScrapedAsset>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { NormaliseUrl(baseUri) };
        var queue = new Queue<(Uri url, int depth)>();
        queue.Enqueue((baseUri, 0));

        int pagesScraped = 0;

        while (queue.Count > 0 && pagesScraped < maxPages)
        {
            ct.ThrowIfCancellationRequested();
            var (currentUrl, depth) = queue.Dequeue();

            string html;
            try
            {
                html = await Http.GetStringAsync(currentUrl, ct);
            }
            catch
            {
                continue;
            }

            var pageAssets = await ExtractAssetsFromHtmlAsync(html, currentUrl, seen, ct);
            assets.AddRange(pageAssets);
            pagesScraped++;
            onProgress?.Invoke((pagesScraped, assets.Count));

            if (crawlSubdirectories && depth < maxDepth)
            {
                var links = ExtractCrawlLinks(html, currentUrl, baseUri);
                foreach (var link in links)
                {
                    var norm = NormaliseUrl(link);
                    if (visited.Add(norm))
                    {
                        queue.Enqueue((link, depth + 1));
                    }
                }
            }
        }

        return assets;
    }

    /// <inheritdoc/>
    public async Task<string> DownloadAssetAsync(
        ScrapedAsset asset,
        string outputDir,
        IProgress<double>? progress,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);
        var safeFileName = SanitiseFileName(asset.FileName);
        var outputPath = GetUniqueFilePath(outputDir, safeFileName);

        if (asset.IsHls || asset.IsDash)
        {
            var mp4Name = Path.ChangeExtension(safeFileName, ".mp4");
            outputPath = GetUniqueFilePath(outputDir, mp4Name);

            var psi = new ProcessStartInfo
            {
                FileName = _depManager.FfmpegPath,
                Arguments = $"-i \"{asset.Url}\" -c copy \"{outputPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            if (process is not null)
            {
                await process.WaitForExitAsync(ct);
            }

            return outputPath;
        }

        if (asset.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var base64Match = DataUriRegex().Match(asset.Url);
            if (base64Match.Success)
            {
                var bytes = Convert.FromBase64String(base64Match.Groups[2].Value);
                await File.WriteAllBytesAsync(outputPath, bytes, ct);
                return outputPath;
            }
        }

        using var response = await Http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long bytesRead = 0;
        int read;

        while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, read), ct);
            bytesRead += read;

            if (totalBytes > 0)
            {
                progress?.Report((double)bytesRead / totalBytes * 100);
            }
        }

        progress?.Report(100);
        return outputPath;
    }

    private async Task<List<ScrapedAsset>> ExtractAssetsFromHtmlAsync(
        string html, Uri pageUrl, HashSet<string> seen, CancellationToken ct)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var assets = new List<ScrapedAsset>();

        // <img src>, <img data-src>, <img data-lazy-src>
        foreach (var img in SafeSelect(doc.DocumentNode, "//img"))
        {
            TryAddUrl(img.GetAttributeValue("src", ""), pageUrl, seen, assets, AssetType.Image);
            TryAddUrl(img.GetAttributeValue("data-src", ""), pageUrl, seen, assets, AssetType.Image);
            TryAddUrl(img.GetAttributeValue("data-lazy-src", ""), pageUrl, seen, assets, AssetType.Image);

            var srcset = img.GetAttributeValue("srcset", "");
            if (!string.IsNullOrWhiteSpace(srcset))
            {
                var bestUrl = ParseSrcsetBestUrl(srcset);
                if (!string.IsNullOrEmpty(bestUrl))
                {
                    TryAddUrl(bestUrl, pageUrl, seen, assets, AssetType.Image);
                }
            }
        }

        // <video src>, <video poster>
        foreach (var video in SafeSelect(doc.DocumentNode, "//video"))
        {
            TryAddUrl(video.GetAttributeValue("src", ""), pageUrl, seen, assets, AssetType.Video);
            TryAddUrl(video.GetAttributeValue("poster", ""), pageUrl, seen, assets, AssetType.Image);
        }

        // <source src> inside <video> or <audio>
        foreach (var source in SafeSelect(doc.DocumentNode, "//video//source|//audio//source"))
        {
            var src = source.GetAttributeValue("src", "");
            var parent = source.ParentNode?.Name;
            var type = parent == "audio" ? AssetType.Audio : AssetType.Video;
            TryAddUrl(src, pageUrl, seen, assets, type);
        }

        // <audio src>
        foreach (var audio in SafeSelect(doc.DocumentNode, "//audio"))
        {
            TryAddUrl(audio.GetAttributeValue("src", ""), pageUrl, seen, assets, AssetType.Audio);
        }

        // <a href> with known downloadable extension
        foreach (var anchor in SafeSelect(doc.DocumentNode, "//a[@href]"))
        {
            var href = anchor.GetAttributeValue("href", "");
            if (HasDownloadableExtension(href))
            {
                TryAddUrl(href, pageUrl, seen, assets);
            }
        }

        // <a download> attribute — browser download hint, any file type
        foreach (var anchor in SafeSelect(doc.DocumentNode, "//a[@download]"))
        {
            var href = anchor.GetAttributeValue("href", "");
            if (!string.IsNullOrWhiteSpace(href))
            {
                TryAddUrl(href, pageUrl, seen, assets);
            }
        }

        // <object data>, <embed src> — plugins/embedded content
        foreach (var obj in SafeSelect(doc.DocumentNode, "//object[@data]|//embed[@src]"))
        {
            var src = obj.GetAttributeValue("data", "") ?? obj.GetAttributeValue("src", "");
            if (HasDownloadableExtension(src ?? ""))
            {
                TryAddUrl(src!, pageUrl, seen, assets);
            }
        }

        // <iframe src> — one level of recursion
        foreach (var iframe in SafeSelect(doc.DocumentNode, "//iframe[@src]"))
        {
            var src = iframe.GetAttributeValue("src", "");
            if (!string.IsNullOrWhiteSpace(src) && Uri.TryCreate(pageUrl, src, out var iframeUri))
            {
                try
                {
                    var iframeHtml = await Http.GetStringAsync(iframeUri, ct);
                    var iframeAssets = await ExtractAssetsFromHtmlAsync(iframeHtml, iframeUri, seen, ct);
                    assets.AddRange(iframeAssets);
                }
                catch
                {
                    // Iframe content may be unreachable — skip.
                }
            }
        }

        // <link rel="preload" as="image|video">
        foreach (var link in SafeSelect(doc.DocumentNode, "//link[@rel='preload']"))
        {
            var asAttr = link.GetAttributeValue("as", "").ToLowerInvariant();
            var href = link.GetAttributeValue("href", "");
            if (asAttr == "image")
            {
                TryAddUrl(href, pageUrl, seen, assets, AssetType.Image);
            }
            else if (asAttr == "video")
            {
                TryAddUrl(href, pageUrl, seen, assets, AssetType.Video);
            }
        }

        // Inline style attributes: url(...)
        foreach (var node in SafeSelect(doc.DocumentNode, "//*[@style]"))
        {
            var style = node.GetAttributeValue("style", "");
            foreach (Match m in CssUrlRegex().Matches(style))
            {
                TryAddUrl(m.Groups[1].Value, pageUrl, seen, assets);
            }
        }

        // <script> tag bodies
        foreach (var script in SafeSelect(doc.DocumentNode, "//script"))
        {
            var text = script.InnerText;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            // Quoted strings with known media extensions
            foreach (Match m in MediaUrlInQuotesRegex().Matches(text))
            {
                TryAddUrl(m.Groups[1].Value, pageUrl, seen, assets);
            }

            // JSON-style field patterns
            foreach (var field in JsonFieldNames)
            {
                var pattern = new Regex($"\"{Regex.Escape(field)}\"\\s*:\\s*\"(https?://[^\"]+)\"",
                    RegexOptions.IgnoreCase);
                foreach (Match m in pattern.Matches(text))
                {
                    TryAddUrl(m.Groups[1].Value, pageUrl, seen, assets);
                }
            }

            // Walk embedded JSON objects: window.__INITIAL_STATE__, window.__NEXT_DATA__, etc.
            foreach (Match m in WindowJsonAssignmentRegex().Matches(text))
            {
                var jsonStart = m.Index + m.Length - 1;
                var jsonText = ExtractBalancedJson(text, jsonStart);
                if (!string.IsNullOrEmpty(jsonText))
                {
                    try
                    {
                        var token = JToken.Parse(jsonText);
                        CollectUrlsFromJson(token, pageUrl, seen, assets);
                    }
                    catch
                    {
                        // Malformed JSON — skip.
                    }
                }
            }

            // data:image base64
            foreach (Match m in DataImageBase64Regex().Matches(text))
            {
                var mime = m.Groups[1].Value;
                var b64 = m.Groups[2].Value;
                var ext = mime.Contains("png") ? ".png"
                    : mime.Contains("gif") ? ".gif"
                    : mime.Contains("webp") ? ".webp"
                    : mime.Contains("svg") ? ".svg"
                    : ".jpg";

                var dataUrl = $"data:image/{mime};base64,{b64}";
                if (seen.Add(dataUrl))
                {
                    assets.Add(new ScrapedAsset
                    {
                        Url = dataUrl,
                        FileName = $"image_{assets.Count + 1:D3}{ext}",
                        Type = AssetType.Image,
                        MimeHint = $"image/{mime}",
                        SourcePageUrl = pageUrl.ToString(),
                    });
                }
            }
        }

        // Process HLS/DASH assets
        var hlsDashAssets = new List<ScrapedAsset>();
        foreach (var asset in assets.ToList())
        {
            if (asset.Url.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var m3u8Content = await Http.GetStringAsync(asset.Url, ct);
                    var variants = ParseHlsPlaylist(m3u8Content, asset.Url);
                    if (variants.Count > 0)
                    {
                        assets.Remove(asset);
                        foreach (var variant in variants)
                        {
                            variant.SourcePageUrl = pageUrl.ToString();
                            if (seen.Add(variant.Url))
                            {
                                hlsDashAssets.Add(variant);
                            }
                        }
                    }
                    else
                    {
                        asset.IsHls = true;
                    }
                }
                catch
                {
                    asset.IsHls = true;
                }
            }
            else if (asset.Url.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase))
            {
                asset.IsDash = true;
                try
                {
                    var mpdContent = await Http.GetStringAsync(asset.Url, ct);
                    var mpdDoc = new HtmlDocument();
                    mpdDoc.LoadHtml(mpdContent);
                    foreach (var baseUrl in SafeSelect(mpdDoc.DocumentNode, "//BaseURL"))
                    {
                        var url = baseUrl.InnerText.Trim();
                        if (!string.IsNullOrEmpty(url))
                        {
                            TryAddUrl(url, new Uri(asset.Url), seen, hlsDashAssets, AssetType.Video);
                        }
                    }
                }
                catch
                {
                    // Use manifest URL as-is.
                }
            }
        }

        assets.AddRange(hlsDashAssets);
        return assets;
    }

    private static List<ScrapedAsset> ParseHlsPlaylist(string content, string manifestUrl)
    {
        var variants = new List<ScrapedAsset>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var baseUri = new Uri(manifestUrl);

        if (content.Contains("#EXT-X-STREAM-INF"))
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!line.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var resolution = "";
                var resMatch = Regex.Match(line, @"RESOLUTION=(\d+x\d+)", RegexOptions.IgnoreCase);
                if (resMatch.Success)
                {
                    resolution = resMatch.Groups[1].Value;
                }

                if (i + 1 < lines.Length)
                {
                    var variantUrl = lines[i + 1].Trim();
                    if (!variantUrl.StartsWith('#'))
                    {
                        if (Uri.TryCreate(baseUri, variantUrl, out var absoluteUrl))
                        {
                            variants.Add(new ScrapedAsset
                            {
                                Url = absoluteUrl.ToString(),
                                FileName = Path.GetFileName(absoluteUrl.LocalPath),
                                Type = AssetType.Video,
                                IsHls = true,
                                Quality = resolution,
                                Resolution = resolution,
                            });
                        }
                    }
                }
            }
        }
        else if (content.Contains("#EXTINF"))
        {
            variants.Add(new ScrapedAsset
            {
                Url = manifestUrl,
                FileName = Path.GetFileName(baseUri.LocalPath),
                Type = AssetType.Video,
                IsHls = true,
            });
        }

        return variants;
    }

    private static List<Uri> ExtractCrawlLinks(string html, Uri currentUrl, Uri baseUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var links = new List<Uri>();

        foreach (var anchor in SafeSelect(doc.DocumentNode, "//a[@href]"))
        {
            var href = anchor.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Uri.TryCreate(currentUrl, href, out var resolved))
            {
                continue;
            }

            if (!resolved.Host.Equals(baseUrl.Host, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!resolved.AbsolutePath.StartsWith(baseUrl.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (HasDownloadableExtension(resolved.AbsolutePath))
            {
                continue;
            }

            links.Add(resolved);
        }

        return links;
    }

    private static void TryAddUrl(string url, Uri pageUrl, HashSet<string> seen,
        List<ScrapedAsset> assets, AssetType? hintType = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        url = url.Trim();

        if (!Uri.TryCreate(pageUrl, url, out var absolute))
        {
            return;
        }

        var absoluteStr = absolute.ToString();
        if (!seen.Add(absoluteStr))
        {
            return;
        }

        var ext = Path.GetExtension(absolute.LocalPath).ToLowerInvariant();
        var type = hintType ?? ClassifyExtension(ext);

        assets.Add(new ScrapedAsset
        {
            Url = absoluteStr,
            FileName = SanitiseFileName(Path.GetFileName(absolute.LocalPath)),
            Type = type,
            FileExtension = ext,
            IsHls = ext == ".m3u8",
            IsDash = ext == ".mpd",
            SourcePageUrl = pageUrl.ToString(),
        });
    }

    private static AssetType ClassifyExtension(string ext)
    {
        if (ImageExtensions.Contains(ext)) return AssetType.Image;
        if (VideoExtensions.Contains(ext)) return AssetType.Video;
        if (AudioExtensions.Contains(ext)) return AssetType.Audio;
        if (DocumentExtensions.Contains(ext)) return AssetType.Document;
        if (SpreadsheetExtensions.Contains(ext)) return AssetType.Spreadsheet;
        if (PresentationExtensions.Contains(ext)) return AssetType.Presentation;
        if (ArchiveExtensions.Contains(ext)) return AssetType.Archive;
        if (ExecutableExtensions.Contains(ext)) return AssetType.Executable;
        if (FontExtensions.Contains(ext)) return AssetType.Font;
        if (DatabaseExtensions.Contains(ext)) return AssetType.Database;
        if (CodeExtensions.Contains(ext)) return AssetType.Code;
        if (TextExtensions.Contains(ext)) return AssetType.Text;
        if (DiskExtensions.Contains(ext)) return AssetType.Disk;
        return AssetType.Other;
    }

    private static bool HasDownloadableExtension(string urlOrPath)
    {
        try
        {
            var ext = Path.GetExtension(urlOrPath);
            return AllDownloadableExtensions.Contains(ext);
        }
        catch
        {
            return false;
        }
    }

    private static string ParseSrcsetBestUrl(string srcset)
    {
        var entries = srcset.Split(',', StringSplitOptions.RemoveEmptyEntries);
        string bestUrl = "";
        int bestWidth = -1;

        foreach (var entry in entries)
        {
            var parts = entry.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            var entryUrl = parts[0];
            if (parts.Length > 1 && parts[1].EndsWith('w'))
            {
                if (int.TryParse(parts[1][..^1], out int width) && width > bestWidth)
                {
                    bestWidth = width;
                    bestUrl = entryUrl;
                }
            }
            else
            {
                // No width descriptor — keep the last one as fallback
                bestUrl = entryUrl;
            }
        }

        return bestUrl;
    }

    private static void CollectUrlsFromJson(JToken token, Uri pageUrl, HashSet<string> seen, List<ScrapedAsset> assets)
    {
        switch (token)
        {
            case JValue jValue when jValue.Type == JTokenType.String:
            {
                var val = jValue.ToString();
                if ((val.StartsWith("http", StringComparison.OrdinalIgnoreCase) || val.StartsWith('/'))
                    && HasDownloadableExtension(val))
                {
                    TryAddUrl(val, pageUrl, seen, assets);
                }

                break;
            }
            case JArray jArray:
            {
                foreach (var item in jArray)
                {
                    CollectUrlsFromJson(item, pageUrl, seen, assets);
                }

                break;
            }
            case JObject jObj:
            {
                foreach (var prop in jObj.Properties())
                {
                    CollectUrlsFromJson(prop.Value, pageUrl, seen, assets);
                }

                break;
            }
        }
    }

    private static string? ExtractBalancedJson(string text, int start)
    {
        if (start >= text.Length)
        {
            return null;
        }

        var open = text[start];
        var close = open == '{' ? '}' : open == '[' ? ']' : (char?)null;
        if (close is null)
        {
            return null;
        }

        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    return text[start..(i + 1)];
                }
            }
        }

        return null;
    }

    private static string SanitiseFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "download";
        }

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }

    private static string GetUniqueFilePath(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
        {
            return path;
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var counter = 1;

        do
        {
            path = Path.Combine(dir, $"{nameWithoutExt}({counter}){ext}");
            counter++;
        }
        while (File.Exists(path));

        return path;
    }

    private static string NormaliseUrl(Uri uri)
    {
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
    }

    [GeneratedRegex(@"url\(['""]?([^'"")\s]+)['""]?\)", RegexOptions.IgnoreCase)]
    private static partial Regex CssUrlRegex();

    [GeneratedRegex(@"""(https?://[^""]+\.(?:mp4|mkv|webm|mov|avi|flv|m3u8|mpd|wmv|m4v|3gp|ts|mp3|flac|ogg|wav|aac|m4a|wma|opus|jpg|jpeg|png|gif|webp|svg|avif|bmp|ico|tiff|heic|psd|pdf|docx|doc|rtf|epub|xlsx|xls|csv|pptx|ppt|zip|rar|7z|tar|gz|bz2|xz|zst|tgz|exe|msi|dmg|pkg|deb|rpm|apk|jar|iso|img|torrent|ttf|otf|woff|woff2|sql|db|sqlite|json|xml|yaml|yml|txt|md|log|srt|vtt))""", RegexOptions.IgnoreCase)]
    private static partial Regex MediaUrlInQuotesRegex();

    [GeneratedRegex(@"window\.\w+\s*=\s*(\{|\[)", RegexOptions.IgnoreCase)]
    private static partial Regex WindowJsonAssignmentRegex();

    [GeneratedRegex(@"data:image/([a-zA-Z+]+);base64,([A-Za-z0-9+/=]+)")]
    private static partial Regex DataImageBase64Regex();

    [GeneratedRegex(@"data:(image/[^;]+);base64,([A-Za-z0-9+/=]+)")]
    private static partial Regex DataUriRegex();
}
        