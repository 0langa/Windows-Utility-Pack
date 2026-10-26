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
                var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
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

            var name = valueName == "(Default)" ? string.Empty : valueName;
            ValidateValueName(name);
            var kind = ParseKind(valueKind);
            var data = ParseValueData(kind, valueData);
            using var key = Registry.CurrentUser.CreateSubKey(normalized, writable: true)
                ?? throw new InvalidOperationException("Unable to open registry key for write.");
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
            await AtomicFile.WriteAllTextAsync(outputFilePath, json, cancellationToken).ConfigureAwait(false);
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

            if (new FileInfo(inputFilePath).Length > 16 * 1024 * 1024) throw new InvalidDataException("Registry backups are limited to 16 MiB.");
            var json = await File.ReadAllTextAsync(inputFilePath, cancellationToken).ConfigureAwait(false);
            var node = JsonSerializer.Deserialize<RegistryBackupNode>(json)
                ?? throw new InvalidOperationException("Backup file content is invalid.");

            if (!node.RelativePath.StartsWith(RootPrefix + "\\", StringComparison.OrdinalIgnoreCase) &&
                !node.RelativePath.Equals(RootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Backup path must be within HKCU\\Software.");
            }

            var operations = new List<RestoreOperation>();
            ValidateTree(node, null, operations, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            var undo = new Stack<Action>();
            try
            {
                foreach (var operation in operations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnsureKey(operation.Path, undo);
                    using var key = Registry.CurrentUser.OpenSubKey(operation.Path, true)!;
                    foreach (var value in operation.Values)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var exists = key.GetValueNames().Contains(value.Name, StringComparer.OrdinalIgnoreCase);
                        var previous = exists ? key.GetValue(value.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) : null;
                        var previousKind = exists ? key.GetValueKind(value.Name) : RegistryValueKind.Unknown;
                        undo.Push(() =>
                        {
                            using var rollbackKey = Registry.CurrentUser.OpenSubKey(operation.Path, true)
                                ?? throw new IOException($"Cannot reopen {operation.Path} for rollback.");
                            if (exists) rollbackKey.SetValue(value.Name, previous!, previousKind);
                            else rollbackKey.DeleteValue(value.Name, false);
                        });
                        key.SetValue(value.Name, value.Data, value.Kind);
                    }
                }
            }
            catch (Exception original)
            {
                var failures = new List<Exception>();
                while (undo.TryPop(out var rollback)) { try { rollback(); } catch (Exception ex) { failures.Add(ex); } }
                if (failures.Count > 0) throw new AggregateException("Restore failed and some registry changes could not be rolled back.", new[] { original }.Concat(failures));
                throw;
            }
        }, cancellationToken);
    }

    private static RegistryBackupNode BuildNode(RegistryKey key, string path, CancellationToken cancellationToken)
    {
        var node = new RegistryBackupNode { RelativePath = path };

        foreach (var valueName in key.GetValueNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var kind = key.GetValueKind(valueName);
            var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            node.Values.Add(new RegistryBackupValue
            {
                Name = string.IsNullOrEmpty(valueName) ? "(Default)" : valueName,
                Kind = kind.ToString(),
                Data = SerializeValue(kind, value),
                Format = kind == RegistryValueKind.MultiString ? "json-string-array" : null,
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

    private sealed record RestoreValue(string Name, RegistryValueKind Kind, object Data);
    private sealed record RestoreOperation(string Path, List<RestoreValue> Values);

    private static void ValidateTree(RegistryBackupNode node, string? parent, List<RestoreOperation> operations, HashSet<string> visited)
    {
        if (node is null || node.Values is null || node.Children is null) throw new InvalidDataException("Invalid backup node.");
        var normalized = NormalizePath(node.RelativePath);
        if (parent is not null && !string.Equals(normalized[..normalized.LastIndexOf('\\')], parent, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A backup child must be directly beneath its parent key.");
        if (!visited.Add(normalized) || visited.Count > 10_000) throw new InvalidDataException("The backup has duplicate keys or too many keys.");
        var values = new List<RestoreValue>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in node.Values)
        {
            if (value is null) throw new InvalidDataException("Invalid backup value.");
            var name = value.Name == "(Default)" ? string.Empty : value.Name;
            ValidateValueName(name);
            if (!names.Add(name) || names.Count > 10_000) throw new InvalidDataException("Duplicate registry value or too many values.");
            var kind = ParseKind(value.Kind);
            var data = kind == RegistryValueKind.MultiString && value.Format is null
                ? value.Data.Split('|', StringSplitOptions.None) // legacy backups used pipes; retain their whitespace
                : ParseValueData(kind, value.Data);
            values.Add(new RestoreValue(name, kind, data));
        }
        operations.Add(new RestoreOperation(normalized, values));
        foreach (var child in node.Children) ValidateTree(child, normalized, operations, visited);
    }

    private static void EnsureKey(string path, Stack<Action> undo)
    {
        var parts = path.Split('\\');
        for (var i = 1; i <= parts.Length; i++)
        {
            var prefix = string.Join("\\", parts.Take(i));
            using var existing = Registry.CurrentUser.OpenSubKey(prefix);
            if (existing is not null) continue;
            using var created = Registry.CurrentUser.CreateSubKey(prefix, true);
            undo.Push(() =>
            {
                using (var key = Registry.CurrentUser.OpenSubKey(prefix))
                {
                    if (key is null) return;
                    if (key.SubKeyCount != 0 || key.ValueCount != 0) throw new IOException($"New key {prefix} is no longer empty; it was retained.");
                }
                Registry.CurrentUser.DeleteSubKey(prefix, false);
            });
        }
    }

    private static void ValidateValueName(string name)
    {
        if (name is null || name.Length > 16383 || name.Contains('\0')) throw new InvalidDataException("Invalid registry value name.");
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

        if (normalized.Length > 4096 || normalized.Contains('\0') || normalized.Split('\\').Any(s => s.Length is 0 or > 255 || s is "." or ".."))
            throw new InvalidDataException("Invalid registry path.");
        return normalized;
    }

    private static RegistryValueKind ParseKind(string kind)
    {
        return Enum.TryParse<RegistryValueKind>(kind, ignoreCase: true, out var parsed) &&
            parsed is RegistryValueKind.String or RegistryValueKind.ExpandString or RegistryValueKind.MultiString or RegistryValueKind.Binary or RegistryValueKind.DWord or RegistryValueKind.QWord or RegistryValueKind.None
            ? parsed : throw new InvalidDataException($"Unsupported registry value type: {kind}.");
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
                : data.TrimStart().StartsWith('[') ? JsonSerializer.Deserialize<string[]>(data) ?? throw new InvalidDataException("Invalid multi-string JSON array.") : data.Split('|'),

            RegistryValueKind.Binary or RegistryValueKind.None => string.IsNullOrEmpty(data)
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
            RegistryValueKind.MultiString when value is string[] values => JsonSerializer.Serialize(values),
            RegistryValueKind.Binary or RegistryValueKind.None when value is byte[] bytes => Convert.ToBase64String(bytes),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }
}
