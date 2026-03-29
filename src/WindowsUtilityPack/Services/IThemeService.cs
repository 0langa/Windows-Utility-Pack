namespace WindowsUtilityPack.Services;

/// <summary>
/// Defines the theme values available in the application.
/// </summary>
public enum AppTheme
{
    Dark,
    Light
}

/// <summary>
/// Contract for the application theme management service.
/// </summary>
public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    void SetTheme(AppTheme theme);
    event EventHandler<AppTheme>? ThemeChanged;
}
