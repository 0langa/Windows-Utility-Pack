using System.Collections.ObjectModel;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.DeveloperProductivity.ApiMockServer;

/// <summary>
/// ViewModel for API mock server operations.
/// </summary>
public sealed class ApiMockServerViewModel : ViewModelBase
{
    private readonly IApiMockServerService _service;
    private readonly IUserDialogService _dialogs;

    private ApiMockEndpoint? _selectedEndpoint;
    private string _statusMessage = "Ready.";
    private int _port = 5057;
    private bool _isBusy;

    public ObservableCollection<ApiMockEndpoint> Endpoints { get; } = [];
    public ObservableCollection<ApiMockRequestLogEntry> RequestLog { get; } = [];

    public ApiMockEndpoint? SelectedEndpoint
    {
        get => _selectedEndpoint;
        set => SetProperty(ref _selectedEndpoint, value);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public bool IsRunning => _service.IsRunning;

    public string BaseUrl => _service.BaseUrl;

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

    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public RelayCommand AddEndpointCommand { get; }
    public RelayCommand RemoveEndpointCommand { get; }
    public RelayCommand SaveEndpointsCommand { get; }
    public RelayCommand RefreshLogCommand { get; }
    public RelayCommand ClearLogCommand { get; }

    public ApiMockServerViewModel(IApiMockServerService service, IUserDialogService dialogs)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        StartCommand = new AsyncRelayCommand(_ => StartAsync());
        StopCommand = new AsyncRelayCommand(_ => StopAsync());
        AddEndpointCommand = new RelayCommand(_ => AddEndpoint());
        RemoveEndpointCommand = new RelayCommand(_ => RemoveEndpoint());
        SaveEndpointsCommand = new RelayCommand(_ => SaveEndpoints());
        RefreshLogCommand = new RelayCommand(_ => RefreshLog());
        ClearLogCommand = new RelayCommand(_ => ClearLog());

        LoadEndpoints();
        RefreshLog();
    }

    internal async Task StartAsync()
    {
        IsBusy = true;
        try
        {
            SaveEndpoints();
            await _service.StartAsync(Port).ConfigureAwait(true);
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(BaseUrl));
            StatusMessage = $"API mock server started at {_service.BaseUrl}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to start API mock server.";
            _dialogs.ShowError("API Mock Server", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task StopAsync()
    {
        IsBusy = true;
        try
        {
            await _service.StopAsync().ConfigureAwait(true);
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(BaseUrl));
            StatusMessage = "API mock server stopped.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadEndpoints()
    {
        Endpoints.Clear();
        foreach (var endpoint in _service.GetEndpoints())
        {
            Endpoints.Add(endpoint);
        }
    }

    private void AddEndpoint()
    {
        var endpoint = new ApiMockEndpoint
        {
            Name = "New endpoint",
            Method = "GET",
            Path = "/new",
            StatusCode = 200,
            ContentType = "application/json",
            ResponseBody = "{\"ok\":true}",
            Enabled = true,
        };

        Endpoints.Add(endpoint);
        SelectedEndpoint = endpoint;
        StatusMessage = "Endpoint added. Click Save Endpoints to apply.";
    }

    private void RemoveEndpoint()
    {
        if (SelectedEndpoint is null)
        {
            return;
        }

        Endpoints.Remove(SelectedEndpoint);
        SelectedEndpoint = null;
        StatusMessage = "Endpoint removed. Click Save Endpoints to apply.";
    }

    private void SaveEndpoints()
    {
        _service.SetEndpoints(Endpoints.ToList());
        StatusMessage = "Endpoint configuration saved.";
    }

    private void RefreshLog()
    {
        RequestLog.Clear();
        foreach (var entry in _service.GetRequestLog())
        {
            RequestLog.Add(entry);
        }
    }

    private void ClearLog()
    {
        _service.ClearRequestLog();
        RefreshLog();
        StatusMessage = "Request log cleared.";
    }
}