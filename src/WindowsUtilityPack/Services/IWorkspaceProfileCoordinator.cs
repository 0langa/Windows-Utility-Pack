using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Coordinates workspace profile persistence and application to current shell settings.
/// </summary>
public interface IWorkspaceProfileCoordinator
{
    /// <summary>
    /// Returns all stored workspace profiles.
    /// </summary>
    Task<IReadOnlyList<WorkspaceProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a profile payload.
    /// </summary>
    Task SaveProfileAsync(WorkspaceProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stored profile by name.
    /// </summary>
    Task<bool> DeleteProfileAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a profile to startup page and favorite tool settings.
    /// </summary>
    Task<bool> ApplyProfileAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures current state as a profile.
    /// </summary>
    Task<WorkspaceProfile> CaptureCurrentAsync(
        string name,
        string description,
        string startupToolKey,
        IReadOnlyList<string> pinnedToolKeys,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default coordinator for workspace profile capture and apply flows.
/// </summary>
public sealed class WorkspaceProfileCoordinator : IWorkspaceProfileCoordinator
{
    private readonly IWorkspaceProfileService _profiles;
    private readonly ISettingsService _settings;
    private readonly IActivityLogService? _activityLog;

    public WorkspaceProfileCoordinator(
        IWorkspaceProfileService profiles,
        ISettingsService settings,
        IActivityLogService? activityLog = null)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _activityLog = activityLog;
    }

    public Task<IReadOnlyList<WorkspaceProfile>> GetProfilesAsync(CancellationToken cancellationToken = default)
        => _profiles.GetProfilesAsync(cancellationToken);

    public Task SaveProfileAsync(WorkspaceProfile profile, CancellationToken cancellationToken = default)
        => _profiles.SaveProfileAsync(profile, cancellationToken);

    public Task<bool> DeleteProfileAsync(string name, CancellationToken cancellationToken = default)
        => _profiles.DeleteProfileAsync(name, cancellationToken);

    public async Task<bool> ApplyProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var profile = await _profiles.GetProfileAsync(name, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return false;
        }

        var settings = _settings.Load();
        settings.StartupPage = string.IsNullOrWhiteSpace(profile.StartupToolKey)
            ? "home"
            : profile.StartupToolKey;
        settings.FavoriteToolKeys = profile.PinnedToolKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _settings.Save(settings);

        if (_activityLog is not null)
        {
            await _activityLog.LogAsync("WorkspaceProfiles", "Apply", profile.Name).ConfigureAwait(false);
        }

        return true;
    }

    public async Task<WorkspaceProfile> CaptureCurrentAsync(
        string name,
        string description,
        string startupToolKey,
        IReadOnlyList<string> pinnedToolKeys,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Profile name is required.", nameof(name));
        }

        var normalizedPinned = (pinnedToolKeys ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profile = new WorkspaceProfile
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            StartupToolKey = string.IsNullOrWhiteSpace(startupToolKey) ? "home" : startupToolKey.Trim(),
            PinnedToolKeys = normalizedPinned,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
        };

        await _profiles.SaveProfileAsync(profile, cancellationToken).ConfigureAwait(false);

        if (_activityLog is not null)
        {
            await _activityLog.LogAsync("WorkspaceProfiles", "Save", profile.Name).ConfigureAwait(false);
        }

        return profile;
    }
}