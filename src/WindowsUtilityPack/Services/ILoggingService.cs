namespace WindowsUtilityPack.Services;

/// <summary>
/// Contract for lightweight structured application logging.
/// Log entries are written to a plain-text file in the user profile.
/// </summary>
public interface ILoggingService
{
    /// <summary>Logs an informational message.</summary>
    void LogInfo(string message);

    /// <summary>Logs a non-fatal warning message.</summary>
    void LogWarning(string message);

    /// <summary>Logs an error message, optionally including exception details.</summary>
    void LogError(string message, Exception? ex = null);
}
