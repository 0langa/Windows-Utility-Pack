namespace WindowsUtilityPack.Models;

/// <summary>
/// A registry value row exposed in Registry Editor.
/// </summary>
public sealed class RegistryValueRow
{
    public string Name { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string DisplayData { get; init; } = string.Empty;
}

/// <summary>
/// Serialized backup for a registry key subtree.
/// </summary>
public sealed class RegistryBackupNode
{
    public string RelativePath { get; init; } = string.Empty;

    public List<RegistryBackupValue> Values { get; init; } = [];

    public List<RegistryBackupNode> Children { get; init; } = [];
}

/// <summary>
/// Serialized registry value representation.
/// </summary>
public sealed class RegistryBackupValue
{
    public string Name { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string Data { get; init; } = string.Empty;
}