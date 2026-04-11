using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Safe registry operations for HKCU-scoped editing.
/// </summary>
public interface IRegistryEditorService
{
    Task<IReadOnlyList<string>> GetSubKeyNamesAsync(string keyPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegistryValueRow>> GetValuesAsync(string keyPath, CancellationToken cancellationToken = default);

    Task SetValueAsync(string keyPath, string valueName, string valueData, string valueKind, CancellationToken cancellationToken = default);

    Task DeleteValueAsync(string keyPath, string valueName, CancellationToken cancellationToken = default);

    Task BackupAsync(string keyPath, string outputFilePath, CancellationToken cancellationToken = default);

    Task RestoreAsync(string inputFilePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registry editor service implementation limited to HKCU.
/// </summary>
public sealed class RegistryEditorService : IRegistryEditorService
{
    private const string RootPrefix = "Software";

    public Task<IReadOnlyList<string>> GetSubKeyNamesAsync(string keyPath, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<string>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizePath(keyPath);

            using var key = Registry.CurrentUser.OpenSubKey(normalized, writable: false)
                ?? throw new InvalidOperationException("Registry key does not exist.");

            return key.GetSubKeyNames()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, cancellationToken);
    }

    public Task<IReadOnlyList<RegistryValueRow>> GetValuesAsync(string keyPath, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<RegistryValueRow>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizePath(keyPath);

            using var key = Registry.CurrentUser.OpenSubKey(normalized, writable: false)
                ?? throw new InvalidOperationException("Registry key does not exist.");

            var names = key.GetValueNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
            var rows = new List<RegistryValueRow>();
            foreach (var name in names)
            {
                var kind = key.GetValueKind(name);
                var value = key.GetValue(name);
                rows.Add(new RegistryValueRow
                {
                    Name = string.IsNullOrEmpty(name) ? "(Default)" : name,
                    Kind = kind.ToString(),
                    DisplayData = SerializeValue(kind, value),
                });
            }

            return rows;
        }, cancellationToken);
    }

    public Task SetValueAsync(string keyPath, string valueName, string valueData, string valueKind, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizePath(keyPath);

            using var key = Registry.CurrentUser.CreateSubKey(normalized, writable: true)
                ?? throw new InvalidOperationException("Unable to open registry key for write.");

            var name = valueName == "(Default)" ? string.Empty : valueName;
            var kind = ParseKind(valueKind);
            var data = ParseValueData(kind, valueData);
            key.SetValue(name, data, kind);
        }, cancellationToken);
    }

    public Task DeleteValueAsync(string keyPath, string valueName, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizePath(keyPath);

            using var key = Registry.CurrentUser.OpenSubKey(normalized, writable: true)
                ?? throw new InvalidOperationException("Registry key does not exist.");

            var name = valueName == "(Default)" ? string.Empty : valueName;
            key.DeleteValue(name, throwOnMissingValue: false);
        }, cancellationToken);
    }

    public Task BackupAsync(string keyPath, string outputFilePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizePath(keyPath);

            using var key = Registry.CurrentUser.OpenSubKey(normalized, writable: false)
                ?? throw new InvalidOperationException("Registry key does not exist.");

            var root = BuildNode(key, normalized, cancellationToken);
            var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputFilePath, json, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task RestoreAsync(string inputFilePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(inputFilePath))
            {
                throw new FileNotFoundException("Backup file does not exist.", inputFilePath);
            }

            var json = await File.ReadAllTextAsync(inputFilePath, cancellationToken).ConfigureAwait(false);
            var node = JsonSerializer.Deserialize<RegistryBackupNode>(json)
                ?? throw new InvalidOperationException("Backup file content is invalid.");

            if (!node.RelativePath.StartsWith(RootPrefix + "\\", StringComparison.OrdinalIgnoreCase) &&
                !node.RelativePath.Equals(RootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Backup path must be within HKCU\\Software.");
            }

            RestoreNode(node, cancellationToken);
        }, cancellationToken);
    }

    private static RegistryBackupNode BuildNode(RegistryKey key, string path, CancellationToken cancellationToken)
    {
        var node = new RegistryBackupNode { RelativePath = path };

        foreach (var valueName in key.GetValueNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var kind = key.GetValueKind(valueName);
            var value = key.GetValue(valueName);
            node.Values.Add(new RegistryBackupValue
            {
                Name = string.IsNullOrEmpty(valueName) ? "(Default)" : valueName,
                Kind = kind.ToString(),
                Data = SerializeValue(kind, value),
            });
        }

        foreach (var childName in key.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var child = key.OpenSubKey(childName, writable: false);
            if (child is null)
            {
                continue;
            }

            var childPath = path + "\\" + childName;
            node.Children.Add(BuildNode(child, childPath, cancellationToken));
        }

        return node;
    }

    private static void RestoreNode(RegistryBackupNode node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = NormalizePath(node.RelativePath);

        using var key = Registry.CurrentUser.CreateSubKey(normalized, writable: true)
            ?? throw new InvalidOperationException("Unable to create or open backup restore path.");

        foreach (var value in node.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = value.Name == "(Default)" ? string.Empty : value.Name;
            var kind = ParseKind(value.Kind);
            key.SetValue(name, ParseValueData(kind, value.Data), kind);
        }

        foreach (var child in node.Children)
        {
            RestoreNode(child, cancellationToken);
        }
    }

    private static string NormalizePath(string keyPath)
    {
        var trimmed = (keyPath ?? string.Empty).Trim().Trim('\\');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Registry path is required.");
        }

        var normalized = trimmed.Replace('/', '\\');
        if (normalized.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[5..];
        }

        if (!normalized.StartsWith(RootPrefix + "\\", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Equals(RootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only HKCU\\Software paths are supported.");
        }

        return normalized;
    }

    private static RegistryValueKind ParseKind(string kind)
    {
        return Enum.TryParse<RegistryValueKind>(kind, ignoreCase: true, out var parsed)
            ? parsed
            : RegistryValueKind.String;
    }

    private static object ParseValueData(RegistryValueKind kind, string data)
    {
        return kind switch
        {
            RegistryValueKind.DWord => int.TryParse(data, out var dword)
                ? dword
                : throw new InvalidOperationException("DWord values must be valid 32-bit integers."),

            RegistryValueKind.QWord => long.TryParse(data, out var qword)
                ? qword
                : throw new InvalidOperationException("QWord values must be valid 64-bit integers."),

            RegistryValueKind.MultiString => string.IsNullOrEmpty(data)
                ? Array.Empty<string>()
                : data.Split('|', StringSplitOptions.TrimEntries),

            RegistryValueKind.Binary => string.IsNullOrEmpty(data)
                ? Array.Empty<byte>()
                : Convert.FromBase64String(data),

            _ => data,
        };
    }

    private static string SerializeValue(RegistryValueKind kind, object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return kind switch
        {
            RegistryValueKind.MultiString when value is string[] values => string.Join("|", values),
            RegistryValueKind.Binary when value is byte[] bytes => Convert.ToBase64String(bytes),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }
}