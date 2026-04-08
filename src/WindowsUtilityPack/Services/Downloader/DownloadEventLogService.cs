using System.Text;
using System.IO;
using WindowsUtilityPack.Tools.NetworkInternet.Downloader.Models;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>Stores bounded in-memory downloader events and writes optional diagnostics logs.</summary>
public sealed class DownloadEventLogService : IDownloadEventLogService
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsUtilityPack",
        "logs");

    private static readonly string LogPath = Path.Combine(LogDirectory, "downloader.log");

    private const long MaxLogSizeBytes = 2_000_000;
    private readonly object _sync = new();
    private readonly Func<DownloaderSettings> _settingsAccessor;

    public event EventHandler<DownloadEventRecord>? EventRecorded;

    public DownloadEventLogService(Func<DownloaderSettings> settingsAccessor)
    {
        _settingsAccessor = settingsAccessor;
    }

    public void Log(DownloaderLogLevel level, string message, Guid? jobId = null)
    {
        var settings = _settingsAccessor();
        if (settings.Logging.LogLevel == DownloaderLogLevel.Off)
        {
            return;
        }

        if (settings.Logging.LogLevel == DownloaderLogLevel.ErrorsOnly
            && level != DownloaderLogLevel.ErrorsOnly)
        {
            return;
        }

        var record = new DownloadEventRecord
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            JobId = jobId,
            Message = message,
        };

        EventRecorded?.Invoke(this, record);

        lock (_sync)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded();

                var prefix = jobId.HasValue ? $"[{jobId.Value:N}] " : string.Empty;
                var line = $"[{record.Timestamp:yyyy-MM-dd HH:mm:ss}] [{record.Level}] {prefix}{record.Message}";
                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Logging must never crash downloader workflows.
            }
        }

        if (level == DownloaderLogLevel.ErrorsOnly)
        {
            try
            {
                App.LoggingService.LogError($"Downloader: {message}");
            }
            catch
            {
                // Non-fatal.
            }
        }
    }

    public async Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(LogDirectory);

        if (!File.Exists(LogPath))
        {
            await File.WriteAllTextAsync(LogPath, "Downloader diagnostics log is empty.", cancellationToken);
        }

        return LogPath;
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogPath))
        {
            return;
        }

        var info = new FileInfo(LogPath);
        if (info.Length <= MaxLogSizeBytes)
        {
            return;
        }

        var archivePath = LogPath + ".1";
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        File.Move(LogPath, archivePath);
    }
}
