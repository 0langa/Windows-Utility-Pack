using WindowsUtilityPack.Services;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>
/// Tests for <see cref="ITrayService"/> contract behaviour using a stub implementation.
/// The real <see cref="TrayService"/> wraps WinForms NotifyIcon, which requires a
/// UI thread and desktop session, so tests exercise the interface contract via a stub.
/// </summary>
public sealed class TrayServiceTests
{
    // ── Event routing ─────────────────────────────────────────────────────────

    [Fact]
    public void ShowRequested_IsRaised_WhenStubSignalsSomeOpenRequest()
    {
        var stub = new StubTrayService();
        var raised = false;
        stub.ShowRequested += (_, _) => raised = true;

        stub.SimulateShowRequest();

        Assert.True(raised);
    }

    [Fact]
    public void ExitRequested_IsRaised_WhenStubSignalsExit()
    {
        var stub = new StubTrayService();
        var raised = false;
        stub.ExitRequested += (_, _) => raised = true;

        stub.SimulateExitRequest();

        Assert.True(raised);
    }

    [Fact]
    public void QuickActionRequested_CarriesToolKey()
    {
        var stub = new StubTrayService();
        string? receivedKey = null;
        stub.QuickActionRequested += (_, key) => receivedKey = key;

        stub.SimulateQuickAction("storage-master");

        Assert.Equal("storage-master", receivedKey);
    }

    // ── Initialization ────────────────────────────────────────────────────────

    [Fact]
    public void Initialize_SetsIsVisibleTrue()
    {
        var stub = new StubTrayService();
        Assert.False(stub.IsVisible);

        stub.Initialize();

        Assert.True(stub.IsVisible);
    }

    [Fact]
    public void Initialize_WithQuickActions_StoresActions()
    {
        var stub = new StubTrayService();
        var actions = new[]
        {
            new TrayQuickAction { Key = "ping-tool",    Label = "Ping Tool" },
            new TrayQuickAction { Key = "dns-lookup",   Label = "DNS Lookup" },
        };

        stub.Initialize(actions);

        Assert.Equal(2, stub.QuickActions.Count);
        Assert.Contains(stub.QuickActions, a => a.Key == "ping-tool");
    }

    [Fact]
    public void Initialize_CalledTwice_UpdatesQuickActions()
    {
        var stub = new StubTrayService();
        stub.Initialize([new TrayQuickAction { Key = "tool-a", Label = "Tool A" }]);

        stub.Initialize([new TrayQuickAction { Key = "tool-b", Label = "Tool B" }]);

        Assert.Single(stub.QuickActions);
        Assert.Equal("tool-b", stub.QuickActions[0].Key);
    }

    // ── Quick-action management ───────────────────────────────────────────────

    [Fact]
    public void UpdateQuickActions_BeforeInit_IsNoOp()
    {
        var stub = new StubTrayService();

        // Should not throw.
        stub.UpdateQuickActions([new TrayQuickAction { Key = "x", Label = "X" }]);

        Assert.Empty(stub.QuickActions);
    }

    [Fact]
    public void UpdateQuickActions_AfterInit_ReplacesExistingItems()
    {
        var stub = new StubTrayService();
        stub.Initialize([new TrayQuickAction { Key = "old", Label = "Old" }]);

        stub.UpdateQuickActions(
        [
            new TrayQuickAction { Key = "new-1", Label = "New 1" },
            new TrayQuickAction { Key = "new-2", Label = "New 2" },
        ]);

        Assert.Equal(2, stub.QuickActions.Count);
        Assert.DoesNotContain(stub.QuickActions, a => a.Key == "old");
    }

    [Fact]
    public void UpdateQuickActions_WithEmptyList_ClearsItems()
    {
        var stub = new StubTrayService();
        stub.Initialize([new TrayQuickAction { Key = "x", Label = "X" }]);

        stub.UpdateQuickActions([]);

        Assert.Empty(stub.QuickActions);
    }

    // ── Show / Hide ───────────────────────────────────────────────────────────

    [Fact]
    public void Show_MakesIconVisible()
    {
        var stub = new StubTrayService();
        stub.Initialize();
        stub.Hide();
        Assert.False(stub.IsVisible);

        stub.Show();

        Assert.True(stub.IsVisible);
    }

    [Fact]
    public void Hide_MakesIconInvisible()
    {
        var stub = new StubTrayService();
        stub.Initialize();
        Assert.True(stub.IsVisible);

        stub.Hide();

        Assert.False(stub.IsVisible);
    }

    // ── Balloon notifications ─────────────────────────────────────────────────

    [Fact]
    public void ShowBalloon_RecordsTitleMessageAndIcon()
    {
        var stub = new StubTrayService();
        stub.Initialize();

        stub.ShowBalloon("Test Title", "Test message", TrayBalloonIcon.Warning);

        Assert.Single(stub.Balloons);
        var balloon = stub.Balloons[0];
        Assert.Equal("Test Title", balloon.Title);
        Assert.Equal("Test message", balloon.Message);
        Assert.Equal(TrayBalloonIcon.Warning, balloon.Icon);
    }

    [Fact]
    public void ShowBalloon_DefaultsToInfoIcon()
    {
        var stub = new StubTrayService();
        stub.Initialize();

        stub.ShowBalloon("T", "M");

        Assert.Equal(TrayBalloonIcon.Info, stub.Balloons[0].Icon);
    }

    [Fact]
    public void ShowBalloon_BeforeInit_IsNoOp()
    {
        var stub = new StubTrayService();

        stub.ShowBalloon("T", "M");

        Assert.Empty(stub.Balloons);
    }

    // ── Disposal ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_SetsIsVisibleFalse()
    {
        var stub = new StubTrayService();
        stub.Initialize();

        stub.Dispose();

        Assert.False(stub.IsVisible);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var stub = new StubTrayService();
        stub.Initialize();
        stub.Dispose();

        var ex = Record.Exception(() => stub.Dispose());
        Assert.Null(ex);
    }

    // ── TrayQuickAction model ─────────────────────────────────────────────────

    [Fact]
    public void TrayQuickAction_RequiredProperties_CanBeSet()
    {
        var action = new TrayQuickAction { Key = "my-tool", Label = "My Tool" };

        Assert.Equal("my-tool", action.Key);
        Assert.Equal("My Tool", action.Label);
    }

    // ── Stub implementation ───────────────────────────────────────────────────

    /// <summary>
    /// Testable stub for <see cref="ITrayService"/> that records state
    /// without touching any WinForms / desktop APIs.
    /// </summary>
    private sealed class StubTrayService : ITrayService
    {
        private bool _initialized;
        private bool _visible;
        private bool _disposed;

        public event EventHandler? ShowRequested;
        public event EventHandler? ExitRequested;
        public event EventHandler<string>? QuickActionRequested;

        public bool IsVisible => _visible;

        public List<TrayQuickAction> QuickActions { get; private set; } = [];

        public record BalloonRecord(string Title, string Message, TrayBalloonIcon Icon);
        public List<BalloonRecord> Balloons { get; } = [];

        public void Initialize(IReadOnlyList<TrayQuickAction>? quickActions = null)
        {
            if (_initialized)
            {
                // Re-entry: update quick actions only.
                if (quickActions is not null)
                {
                    QuickActions = [.. quickActions];
                }
                return;
            }

            _initialized = true;
            _visible     = true;
            QuickActions = quickActions is not null ? [.. quickActions] : [];
        }

        public void UpdateQuickActions(IReadOnlyList<TrayQuickAction> quickActions)
        {
            if (!_initialized) return;
            QuickActions = [.. quickActions];
        }

        public void ShowBalloon(string title, string message, TrayBalloonIcon icon = TrayBalloonIcon.Info)
        {
            if (!_initialized) return;
            Balloons.Add(new BalloonRecord(title, message, icon));
        }

        public void Show()
        {
            if (_initialized) _visible = true;
        }

        public void Hide()
        {
            if (_initialized) _visible = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _visible  = false;
        }

        // Test helpers to simulate tray events.
        public void SimulateShowRequest()   => ShowRequested?.Invoke(this, EventArgs.Empty);
        public void SimulateExitRequest()   => ExitRequested?.Invoke(this, EventArgs.Empty);
        public void SimulateQuickAction(string key) => QuickActionRequested?.Invoke(this, key);
    }
}
