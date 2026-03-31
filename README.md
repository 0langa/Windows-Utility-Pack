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

## Current Status

The project is in an active, compilable, and runnable state. Six tools are currently integrated, including the new text conversion workflow for structured and document formats.

| Tool | Category | Status |
|------|----------|--------|
| Disk Info Viewer | System Utilities | ✅ Complete |
| Bulk File Renamer | File & Data Tools | ✅ Complete |
| Password Generator | Security & Privacy | ✅ Complete |
| Ping Tool | Network & Internet | ✅ Complete |
| Regex Tester | Developer & Productivity | ✅ Complete |
| Text Format Converter & Formatter | Developer & Productivity | ✅ Complete |

Nine additional tool slots are reserved as navigation placeholders (Task Manager Plus, Duplicate Finder, etc.) and can be implemented incrementally.

## How to Build & Run

**Prerequisites:** Windows OS, .NET 10 SDK.

```bash
git clone https://github.com/0langa/Windows-Utility-Pack.git
dotnet buildWindowsUtilityPack.sln
dotnet run --project src/WindowsUtilityPack/WindowsUtilityPack.csproj

# Tests (requires Windows — WPF Desktop runtime needed):
dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj
```

> `dotnet build` works on Linux/Mac via `EnableWindowsTargeting=true`.  
> `dotnet test` requires the Windows Desktop runtime and must run on Windows.

## Architecture Overview

The application uses a clean MVVM architecture with a centralised navigation service:

```
App (entry point)
 ├── Initialises services (Navigation, Theme, Settings, Logging, Notification)
 ├── Registers tools in ToolRegistry → NavigationService
 └── Opens MainWindow

MainWindow
 ├── Header (logo/home button + theme toggle)
 ├── NavBar (5 category hover-dropdown menus)
 ├── ContentControl ← bound to NavigationService.CurrentView
 └── StatusBar ← reflects last navigation action

NavigationService
 ├── Dictionary of key → ViewModel factory
 ├── NavigateTo(key) creates a fresh VM and raises Navigated event
 └── DataTemplates in App.xaml auto-resolve the correct View
```

## Solution Structure

```
WindowsUtilityPack.sln
├── src/WindowsUtilityPack/
│   ├── App.xaml(.cs)              Entry point — service init, tool registration
│   ├── MainWindow.xaml(.cs)       Shell window — nav bar, content area, status bar
│   ├── Commands/                  RelayCommand, AsyncRelayCommand
│   ├── Controls/                  CategoryMenuButton (hover-popup nav button)
│   ├── Converters/                BooleanToVisibility, ThemeToIcon
│   ├── Models/                    ToolDefinition, CategoryItem
│   ├── Resources/                 Styles.xaml (shared button/card styles)
│   ├── Services/                  All service interfaces + implementations
│   ├── Themes/                    DarkTheme.xaml, LightTheme.xaml
│   ├── Tools/                     Tool ViewModels + Views + ToolRegistry
│   ├── ViewModels/                MainWindowViewModel, HomeViewModel, ViewModelBase
│   └── Views/                     HomeView.xaml
└── tests/WindowsUtilityPack.Tests/
    ├── Services/                  NavigationServiceTests (4 tests)
    └── ViewModels/                ViewModelBaseTests, PasswordGeneratorTests, RegexTesterTests
```

## Theming

The theme toggle (☀/🌙 button) switches between dark and light colour schemes at runtime.  
Theme preference is saved to settings and restored on next launch.

All colours are defined as `DynamicResource` brushes in `Themes/DarkTheme.xaml` and `Themes/LightTheme.xaml`.

## How to Add a New Tool

1. Create `src/WindowsUtilityPack/Tools/<Category>/<ToolName>/` with a `ViewModel` + `View` pair.
2. Register in `App.xaml.cs` → `RegisterTools()`.
3. Add a `DataTemplate` in `App.xaml`.
4. Add a `MenuEntry` in the relevant `CategoryMenuButton` in `MainWindow.xaml`.

See **[docs/TEXT_FORMAT_CONVERTER.md](docs/TEXT_FORMAT_CONVERTER.md)** for the text conversion architecture and extension guidance.

## Settings Persistence

Settings are stored as JSON at `%LOCALAPPDATA%\WindowsUtilityPack\settings.json`.  
Persisted values: theme (dark/light), window position and size.

## Detailed Documentation

For architecture details about the Text Format Converter & Formatter, supported conversion directions, preview/export flow, and extension guidance, see:

📄 **[docs/TEXT_FORMAT_CONVERTER.md](docs/TEXT_FORMAT_CONVERTER.md)**

