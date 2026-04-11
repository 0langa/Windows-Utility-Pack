using System.Collections.ObjectModel;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.NetworkInternet.SshRemoteTool;

/// <summary>
/// ViewModel for SSH profile management and remote connectivity checks.
/// </summary>
public sealed class SshRemoteToolViewModel : ViewModelBase
{
    private readonly ISshRemoteToolService _service;
    private readonly IUserDialogService _dialogs;
    private readonly IClipboardService _clipboard;

    private SshConnectionProfile? _selectedProfile;
    private string _name = string.Empty;
    private string _host = string.Empty;
    private int _port = 22;
    private string _username = string.Empty;
    private string _privateKeyPath = string.Empty;
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    public ObservableCollection<SshConnectionProfile> Profiles { get; } = [];

    public SshConnectionProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value))
            {
                return;
            }

            if (value is null)
            {
                return;
            }

            Name = value.Name;
            Host = value.Host;
            Port = value.Port;
            Username = value.Username;
            PrivateKeyPath = value.PrivateKeyPath;
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string PrivateKeyPath
    {
        get => _privateKeyPath;
        set => SetProperty(ref _privateKeyPath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand SaveProfileCommand { get; }
    public AsyncRelayCommand DeleteProfileCommand { get; }
    public AsyncRelayCommand TestConnectionCommand { get; }
    public RelayCommand CopyCommandCommand { get; }
    public RelayCommand NewProfileCommand { get; }

    public SshRemoteToolViewModel(ISshRemoteToolService service, IUserDialogService dialogs, IClipboardService clipboard)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        SaveProfileCommand = new AsyncRelayCommand(_ => SaveProfileAsync());
        DeleteProfileCommand = new AsyncRelayCommand(_ => DeleteProfileAsync());
        TestConnectionCommand = new AsyncRelayCommand(_ => TestConnectionAsync());
        CopyCommandCommand = new RelayCommand(_ => CopyCommand());
        NewProfileCommand = new RelayCommand(_ => NewProfile());

        _ = RefreshAsync();
    }

    internal async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var profiles = await _service.GetProfilesAsync().ConfigureAwait(true);
            Profiles.Clear();
            foreach (var profile in profiles)
            {
                Profiles.Add(profile);
            }

            StatusMessage = $"Loaded {Profiles.Count:N0} SSH profiles.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to load SSH profiles.";
            _dialogs.ShowError("SSH Remote Tool", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task SaveProfileAsync()
    {
        IsBusy = true;
        try
        {
            var profile = new SshConnectionProfile
            {
                Name = Name,
                Host = Host,
                Port = Port,
                Username = Username,
                PrivateKeyPath = PrivateKeyPath,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            };

            await _service.SaveProfileAsync(profile).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            StatusMessage = $"Saved SSH profile '{Name}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to save SSH profile.";
            _dialogs.ShowError("SSH Remote Tool", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task DeleteProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return;
        }

        if (!_dialogs.Confirm("Delete SSH profile", $"Delete profile '{Name}'?"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            _ = await _service.DeleteProfileAsync(Name).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            NewProfile();
            StatusMessage = "SSH profile deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to delete SSH profile.";
            _dialogs.ShowError("SSH Remote Tool", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task TestConnectionAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _service.TestConnectionAsync(Host, Port, TimeSpan.FromSeconds(5)).ConfigureAwait(true);
            StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CopyCommand()
    {
        var command = _service.BuildSshCommand(new SshConnectionProfile
        {
            Name = Name,
            Host = Host,
            Port = Port,
            Username = Username,
            PrivateKeyPath = PrivateKeyPath,
        });

        _clipboard.SetText(command);
        StatusMessage = "SSH command copied to clipboard.";
    }

    private void NewProfile()
    {
        SelectedProfile = null;
        Name = string.Empty;
        Host = string.Empty;
        Port = 22;
        Username = string.Empty;
        PrivateKeyPath = string.Empty;
        StatusMessage = "New profile form ready.";
    }
}