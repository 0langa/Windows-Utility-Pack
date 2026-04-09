namespace WindowsUtilityPack.Services.Downloader;

/// <summary>
/// Centralised list of well-known media and gallery hosts used by the engine resolver
/// and the ViewModel's workflow-prediction helper.
/// Fix Issue 16: a single source of truth prevents the engine and the UI from diverging.
/// </summary>
public static class DownloaderKnownHosts
{
    /// <summary>Hosts handled by the media engine (yt-dlp).</summary>
    public static readonly IReadOnlyList<string> MediaHosts =
    [
        "youtube",
        "youtu.be",
        "vimeo",
        "dailymotion",
        "twitch",
        "soundcloud",
    ];

    /// <summary>Hosts handled by the gallery engine (gallery-dl).</summary>
    public static readonly IReadOnlyList<string> GalleryHosts =
    [
        "imgur",
        "reddit",
        "flickr",
        "deviantart",
        "pixiv",
        "tumblr",
    ];

    /// <summary>Returns true if <paramref name="host"/> matches any entry in <paramref name="hosts"/>.</summary>
    public static bool Matches(string host, IReadOnlyList<string> hosts)
    {
        foreach (var known in hosts)
        {
            if (host.Contains(known, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
