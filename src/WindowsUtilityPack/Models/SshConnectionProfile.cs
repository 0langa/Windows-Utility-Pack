namespace WindowsUtilityPack.Models;

/// <summary>
/// Persisted SSH connection profile.
/// </summary>
public sealed class SshConnectionProfile
{
    public string Name { get; init; } = string.Empty;

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 22;

    public string Username { get; init; } = string.Empty;

    public string PrivateKeyPath { get; init; } = string.Empty;

    public DateTime CreatedUtc { get; init; }

    public DateTime UpdatedUtc { get; init; }
}