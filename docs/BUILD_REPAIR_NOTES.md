# Build Repair Notes

**Pass:** Strict Build-Repair  
**Date:** 2026-03-29  
**Target:** Windows Utility Pack — WPF / C# / .NET 10

---

## Overview

This document records findings, fixes, and outstanding items from the build-repair pass performed on the Windows Utility Pack repository.

---

## Step 1 — Solution Structure Audit

| Item | Status | Notes |
|------|--------|-------|
| `WindowsUtilityPack.sln` | ✅ Valid | References both projects with correct GUIDs |
| `src/WindowsUtilityPack/WindowsUtilityPack.csproj` | ✅ Valid | `net10.0-windows`, `UseWPF=true`, `EnableWindowsTargeting=true` |
| `tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj` | ✅ Valid | xUnit test project with project reference to main app |
| Namespace alignment | ✅ Consistent | All tool ViewModels use deep namespaces (`WindowsUtilityPack.Tools.<Category>.<Tool>`) |
| NuGet packages | ✅ Valid | Test project uses `Microsoft.NET.Test.Sdk 17.11.1`, `xunit 2.9.2`, `xunit.runner.visualstudio 2.8.2` |
| No external NuGet packages in main project | ✅ Intentional | Application is self-contained using only framework APIs |

---

## Step 2 — Compilation Errors Found

**Result: None.** The solution built cleanly on the first attempt.

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

The codebase was in a clean, compilable state when the repair pass began.

---

## Step 3 — Startup / Run Blockers

| Item | Status | Notes |
|------|--------|-------|
| `App.xaml` StartupUri | ✅ OK | `StartupUri="MainWindow.xaml"` correctly wired |
| `App.xaml.cs` OnStartup | ✅ OK | All 5 services initialised before window creation |
| `MainWindow.xaml.cs` DataContext | ✅ OK | `MainWindowViewModel` constructed with services injected |
| Initial theme loading | ✅ OK | `DarkTheme.xaml` loaded via `App.xaml` merged dictionaries; `ThemeService` switches correctly at runtime |
| `NavigationService` registration | ✅ OK | `ToolRegistry.RegisterAll()` called in `OnStartup`; `"home"` navigation called in `MainWindow` constructor |
| Resource dictionary keys | ✅ OK | All `DynamicResource` keys (`AppBackgroundBrush`, `AccentBrush`, etc.) are present in both theme files |
| `DropShadowEffect` static resource | ✅ OK | Defined in `Resources/Styles.xaml` which is loaded after the theme |

---

## Step 4 — Minimum Runtime Integrity

| Item | Status | Notes |
|------|--------|-------|
| `HomeViewModel.NavigateCommand` | ✅ OK | Correctly calls `App.NavigationService` |
| `MainWindowViewModel` command wiring | ✅ OK | `ToggleThemeCommand`, `NavigateCommand`, `NavigateHomeCommand` all initialised in constructor |
| `CategoryMenuButton` dropdown | ✅ OK | Popup is `StaysOpen=False`; `OnDropdownItemClick` fires `NavigateCommand` with `ToolKey` |
| Content area DataTemplates | ✅ OK | All 6 ViewModels mapped to Views in `App.xaml` |
| Settings load/save | ✅ OK | Falls back to defaults silently if file is missing or corrupt |
| `ThemeService.SetTheme` guard | ✅ OK | Early-return when theme unchanged avoids unnecessary re-merge |

---

## Step 5 — Dependency Repair

No dependency changes were required.

The main project carries **zero NuGet dependencies** (WPF is a framework reference). The test project already references compatible packages.

---

## Step 6 — CI / Build Infrastructure Added

### Problem

The repository had no CI/CD pipeline. The test project requires `Microsoft.WindowsDesktop.App` (the WPF runtime) to *run*, which is not available on Linux runners. This caused `dotnet test` to fail on Linux with:

```
Testhost process … exited with error: You must install or update .NET to run this application.
Framework: 'Microsoft.WindowsDesktop.App', version '10.0.0' (x64)
```

> **Note:** `dotnet build` works on Linux because of `EnableWindowsTargeting=true`. Only *test execution* requires the WPF Desktop runtime.

### Fix

Added `.github/workflows/build.yml` with two jobs:

| Job | Runner | Purpose |
|-----|--------|---------|
| `build` | `ubuntu-latest` | Cross-compile verification; fast feedback |
| `test` | `windows-latest` | Full test execution with WPF desktop runtime |

---

## Step 7 — Known Remaining Items

These are **not blockers** but are noted for the cleanup/polish pass:

1. **`HomeViewModel` couples to `App` static property** — `HomeViewModel` calls `App.NavigationService` directly. This is acceptable for now but makes unit testing the ViewModel harder. A constructor-injected `INavigationService` would be cleaner.

2. **Empty placeholder tool keys** — The navigation bar menu entries for "Task Manager Plus", "Duplicate Finder", "CSV / JSON Tools", "Privacy Dashboard", "Secure File Shredder", "Network Scanner", "DNS Utility", "Text Diff Tool", and "Clipboard Manager" all have `ToolKey=""`. Clicking them does nothing (the guard `!string.IsNullOrEmpty(toolKey)` in `OnDropdownItemClick` prevents a crash). This is by design for planned future tools.

3. **`LoggingService` and `SettingsService` swallow exceptions silently** — Errors during log writes and settings saves are caught and discarded. Acceptable for a desktop utility but worth revisiting.

4. **`Behaviors/` and `Assets/` folders are empty** — They contain only `.gitkeep` placeholders. No action needed until those features are implemented.

5. **`CategoryItem` model is unused** — Defined in `Models/CategoryItem.cs` but never referenced. Safe to remove in a polish pass.

---

## Assumptions

- The application is intended to run on Windows only (WPF is Windows-only).
- `EnableWindowsTargeting=true` is intentional and correct for cross-platform build agents.
- The dark theme is the intended default; the `App.xaml` hard-codes `DarkTheme.xaml` as the startup theme which the `ThemeService` then replaces from saved settings if needed.

---

## Manual Verification Checklist

When running locally on Windows, please verify:

- [ ] Application launches to the home screen with dark theme
- [ ] Theme toggle (☀ / 🌙) switches between dark and light correctly
- [ ] All 5 category dropdowns open on hover and close when mouse leaves
- [ ] Clicking a tool card or menu item navigates to the correct view
- [ ] Logo / title click returns to home screen
- [ ] Password Generator generates passwords and copies to clipboard
- [ ] Bulk File Renamer loads a folder and shows rename preview
- [ ] Disk Info Viewer lists drives with usage bars
- [ ] Ping Tool sends pings and displays results
- [ ] Regex Tester highlights matches in real time
- [ ] Window position/size is saved and restored on restart
- [ ] All 15 unit tests pass (`dotnet test` on Windows)
