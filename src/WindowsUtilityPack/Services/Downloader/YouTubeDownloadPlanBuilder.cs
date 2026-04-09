using System.Text;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>
/// Builds yt-dlp format expressions and readable plan text for focused YouTube video downloads.
/// </summary>
public static class YouTubeDownloadPlanBuilder
{
    /// <summary>
    /// Returns true when the URL points to YouTube (youtube.com or youtu.be).
    /// </summary>
    public static bool IsYouTubeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        return host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase)
               || host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".youtu.be", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds an explicit yt-dlp <c>-f</c> expression from user selections.
    /// </summary>
    public static string BuildFormatExpression(YouTubeDownloadOptions? options)
    {
        options ??= new YouTubeDownloadOptions();

        var videoFilters = new StringBuilder();
        var audioFilters = new StringBuilder();
        var maxHeight = ResolveMaxHeight(options.VideoQuality);
        var maxFps = ResolveMaxFps(options.FrameRate);
        var container = NormalizeContainer(options.Container);

        if (maxHeight.HasValue)
        {
            videoFilters.Append($"[height<={maxHeight.Value}]");
        }

        if (maxFps.HasValue)
        {
            videoFilters.Append($"[fps<={maxFps.Value}]");
        }

        var videoCodecFilter = ResolveVideoCodecFilter(options.VideoCodec);
        if (!string.IsNullOrWhiteSpace(videoCodecFilter))
        {
            videoFilters.Append(videoCodecFilter);
        }

        if (container is "mp4" or "webm")
        {
            videoFilters.Append($"[ext={container}]");
        }

        var maxAbr = ResolveAudioAbr(options.AudioQuality);
        if (maxAbr.HasValue)
        {
            audioFilters.Append($"[abr<={maxAbr.Value}]");
        }

        var audioCodecFilter = ResolveAudioCodecFilter(options.AudioCodec, container);
        if (!string.IsNullOrWhiteSpace(audioCodecFilter))
        {
            audioFilters.Append(audioCodecFilter);
        }

        var videoSelector = $"bestvideo{videoFilters}";
        var audioSelector = $"bestaudio{audioFilters}";
        var fallbackSelector = maxHeight.HasValue ? $"best[height<={maxHeight.Value}]" : "best";
        return $"{videoSelector}+{audioSelector}/{fallbackSelector}";
    }

    /// <summary>
    /// Builds a human-readable plan summary for the selected YouTube profile.
    /// </summary>
    public static string BuildSummary(YouTubeDownloadOptions? options)
    {
        options ??= new YouTubeDownloadOptions();

        return $"YouTube plan: video {options.VideoQuality}, {options.FrameRate}, {options.VideoCodec}, audio {options.AudioQuality}, {options.AudioCodec}, container {NormalizeContainer(options.Container).ToUpperInvariant()}.";
    }

    private static int? ResolveMaxHeight(string? quality)
    {
        var text = quality?.Trim() ?? string.Empty;
        if (text.StartsWith("2160", StringComparison.OrdinalIgnoreCase))
        {
            return 2160;
        }

        if (text.StartsWith("1440", StringComparison.OrdinalIgnoreCase))
        {
            return 1440;
        }

        if (text.StartsWith("1080", StringComparison.OrdinalIgnoreCase))
        {
            return 1080;
        }

        if (text.StartsWith("720", StringComparison.OrdinalIgnoreCase))
        {
            return 720;
        }

        if (text.StartsWith("480", StringComparison.OrdinalIgnoreCase))
        {
            return 480;
        }

        if (text.StartsWith("360", StringComparison.OrdinalIgnoreCase))
        {
            return 360;
        }

        return null;
    }

    private static int? ResolveMaxFps(string? frameRate)
    {
        var text = frameRate?.Trim() ?? string.Empty;
        if (text.Contains("60", StringComparison.OrdinalIgnoreCase))
        {
            return 60;
        }

        if (text.Contains("30", StringComparison.OrdinalIgnoreCase))
        {
            return 30;
        }

        return null;
    }

    private static int? ResolveAudioAbr(string? audioQuality)
    {
        var text = audioQuality?.Trim() ?? string.Empty;
        if (text.Contains("256", StringComparison.OrdinalIgnoreCase))
        {
            return 256;
        }

        if (text.Contains("192", StringComparison.OrdinalIgnoreCase))
        {
            return 192;
        }

        if (text.Contains("128", StringComparison.OrdinalIgnoreCase))
        {
            return 128;
        }

        if (text.Contains("96", StringComparison.OrdinalIgnoreCase))
        {
            return 96;
        }

        return null;
    }

    private static string ResolveVideoCodecFilter(string? codec)
    {
        var text = codec?.Trim() ?? string.Empty;
        if (text.Contains("H.264", StringComparison.OrdinalIgnoreCase)
            || text.Contains("AVC", StringComparison.OrdinalIgnoreCase))
        {
            return "[vcodec*=avc1]";
        }

        if (text.Contains("VP9", StringComparison.OrdinalIgnoreCase))
        {
            return "[vcodec*=vp09]";
        }

        if (text.Contains("AV1", StringComparison.OrdinalIgnoreCase))
        {
            return "[vcodec*=av01]";
        }

        return string.Empty;
    }

    private static string ResolveAudioCodecFilter(string? codec, string container)
    {
        var text = codec?.Trim() ?? string.Empty;
        if (text.Contains("M4A", StringComparison.OrdinalIgnoreCase)
            || text.Contains("AAC", StringComparison.OrdinalIgnoreCase))
        {
            return "[ext=m4a]";
        }

        if (text.Contains("Opus", StringComparison.OrdinalIgnoreCase))
        {
            return "[acodec*=opus]";
        }

        if (text.Contains("Vorbis", StringComparison.OrdinalIgnoreCase))
        {
            return "[acodec*=vorbis]";
        }

        if (container == "mp4")
        {
            return "[ext=m4a]";
        }

        if (container == "webm")
        {
            return "[ext=webm]";
        }

        return string.Empty;
    }

    private static string NormalizeContainer(string? container)
    {
        var value = container?.Trim().ToLowerInvariant();
        return value is "mp4" or "mkv" or "webm" ? value : "mp4";
    }
}

/// <summary>
/// Selection model for the focused YouTube tab.
/// </summary>
public sealed class YouTubeDownloadOptions
{
    public string VideoQuality { get; set; } = "1080p (Full HD)";

    public string FrameRate { get; set; } = "Up to 60 fps";

    public string VideoCodec { get; set; } = "Any codec";

    public string AudioQuality { get; set; } = "Best available";

    public string AudioCodec { get; set; } = "Best available";

    public string Container { get; set; } = "mp4";
}
