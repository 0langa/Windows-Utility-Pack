namespace WindowsUtilityPack.Services.Downloader;

/// <summary>
/// Manages external tool dependencies (yt-dlp, gallery-dl, ffmpeg) required by the download engine.
/// </summary>
public interface IDependencyManagerService
{
    /// <summary>Downloads and installs any missing tool binaries.</summary>
    /// <param name="onProgress">Callback for human-readable progress messages.</param>
    /// <param name="ct">Cancellation token.</param>
    Task EnsureAllAsync(Action<string> onProgress, CancellationToken ct = default);

    /// <summary>Runs yt-dlp self-update and returns the stdout output.</summary>
    Task<string> UpdateYtDlpAsync(CancellationToken ct = default);

    /// <summary>Checks which tool binaries are present on disk.</summary>
    DependencyStatus Check();

    /// <summary>Absolute path to the yt-dlp executable.</summary>
    string YtDlpPath { get; }

    /// <summary>Absolute path to the gallery-dl executable.</summary>
    string GalleryDlPath { get; }

    /// <summary>Absolute path to the ffmpeg executable.</summary>
    string FfmpegPath { get; }
}

/// <summary>Indicates which tool binaries are available.</summary>
public record DependencyStatus(bool YtDlpOk, bool GalleryDlOk, bool FfmpegOk)
{
    /// <summary>True when all required tools are present.</summary>
    public bool AllOk => YtDlpOk && GalleryDlOk && FfmpegOk;
}
