using System.Security.Cryptography.X509Certificates;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Provides certificate store browsing and export operations.
/// </summary>
public interface ICertificateManagerService
{
    Task<IReadOnlyList<CertificateManagerRow>> GetCertificatesAsync(
        StoreLocation location,
        StoreName storeName,
        string? query,
        CancellationToken cancellationToken = default);

    Task<string> GetCertificateDetailsAsync(
        StoreLocation location,
        StoreName storeName,
        string thumbprint,
        CancellationToken cancellationToken = default);

    Task<string> ExportCertificatePemAsync(
        StoreLocation location,
        StoreName storeName,
        string thumbprint,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default certificate manager service based on X509Store.
/// </summary>
public sealed class CertificateManagerService : ICertificateManagerService
{
    public Task<IReadOnlyList<CertificateManagerRow>> GetCertificatesAsync(
        StoreLocation location,
        StoreName storeName,
        string? query,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<CertificateManagerRow>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var store = new X509Store(storeName, location);
            store.Open(OpenFlags.ReadOnly);

            var normalizedQuery = query?.Trim();

            return store.Certificates
                .Cast<X509Certificate2>()
                .Where(c =>
                    string.IsNullOrWhiteSpace(normalizedQuery) ||
                    c.Subject.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                    c.Thumbprint?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) == true)
                .Select(c => new CertificateManagerRow
                {
                    Subject = c.Subject,
                    Issuer = c.Issuer,
                    Thumbprint = c.Thumbprint ?? string.Empty,
                    NotBefore = c.NotBefore.ToString("u"),
                    NotAfter = c.NotAfter.ToString("u"),
                    HasPrivateKey = c.HasPrivateKey,
                })
                .OrderBy(c => c.Subject, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, cancellationToken);
    }

    public Task<string> GetCertificateDetailsAsync(
        StoreLocation location,
        StoreName storeName,
        string thumbprint,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var cert = FindCertificate(location, storeName, thumbprint);

            return string.Join(Environment.NewLine,
            [
                $"Subject: {cert.Subject}",
                $"Issuer: {cert.Issuer}",
                $"Thumbprint: {cert.Thumbprint}",
                $"Serial: {cert.SerialNumber}",
                $"Not Before: {cert.NotBefore:u}",
                $"Not After: {cert.NotAfter:u}",
                $"Has Private Key: {cert.HasPrivateKey}",
                $"Signature Algorithm: {cert.SignatureAlgorithm?.FriendlyName}",
            ]);
        }, cancellationToken);
    }

    public Task<string> ExportCertificatePemAsync(
        StoreLocation location,
        StoreName storeName,
        string thumbprint,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var cert = FindCertificate(location, storeName, thumbprint);
            var bytes = cert.Export(X509ContentType.Cert);
            return FormatPem(bytes, "CERTIFICATE");
        }, cancellationToken);
    }

    internal static string FormatPem(byte[] bytes, string label)
    {
        var base64 = Convert.ToBase64String(bytes);
        var lines = Enumerable.Range(0, (base64.Length + 63) / 64)
            .Select(i => base64.Substring(i * 64, Math.Min(64, base64.Length - (i * 64))));

        return $"-----BEGIN {label}-----{Environment.NewLine}" +
               string.Join(Environment.NewLine, lines) +
               $"{Environment.NewLine}-----END {label}-----";
    }

    private static X509Certificate2 FindCertificate(StoreLocation location, StoreName storeName, string thumbprint)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            throw new InvalidOperationException("Thumbprint is required.");
        }

        using var store = new X509Store(storeName, location);
        store.Open(OpenFlags.ReadOnly);

        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
        if (matches.Count == 0)
        {
            throw new InvalidOperationException("Certificate was not found in selected store.");
        }

        return new X509Certificate2(matches[0]);
    }
}