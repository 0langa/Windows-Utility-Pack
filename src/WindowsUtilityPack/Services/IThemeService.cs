namespace WindowsUtilityPack.Services;

/// <summary>
/// Defines the theme values available in the application.
/// </summary>
public enum AppTheme
{
    /// <summary>Dark navy/blue colour scheme (default).</summary>
    Dark,
    /// <summary>Light grey/white colour scheme.</summary>
    Light
}

/// <summary>
/// Contract for the application theme management service.
/// Implementations swap ResourceDictionary entries at runtime to change the visual theme.
/// </summary>
public interface IThemeService
{
    /// <summary>Gets the currently active theme.</summary>
    AppTheme CurrentTheme { get; }

    /// <summary>
    /// Switches the application theme.  No-ops if the requested theme is already active.
    /// </summary>
    void SetTheme(AppTheme theme);

    /// <summary>Raised whenever the active theme changes.</summary>
    event EventHandler<AppTheme>? ThemeChanged;
}
