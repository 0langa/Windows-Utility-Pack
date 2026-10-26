using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Reads and writes <see cref="AppSettings"/> as indented JSON.
/// The settings file is created automatically on first save.
/// Load/Save failures are logged via <see cref="ILoggingService"/> (when available)
/// rather than silently swallowed, but never crash the application.
/// </summary>
public class SettingsService : ISettingsService
{
    // Full path: %LOCALAPPDATA%\WindowsUtilityPack\settings.json
    private readonly string SettingsPath;
    private static readonly ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConditionalWeakTable<AppSettings, JsonObject> _baselines = new();
    private object Gate => Gates.GetOrAdd(SettingsPath, _ => new object());

    public SettingsService(string? settingsPath = null)
    {
        SettingsPath = Path.GetFullPath(settingsPath ?? Path.Combine(AppDataPaths.LocalDirectory, "settings.json"));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <inheritdoc/>
    public AppSettings Load()
    {
        lock (Gate)
        {
            AppSettings settings;
            try
            {
                var root = ReadPlainRoot();
                settings = root.Deserialize<AppSettings>(JsonOptions) ?? throw new JsonException("Settings cannot be null.");
                // Migrate legacy plaintext requests in place; no plaintext backup is created.
                if (File.Exists(SettingsPath) && string.IsNullOrEmpty(settings.ProtectedSensitiveSettings) && HasSensitiveValues(root))
                    AtomicFile.WriteAllText(SettingsPath, Protect(root).ToJsonString(JsonOptions));
            }
            catch (Exception ex)
            {
                PreserveCorruptSettingsCopy(quarantine: ex is JsonException);
                SafeLogError("Failed to load settings", ex);
                ReportFailure($"Settings could not be loaded: {ex.Message}");
                settings = new AppSettings();
            }
            _baselines.Add(settings, Serialize(settings));
            return settings;
        }
    }

    public void Save(AppSettings settings)
    {
        if (!TrySave(settings, out var error)) ReportFailure($"Settings were not saved: {error}");
    }

    public bool TrySave(AppSettings settings, out string? error)
    {
        lock (Gate)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(settings);
                var edited = Serialize(settings);
                var merged = _baselines.TryGetValue(settings, out var baseline)
                    ? MergeChanges(ReadPlainRoot(), baseline, edited) : edited.DeepClone().AsObject();
                AtomicFile.WriteAllText(SettingsPath, Protect(merged).ToJsonString(JsonOptions));
                _baselines.AddOrUpdate(settings, edited);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                SafeLogError("Failed to save settings", ex);
                error = ex.Message;
                return false;
            }
        }
    }

    private static JsonObject Serialize(AppSettings settings) => JsonSerializer.SerializeToNode(settings, JsonOptions)!.AsObject();
    private JsonObject ReadPlainRoot()
    {
        if (!File.Exists(SettingsPath)) return Serialize(new AppSettings());
        if (new FileInfo(SettingsPath).Length > 16 * 1024 * 1024) throw new InvalidDataException("Settings file exceeds 16 MiB.");
        var root = JsonNode.Parse(File.ReadAllText(SettingsPath))?.AsObject() ?? throw new JsonException("Settings cannot be null.");
        var protectedText = root[nameof(AppSettings.ProtectedSensitiveSettings)]?.GetValue<string>();
        if (!string.IsNullOrEmpty(protectedText))
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedText), null, DataProtectionScope.CurrentUser);
            try
            {
                var sensitive = JsonNode.Parse(bytes)!.AsObject();
                foreach (var name in SensitiveFields) root[name] = sensitive[name]?.DeepClone();
                if (root["DownloaderSettings"]?["Connections"] is JsonObject connections)
                    connections["CustomHeaders"] = sensitive["DownloaderHeaders"]?.DeepClone();
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        return root;
    }

    private static readonly string[] SensitiveFields =
    [nameof(AppSettings.PentestLastRequestUrl), nameof(AppSettings.PentestLastRequestHeaders),
     nameof(AppSettings.PentestLastRequestBody), nameof(AppSettings.PentestLastQueryParameters), nameof(AppSettings.PentestLastTargetUrl)];

    private static bool HasSensitiveValues(JsonObject root) => SensitiveFields.Any(name => !string.IsNullOrEmpty(root[name]?.GetValue<string>())) ||
        !string.IsNullOrEmpty(root["DownloaderSettings"]?["Connections"]?["CustomHeaders"]?.GetValue<string>());

    private static JsonObject Protect(JsonObject plain)
    {
        var root = plain.DeepClone().AsObject();
        var sensitive = new JsonObject();
        foreach (var name in SensitiveFields)
        {
            sensitive[name] = root[name]?.DeepClone();
            root[name] = "";
        }
        if (root["DownloaderSettings"]?["Connections"] is JsonObject connections)
        {
            sensitive["DownloaderHeaders"] = connections["CustomHeaders"]?.DeepClone();
            connections["CustomHeaders"] = "";
        }
        var bytes = Encoding.UTF8.GetBytes(sensitive.ToJsonString());
        try { root[nameof(AppSettings.ProtectedSensitiveSettings)] = Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        return root;
    }

    private static JsonObject MergeChanges(JsonObject latest, JsonObject baseline, JsonObject edited)
    {
        foreach (var (name, value) in edited)
        {
            baseline.TryGetPropertyValue(name, out var before);
            if (JsonNode.DeepEquals(before, value)) continue;
            if (before is JsonObject oldObject && value is JsonObject newObject && latest[name] is JsonObject latestObject)
                MergeChanges(latestObject, oldObject, newObject);
            else latest[name] = value?.DeepClone();
        }
        foreach (var name in baseline.Select(pair => pair.Key).Except(edited.Select(pair => pair.Key))) latest.Remove(name);
        return latest;
    }

    private static void ReportFailure(string message)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.HasShutdownStarted)
            _ = dispatcher.InvokeAsync(() => App.NotificationService?.ShowError(message));
    }
    private void PreserveCorruptSettingsCopy(bool quarantine)
    {
        if (!File.Exists(SettingsPath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            var fileName = Path.GetFileNameWithoutExtension(SettingsPath);
            var extension = Path.GetExtension(SettingsPath);
            var backupPath = Path.Combine(
                directory,
                $"{fileName}.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{extension}");

            if (!File.Exists(backupPath))
            {
                if (quarantine) File.Move(SettingsPath, backupPath);
                else File.Copy(SettingsPath, backupPath);
            }
        }
        catch (Exception ex)
        {
            SafeLogError("Failed to preserve corrupt settings copy", ex);
        }
    }

    private static void SafeLogError(string message, Exception ex)
    {
        try
        {
            App.TryGetLoggingService()?.LogError(message, ex);
        }
        catch
        {
            // Settings persistence should never crash startup/shutdown.
        }
    }
}
