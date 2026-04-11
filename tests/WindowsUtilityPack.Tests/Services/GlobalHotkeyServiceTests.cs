using System.Windows.Input;
using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>
/// Tests for <see cref="IGlobalHotkeyService"/> contract behaviour using a stub,
/// and for the internal <see cref="GlobalHotkeyService.TryParseGesture"/> helper.
/// </summary>
public sealed class GlobalHotkeyServiceTests
{
    // ── TryParseGesture (internal static) ────────────────────────────────────

    [Fact]
    public void TryParseGesture_ParsesValidGesture()
    {
        var result = GlobalHotkeyService.TryParseGesture("Ctrl+Shift+S", out var key, out var modifiers);

        Assert.True(result);
        Assert.Equal(Key.S, key);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, modifiers);
    }

    [Fact]
    public void TryParseGesture_RejectsInvalidGesture()
    {
        var result = GlobalHotkeyService.TryParseGesture("not-a-gesture", out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryParseGesture_ParsesCtrlK()
    {
        var result = GlobalHotkeyService.TryParseGesture("Ctrl+K", out var key, out var modifiers);

        Assert.True(result);
        Assert.Equal(Key.K, key);
        Assert.Equal(ModifierKeys.Control, modifiers);
    }

    // ── IGlobalHotkeyService contract via stub ────────────────────────────────

    [Fact]
    public void IsStarted_FalseInitially()
    {
        var svc = new StubGlobalHotkeyService();
        Assert.False(svc.IsStarted);
    }

    [Fact]
    public void Start_SetsIsStartedTrue()
    {
        var svc = new StubGlobalHotkeyService();
        svc.Start();
        Assert.True(svc.IsStarted);
    }

    [Fact]
    public void Stop_SetsIsStartedFalse()
    {
        var svc = new StubGlobalHotkeyService();
        svc.Start();
        svc.Stop();
        Assert.False(svc.IsStarted);
    }

    [Fact]
    public void ActiveRegistrations_EmptyInitially()
    {
        var svc = new StubGlobalHotkeyService();
        Assert.Empty(svc.ActiveRegistrations);
    }

    [Fact]
    public void RegistrationIssues_EmptyInitially()
    {
        var svc = new StubGlobalHotkeyService();
        Assert.Empty(svc.RegistrationIssues);
    }

    [Fact]
    public void HotkeyPressed_FiredWithCorrectAction_WhenSimulated()
    {
        var svc = new StubGlobalHotkeyService();
        svc.Start();

        ShellHotkeyAction? received = null;
        svc.HotkeyPressed += (_, action) => received = action;

        svc.SimulateHotkey(ShellHotkeyAction.OpenCommandPalette);

        Assert.NotNull(received);
        Assert.Equal(ShellHotkeyAction.OpenCommandPalette, received!.Value);
    }

    [Fact]
    public void HotkeyPressed_NotFired_WhenNotStarted()
    {
        var svc = new StubGlobalHotkeyService();
        var fired = false;
        svc.HotkeyPressed += (_, _) => fired = true;

        svc.SimulateHotkey(ShellHotkeyAction.OpenCommandPalette);

        Assert.False(fired);
    }

    [Fact]
    public void RegistrationsChanged_FiredOnRefresh()
    {
        var svc = new StubGlobalHotkeyService();
        var changed = false;
        svc.RegistrationsChanged += (_, _) => changed = true;

        svc.Refresh();

        Assert.True(changed);
    }

    [Fact]
    public void Refresh_PopulatesActiveRegistrations()
    {
        var svc = new StubGlobalHotkeyService();
        svc.AddRegistration(new GlobalHotkeyRegistration
        {
            Id       = 1,
            Action   = ShellHotkeyAction.OpenCommandPalette,
            Key      = Key.K,
            Modifiers = ModifierKeys.Control,
        });

        svc.Refresh();

        Assert.Single(svc.ActiveRegistrations);
        Assert.Equal(ShellHotkeyAction.OpenCommandPalette, svc.ActiveRegistrations[0].Action);
    }

    [Fact]
    public void Dispose_SetsIsStartedFalse()
    {
        var svc = new StubGlobalHotkeyService();
        svc.Start();
        svc.Dispose();
        Assert.False(svc.IsStarted);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var svc = new StubGlobalHotkeyService();
        svc.Dispose();
        var ex = Record.Exception(() => svc.Dispose());
        Assert.Null(ex);
    }

    // ── GlobalHotkeyRegistration model ───────────────────────────────────────

    [Fact]
    public void GlobalHotkeyRegistration_DisplayGesture_FormatsCorrectly()
    {
        var reg = new GlobalHotkeyRegistration
        {
            Id        = 1,
            Action    = ShellHotkeyAction.OpenCommandPalette,
            Key       = Key.K,
            Modifiers = ModifierKeys.Control,
        };

        Assert.Contains("K", reg.DisplayGesture);
        Assert.Contains("Control", reg.DisplayGesture);
    }

    [Fact]
    public void HotkeyRegistrationIssue_StoresProperties()
    {
        var issue = new HotkeyRegistrationIssue
        {
            Action  = "OpenCommandPalette",
            Gesture = "Ctrl+K",
            Message = "Already registered",
        };

        Assert.Equal("OpenCommandPalette", issue.Action);
        Assert.Equal("Ctrl+K", issue.Gesture);
        Assert.Equal("Already registered", issue.Message);
    }

    // ── Stub implementation ───────────────────────────────────────────────────

    private sealed class StubGlobalHotkeyService : IGlobalHotkeyService
    {
        private bool _started;
        private bool _disposed;
        private readonly List<GlobalHotkeyRegistration> _registrations = [];
        private readonly List<HotkeyRegistrationIssue>  _issues        = [];

        public event EventHandler<ShellHotkeyAction>? HotkeyPressed;
        public event EventHandler? RegistrationsChanged;

        public bool IsStarted => _started;
        public IReadOnlyList<GlobalHotkeyRegistration> ActiveRegistrations => _registrations;
        public IReadOnlyList<HotkeyRegistrationIssue>  RegistrationIssues  => _issues;

        public void Start()  { _started = true; }
        public void Stop()   { _started = false; }
        public void Refresh() => RegistrationsChanged?.Invoke(this, EventArgs.Empty);

        public void AddRegistration(GlobalHotkeyRegistration reg) => _registrations.Add(reg);
        public void AddIssue(HotkeyRegistrationIssue issue)       => _issues.Add(issue);

        /// <summary>Simulates a hotkey press — only fires when started.</summary>
        public void SimulateHotkey(ShellHotkeyAction action)
        {
            if (_started)
                HotkeyPressed?.Invoke(this, action);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _started  = false;
        }
    }
}
