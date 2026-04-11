using WindowsUtilityPack.Services.Downloader;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader;

public sealed partial class DownloaderViewModel
{
    private void SelectHelpTopic(string? topic)
    {
        HelpContent = DownloaderHelpContentProvider.GetContent(topic);
    }

    private bool FilterHelpTopic(object obj)
    {
        if (obj is not string topic)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(HelpSearchText))
        {
            return true;
        }

        var query = HelpSearchText.Trim();
        if (topic.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var content = DownloaderHelpContentProvider.GetContent(topic);
        return content.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractFirstNormalizedUrl(string input, out string normalized)
    {
        normalized = ExtractNormalizedUrls(input).FirstOrDefault() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(normalized);
    }

    private static List<string> ExtractNormalizedUrls(string input)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(input))
        {
            return result;
        }

        var tokens = input.Split(['\r', '\n', '\t', ' ', ';', ','], StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var candidate = token.Trim().Trim('"', '\'', '<', '>', '(', ')', '[', ']', '{', '}');
            if (candidate.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                candidate = "https://" + candidate;
            }

            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !string.IsNullOrWhiteSpace(uri.Host))
            {
                result.Add(uri.ToString());
            }
        }

        return result;
    }

    private static string FormatIgnoredMessage(int ignoredCount)
    {
        return ignoredCount <= 0 ? string.Empty : $", ignored {ignoredCount} non-YouTube link(s)";
    }

    private static string PredictWorkflow(string url, DownloaderMode mode)
        => DownloaderWorkflowPredictor.Predict(url, mode);

    private static string BuildRouteReason(string url, string workflow, DownloaderMode mode)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "Routing fallback: input could not be normalized as an absolute URL.";
        }

        var host = uri.Host;
        return mode switch
        {
            DownloaderMode.QuickDownload =>
                $"Mode intent: Quick Download. Host '{host}' classified as {workflow}. Action: Add/Download will queue direct or engine-resolved job.",
            DownloaderMode.MediaDownload =>
                $"Mode intent: Media Download. Host '{host}' classified as {workflow}. Action: selected video/audio plan is applied before queue start.",
            DownloaderMode.AssetGrabber =>
                $"Mode intent: Asset Grabber. Host '{host}' classified as {workflow}. Action: scan and stage assets first (no direct fallback download).",
            DownloaderMode.SiteCrawl =>
                $"Mode intent: Site Crawl. Host '{host}' classified as {workflow}. Action: crawl scope + staged selection before queueing.",
            _ =>
                $"Mode intent: {mode}. Host '{host}' classified as {workflow}.",
        };
    }

    private static bool TryBuildDateTimeOffset(DateTime date, string timeText, out DateTimeOffset value)
    {
        value = default;
        if (!TimeSpan.TryParse(timeText, out var time))
        {
            return false;
        }

        var local = new DateTime(date.Year, date.Month, date.Day, time.Hours, time.Minutes, 0, DateTimeKind.Local);
        value = new DateTimeOffset(local);
        return true;
    }
}
