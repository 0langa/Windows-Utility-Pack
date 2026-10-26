using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>
/// Unit tests for <see cref="NavigationService"/>.
/// These tests verify the core navigation contract without requiring WPF runtime
/// (no UI thread, no Application object needed).
/// </summary>
public class NavigationServiceTests
{
    // Minimal ViewModelBase subclasses used as navigation targets.
    private class TestVm  : ViewModelBase { }
    private class OtherVm : ViewModelBase { }
    private sealed class GuardVm : ViewModelBase, INavigationGuard { public bool CanNavigateAway() => false; }
    private sealed class LiveVm : ViewModelBase, IViewLifetime, IDisposable
    {
        public bool Active { get; private set; }
        public bool Disposed { get; private set; }
        public void Activate() => Active = true;
        public void Deactivate() => Active = false;
        public void Dispose() { Active = false; Disposed = true; }
    }

    [Fact]
    public void BackNavigation_RestoresRegisteredIdentityAndViewLifetime()
    {
        using var svc = new NavigationService();
        var first = new LiveVm();
        var second = new LiveVm();
        svc.Register("first-key", () => first);
        svc.Register("second-key", () => second);
        svc.NavigateTo("first-key");
        Assert.True(first.Active);
        svc.NavigateTo("second-key");
        Assert.False(first.Active);
        Assert.Equal("second-key", svc.CurrentToolKey);
        svc.GoBack();
        Assert.True(first.Active);
        Assert.True(second.Disposed);
        Assert.Equal("first-key", svc.CurrentToolKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NavigationVeto_DoesNotConstructTarget(bool generic)
    {
        using var svc = new NavigationService();
        svc.NavigateTo(new GuardVm());
        var constructed = 0;
        svc.Register("target", () => { constructed++; return new OtherVm(); });
        svc.Register<OtherVm>(() => { constructed++; return new OtherVm(); });
        if (generic) svc.Navigate<OtherVm>(); else svc.NavigateTo("target");
        Assert.Equal(0, constructed);
        Assert.False(svc.CanGoBack);
    }

    [Fact]
    public void InvalidNavigation_DoesNotAlterHistory()
    {
        using var svc = new NavigationService();
        var original = new TestVm();
        svc.NavigateTo(original);
        svc.NavigateTo(new object());
        Assert.Same(original, svc.CurrentView);
        Assert.False(svc.CanGoBack);
    }

    [Fact]
    public void GoBack_RaisesNavigationEventForRestoredView()
    {
        using var svc = new NavigationService();
        svc.NavigateTo(new TestVm());
        svc.NavigateTo(new OtherVm());
        Type? raised = null;
        svc.Navigated += (_, type) => raised = type;
        svc.GoBack();
        Assert.Equal(typeof(TestVm), raised);
    }

    [Fact]
    public void NavigateTo_String_SetsCurrentView()
    {
        var svc = new NavigationService();
        svc.Register("test", () => new TestVm());
        svc.NavigateTo("test");
        Assert.IsType<TestVm>(svc.CurrentView);
    }

    [Fact]
    public void NavigateTo_Generic_SetsCurrentView()
    {
        var svc = new NavigationService();
        svc.Register<TestVm>(() => new TestVm());
        svc.NavigateTo<TestVm>();
        Assert.IsType<TestVm>(svc.CurrentView);
    }

    [Fact]
    public void NavigateTo_RaisesNavigatedEvent()
    {
        var svc = new NavigationService();
        svc.Register("test", () => new TestVm());
        Type? raised = null;
        svc.Navigated += (_, vmType) => raised = vmType;
        svc.NavigateTo("test");
        Assert.Equal(typeof(TestVm), raised);
    }

    [Fact]
    public void NavigateTo_UnknownKey_DoesNotChangeView()
    {
        // Unknown keys should be silently ignored (not throw or clear current view).
        var svc = new NavigationService();
        svc.Register("test", () => new TestVm());
        svc.NavigateTo("test");
        svc.NavigateTo("unknown");
        Assert.IsType<TestVm>(svc.CurrentView);
    }
}
