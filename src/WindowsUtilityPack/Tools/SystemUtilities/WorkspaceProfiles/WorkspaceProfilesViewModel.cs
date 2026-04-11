using System.Collections.ObjectModel;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SystemUtilities.WorkspaceProfiles;

/// <summary>
/// ViewModel for workspace profile management.
/// </summary>
public sealed class WorkspaceProfilesViewModel : ViewModelBase
{
    private readonly IWorkspaceProfileCoordinator _coordinator;
    private readonly IUserDialogService _dialogs;

    private WorkspaceProfile? _selectedProfile;
    private string _profileName = string.Empty;
    private string _profileDescription = string.Empty;
    private string _startupToolKey = "home";
    private string _pinnedToolsCsv = string.Empty;
    private string _statusMessage = "Create and apply reusable workspace profiles.";

    public ObservableCollection<WorkspaceProfile> Profiles { get; } = [];
    public ObservableCollection<ToolDefinition> AvailableTools { get; } = [];

    public WorkspaceProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value) && value is not null)
            {
                ProfileName = value.Name;
                ProfileDescription = value.Description;
                StartupToolKey = value.StartupToolKey;
                PinnedToolsCsv = string.Join(",", value.PinnedToolKeys);
            }
        }
    }

    public string ProfileName
    {
        get => _profileName;
        set => SetProperty(ref _profileName, value);
    }

    public string ProfileDescription
    {
        get => _profileDescription;
        set => SetProperty(ref _profileDescription, value);
    }

    public string StartupToolKey
    {
        get => _startupToolKey;
        set => SetProperty(ref _startupToolKey, value);
    }

    public string PinnedToolsCsv
    {
        get => _pinnedToolsCsv;
        set => SetProperty(ref _pinnedToolsCsv, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand SaveProfileCommand { get; }
    public AsyncRelayCommand ApplyProfileCommand { get; }
    public AsyncRelayCommand DeleteProfileCommand { get; }

    public WorkspaceProfilesViewModel(IWorkspaceProfileCoordinator coordinator, IUserDialogService dialogs)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        foreach (var tool in ToolRegistry.GetDisplayTools().OrderBy(t => t.Name))
        {
            AvailableTools.Add(tool);
        }

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        SaveProfileCommand = new AsyncRelayCommand(_ => SaveProfileAsync());
        ApplyProfileCommand = new AsyncRelayCommand(_ => ApplyProfileAsync(), _ => SelectedProfile is not null);
        DeleteProfileCommand = new AsyncRelayCommand(_ => DeleteProfileAsync(), _ => SelectedProfile is not null);

        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var profiles = await _coordinator.GetProfilesAsync().ConfigureAwait(true);
        Profiles.Clear();
        foreach (var profile in profiles)
        {
            Profiles.Add(profile);
        }

        if (Profiles.Count > 0 && SelectedProfile is null)
        {
            SelectedProfile = Profiles[0];
        }

        StatusMessage = Profiles.Count == 0
            ? "No profiles yet. Create your first workspace profile."
            : $"Loaded {Profiles.Count:N0} workspace profiles.";
    }

    private async Task SaveProfileAsync()
    {
        var name = ProfileName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Profile name is required.";
            return;
        }

        var keys = ParsePinnedTools(PinnedToolsCsv);
        await _coordinator.CaptureCurrentAsync(
            name,
            ProfileDescription,
            StartupToolKey,
            keys).ConfigureAwait(true);

        await RefreshAsync().ConfigureAwait(true);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        StatusMessage = $"Saved profile '{name}'.";
    }

    private async Task ApplyProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var applied = await _coordinator.ApplyProfileAsync(SelectedProfile.Name).ConfigureAwait(true);
        if (!applied)
        {
            StatusMessage = "Unable to apply selected profile.";
            return;
        }

        StatusMessage = $"Applied profile '{SelectedProfile.Name}'. Restart to apply startup-page change.";
        _dialogs.ShowInfo("Workspace profile applied", "Profile settings were applied. Restart the app to use the new startup page.");
    }

    private async Task DeleteProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (!_dialogs.Confirm("Delete profile", $"Delete workspace profile '{SelectedProfile.Name}'?"))
        {
            return;
        }

        var name = SelectedProfile.Name;
        var removed = await _coordinator.DeleteProfileAsync(name).ConfigureAwait(true);
        if (!removed)
        {
            StatusMessage = "Selected profile could not be deleted.";
            return;
        }

        SelectedProfile = null;
        await RefreshAsync().ConfigureAwait(true);
        StatusMessage = $"Deleted profile '{name}'.";
    }

    private static IReadOnlyList<string> ParsePinnedTools(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}