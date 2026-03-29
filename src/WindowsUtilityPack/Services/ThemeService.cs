using System.Windows;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Manages the application theme by swapping ResourceDictionary entries at runtime.
///
/// How it works:
///   1. <c>App.xaml</c> loads <c>DarkTheme.xaml</c> as the initial merged dictionary.
///   2. When <see cref="SetTheme"/> is called, the existing dark/light dictionary is
///      located by URL substring match, removed, and replaced with the requested one.
///   3. Because all colours are defined as <c>DynamicResource</c> brushes, WPF
///      automatically re-renders every bound element when the dictionary changes.
/// </summary>
public class ThemeService : IThemeService
{
    // Relative URIs for the two theme resource dictionaries.
    private const string DarkThemeUri  = "Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Themes/LightTheme.xaml";

    /// <inheritdoc/>
    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    /// <inheritdoc/>
    public event EventHandler<AppTheme>? ThemeChanged;

    /// <inheritdoc/>
    public void SetTheme(AppTheme theme)
    {
        // Avoid unnecessary dictionary churn when the theme is already active.
        if (CurrentTheme == theme)
            return;

        CurrentTheme = theme;
        ApplyTheme(theme);
        ThemeChanged?.Invoke(this, theme);
    }

    /// <summary>
    /// Removes the currently loaded theme dictionary and inserts the requested one.
    /// Inserting at position 0 ensures theme brushes are defined before style-level
    /// overrides (Styles.xaml is loaded at position 1).
    /// </summary>
    private static void ApplyTheme(AppTheme theme)
    {
        var mergedDicts = Application.Current.Resources.MergedDictionaries;
        var themeUri = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri;

        // Remove the existing theme dictionary (identified by its Source URI).
        var existing = mergedDicts
            .FirstOrDefault(d => d.Source != null &&
                                 (d.Source.OriginalString.Contains("DarkTheme") ||
                                  d.Source.OriginalString.Contains("LightTheme")));
        if (existing != null)
            mergedDicts.Remove(existing);

        // Insert the new theme at position 0 so it does not override specific styles.
        var newDict = new ResourceDictionary
        {
            Source = new Uri(themeUri, UriKind.Relative)
        };
        mergedDicts.Insert(0, newDict);
    }
}
