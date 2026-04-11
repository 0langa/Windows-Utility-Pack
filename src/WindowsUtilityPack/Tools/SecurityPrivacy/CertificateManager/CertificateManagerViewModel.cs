using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SecurityPrivacy.CertificateManager;

/// <summary>
/// ViewModel for certificate store browsing and exports.
/// </summary>
public sealed class CertificateManagerViewModel : ViewModelBase
{
    private readonly ICertificateManagerService _service;
    private readonly IClipboardService _clipboard;
    private readonly IUserDialogService _dialogs;

    private StoreLocation _selectedLocation = StoreLocation.CurrentUser;
    private StoreName _selectedStoreName = StoreName.My;
    private CertificateManagerRow? _selectedCertificate;
    private string _searchQuery = string.Empty;
    private string _statusMessage = "Ready.";
    private bool _isBusy;

    public ObservableCollection<CertificateManagerRow> Certificates { get; } = [];

    public IReadOnlyList<StoreLocation> StoreLocations { get; } = Enum.GetValues<StoreLocation>();

    public IReadOnlyList<StoreName> StoreNames { get; } = Enum.GetValues<StoreName>();

    public StoreLocation SelectedLocation
    {
        get => _selectedLocation;
        set => SetProperty(ref _selectedLocation, value);
    }

    public StoreName SelectedStoreName
    {
        get => _selectedStoreName;
        set => SetProperty(ref _selectedStoreName, value);
    }

    public CertificateManagerRow? SelectedCertificate
    {
        get => _selectedCertificate;
        set => SetProperty(ref _selectedCertificate, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
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
    public AsyncRelayCommand CopyDetailsCommand { get; }
    public AsyncRelayCommand CopyPemCommand { get; }

    public CertificateManagerViewModel(ICertificateManagerService service, IClipboardService clipboard, IUserDialogService dialogs)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        CopyDetailsCommand = new AsyncRelayCommand(_ => CopyDetailsAsync());
        CopyPemCommand = new AsyncRelayCommand(_ => CopyPemAsync());

        _ = RefreshAsync();
    }

    internal async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var rows = await _service.GetCertificatesAsync(SelectedLocation, SelectedStoreName, SearchQuery).ConfigureAwait(true);
            Certificates.Clear();
            foreach (var row in rows)
            {
                Certificates.Add(row);
            }

            StatusMessage = $"Loaded {Certificates.Count:N0} certificates.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to load certificates.";
            _dialogs.ShowError("Certificate Manager", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task CopyDetailsAsync()
    {
        if (SelectedCertificate is null)
        {
            return;
        }

        var text = await _service.GetCertificateDetailsAsync(SelectedLocation, SelectedStoreName, SelectedCertificate.Thumbprint).ConfigureAwait(true);
        _clipboard.SetText(text);
        StatusMessage = "Certificate details copied.";
    }

    internal async Task CopyPemAsync()
    {
        if (SelectedCertificate is null)
        {
            return;
        }

        var pem = await _service.ExportCertificatePemAsync(SelectedLocation, SelectedStoreName, SelectedCertificate.Thumbprint).ConfigureAwait(true);
        _clipboard.SetText(pem);
        StatusMessage = "Certificate PEM copied.";
    }
}