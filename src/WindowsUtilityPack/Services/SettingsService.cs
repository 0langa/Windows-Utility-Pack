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
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
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
}
