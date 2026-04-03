using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ThemeService"/>.
///
/// Uses a <see cref="TestableThemeService"/> subclass that stubs out the
/// WPF ResourceDictionary mutation so the tests can run without a live
/// <see cref="System.Windows.Application"/> instance.
/// </summary>
public class ThemeServiceTests
{
    // ── Testable subclass ──────────────────────────────────────────────────────

    /// <summary>
    /// ThemeService variant that records ApplyTheme calls without touching WPF resources.
    /// </summary>
    private sealed class TestableThemeService : ThemeService
    {
        public List<AppTheme> AppliedThemes { get; } = [];

        protected override void ApplyTheme(AppTheme theme)
        {
            AppliedThemes.Add(theme);
        }
    }

    // ── CurrentTheme / EffectiveTheme ──────────────────────────────────────────

    [Fact]
    public void InitialState_IsDark()
    {
        var svc = new TestableThemeService();

        Assert.Equal(AppTheme.Dark, svc.CurrentTheme);
        Assert.Equal(AppTheme.Dark, svc.EffectiveTheme);
    }

    [Fact]
    public void SetTheme_Light_UpdatesCurrentAndEffectiveTheme()
    {
        var svc = new TestableThemeService();

        svc.SetTheme(AppTheme.Light);

        Assert.Equal(AppTheme.Light, svc.CurrentTheme);
        Assert.Equal(AppTheme.Light, svc.EffectiveTheme);
    }

    [Fact]
    public void SetTheme_Dark_UpdatesCurrentAndEffectiveTheme()
    {
        var svc = new TestableThemeService();
        svc.SetTheme(AppTheme.Light); // change away from default first

        svc.SetTheme(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, svc.CurrentTheme);
        Assert.Equal(AppTheme.Dark, svc.EffectiveTheme);
    }

    [Fact]
    public void SetTheme_System_SetsCurrentThemeToSystem()
    {
        var svc = new TestableThemeService();

        svc.SetTheme(AppTheme.System);

        Assert.Equal(AppTheme.System, svc.CurrentTheme);
        // EffectiveTheme is Dark or Light depending on the OS; never System.
        Assert.NotEqual(AppTheme.System, svc.EffectiveTheme);
    }

    // ── ApplyTheme call tracking ───────────────────────────────────────────────

    [Fact]
    public void SetTheme_Light_CallsApplyThemeOnce_WhenInitiallyDark()
    {
        var svc = new TestableThemeService();

        svc.SetTheme(AppTheme.Light);

        Assert.Single(svc.AppliedThemes);
        Assert.Equal(AppTheme.Light, svc.AppliedThemes[0]);
    }

    [Fact]
    public void SetTheme_SameEffectiveTheme_DoesNotCallApplyTheme()
    {
        // Both Dark → no-op because EffectiveTheme is already Dark.
        var svc = new TestableThemeService();

        svc.SetTheme(AppTheme.Dark);

        Assert.Empty(svc.AppliedThemes);
    }

    // ── ThemeChanged event ─────────────────────────────────────────────────────

    [Fact]
    public void SetTheme_RaisesThemeChanged_WhenEffectiveThemeChanges()
    {
        var svc = new TestableThemeService();
        var raised = new List<AppTheme>();
        svc.ThemeChanged += (_, t) => raised.Add(t);

        svc.SetTheme(AppTheme.Light);

        Assert.Single(raised);
        Assert.Equal(AppTheme.Light, raised[0]);
    }

    [Fact]
    public void SetTheme_DoesNotRaiseThemeChanged_WhenEffectiveThemeUnchanged()
    {
        var svc = new TestableThemeService();
        var raised = 0;
        svc.ThemeChanged += (_, _) => raised++;

        // Initial effective theme is Dark; setting Dark again is a no-op.
        svc.SetTheme(AppTheme.Dark);

        Assert.Equal(0, raised);
    }

    // ── System-mode subscription correctness (the audited bug) ────────────────

    /// <summary>
    /// Before the fix, switching to System mode while the effective theme was already
    /// identical to the OS theme would return early before wiring SystemEvents.
    /// This test verifies that setting System twice still keeps CurrentTheme as System.
    /// </summary>
    [Fact]
    public void SetTheme_System_WhenCalledTwice_KeepsCurrentThemeAsSystem()
    {
        var svc = new TestableThemeService();

        svc.SetTheme(AppTheme.System);
        svc.SetTheme(AppTheme.System); // second call — must not reset to non-System

        Assert.Equal(AppTheme.System, svc.CurrentTheme);
    }

    [Fact]
    public void SetTheme_ExplicitTheme_After_System_SetsCurrentThemeCorrectly()
    {
        var svc = new TestableThemeService();

        svc.SetTheme(AppTheme.System);
        svc.SetTheme(AppTheme.Light);

        Assert.Equal(AppTheme.Light, svc.CurrentTheme);
        Assert.Equal(AppTheme.Light, svc.EffectiveTheme);
    }

    [Fact]
    public void SetTheme_SwitchingFromSystemToExplicit_DoesNotLeaveSystemModeActive()
    {
        var svc = new TestableThemeService();
        svc.SetTheme(AppTheme.System);

        // Switch back to an explicit theme.
        svc.SetTheme(AppTheme.Dark);

        // CurrentTheme should reflect the explicit choice.
        Assert.Equal(AppTheme.Dark, svc.CurrentTheme);
        // EffectiveTheme must also be Dark.
        Assert.Equal(AppTheme.Dark, svc.EffectiveTheme);
    }
}
