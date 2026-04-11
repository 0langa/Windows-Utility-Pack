using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Persists reusable workspace profiles.
/// </summary>
public interface IWorkspaceProfileService
{
    /// <summary>
    /// Returns all saved profiles ordered by latest update time.
    /// </summary>
    Task<IReadOnlyList<WorkspaceProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one profile by name, or null when not found.
    /// </summary>
    Task<WorkspaceProfile?> GetProfileAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a profile.
    /// </summary>
    Task SaveProfileAsync(WorkspaceProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a profile by name.
    /// </summary>
    Task<bool> DeleteProfileAsync(string name, CancellationToken cancellationToken = default);
}