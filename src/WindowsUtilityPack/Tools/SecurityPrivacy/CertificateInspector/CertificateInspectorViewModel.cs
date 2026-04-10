using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Win32;
using WindowsUtilityPack.Commands;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;

namespace WindowsUtilityPack.Tools.SecurityPrivacy.CertificateInspector;

/// <summary>Parsed certificate information for display.</summary>
public class CertificateInfo
{
    public string       Subject                { get; set; } = string.Empty;
    public string       Issuer                 { get; set; } = string.Empty;
    public string       Thumbprint             { get; set; } = string.Empty;
    public string       SerialNumber           { get; set; } = string.Empty;
    public string       SignatureAlgorithm     { get; set; } = string.Empty;
    public DateTime     ValidFrom              { get; set; }
    public DateTime     ValidTo                { get; set; }
    public string       DaysUntilExpiry        { get; set; } = string.Empty;
    public string       ExpiryStatus           { get; set; } = string.Empty;
    public List<string> SubjectAlternativeNames { get; set; } = [];
    public string       KeyUsage               { get; set; } = string.Empty;
    public string       PublicKeyAlgorithm     { get; set; } = string.Empty;
    public int          KeySize                { get; set; }
    public string       Version                { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for the Certificate Inspector tool.
/// Supports URL (TLS), local file, and PEM text input modes.
/// </summary>
public class CertificateInspectorViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboard;

    private string           _inputMode   = "URL";
    private string           _urlOrHost   = "github.com";
    private int              _port        = 443;
    private string           _pemText     = string.Empty;
    private string           _filePath    = string.Empty;
    private CertificateInfo? _certificate;
    private bool             _isLoading;
    private string           _statusMessage = string.Empty;

    public string InputMode
    {
        get => _inputMode;
        set
        {
            if (SetProperty(ref _inputMode, value))
            {
                OnPropertyChanged(nameof(IsUrlMode));
                OnPropertyChanged(nameof(IsFileMode));
                OnPropertyChanged(nameof(IsPemMode));
            }
        }
    }

    public bool IsUrlMode  { get => _inputMode == "URL";  set { if (value) InputMode = "URL";  } }
    public bool IsFileMode { get => _inputMode == "File"; set { if (value) InputMode = "File"; } }
    public bool IsPemMode  { get => _inputMode == "PEM";  set { if (value) InputMode = "PEM";  } }

    public string UrlOrHost
    {
        get => _urlOrHost;
        set => SetProperty(ref _urlOrHost, value);
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, Math.Clamp(value, 1, 65535));
    }

    public string PemText
    {
        get => _pemText;
        set => SetProperty(ref _pemText, value);
    }

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public CertificateInfo? Certificate
    {
        get => _certificate;
        set => SetProperty(ref _certificate, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AsyncRelayCommand InspectCommand    { get; }
    public RelayCommand      BrowseFileCommand { get; }
    public RelayCommand      CopyInfoCommand   { get; }

    public CertificateInspectorViewModel(IClipboardService clipboard)
    {
        _clipboard = clipboard;

        InspectCommand    = new AsyncRelayCommand(_ => InspectAsync(), _ => !IsLoading);
        BrowseFileCommand = new RelayCommand(_ => BrowseFile());
        CopyInfoCommand   = new RelayCommand(_ => CopyInfo(), _ => Certificate != null);
    }

    private async Task InspectAsync()
    {
        IsLoading     = true;
        Certificate   = null;
        StatusMessage = "Inspecting certificate…";

        try
        {
            X509Certificate2? cert = null;

            switch (InputMode)
            {
                case "URL":
                    cert = await GetCertificateFromUrlAsync(UrlOrHost, Port);
                    break;

                case "File":
                    if (!File.Exists(FilePath))
                        throw new FileNotFoundException("File not found.", FilePath);
                    cert = new X509Certificate2(FilePath);
                    break;

                case "PEM":
                    var pem   = PemText.Trim();
                    // Strip PEM headers/footers and decode base64
                    var lines = pem.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var b64   = string.Concat(lines
                        .Where(l => !l.StartsWith("-----"))
                        .Select(l => l.Trim()));
                    var bytes = Convert.FromBase64String(b64);
                    cert = new X509Certificate2(bytes);
                    break;
            }

            if (cert == null)
                throw new InvalidOperationException("Could not retrieve certificate.");

            Certificate   = ParseCertificate(cert);
            StatusMessage = "Certificate inspected successfully.";
            cert.Dispose();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task<X509Certificate2?> GetCertificateFromUrlAsync(string host, int port)
    {
        // Strip URL scheme if present
        if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            host = host[8..];
        else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            host = host[7..];
        // Remove path
        var slashIdx = host.IndexOf('/');
        if (slashIdx >= 0) host = host[..slashIdx];

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, port);

        using var ssl = new SslStream(tcp.GetStream(), false,
            (_, _, _, _) => true);   // Accept any cert for inspection

        await ssl.AuthenticateAsClientAsync(host);

        if (ssl.RemoteCertificate == null) return null;

        return new X509Certificate2(ssl.RemoteCertificate);
    }

    private static CertificateInfo ParseCertificate(X509Certificate2 cert)
    {
        var now       = DateTime.Now;
        var days      = (int)(cert.NotAfter - now).TotalDays;
        var expStatus = days < 0
            ? "Expired"
            : days < 30 ? "Expiring Soon" : "Valid";

        var sans = new List<string>();
        var sanExt = cert.Extensions["2.5.29.17"];
        if (sanExt != null)
        {
            // AsnEncodedData.Format gives human-readable SANs
            var formatted = sanExt.Format(false);
            foreach (var part in formatted.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed)) sans.Add(trimmed);
            }
        }

        var keyUsageExt = cert.Extensions["2.5.29.15"];
        var keyUsage    = keyUsageExt?.Format(false) ?? string.Empty;

        var keySize = 0;
        var pubKeyAlgo = cert.PublicKey.Oid.FriendlyName ?? cert.PublicKey.Oid.Value ?? "Unknown";
        try
        {
            if (cert.PublicKey.GetRSAPublicKey() is { } rsa)
                keySize = rsa.KeySize;
            else if (cert.PublicKey.GetECDsaPublicKey() is { } ecdsa)
                keySize = ecdsa.KeySize;
        }
        catch { /* ignore key size errors */ }

        return new CertificateInfo
        {
            Subject                = cert.Subject,
            Issuer                 = cert.Issuer,
            Thumbprint             = cert.Thumbprint,
            SerialNumber           = cert.SerialNumber,
            SignatureAlgorithm     = cert.SignatureAlgorithm.FriendlyName ?? cert.SignatureAlgorithm.Value ?? "Unknown",
            ValidFrom              = cert.NotBefore,
            ValidTo                = cert.NotAfter,
            DaysUntilExpiry        = days < 0 ? $"{Math.Abs(days)} days ago" : $"{days} days",
            ExpiryStatus           = expStatus,
            SubjectAlternativeNames = sans,
            KeyUsage               = keyUsage,
            PublicKeyAlgorithm     = pubKeyAlgo,
            KeySize                = keySize,
            Version                = $"V{cert.Version}",
        };
    }

    private void BrowseFile()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select Certificate",
            Filter = "Certificate Files (*.cer;*.pem;*.crt;*.der)|*.cer;*.pem;*.crt;*.der|All Files (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true)
        {
            FilePath  = dlg.FileName;
            InputMode = "File";
        }
    }

    private void CopyInfo()
    {
        if (Certificate == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("Certificate Information");
        sb.AppendLine(new string('-', 50));
        sb.AppendLine($"Subject:            {Certificate.Subject}");
        sb.AppendLine($"Issuer:             {Certificate.Issuer}");
        sb.AppendLine($"Valid From:         {Certificate.ValidFrom:yyyy-MM-dd}");
        sb.AppendLine($"Valid To:           {Certificate.ValidTo:yyyy-MM-dd}");
        sb.AppendLine($"Days Until Expiry:  {Certificate.DaysUntilExpiry} ({Certificate.ExpiryStatus})");
        sb.AppendLine($"Serial Number:      {Certificate.SerialNumber}");
        sb.AppendLine($"Thumbprint:         {Certificate.Thumbprint}");
        sb.AppendLine($"Signature Algo:     {Certificate.SignatureAlgorithm}");
        sb.AppendLine($"Public Key:         {Certificate.PublicKeyAlgorithm} {Certificate.KeySize}-bit");
        sb.AppendLine($"Version:            {Certificate.Version}");
        if (Certificate.SubjectAlternativeNames.Count > 0)
        {
            sb.AppendLine("SANs:");
            foreach (var san in Certificate.SubjectAlternativeNames)
                sb.AppendLine($"  {san}");
        }

        _clipboard.SetText(sb.ToString());
        StatusMessage = "Certificate info copied to clipboard.";
    }
}
