namespace WindowsUtilityPack.Services.Identifier;

/// <summary>
/// Generates ULID values according to the canonical 26-character Crockford Base32 format.
/// </summary>
public interface IUlidGenerator
{
    /// <summary>
    /// Creates a new ULID string.
    /// </summary>
    string Generate();
}

