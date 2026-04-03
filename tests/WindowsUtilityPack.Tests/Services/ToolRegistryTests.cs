using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;
using WindowsUtilityPack.ViewModels;
using Xunit;

namespace WindowsUtilityPack.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ToolRegistry"/>.
/// Uses a separate static scope by running sequentially;
/// the registry is a static singleton, so tests must tolerate shared state.
/// </summary>
public class ToolRegistryTests
{
    // Minimal ViewModelBase subclass for testing.
    private class StubVm : ViewModelBase { }

    [Fact]
    public void Register_AddsToolToAll()
    {
        var initialCount = ToolRegistry.All.Count;
        ToolRegistry.Register(new ToolDefinition
        {
            Key = $"test-{Guid.NewGuid()}",
            Name = "Test Tool",
            Category = "TestCategory",
            Factory = () => new StubVm(),
        });

        Assert.True(ToolRegistry.All.Count > initialCount);
    }

    [Fact]
    public void GetByCategory_ReturnsMatchingTools()
    {
        var uniqueCategory = $"Cat-{Guid.NewGuid()}";
        ToolRegistry.Register(new ToolDefinition
        {
            Key = $"test-{Guid.NewGuid()}",
            Name = "Cat Tool",
            Category = uniqueCategory,
            Factory = () => new StubVm(),
        });

        var results = ToolRegistry.GetByCategory(uniqueCategory).ToList();
        Assert.Single(results);
        Assert.Equal("Cat Tool", results[0].Name);
    }

    [Fact]
    public void GetByKey_ReturnsCorrectTool()
    {
        var key = $"find-{Guid.NewGuid()}";
        ToolRegistry.Register(new ToolDefinition
        {
            Key = key,
            Name = "Findable",
            Category = "TestCategory",
            Factory = () => new StubVm(),
        });

        var found = ToolRegistry.GetByKey(key);
        Assert.NotNull(found);
        Assert.Equal("Findable", found!.Name);
    }

    [Fact]
    public void GetByKey_ReturnsNull_ForUnknownKey()
    {
        Assert.Null(ToolRegistry.GetByKey("does-not-exist-" + Guid.NewGuid()));
    }

    [Fact]
    public void GetCategories_ExcludesGeneral()
    {
        // Register a "General" tool — it should NOT appear in categories.
        ToolRegistry.Register(new ToolDefinition
        {
            Key = $"gen-{Guid.NewGuid()}",
            Name = "Home-like",
            Category = "General",
            Factory = () => new StubVm(),
        });

        var cats = ToolRegistry.GetCategories();
        Assert.DoesNotContain(cats, c => c.Label == "General");
    }

    [Fact]
    public void GetDisplayTools_ExcludesGeneral()
    {
        var tools = ToolRegistry.GetDisplayTools();
        Assert.DoesNotContain(tools, t => t.Category.Equals("General", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegisterAll_RegistersKeysWithNavigationService()
    {
        var navSvc = new NavigationService();
        var key = $"nav-{Guid.NewGuid()}";
        ToolRegistry.Register(new ToolDefinition
        {
            Key = key,
            Name = "NavTest",
            Category = "TestCat",
            Factory = () => new StubVm(),
        });

        ToolRegistry.RegisterAll(navSvc);
        navSvc.NavigateTo(key);

        Assert.IsType<StubVm>(navSvc.CurrentView);
    }
}
