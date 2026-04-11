using System.Windows.Controls;
using WindowsUtilityPack.Models;
using WindowsUtilityPack.Services;
using WindowsUtilityPack.ViewModels;
using Xunit;

namespace WindowsUtilityPack.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="MainWindowViewModel"/> — command palette,
/// notifications, and navigation subsystems.
///
/// Theme-toggle tests are intentionally excluded: <c>ToggleTheme()</c>
/// persists directly to <c>App.SettingsService</c>, which is not
/// initialised during unit tests.
/// </summary>
public sealed class MainWindowViewModelTests
{
    // ── Command palette — open / close ────────────────────────────────────────

    [Fact]
    public void OpenCommandPalette_SetsIsCommandPaletteOpenTrue()
    {
        var vm = CreateVm();

        vm.OpenCommandPaletteCommand.Execute(null);

        Assert.True(vm.IsCommandPaletteOpen);
    }

    [Fact]
    public void CloseCommandPalette_SetsIsCommandPaletteOpenFalse()
    {
        var vm = CreateVm();
        vm.OpenCommandPaletteCommand.Execute(null);

        vm.CloseCommandPaletteCommand.Execute(null);

        Assert.False(vm.IsCommandPaletteOpen);
    }

    [Fact]
    public void OpenCommandPalette_ClearsQueryAndRefreshesResults()
    {
        var palette = new StubCommandPaletteService();
        var vm      = CreateVm(palette: palette);
        vm.CommandPaletteQuery = "stale query";

        vm.OpenCommandPaletteCommand.Execute(null);

        Assert.Equal(string.Empty, vm.CommandPaletteQuery);
        Assert.True(palette.SearchCallCount > 0);
    }

    [Fact]
    public void CloseCommandPalette_ClearsQueryAndSelection()
    {
        var vm = CreateVm();
        vm.OpenCommandPaletteCommand.Execute(null);
        vm.CommandPaletteQuery = "some text";

        vm.CloseCommandPaletteCommand.Execute(null);

        Assert.Equal(string.Empty, vm.CommandPaletteQuery);
        Assert.Null(vm.SelectedCommandPaletteItem);
    }

    // ── Command palette — query and results ───────────────────────────────────

    [Fact]
    public void CommandPaletteQuery_Setter_TriggersSearch()
    {
        var palette = new StubCommandPaletteService
        {
            Results =
            [
                new CommandPaletteItem { Id = "shell:home", Title = "Home", CommandKey = "home", Kind = CommandPaletteItemKind.ShellAction },
            ]
        };
        var vm = CreateVm(palette: palette);

        vm.CommandPaletteQuery = "home";

        Assert.NotEmpty(vm.CommandPaletteItems);
    }

    [Fact]
    public void CommandPaletteItems_PopulatedFromService_OnOpen()
    {
        var palette = new StubCommandPaletteService
        {
            Results =
            [
                new CommandPaletteItem { Id = "t1", Title = "Tool One", CommandKey = "tool-one", Kind = CommandPaletteItemKind.Tool },
                new CommandPaletteItem { Id = "t2", Title = "Tool Two", CommandKey = "tool-two", Kind = CommandPaletteItemKind.Tool },
            ]
        };
        var vm = CreateVm(palette: palette);

        vm.OpenCommandPaletteCommand.Execute(null);

        Assert.Equal(2, vm.CommandPaletteItems.Count);
    }

    [Fact]
    public void SelectedCommandPaletteItem_DefaultsToFirstResult()
    {
        var palette = new StubCommandPaletteService
        {
            Results =
            [
                new CommandPaletteItem { Id = "first",  Title = "First",  CommandKey = "first",  Kind = CommandPaletteItemKind.ShellAction },
                new CommandPaletteItem { Id = "second", Title = "Second", CommandKey = "second", Kind = CommandPaletteItemKind.ShellAction },
            ]
        };
        var vm = CreateVm(palette: palette);

        vm.OpenCommandPaletteCommand.Execute(null);

        Assert.NotNull(vm.SelectedCommandPaletteItem);
        Assert.Equal("first", vm.SelectedCommandPaletteItem!.Id);
    }

    [Fact]
    public void CommandPaletteItems_EmptyWhenNoServiceProvided()
    {
        var vm = CreateVm(palette: null);

        vm.OpenCommandPaletteCommand.Execute(null);

        Assert.Empty(vm.CommandPaletteItems);
    }

    // ── Command palette — execute ─────────────────────────────────────────────

    [Fact]
    public void ExecuteCommandPaletteItem_ToolKind_NavigatesToToolKey()
    {
        var nav     = new StubNavigationService();
        var palette = new StubCommandPaletteService
        {
            Results = [new CommandPaletteItem { Id = "tool:ping", Title = "Ping", CommandKey = "ping-tool", Kind = CommandPaletteItemKind.Tool }]
        };
        var vm = CreateVm(navigation: nav, palette: palette);
        vm.OpenCommandPaletteCommand.Execute(null);

        // Execute uses async void — task completes synchronously since no activityLogService.
        vm.ExecuteCommandPaletteItemCommand.Execute(vm.SelectedCommandPaletteItem);

        Assert.Equal("ping-tool", nav.LastNavigatedKey);
        Assert.False(vm.IsCommandPaletteOpen);
    }

    [Fact]
    public void ExecuteCommandPaletteItem_HomeShellAction_NavigatesHome()
    {
        var nav     = new StubNavigationService();
        var palette = new StubCommandPaletteService
        {
            Results = [new CommandPaletteItem { Id = "shell:home", Title = "Home", CommandKey = "home", Kind = CommandPaletteItemKind.ShellAction }]
        };
        var vm = CreateVm(navigation: nav, palette: palette);
        vm.OpenCommandPaletteCommand.Execute(null);

        vm.ExecuteCommandPaletteItemCommand.Execute(vm.SelectedCommandPaletteItem);

        Assert.Equal("home", nav.LastNavigatedKey);
    }

    [Fact]
    public void ExecuteCommandPaletteItem_NullItem_DoesNotNavigate()
    {
        var nav = new StubNavigationService();
        var vm  = CreateVm(navigation: nav);
        vm.OpenCommandPaletteCommand.Execute(null);

        vm.ExecuteCommandPaletteItemCommand.Execute(null);

        Assert.Null(nav.LastNavigatedKey);
    }

    [Fact]
    public void ExecuteCommandPaletteItem_ClosesAfterExecution()
    {
        var palette = new StubCommandPaletteService
        {
            Results = [new CommandPaletteItem { Id = "t", Title = "T", CommandKey = "t", Kind = CommandPaletteItemKind.Tool }]
        };
        var vm = CreateVm(palette: palette);
        vm.OpenCommandPaletteCommand.Execute(null);

        vm.ExecuteCommandPaletteItemCommand.Execute(vm.SelectedCommandPaletteItem);

        Assert.False(vm.IsCommandPaletteOpen);
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    [Fact]
    public void Notification_WhenRequested_IsVisibleWithCorrectText()
    {
        var notifications = new StubNotificationService();
        var vm            = CreateVm(notifications: notifications);

        notifications.RaiseInfo("Hello from tests");

        Assert.True(vm.IsNotificationVisible);
        Assert.Equal("Hello from tests", vm.NotificationText);
    }

    [Fact]
    public void DismissNotification_HidesTheBanner()
    {
        var notifications = new StubNotificationService();
        var vm            = CreateVm(notifications: notifications);
        notifications.RaiseInfo("Hello");

        vm.DismissNotificationCommand.Execute(null);

        Assert.False(vm.IsNotificationVisible);
    }

    [Fact]
    public void Notification_TypeIsPreserved_ForError()
    {
        var notifications = new StubNotificationService();
        var vm            = CreateVm(notifications: notifications);

        notifications.Raise("Failure", NotificationType.Error);

        Assert.Equal(NotificationType.Error, vm.NotificationType);
    }

    [Fact]
    public void Notification_TypeIsPreserved_ForSuccess()
    {
        var notifications = new StubNotificationService();
        var vm            = CreateVm(notifications: notifications);

        notifications.Raise("Done", NotificationType.Success);

        Assert.Equal(NotificationType.Success, vm.NotificationType);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    [Fact]
    public void NavigateHomeCommand_NavigatesToHomeKey()
    {
        var nav = new StubNavigationService();
        var vm  = CreateVm(navigation: nav);

        vm.NavigateHomeCommand.Execute(null);

        Assert.Equal("home", nav.LastNavigatedKey);
    }

    [Fact]
    public void NavigateCommand_UsesPassedKey()
    {
        var nav = new StubNavigationService();
        var vm  = CreateVm(navigation: nav);

        vm.NavigateCommand.Execute("storage-master");

        Assert.Equal("storage-master", nav.LastNavigatedKey);
    }

    // ── StatusMessage ─────────────────────────────────────────────────────────

    [Fact]
    public void StatusMessage_UpdatedOnNavigation()
    {
        var nav = new StubNavigationService();
        var vm  = CreateVm(navigation: nav);

        nav.SimulateNavigation(typeof(StubToolViewModel));

        // Status should now reference the ViewModel type name.
        Assert.Contains("Stub", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DetachesNavigationEvents_SoStatusNoLongerUpdates()
    {
        var nav = new StubNavigationService();
        var vm  = CreateVm(navigation: nav);
        vm.Dispose();

        var statusBefore = vm.StatusMessage;
        nav.SimulateNavigation(typeof(StubToolViewModel));

        // Disposed VM should not update status.
        Assert.Equal(statusBefore, vm.StatusMessage);
    }

    [Fact]
    public void Dispose_DetachesNotificationEvents_SoNoMoreBanners()
    {
        var notifications = new StubNotificationService();
        var vm            = CreateVm(notifications: notifications);
        vm.Dispose();

        notifications.RaiseInfo("Should be ignored");

        Assert.False(vm.IsNotificationVisible);
    }

    // ── Property-changed notifications ────────────────────────────────────────

    [Fact]
    public void IsCommandPaletteOpen_RaisesPropertyChanged()
    {
        var vm = CreateVm();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsCommandPaletteOpen))
                raised = true;
        };

        vm.OpenCommandPaletteCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void CommandPaletteQuery_RaisesPropertyChanged()
    {
        var vm = CreateVm();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.CommandPaletteQuery))
                raised = true;
        };

        vm.CommandPaletteQuery = "test";

        Assert.True(raised);
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    private static MainWindowViewModel CreateVm(
        StubNavigationService?     navigation    = null,
        StubNotificationService?   notifications = null,
        StubCommandPaletteService? palette       = null)
    {
        navigation    ??= new StubNavigationService();
        var theme       = new StubThemeService(AppTheme.Dark);
        notifications ??= new StubNotificationService();

        return new MainWindowViewModel(navigation, theme, notifications, palette);
    }

    // ── Stub implementations ──────────────────────────────────────────────────

    private sealed class StubToolViewModel : ViewModelBase { }

    private sealed class StubNavigationService : INavigationService
    {
        public string? LastNavigatedKey { get; private set; }

        public object? CurrentView => null;
        public ViewModelBase CurrentViewModel => new StubToolViewModel();
        public bool CanGoBack => false;

        public event EventHandler? CurrentViewChanged
        {
            add { }
            remove { }
        }
        public event EventHandler<Type>? Navigated;

        public void SetContentHost(ContentControl host) { }

        public void Navigate<TViewModel>() where TViewModel : ViewModelBase { }

        public void NavigateTo(object viewModel)
        {
            if (viewModel is string key)
            {
                LastNavigatedKey = key;
            }

            Navigated?.Invoke(this, viewModel?.GetType() ?? typeof(object));
        }

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        {
            Navigated?.Invoke(this, typeof(TViewModel));
        }

        public void GoBack() { }

        public void ClearHistory() { }

        public void Register<TViewModel>(Func<TViewModel> factory) where TViewModel : ViewModelBase { }

        public void Register(string key, Func<ViewModelBase> factory) { }

        /// <summary>Fires the Navigated event with the given type.</summary>
        public void SimulateNavigation(Type vmType) => Navigated?.Invoke(this, vmType);
    }

    private sealed class StubThemeService : IThemeService
    {
        public AppTheme CurrentTheme { get; private set; }
        public AppTheme EffectiveTheme => CurrentTheme;

        public event EventHandler<AppTheme>? ThemeChanged;

        public StubThemeService(AppTheme initial) => CurrentTheme = initial;

        public void SetTheme(AppTheme theme)
        {
            CurrentTheme = theme;
            ThemeChanged?.Invoke(this, theme);
        }
    }

    private sealed class StubNotificationService : INotificationService
    {
        public event EventHandler<NotificationEventArgs>? NotificationRequested;

        public void ShowInfo(string message)    => Raise(message, NotificationType.Info);
        public void ShowSuccess(string message) => Raise(message, NotificationType.Success);
        public void ShowError(string message)   => Raise(message, NotificationType.Error);

        public void RaiseInfo(string message) => Raise(message, NotificationType.Info);

        public void Raise(string message, NotificationType type)
            => NotificationRequested?.Invoke(this, new NotificationEventArgs(message, type));
    }

    private sealed class StubCommandPaletteService : ICommandPaletteService
    {
        public IReadOnlyList<CommandPaletteItem> Results { get; set; } = [];
        public int SearchCallCount { get; private set; }
        public string? LastRecordedExecution { get; private set; }

        public IReadOnlyList<CommandPaletteItem> Search(string? query, int limit = 20)
        {
            SearchCallCount++;
            return Results;
        }

        public void RecordExecution(string itemId)
        {
            LastRecordedExecution = itemId;
        }
    }
}
