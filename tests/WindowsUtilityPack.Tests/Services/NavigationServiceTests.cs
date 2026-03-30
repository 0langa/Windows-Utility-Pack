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
        svc.NavigateTo<TestVm>();
        Assert.IsType<TestVm>(svc.CurrentView);
    }

    [Fact]
    public void NavigateTo_RaisesNavigatedEvent()
    {
        var svc = new NavigationService();
        svc.Register("test", () => new TestVm());
        ViewModelBase? raised = null;
        svc.Navigated += (_, vm) => raised = vm;
        svc.NavigateTo("test");
        Assert.IsType<TestVm>(raised);
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

    [Fact]
    public void NavigateTo_String_SetsCurrentKey()
    {
        var svc = new NavigationService();
        svc.Register("test", () => new TestVm());
        svc.NavigateTo("test");
        Assert.Equal("test", svc.CurrentKey);
    }

    [Fact]
    public void NavigateTo_UnknownKey_DoesNotChangeCurrentKey()
    {
        var svc = new NavigationService();
        svc.Register("test", () => new TestVm());
        svc.NavigateTo("test");
        svc.NavigateTo("unknown");
        // CurrentKey should still reflect the last successful navigation.
        Assert.Equal("test", svc.CurrentKey);
    }

    [Fact]
    public void CurrentKey_IsNull_BeforeAnyNavigation()
    {
        var svc = new NavigationService();
        Assert.Null(svc.CurrentKey);
    }
}
