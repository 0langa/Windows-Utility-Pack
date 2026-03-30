# Windows Utility Pack — Project Status & Quickstart

> **Intended audience:** Developer continuing this project, or an AI coding agent picking up a new session.

---

## 1. Project Status Summary

### What exists and works

| Area | Status | Notes |
|------|--------|-------|
| Solution structure | ✅ Complete | `WindowsUtilityPack.sln` → `src/` + `tests/` |
| Build | ✅ Clean | 0 errors, 0 warnings on .NET 10 |
| CI pipeline | ✅ Working | Build on Ubuntu, tests on Windows (see `.github/workflows/build.yml`) |
| MVVM infrastructure | ✅ Complete | `ViewModelBase`, `RelayCommand`, `AsyncRelayCommand` |
| Navigation service | ✅ Complete | Key-based, ViewModel-first, event-driven |
| Theme service | ✅ Complete | Dark ↔ Light runtime swap via ResourceDictionary |
| Settings persistence | ✅ Complete | JSON in `%LOCALAPPDATA%\WindowsUtilityPack\settings.json` |
| Logging service | ✅ Complete | Append-only file log in `%LOCALAPPDATA%\WindowsUtilityPack\app.log` |
| Notification service | ✅ Infrastructure only | Event raised; UI toast panel not yet built |
| Main shell (MainWindow) | ✅ Complete | Header, nav bar, content area, status bar |
| Home screen | ✅ Complete | Feature cards for all 5 tools |
| Disk Info Viewer | ✅ Complete | Lists drives with usage bar |
| Bulk File Renamer | ✅ Complete | Browse, preview, apply, conflict detection |
| Password Generator | ✅ Complete | Configurable charset, strength label, clipboard copy |
| Ping Tool | ✅ Complete | Async multi-ping, results table, summary |
| Regex Tester | ✅ Complete | Live matching, group capture, option flags |
| Unit tests | ✅ 15 tests pass | NavigationService, ViewModelBase, PasswordGenerator, RegexTester |

### What is placeholder / not yet built

| Item | Location | Notes |
|------|----------|-------|
| Notification toast UI | `MainWindow.xaml` | `NotificationService` fires events but nothing renders them yet |
| Task Manager Plus | `MainWindow.xaml` menu entry `ToolKey=""` | Placeholder — no ViewModel/View yet |
| Duplicate Finder | `MainWindow.xaml` menu entry `ToolKey=""` | Placeholder |
| CSV / JSON Tools | `MainWindow.xaml` menu entry `ToolKey=""` | Placeholder |
| Privacy Dashboard | `MainWindow.xaml` menu entry `ToolKey=""` | Placeholder |
| Secure File Shredder | `MainWindow.xaml` menu entry `ToolKey=""` | Placeholder |
| Network Scanner | `MainWindow.xaml` menu entry `ToolKey=""` | Placeholder |
| DNS Utility | `MainWindow.xaml` menu entry `ToolKey=""` | Placeholder |
| Text Diff Tool | `MainWindow.xaml` menu entry `ToolKey=""` | Placeholder |
| Clipboard Manager | `MainWindow.xaml` menu entry `ToolKey=""` | Placeholder |
| `Behaviors/` folder | `src/.../Behaviors/` | Empty — intended for future WPF attached behaviors |
| `Assets/` folder | `src/.../Assets/` | Empty — intended for future icons/images |

---

## 2. Architecture Overview

### MVVM Structure

```
View (XAML UserControl)
  └── DataContext → ViewModel (ViewModelBase subclass)
        └── Services (injected via constructor or App static)
              └── NavigationService / ThemeService / SettingsService / …
```

- Views are **pure XAML** with minimal code-behind (only `InitializeComponent()`).
- ViewModels contain **all business logic** and expose bindable properties + commands.
- `ViewModelBase` provides `INotifyPropertyChanged` via `SetProperty<T>`.
- Commands use `RelayCommand` (sync) or `AsyncRelayCommand` (async/await).

### How Navigation Works

```
User clicks menu item / card
  → NavigateCommand.Execute("tool-key")
      → NavigationService.NavigateTo("tool-key")
          → factory() creates a new ViewModel instance
          → NavigationService.CurrentView = newViewModel
          → Navigated event fires
              → MainWindowViewModel.CurrentView notified
                  → ContentControl in MainWindow.xaml re-renders
                      → WPF DataTemplate (App.xaml) matches ViewModel type → shows correct View
```

Key files:
- `Services/NavigationService.cs` — stores the key→factory dictionary
- `Tools/ToolRegistry.cs` — registers all tools at startup
- `App.xaml.cs` — calls `RegisterTools()` → `ToolRegistry.RegisterAll(NavigationService)`
- `App.xaml` — contains all `<DataTemplate DataType=…>` entries
- `MainWindow.xaml` — `ContentControl` bound to `MainWindowViewModel.CurrentView`

### How Theming Works

```
App.xaml loads DarkTheme.xaml as merged dict #0
OnStartup → ThemeService.SetTheme(settings.Theme)
  → if Light: removes DarkTheme dict, inserts LightTheme dict at position 0
  → all DynamicResource brushes in Views auto-update
```

Key files:
- `Themes/DarkTheme.xaml` and `Themes/LightTheme.xaml` — brush definitions
- `Services/ThemeService.cs` — swaps the active ResourceDictionary
- `Resources/Styles.xaml` — shared styles (loaded after theme, so theme brushes apply)
- `MainWindowViewModel.ToggleThemeCommand` — wires the toggle button

### How Tools Are Structured

Each tool lives under `src/WindowsUtilityPack/Tools/<Category>/<ToolName>/`:

```
Tools/
  SystemUtilities/DiskInfo/
    DiskInfoViewModel.cs   ← business logic, ObservableCollection<DriveInfoItem>
    DiskInfoView.xaml      ← purely declarative UI
    DiskInfoView.xaml.cs   ← only InitializeComponent()
```

Tools are registered in `App.xaml.cs` and mapped in `App.xaml`.

### Settings Persistence

- **Load**: `SettingsService.Load()` reads `%LOCALAPPDATA%\WindowsUtilityPack\settings.json`
- **Save**: `MainWindow.OnWindowClosing` calls `SettingsService.Save(settings)` with geometry + theme
- Failures silently return defaults — the app never crashes due to missing/corrupt settings

### Reusable Infrastructure

| Component | Location | Purpose |
|-----------|----------|---------|
| `RelayCommand` | `Commands/RelayCommand.cs` | Sync MVVM command |
| `AsyncRelayCommand` | `Commands/AsyncRelayCommand.cs` | Async MVVM command (re-entrancy safe) |
| `ViewModelBase` | `ViewModels/ViewModelBase.cs` | `INotifyPropertyChanged` base |
| `BooleanToVisibilityConverter` | `Converters/` | Bool → Visibility (invertible) |
| `ThemeToIconConverter` | `Converters/` | `AppTheme` → emoji icon |
| `CategoryMenuButton` | `Controls/` | Hover-popup nav button with `MenuEntry` items |

---

## 3. File / Folder Overview

```
WindowsUtilityPack.sln
├── src/WindowsUtilityPack/
│   ├── App.xaml(.cs)           Entry point, service init, tool registration
│   ├── MainWindow.xaml(.cs)    Shell: header + nav bar + content area + status bar
│   ├── Commands/               RelayCommand, AsyncRelayCommand
│   ├── Controls/               CategoryMenuButton (nav bar hover dropdown)
│   ├── Converters/             BooleanToVisibility, ThemeToIcon
│   ├── Models/                 CategoryItem (future), ToolDefinition
│   ├── Resources/              Styles.xaml (shared widget styles)
│   ├── Services/               All service interfaces + implementations
│   ├── Themes/                 DarkTheme.xaml, LightTheme.xaml
│   ├── Tools/                  Tool ViewModels + Views, ToolRegistry
│   ├── ViewModels/             MainWindowViewModel, HomeViewModel, ViewModelBase
│   └── Views/                  HomeView.xaml
├── tests/WindowsUtilityPack.Tests/
│   ├── Services/               NavigationServiceTests
│   └── ViewModels/             ViewModelBaseTests, PasswordGeneratorTests, RegexTesterTests
└── docs/
    ├── BUILD_REPAIR_NOTES.md   Historical repair pass notes
    └── PROJECT_STATUS_AND_QUICKSTART.md  ← this file
```

---

## 4. Quickstart for Future Development

### Build & Run

```bash
# Prerequisites: Windows OS, .NET 10 SDK
git clone https://github.com/0langa/Windows-Utility-Pack.git
cd Windows-Utility-Pack

dotnet build WindowsUtilityPack.sln
dotnet run --project src/WindowsUtilityPack/WindowsUtilityPack.csproj

# Run tests (Windows only — WPF runtime required):
dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj
```

> **Linux CI note:** `dotnet build` works on Linux due to `EnableWindowsTargeting=true`.
> `dotnet test` requires `Microsoft.WindowsDesktop.App` and must run on Windows.

### What to Check First if Something Fails

| Symptom | Where to look |
|---------|---------------|
| Build error in XAML | Check `App.xaml` DataTemplates and namespace imports |
| App crashes on startup | Check `App.xaml.cs OnStartup` — service init order |
| Navigation does nothing | Check `ToolRegistry.RegisterAll` is called; check tool key matches |
| Theme not applying | Check theme XAML brush key names match `DynamicResource` references |
| Test failure | Run `dotnet test -v normal` on Windows for full output |

### How to Add a New Tool

1. **Create the folder and files:**
   ```
   src/WindowsUtilityPack/Tools/<Category>/<ToolName>/
     <ToolName>ViewModel.cs   (extends ViewModelBase)
     <ToolName>View.xaml      (UserControl)
     <ToolName>View.xaml.cs   (only InitializeComponent)
   ```

2. **Register in `App.xaml.cs`** (inside `RegisterTools()`):
   ```csharp
   ToolRegistry.Register(new Models.ToolDefinition
   {
       Key      = "my-tool",
       Name     = "My Tool",
       Category = "My Category",
       Icon     = "🔧",
       Description = "What it does",
       Factory  = () => new MyToolViewModel(),
   });
   ```

3. **Add DataTemplate in `App.xaml`:**
   ```xml
   xmlns:myTool="clr-namespace:WindowsUtilityPack.Tools.MyCategory.MyTool"
   ...
   <DataTemplate DataType="{x:Type myTool:MyToolViewModel}">
       <myTool:MyToolView/>
   </DataTemplate>
   ```

4. **Add a menu entry in `MainWindow.xaml`** (in the appropriate `CategoryMenuButton`):
   ```xml
   <controls:MenuEntry Label="My Tool" ToolKey="my-tool"/>
   ```

5. **Optionally add a card on the home page** in `Views/HomeView.xaml`.

### Where to Edit Themes / Styles

| What | Where |
|------|-------|
| Colour palette | `Themes/DarkTheme.xaml` and `Themes/LightTheme.xaml` |
| Shared widget styles (buttons, cards) | `Resources/Styles.xaml` |
| Theme switching logic | `Services/ThemeService.cs` |

### Where Commands / Services / Settings Live

| Component | File |
|-----------|------|
| Sync command | `Commands/RelayCommand.cs` |
| Async command | `Commands/AsyncRelayCommand.cs` |
| Navigation | `Services/NavigationService.cs` |
| Theme | `Services/ThemeService.cs` |
| Settings | `Services/SettingsService.cs` |
| Logging | `Services/LoggingService.cs` |
| Notifications | `Services/NotificationService.cs` |

---

## 5. Guidance for Future AI Coding Sessions

### Architecture to Preserve

- **ViewModel-first navigation**: navigation keys map to ViewModel factories; Views are resolved by WPF DataTemplates. Do not introduce code-behind navigation.
- **Constructor injection**: Services should be injected into ViewModels via constructors where possible. Only fall back to `App.ServiceName` when WPF creates VMs through DataTemplates (e.g. `HomeViewModel`).
- **MVVM purity**: Keep all business logic in ViewModels. Views are XAML-only. Code-behind files contain only `InitializeComponent()`.
- **AsyncRelayCommand for async work**: always use `AsyncRelayCommand` when a command body uses `await`; never `async void` directly in a ViewModel.
- **DynamicResource for theming**: all colour references in Views and Styles must use `DynamicResource`, not `StaticResource`, so live theme switching works.

### Parts to Extend, Not Replace

| Part | How to extend |
|------|--------------|
| `ToolRegistry` | Call `Register()` once per tool in `App.xaml.cs`; add a `DataTemplate` in `App.xaml` |
| `CategoryMenuButton` | Add new `MenuEntry` items in `MainWindow.xaml`; add new `CategoryMenuButton` blocks for new categories |
| `AppSettings` | Add new properties to `AppSettings` class; they serialize/deserialize automatically |
| `INotificationService` | Wire `NotificationRequested` event in `MainWindow.xaml.cs` to a toast overlay |
| `Behaviors/` folder | Add WPF attached behaviors (e.g., auto-scroll, text highlight) |
| `Assets/` folder | Add PNG/SVG icons; reference as `BitmapImage` or `DrawingImage` resources |

### Known Limitations / Unfinished Areas

1. **Notification UI**: `NotificationService` fires events but `MainWindow` does not yet subscribe to them. Add a toast/snackbar overlay in `MainWindow.xaml` and wire it up in `MainWindow.xaml.cs`.

2. **No DI container**: Services are manually constructed in `App.OnStartup` and stored as static properties. For a larger codebase, consider introducing Microsoft.Extensions.DependencyInjection.

3. **HomeViewModel still uses App static**: `HomeViewModel` accepts an optional `INavigationService` parameter for testability but falls back to `App.NavigationService` when constructed by WPF DataTemplates. This is acceptable but worth noting.

4. **`CategoryItem` model is unused**: `Models/CategoryItem.cs` is defined but the nav bar is currently hard-coded in XAML. The model exists as a placeholder for a future dynamic nav bar.

5. **Placeholder menu entries**: Nine menu items have empty `ToolKey=""`. They display correctly but clicking does nothing. Implement the corresponding tools as needed.

6. **No input validation on Ping count**: `PingCount` is clamped to 1–20 in the ViewModel, but the View's `TextBox` allows non-numeric input. Add a numeric-only validation behavior or converter.

---

## 6. Practical Next Steps

Suggested next additions, roughly in priority order:

1. **Notification toast overlay** — Wire `NotificationService` to a visible UI panel (a `Border`/`TextBlock` overlay that fades in/out).

2. **Task Manager Plus** — Show running processes with CPU/memory usage (`System.Diagnostics.Process`).

3. **Text Diff Tool** — Side-by-side or inline diff between two text inputs.

4. **Clipboard Manager** — Monitor clipboard history and allow re-pasting previous entries.

5. **Keyboard shortcut support** — Add `InputBinding` entries in `MainWindow.xaml` for common actions.

6. **Settings UI** — A dedicated Settings page where the user can change theme and other preferences without using the toggle button.

7. **Tests for remaining ViewModels** — Add tests for `DiskInfoViewModel`, `BulkFileRenamerViewModel`, and `PingToolViewModel`.

8. **Numeric TextBox behavior** — Add a behavior in `Behaviors/` to restrict `TextBox` to numeric-only input (used by Ping count and Password length).
