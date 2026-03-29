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

## Solution Structure

```
WindowsUtilityPack.sln
├── src/
│   └── WindowsUtilityPack/
│       ├── App.xaml(.cs)              # Application entry point & DI bootstrap
│       ├── MainWindow.xaml(.cs)       # Shell window with nav bar
│       ├── Assets/                    # Images, icons
│       ├── Commands/
│       │   ├── RelayCommand.cs        # Sync ICommand
│       │   └── AsyncRelayCommand.cs   # Async ICommand
│       ├── Controls/
│       │   └── CategoryMenuButton     # Reusable hover-popup nav button
│       ├── Converters/                # IValueConverter implementations
│       ├── Models/                    # Plain data objects
│       ├── Resources/
│       │   └── Styles.xaml            # All shared styles
│       ├── Services/
│       │   ├── IThemeService.cs       # Theme service contract
│       │   ├── ThemeService.cs        # Runtime theme switcher
│       │   ├── INavigationService.cs  # Navigation contract
│       │   └── NavigationService.cs   # Simple ViewModel navigator
│       ├── Themes/
│       │   ├── DarkTheme.xaml         # Dark color palette
│       │   └── LightTheme.xaml        # Light color palette
│       ├── ViewModels/
│       │   ├── ViewModelBase.cs       # INotifyPropertyChanged base
│       │   ├── MainWindowViewModel.cs # Shell ViewModel
│       │   └── HomeViewModel.cs       # Dashboard ViewModel
│       └── Views/
│           └── HomeView.xaml(.cs)     # Dashboard view
└── tests/
    └── WindowsUtilityPack.Tests/
        └── ViewModels/
            └── ViewModelBaseTests.cs  # Unit tests for ViewModelBase
```

## How to Build & Run

**Prerequisites:** Windows OS, .NET 10 SDK, Visual Studio 2022+ or JetBrains Rider.

```bash
# Clone the repository
git clone https://github.com/0langa/Windows-Utility-Pack.git

# Build
dotnet build WindowsUtilityPack.sln

# Run
dotnet run --project src/WindowsUtilityPack/WindowsUtilityPack.csproj

# Run tests (Windows required — project targets net10.0-windows)
dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj
```

## Theming

Themes live in `src/WindowsUtilityPack/Themes/`:

- **DarkTheme.xaml** – dark navy/red accent palette (default)
- **LightTheme.xaml** – clean white/blue accent palette

At runtime the `ThemeService` swaps theme ResourceDictionaries inside `Application.Current.Resources`. Click the ☀/🌙 button in the top-right header to toggle.

To add a new theme:
1. Create `Themes/MyTheme.xaml` and define all required brush keys.
2. Add `AppTheme.MyTheme` to the enum in `IThemeService.cs`.
3. Update `ThemeService.ApplyTheme` to handle the new case.

## How to Add a New Tool / Module

1. **Add a ViewModel** in `ViewModels/` extending `ViewModelBase`.
2. **Add a View** in `Views/` as a `UserControl`.
3. **Register navigation** – call `NavigationService.NavigateTo<YourViewModel>()` from a command.
4. **Add a menu entry** – extend the relevant `CategoryMenuButton.MenuItems` collection in `MainWindow.xaml`.
5. **Wire up styles** – add any new styles to `Resources/Styles.xaml` or the theme dictionaries.

## Future Extension Suggestions

- **Dependency Injection** – swap manual `new ThemeService()` for Microsoft.Extensions.DI.
- **Region/Frame navigation** – embed a `Frame` or content `ContentControl` bound to `CurrentView`.
- **Module system** – load tool assemblies dynamically via MEF or a plugin interface.
- **Settings persistence** – add a `ISettingsService` backed by `System.Text.Json` + `ApplicationData`.
- **Logging** – wire in `Microsoft.Extensions.Logging` or Serilog.
- **Packaging** – publish with `dotnet publish -r win-x64 --self-contained` or use MSIX.
