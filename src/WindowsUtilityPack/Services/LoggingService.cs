using System.IO;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Appends timestamped log lines to a plain-text file.
/// The log file is created (and its parent directory) on first write.
/// Simple size-based rotation is applied: when the file exceeds
/// <see cref="MaxLogSizeBytes"/> (1 MB), it is renamed to <c>app.log.1</c>.
///
/// Log location: <c>%LOCALAPPDATA%\WindowsUtilityPack\app.log</c>
/// </summary>
public class LoggingService : ILoggingService
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsUtilityPack", "app.log");

    private const long MaxLogSizeBytes = 1_048_576; // 1 MB

    // Lock object ensures thread-safe sequential writes.
    private readonly object _lock = new();

    /// <inheritdoc/>
    public void LogInfo(string message) => Write("INFO", message, null);

    /// <inheritdoc/>
    public void LogWarning(string message) => Write("WARN", message, null);

    /// <inheritdoc/>
    public void LogError(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private void Write(string level, string message, Exception? ex)
    {
        try
        {
            lock (_lock)
            {
                var dir = Path.GetDirectoryName(LogPath)!;
                Directory.CreateDirectory(dir);

                RotateIfNeeded();

                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                if (ex != null)
                    line += $"\n  Exception: {ex.GetType().Name}: {ex.Message}\n  {ex.StackTrace ?? "(no stack trace)"}";

                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch { /* swallow logging errors — logging must never crash the app */ }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(LogPath)) return;

            var info = new FileInfo(LogPath);
            if (info.Length <= MaxLogSizeBytes) return;

            var backupPath = LogPath + ".1";
            if (File.Exists(backupPath)) File.Delete(backupPath);
            File.Move(LogPath, backupPath);
        }
        catch { /* rotation failure is non-fatal */ }
    }
}
