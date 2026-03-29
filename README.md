# Windows Utility Pack

A modular collection of Windows desktop utility tools built with WPF, .NET 10, and a clean MVVM architecture.

## Tech Stack

| Component  | Technology               |
|------------|--------------------------|
| Language   | C# 13                    |
| Framework  | .NET 10                  |
| UI         | WPF (Windows only)       |
| Pattern    | MVVM                     |
| Tests      | xUnit                    |

## Architecture Overview

The application uses a clean MVVM architecture with a centralized navigation system:

```
App (entry point)
 ├── Registers tools in ToolRegistry
 ├── Wires services (Navigation, Theme, Settings, Logging)
 └── Opens MainWindow

MainWindow
 ├── Header (logo + theme toggle)
 ├── NavBar (5 category dropdown menus)
 ├── ContentControl ← bound to NavigationService.CurrentView
 └── StatusBar ← shows current action

NavigationService
 ├── Holds a dictionary of key → ViewModel factory
 ├── NavigateTo(key) creates the VM and raises Navigated event
 └── DataTemplates in App.xaml auto-resolve the right View
```

## Solution Structure

```
WindowsUtilityPack.sln
├── src/
│   └── WindowsUtilityPack/
│       ├── App.xaml(.cs)              # Entry point, DI bootstrap, tool registration
│       ├── MainWindow.xaml(.cs)       # Shell: header, nav bar, content area, status bar
│       ├── Commands/
│       │   ├── RelayCommand.cs
│       │   └── AsyncRelayCommand.cs
│       ├── Controls/
│       │   └── CategoryMenuButton     # Hover-popup nav button with NavigateCommand
│       ├── Converters/
│       ├── Models/
│       │   ├── CategoryItem.cs
│       │   └── ToolDefinition.cs      # Metadata + factory for each tool
│       ├── Resources/
│       │   └── Styles.xaml
│       ├── Services/
│       │   ├── INavigationService / NavigationService  # Key-based VM navigation
│       │   ├── IThemeService / ThemeService            # Dark/light theme switching
│       │   ├── ISettingsService / SettingsService      # JSON settings persistence
│       │   ├── ILoggingService / LoggingService        # File-based logging
│       │   └── INotificationService / NotificationService
│       ├── Themes/
│       ├── Tools/
│       │   ├── ToolRegistry.cs        # Central tool registration
│       │   ├── SystemUtilities/DiskInfo/
│       │   ├── FileDataTools/BulkFileRenamer/
│       │   ├── SecurityPrivacy/PasswordGenerator/
│       │   ├── NetworkInternet/PingTool/
│       │   └── DeveloperProductivity/RegexTester/
│       └── ViewModels/ + Views/
└── tests/
    └── WindowsUtilityPack.Tests/
        ├── Services/NavigationServiceTests.cs
        └── ViewModels/
            ├── ViewModelBaseTests.cs
            ├── PasswordGeneratorViewModelTests.cs
            └── RegexTesterViewModelTests.cs
```

## Implemented Tools

| Tool | Category | Status |
|------|----------|--------|
| Disk Info Viewer | System Utilities | ✅ |
| Bulk File Renamer | File & Data Tools | ✅ |
| Password Generator | Security & Privacy | ✅ |
| Ping Tool | Network & Internet | ✅ |
| Regex Tester | Developer & Productivity | ✅ |

## How Navigation Works

1. Each tool is registered in `App.xaml.cs` via `ToolRegistry.Register(new ToolDefinition { Key = "...", Factory = () => new MyViewModel() })`
2. `ToolRegistry.RegisterAll(NavigationService)` maps all keys to the navigation service
3. When a menu item is clicked, `NavigateCommand.Execute("tool-key")` is called
4. `NavigationService.NavigateTo(key)` invokes the factory and sets `CurrentView`
5. The `ContentControl` in `MainWindow.xaml` is bound to `CurrentView`
6. WPF's `DataTemplate` resolution automatically picks the correct View for each ViewModel type

## How to Add a New Tool

1. Create a folder under `src/WindowsUtilityPack/Tools/<Category>/<ToolName>/`
2. Add `<ToolName>ViewModel.cs` extending `ViewModelBase`
3. Add `<ToolName>View.xaml` + code-behind as a `UserControl`
4. Register in `App.xaml.cs`:
   ```csharp
   ToolRegistry.Register(new ToolDefinition {
       Key = "my-tool",
       Name = "My Tool",
       Category = "My Category",
       Factory = () => new MyToolViewModel(),
   });
   ```
5. Add a `DataTemplate` in `App.xaml`:
   ```xml
   <DataTemplate DataType="{x:Type myTool:MyToolViewModel}">
       <myTool:MyToolView/>
   </DataTemplate>
   ```
6. Add a `MenuEntry` in `MainWindow.xaml` with `ToolKey="my-tool"`

## Settings Persistence

Settings are stored as JSON at:
- Windows: `%LOCALAPPDATA%\WindowsUtilityPack\settings.json`

Persisted values: theme (dark/light), window position and size.

## Theming

Toggle between dark and light themes with the ☀/🌙 button. Theme is saved to settings and restored on next launch.

## How to Build & Run

**Prerequisites:** Windows OS, .NET 10 SDK.

```bash
git clone https://github.com/0langa/Windows-Utility-Pack.git
dotnet build WindowsUtilityPack.sln
dotnet run --project src/WindowsUtilityPack/WindowsUtilityPack.csproj
dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj
```

## Future Extension: Plugin System

The `ToolRegistry` is structured to support future MEF-based plugin loading. New tools can be packaged as separate assemblies and loaded at startup. See `Tools/ToolRegistry.cs` for plugin hook comments.
