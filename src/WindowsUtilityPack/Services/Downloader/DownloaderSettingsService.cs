using System.IO;

namespace WindowsUtilityPack.Services.Downloader;

/// <summary>Loads and saves downloader settings through the shared application settings store.</summary>
public sealed class DownloaderSettingsService : IDownloaderSettingsService
{
    private readonly ISettingsService _settingsService;

    public DownloaderSettingsService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public DownloaderSettings Load()
    {
        var settings = _settingsService.Load();
        settings.DownloaderSettings ??= new DownloaderSettings();

        if (string.IsNullOrWhiteSpace(settings.DownloaderSettings.General.DefaultDownloadFolder))
        {
            settings.DownloaderSettings.General.DefaultDownloadFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");
        }

        return settings.DownloaderSettings;
    }

    public void Save(DownloaderSettings settings)
    {
        var appSettings = _settingsService.Load();
        appSettings.DownloaderSettings = settings ?? new DownloaderSettings();
        _settingsService.Save(appSettings);
    }
}
