namespace WindowsUtilityPack.Models;

/// <summary>
/// Certificate summary row for Certificate Manager.
/// </summary>
public sealed class CertificateManagerRow
{
    public string Subject { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;

    public string Thumbprint { get; init; } = string.Empty;

    public string NotBefore { get; init; } = string.Empty;

    public string NotAfter { get; init; } = string.Empty;

    public bool HasPrivateKey { get; init; }
}