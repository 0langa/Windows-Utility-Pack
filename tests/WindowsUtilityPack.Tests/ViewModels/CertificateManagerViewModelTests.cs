using System.Security.Cryptography.X509Certificates;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools.SecurityPrivacy.CertificateManager;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class CertificateManagerViewModelTests
{
    [Fact]
    public async Task RefreshAsync_LoadsCertificates()
    {
        var vm = new CertificateManagerViewModel(new StubService(), new StubClipboard(), new StubDialogs());

        await vm.RefreshAsync();

        Assert.Single(vm.Certificates);
    }

    private sealed class StubService : ICertificateManagerService
    {
        public Task<string> ExportCertificatePemAsync(StoreLocation location, StoreName storeName, string thumbprint, CancellationToken cancellationToken = default)
            => Task.FromResult("pem");

        public Task<IReadOnlyList<CertificateManagerRow>> GetCertificatesAsync(StoreLocation location, StoreName storeName, string? query, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CertificateManagerRow> rows =
            [
                new CertificateManagerRow
                {
                    Subject = "CN=Demo",
                    Issuer = "CN=Demo CA",
                    Thumbprint = "ABC",
                    NotBefore = "2026-01-01",
                    NotAfter = "2027-01-01",
                    HasPrivateKey = false,
                },
            ];

            return Task.FromResult(rows);
        }

        public Task<string> GetCertificateDetailsAsync(StoreLocation location, StoreName storeName, string thumbprint, CancellationToken cancellationToken = default)
            => Task.FromResult("details");
    }

    private sealed class StubClipboard : IClipboardService
    {
        public bool TryGetText(out string text)
        {
            text = string.Empty;
            return false;
        }

        public void SetText(string text) { }

        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }

    private sealed class StubDialogs : IUserDialogService
    {
        public bool Confirm(string title, string message) => true;

        public void ShowError(string title, string message) { }

        public void ShowInfo(string title, string message) { }
    }
}