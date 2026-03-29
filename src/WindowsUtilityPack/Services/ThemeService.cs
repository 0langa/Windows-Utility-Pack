using System.Windows;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Manages the application theme by swapping ResourceDictionary entries at runtime.
/// </summary>
public class ThemeService : IThemeService
{
    private const string DarkThemeUri  = "Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Themes/LightTheme.xaml";

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public event EventHandler<AppTheme>? ThemeChanged;

    public void SetTheme(AppTheme theme)
    {
        if (CurrentTheme == theme)
            return;

        CurrentTheme = theme;
        ApplyTheme(theme);
        ThemeChanged?.Invoke(this, theme);
    }

    private static void ApplyTheme(AppTheme theme)
    {
        var mergedDicts = Application.Current.Resources.MergedDictionaries;
        var themeUri = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri;

        // Remove the existing theme dictionary
        var existing = mergedDicts
            .FirstOrDefault(d => d.Source != null &&
                                 (d.Source.OriginalString.Contains("DarkTheme") ||
                                  d.Source.OriginalString.Contains("LightTheme")));
        if (existing != null)
            mergedDicts.Remove(existing);

        // Insert the new theme at position 0 so it doesn't override specific styles
        var newDict = new ResourceDictionary
        {
            Source = new Uri(themeUri, UriKind.Relative)
        };
        mergedDicts.Insert(0, newDict);
    }
}
