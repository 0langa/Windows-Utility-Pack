namespace WindowsUtilityPack.Services;

/// <summary>
/// Defines the theme values available in the application.
/// </summary>
public enum AppTheme
{
    /// <summary>Dark colour scheme (default).</summary>
    Dark,
    /// <summary>Light colour scheme.</summary>
    Light,
    /// <summary>Follow the Windows system setting automatically.</summary>
    System
}

/// <summary>
/// Contract for the application theme management service.
/// Implementations swap ResourceDictionary entries at runtime to change the visual theme.
/// </summary>
public interface IThemeService
{
    /// <summary>Gets the user-chosen preference (may be <see cref="AppTheme.System"/>).</summary>
    AppTheme CurrentTheme { get; }

    /// <summary>Gets the resolved theme actually displayed (always Dark or Light).</summary>
    AppTheme EffectiveTheme { get; }

    /// <summary>
    /// Switches the application theme. When <see cref="AppTheme.System"/> is passed the
    /// service auto-detects the OS preference and follows future changes.
    /// </summary>
    void SetTheme(AppTheme theme);

    /// <summary>Raised whenever the effective theme changes.</summary>
    event EventHandler<AppTheme>? ThemeChanged;
}
