using System.IO;

namespace WindowsUtilityPack.Services;

public class LoggingService : ILoggingService
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsUtilityPack", "app.log");

    private readonly object _lock = new();

    public void LogInfo(string message) => Write("INFO", message, null);
    public void LogWarning(string message) => Write("WARN", message, null);
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
        catch { /* swallow logging errors */ }
    }
}
