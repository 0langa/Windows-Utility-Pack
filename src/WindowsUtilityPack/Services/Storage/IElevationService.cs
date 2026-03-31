namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Application-level elevation/admin mode service.
///
/// Architecture intent:
///   This service provides a clean, explicit concept of elevation state for
///   the entire application. Storage Master uses it to enable visibility of
///   hidden/system files and protected paths. Future tools can reuse the same
///   infrastructure without duplicating privilege checks.
///
/// Safety model:
///   - IsElevated is read-only (reflects current process privileges).
///   - RestartElevatedAsync requests UAC elevation by relaunching the process.
///   - The UI clearly indicates the current privilege state.
///   - No silent failures: access-denied scenarios surface as explicit errors.
/// </summary>
public interface IElevationService
{
    /// <summary>True if the current process is running with administrator privileges.</summary>
    bool IsElevated { get; }

    /// <summary>
    /// Restarts the application with administrator privileges via UAC prompt.
    /// Shuts down the current instance if the user accepts the UAC dialog.
    /// </summary>
    /// <returns>True if the elevated restart was initiated; false if the user declined UAC.</returns>
    Task<bool> RestartElevatedAsync();
}
