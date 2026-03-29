namespace WindowsUtilityPack.Services;

public class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 700;
}

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
