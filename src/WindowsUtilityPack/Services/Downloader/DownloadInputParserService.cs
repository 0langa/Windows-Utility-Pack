using System.Text.RegularExpressions;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>Extracts and normalizes URLs from user input text, multi-line lists, and noisy clipboard content.</summary>
public sealed partial class DownloadInputParserService : IDownloadInputParserService
{
    public IReadOnlyList<string> ExtractCandidateUrls(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in UrlRegex().Matches(input))
        {
            if (!match.Success)
            {
                continue;
            }

            if (TryNormalizeUrl(match.Value, out var normalized))
            {
                values.Add(normalized);
            }
        }

        if (values.Count == 0)
        {
            foreach (var token in input.Split(['\r', '\n', '\t', ' ', ';', ',', '|'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (TryNormalizeUrl(token, out var normalized))
                {
                    values.Add(normalized);
                }
            }
        }

        return values.ToList();
    }

    public bool TryNormalizeUrl(string input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var candidate = input.Trim().Trim('"', '\'', '<', '>', '(', ')', '[', ']', '{', '}');
        if (candidate.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            candidate = "https://" + candidate;
        }

        if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        normalized = uri.ToString();
        return true;
    }

    [GeneratedRegex(@"((https?://|www\.)[^\s""<>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();
}
