namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

/// <summary>User-selected media profile applied to media engine jobs.</summary>
public sealed class DownloadMediaProfile
{
    public string ProfileName { get; set; } = "Best";

    public string FormatExpression { get; set; } = "bestvideo+bestaudio/best";

    public bool AudioOnly { get; set; }

    public bool DownloadSubtitles { get; set; }

    public bool DownloadThumbnail { get; set; }

    public bool EmbedMetadata { get; set; } = true;

    public bool AllowPlaylist { get; set; }

    public string PreferredAudioFormat { get; set; } = "mp3";

    public string PreferredContainer { get; set; } = "mp4";
}
