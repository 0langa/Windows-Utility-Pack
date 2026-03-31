using System.Windows;
using Microsoft.Win32;

namespace WindowsUtilityPack.Services;

/// <summary>
/// Manages the application theme by swapping ResourceDictionary entries at runtime.
///
/// Supports three modes via <see cref="AppTheme"/>:
///   <list type="bullet">
///     <item><b>Dark / Light</b> — explicit lock.</item>
///     <item><b>System</b> — reads the Windows <c>AppsUseLightTheme</c> registry key
///           and follows OS changes via <see cref="SystemEvents.UserPreferenceChanged"/>.</item>
///   </list>
/// </summary>
public class ThemeService : IThemeService
{
    private const string DarkThemeUri  = "Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Themes/LightTheme.xaml";

    /// <inheritdoc/>
    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    /// <inheritdoc/>
    public AppTheme EffectiveTheme { get; private set; } = AppTheme.Dark;

    /// <inheritdoc/>
    public event EventHandler<AppTheme>? ThemeChanged;

    /// <inheritdoc/>
    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        var resolved = Resolve(theme);

        if (EffectiveTheme == resolved)
            return;

        EffectiveTheme = resolved;
        ApplyTheme(resolved);
        ThemeChanged?.Invoke(this, resolved);

        // Wire / unwire OS change listener.
        SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
        if (theme == AppTheme.System)
            SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
    }

    /// <summary>Resolves <see cref="AppTheme.System"/> to Dark or Light.</summary>
    private static AppTheme Resolve(AppTheme theme) => theme switch
    {
        AppTheme.System => DetectSystemTheme(),
        _ => theme
    };

    /// <summary>Reads the Windows registry to determine the current OS theme.</summary>
    private static AppTheme DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 1 ? AppTheme.Light : AppTheme.Dark;
        }
        catch
        {
            return AppTheme.Dark;
        }
    }

    /// <summary>Reacts to Windows theme changes when following the system setting.</summary>
    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;

        var resolved = DetectSystemTheme();
        if (EffectiveTheme == resolved)
            return;

        EffectiveTheme = resolved;

        Application.Current.Dispatcher.Invoke(() =>
        {
            ApplyTheme(resolved);
            ThemeChanged?.Invoke(this, resolved);
        });
    }

    /// <summary>Swaps the theme ResourceDictionary at position 0.</summary>
    private static void ApplyTheme(AppTheme theme)
    {
        var mergedDicts = Application.Current.Resources.MergedDictionaries;
        var themeUri = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri;

        var existing = mergedDicts
            .FirstOrDefault(d => d.Source != null &&
                                 (d.Source.OriginalString.Contains("DarkTheme") ||
                                  d.Source.OriginalString.Contains("LightTheme")));
        if (existing != null)
            mergedDicts.Remove(existing);

        var newDict = new ResourceDictionary
        {
            Source = new Uri(themeUri, UriKind.Relative)
        };
        mergedDicts.Insert(0, newDict);
    }
}
