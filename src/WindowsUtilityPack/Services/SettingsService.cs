using System.IO;
using System.Text.Json;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Reads and writes <see cref="AppSettings"/> as indented JSON.
/// The settings file is created automatically on first save.
/// All errors are silently swallowed so the application never crashes
/// due to a missing or corrupt settings file.
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
        catch { /* fall through to defaults */ }
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
        catch { /* swallow save errors */ }
    }
}
