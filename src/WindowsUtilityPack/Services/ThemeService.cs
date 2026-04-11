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
public class ThemeService : IThemeService, IDisposable
{
    private const string DarkThemeUri  = "Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Themes/LightTheme.xaml";
    private const string AuroraThemeUri = "Themes/AuroraTheme.xaml";

    /// <inheritdoc/>
    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    /// <inheritdoc/>
    public AppTheme EffectiveTheme { get; private set; } = AppTheme.Dark;

    /// <inheritdoc/>
    public event EventHandler<AppTheme>? ThemeChanged;
    private bool _disposed;

    /// <inheritdoc/>
    public void SetTheme(AppTheme theme)
    {
        ThrowIfDisposed();
        CurrentTheme = theme;

        // Always update OS-event subscription before any early-exit so that switching
        // to System mode while the effective theme already matches the OS theme still
        // correctly follows future OS theme changes.
        SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
        if (theme == AppTheme.System)
            SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;

        var resolved = Resolve(theme);

        if (EffectiveTheme == resolved)
            return;

        EffectiveTheme = resolved;
        ApplyTheme(resolved);
        ThemeChanged?.Invoke(this, resolved);
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
        if (_disposed)
        {
            return;
        }

        if (e.Category != UserPreferenceCategory.General)
            return;

        var resolved = DetectSystemTheme();
        if (EffectiveTheme == resolved)
            return;

        EffectiveTheme = resolved;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        dispatcher.Invoke(() =>
        {
            ApplyTheme(resolved);
            ThemeChanged?.Invoke(this, resolved);
        });
    }

    /// <summary>Swaps the active theme ResourceDictionary.</summary>
    /// <remarks>
    /// Declared <c>protected virtual</c> so that test subclasses can override it
    /// without requiring a live WPF Application instance.
    /// </remarks>
    protected virtual void ApplyTheme(AppTheme theme)
    {
        ThrowIfDisposed();
        var mergedDicts = Application.Current.Resources.MergedDictionaries;
        var themeUri = theme switch
        {
            AppTheme.Light => LightThemeUri,
            AppTheme.Aurora => AuroraThemeUri,
            _ => DarkThemeUri,
        };

        var existing = mergedDicts
            .FirstOrDefault(d => d.Source != null &&
                                 (d.Source.OriginalString.Contains("DarkTheme") ||
                                  d.Source.OriginalString.Contains("LightTheme") ||
                                  d.Source.OriginalString.Contains("AuroraTheme")));
        if (existing != null)
            mergedDicts.Remove(existing);

        var newDict = new ResourceDictionary
        {
            Source = new Uri(themeUri, UriKind.Relative)
        };
        mergedDicts.Insert(0, newDict);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
