# UI Interaction Stability Report

**Date:** 2025-07  
**Scope:** Full interaction-stability and runtime-behavior hardening pass across all tools, services, and shell infrastructure.  
**Goal:** Ensure the app never crashes from normal or slightly imperfect user interaction, commands fire reliably, and bindings resolve correctly.

---

## Summary

Audited all 32 tool ViewModels, 6 core services, the shell ViewModel, navigation infrastructure, and key code-behind files. Found and fixed **8 issues** across **8 files**. The remaining codebase was clean and well-structured.

---

## Issues Found & Fixed

### 1. Model classes without INotifyPropertyChanged (3 instances)

**Pattern:** Plain C# classes used as `ObservableCollection<T>` items had property setters that never raised `PropertyChanged`. UI bindings silently failed to update. Workaround code used a remove/insert hack (`RefreshEntry`) to force the collection to re-render the item.

| File | Class | Properties Affected |
|---|---|---|
| `HostsFileEditor/HostsFileEditorViewModel.cs` | `HostsEntry` | IpAddress, Hostname, Comment, IsEnabled, IsComment |
| `StartupManager/StartupManagerViewModel.cs` | `StartupEntry` | Name, Command, IsEnabled, Source |
| `SecureFileShredder/SecureFileShredderViewModel.cs` | `ShredderFileEntry` | FilePath, FileName, SizeDisplay, Status |

**Fix:** Converted each class to extend `ViewModelBase` with backing fields and `SetProperty` calls. Removed all `RefreshEntry` hack methods and their call sites.

### 2. SettingsWindow.Owner crash on startup

**File:** `ViewModels/MainWindowViewModel.cs` — `OpenSettings()`

**Problem:** Setting `SettingsWindow.Owner = Application.Current.MainWindow` could throw if `MainWindow` was null or not yet loaded during early startup.

**Fix:** Added guard: `Application.Current.MainWindow is { IsLoaded: true }` before assigning `Owner`.

### 3. Unsafe cast in NavigationService

**File:** `Services/NavigationService.cs` — `NavigateTo()`

**Problem:** Direct cast `(ViewModelBase)viewModel` would throw `InvalidCastException` if a factory returned a non-`ViewModelBase` object.

**Fix:** Added `is not ViewModelBase` type check with `Debug.WriteLine` fallback and early return.

### 4. CancellationTokenSource disposal leaks (3 instances)

**Pattern:** Debounce/scheduling methods created new `CancellationTokenSource` instances without disposing the previous one, leaking the underlying `WaitHandle`.

| File | Method |
|---|---|
| `DiffTool/DiffToolViewModel.cs` | `ScheduleComputeDiff()` |
| `RegexTester/RegexTesterViewModel.cs` | `ScheduleRunRegex()` |
| `PortScanner/PortScannerViewModel.cs` | `RunScanAsync()` |

**Fix:** Added `_cts?.Dispose()` before creating or nulling the CTS reference in each case.

---

## Issues Reviewed — No Fix Required

| Area | Finding |
|---|---|
| `StorageMaster/CleanupItemViewModel.RiskColor` | Hard-coded hex color, but property is **not bound** in any XAML template. No runtime impact. |
| `HttpRequestTester.SendRequestAsync` | Already guards with `IsNullOrWhiteSpace(Url)` check; malformed URIs caught by try/catch and shown to user. |
| `PortScanner` cross-thread UI access | All `ObservableCollection` mutations and property updates already wrapped in `Dispatcher.Invoke`. |
| `QrCodeGenerator.ScheduleGenerate` | Already disposes CTS correctly before creating a new one. |
| All other tool ViewModels (25+) | Clean async patterns, proper cancellation, no binding issues found. |
| `ThemeService`, `NotificationService`, `UserDialogService`, `SettingsService` | Well-structured, no stability issues. |
| `RelayCommand`, `AsyncRelayCommand` | Sound implementations with reentrancy guards and `CommandManager.RequerySuggested`. |
| `CategoryMenuButton` code-behind | Dropdown logic is event-driven and self-contained. |

---

## Files Modified

1. `src/WindowsUtilityPack/Tools/SystemUtilities/HostsFileEditor/HostsFileEditorViewModel.cs`
2. `src/WindowsUtilityPack/Tools/SystemUtilities/StartupManager/StartupManagerViewModel.cs`
3. `src/WindowsUtilityPack/Tools/FileDataTools/SecureFileShredder/SecureFileShredderViewModel.cs`
4. `src/WindowsUtilityPack/ViewModels/MainWindowViewModel.cs`
5. `src/WindowsUtilityPack/Services/NavigationService.cs`
6. `src/WindowsUtilityPack/Tools/DeveloperProductivity/DiffTool/DiffToolViewModel.cs`
7. `src/WindowsUtilityPack/Tools/DeveloperProductivity/RegexTester/RegexTesterViewModel.cs`
8. `src/WindowsUtilityPack/Tools/NetworkInternet/PortScanner/PortScannerViewModel.cs`

---

## Verification

- [x] Solution builds successfully
- [x] All existing tests pass
