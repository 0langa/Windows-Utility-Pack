using System.IO;
using System.Text.Json;

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
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsUtilityPack", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <inheritdoc/>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    return settings;
                }

                throw new JsonException("Settings file deserialized to null.");
            }
        }
        catch (Exception ex)
        {
            PreserveCorruptSettingsCopy();
            // Log through the static accessor if the logging service is already initialised.
            try { App.LoggingService?.LogError("Failed to load settings", ex); } catch { }
        }
        return new AppSettings();
    }

    /// <inheritdoc/>
    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex)
        {
            try { App.LoggingService?.LogError("Failed to save settings", ex); } catch { }
        }
    }

    private static void PreserveCorruptSettingsCopy()
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
                $"{fileName}.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}{extension}");

            if (!File.Exists(backupPath))
            {
                File.Copy(SettingsPath, backupPath);
            }
        }
        catch (Exception ex)
        {
            try { App.LoggingService?.LogError("Failed to preserve corrupt settings copy", ex); } catch { }
        }
    }
}
