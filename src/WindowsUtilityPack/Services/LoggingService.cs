using System.IO;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Appends timestamped log lines to a plain-text file.
/// The log file is created (and its parent directory) on first write.
/// All I/O errors are silently swallowed so logging never crashes the application.
///
/// Log location: <c>%LOCALAPPDATA%\WindowsUtilityPack\app.log</c>
/// </summary>
public class LoggingService : ILoggingService
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsUtilityPack", "app.log");

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
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                if (ex != null) line += $"\n  Exception: {ex.Message}";
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch { /* swallow logging errors — logging must never crash the app */ }
    }
}
