using System.IO;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader.Engines;

public abstract class DownloadEngineBase : IDownloadEngine
{
    public abstract DownloadEngineType EngineType { get; }

    public abstract bool CanHandle(DownloadJob job, DownloaderSettings settings);

    public abstract Task<DownloadProbeResult> ProbeAsync(DownloadJob job, DownloaderSettings settings, CancellationToken cancellationToken);

    public abstract Task ExecuteAsync(
        DownloadJob job,
        DownloaderSettings settings,
        DownloadExecutionPlan plan,
        IProgress<DownloadProgressUpdate> progress,
        CancellationToken cancellationToken);

    protected static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "download";
        }

        var sanitized = fileName;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalid, '_');
        }

        sanitized = sanitized.Trim().TrimEnd('.');
        return FileTools.BatchRenameService.IsValidFileName(sanitized) ? sanitized : "download-" + Guid.NewGuid().ToString("N");
    }

    protected static string GetUniqueFilePath(string directory, string fileName)
    {
        var target = Path.Combine(directory, fileName);
        if (!File.Exists(target) && !Directory.Exists(target) && !File.Exists(target + ".wup-download.lock") && !Directory.Exists(target + ".wup-download.lock"))
        {
            return target;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);

        for (var index = 1; index < 10_000; index++)
        {
            var candidate = Path.Combine(directory, $"{baseName} ({index}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate) && !File.Exists(candidate + ".wup-download.lock") && !Directory.Exists(candidate + ".wup-download.lock"))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{baseName}-{Guid.NewGuid():N}{ext}");
    }

    /// <summary>
    /// Wraps an argument in double quotes and escapes any embedded double quotes.
    /// Fix Issue 16: centralised here to avoid copy-paste across engine subclasses.
    /// </summary>
    protected static string QuoteArg(string arg) =>
        $"\"{arg.Replace("\"", "\\\"")}\"";
}
