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
