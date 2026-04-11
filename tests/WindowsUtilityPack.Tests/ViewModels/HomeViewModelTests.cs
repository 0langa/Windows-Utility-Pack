using System.Windows.Controls;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.Tools;
using WindowsUtilityPack.ViewModels;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

public class HomeViewModelTests
{
    private sealed class TestViewModel : ViewModelBase { }

    [Fact]
    public void InspectClipboardCommand_ResetsQuickPingStatus_WhenClipboardNoLongerContainsHost()
    {
        var clipboard = new TestClipboardService("https://example.com/path");
        var vm = CreateViewModel(clipboard);

        Assert.Equal("Ready: example.com", vm.QuickPingStatus);

        clipboard.Text = string.Empty;
        vm.InspectClipboardCommand.Execute(null);

        Assert.Equal("Clipboard is empty", vm.ClipboardSummary);
        Assert.Equal("No URL/host detected in clipboard", vm.QuickPingStatus);
    }

    [Fact]
    public void CopyToolNameCommand_CopiesRegisteredToolName()
    {
        var toolKey = $"copy-tool-{Guid.NewGuid():N}";
        ToolRegistry.Register(new ToolDefinition
        {
            Key = toolKey,
            Name = "Clipboard Copy Tool",
            Category = "Test",
            Factory = static () => new TestViewModel(),
        });

        var clipboard = new TestClipboardService(string.Empty);
        var vm = CreateViewModel(clipboard);

        vm.CopyToolNameCommand.Execute(toolKey);

        Assert.Equal("Clipboard Copy Tool", clipboard.Text);
    }

    [Fact]
    public void ViewDescriptionCommand_WithoutDialogs_CopiesFallbackDescription()
    {
        var toolKey = $"describe-tool-{Guid.NewGuid():N}";
        ToolRegistry.Register(new ToolDefinition
        {
            Key = toolKey,
            Name = "Describe Tool",
            Description = "Explains the selected tool.",
            Category = "Test",
            Factory = static () => new TestViewModel(),
        });

        var clipboard = new TestClipboardService(string.Empty);
        var vm = CreateViewModel(clipboard);

        vm.ViewDescriptionCommand.Execute(toolKey);

        Assert.Equal("Describe Tool: Explains the selected tool.", clipboard.Text);
    }

    [Fact]
    public void SearchQuery_SynonymMatch_FindsRegisteredTool()
    {
        var toolKey = $"uuid-tool-{Guid.NewGuid():N}";
        ToolRegistry.Register(new ToolDefinition
        {
            Key = toolKey,
            Name = "UUID Tool",
            Description = "Generate unique identifiers.",
            Category = "Test",
            Factory = static () => new TestViewModel(),
        });

        var vm = CreateViewModel(new TestClipboardService(string.Empty));

        vm.SearchQuery = "guid";

        Assert.Contains(vm.SearchResults, tool => tool.Key == toolKey);
    }

    [Fact]
    public void SelectCategoryCommand_SelectsAndTogglesSelectedCategoryTools()
    {
        var categoryLabel = $"Category-{Guid.NewGuid():N}";
        var toolKey = $"category-tool-{Guid.NewGuid():N}";
        ToolRegistry.Register(new ToolDefinition
        {
            Key = toolKey,
            Name = "Category Tool",
            Description = "Category selection test tool.",
            Category = categoryLabel,
            Factory = static () => new TestViewModel(),
        });

        var vm = CreateViewModel(new TestClipboardService(string.Empty));
        var category = vm.Categories.First(c => c.Label.Equals(categoryLabel, StringComparison.Ordinal));

        vm.SelectCategoryCommand.Execute(category);

        Assert.Same(category, vm.SelectedCategory);
        Assert.Contains(vm.SelectedCategoryTools, tool => tool.Key == toolKey);

        vm.SelectCategoryCommand.Execute(category);

        Assert.Null(vm.SelectedCategory);
        Assert.Empty(vm.SelectedCategoryTools);
    }

    private static HomeViewModel CreateViewModel(TestClipboardService clipboard)
        => new(
            new TestNavigationService(),
            new TestDashboardService(),
            new TestSettingsService(),
            clipboard);

    private sealed class TestNavigationService : INavigationService
    {
        private readonly ViewModelBase _currentView = new TestViewModel();

        public object? CurrentView => _currentView;

        public event EventHandler? CurrentViewChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<Type>? Navigated
        {
            add { }
            remove { }
        }

        public bool CanGoBack => false;

        public ViewModelBase CurrentViewModel => _currentView;

        public void ClearHistory() { }

        public void GoBack() { }

        public void Navigate<TViewModel>() where TViewModel : ViewModelBase { }

        public void NavigateTo(object viewModel) { }

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase { }

        public void Register<TViewModel>(Func<TViewModel> factory) where TViewModel : ViewModelBase { }

        public void Register(string key, Func<ViewModelBase> factory) { }

        public void SetContentHost(ContentControl host) { }
    }

    private sealed class TestDashboardService : IHomeDashboardService
    {
        public int MaxRecentTools => 10;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public IReadOnlyList<ToolDefinition> GetFavoriteTools() => [];

        public IReadOnlyList<ToolDefinition> GetRecentTools() => [];

        public bool IsFavorite(string toolKey) => false;

        public bool ToggleFavorite(string toolKey) => false;

        public void RecordToolLaunch(string toolKey) { }

        public void ClearRecent() { }

        public void IncrementLaunchCount(string toolKey) { }

        public int GetLaunchCount(string toolKey) => 0;

        public IReadOnlyDictionary<string, int> GetAllLaunchCounts() => new Dictionary<string, int>();

        public IReadOnlyList<string> GetRecentSearches() => [];

        public void AddRecentSearch(string query) { }

        public void ClearRecentSearches() { }
    }

    private sealed class TestSettingsService : ISettingsService
    {
        public AppSettings Load() => new();

        public void Save(AppSettings settings) { }
    }

    private sealed class TestClipboardService : IClipboardService
    {
        public TestClipboardService(string text)
        {
            Text = text;
        }

        public string Text { get; set; }

        public bool TryGetText(out string text)
        {
            text = Text;
            return !string.IsNullOrEmpty(Text);
        }

        public void SetText(string text)
        {
            Text = text;
        }

        public bool TrySetImage(System.Windows.Media.Imaging.BitmapSource image) => false;
    }
}
