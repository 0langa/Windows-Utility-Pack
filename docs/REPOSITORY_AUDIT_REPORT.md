# Repository Audit Report

**Date:** 2026-04-11  
**Scope:** Full repository — architecture, runtime safety, WPF/MVVM correctness, UX, theme/DPI, persistence, async/threading, test coverage  
**Auditor:** Senior-level automated code review pass  
**Branch/Baseline:** `main` (current HEAD)

---

## 1. Executive Summary

The codebase is well-structured, consistently organized, and materially safer than earlier audit snapshots indicated. MVVM boundaries are respected, the ToolRegistry pattern works correctly, and the test baseline is solid for the areas it covers.

However, a serious hardening pass is warranted before this can be considered enterprise-grade. The highest-risk areas are:

- **Initialization fragility:** Static `null!` services in `App.xaml.cs` can cause `NullReferenceException` crashes if any code path touches them before `OnStartup()` completes.
- **Resource leaks:** Multiple ViewModels and services subscribe to events, own `CancellationTokenSource` instances, or hold timers without a disposal path, meaning navigation away from a tool does not free its resources.
- **Theme breakage:** Three tool views contain hardcoded colors that make UI elements invisible or visually broken under dark or Aurora themes.
- **Security gaps:** `LocalSecretVaultViewModel` retains decryption key bytes in memory indefinitely and has no brute-force protection on the unlock flow.
- **Missing test coverage:** 27 of 33 tool ViewModels have no automated test coverage. Two tools with identified runtime issues (NetworkSpeedTest, PortScanner) are entirely untested.
- **UX friction:** Several tools have poor empty-state messaging, no unsaved-change warnings, or broken interaction patterns (PasswordBox sync, read-only URL fields, invisible error states after failed scans).

---

## 2. Audit Scope and Method

### Areas inspected

- Startup and initialization flow (`App.xaml.cs`, `App.xaml`)
- Shell and navigation (`MainWindow`, `MainWindowViewModel`, `NavigationService`)
- Home dashboard (`HomeViewModel`, `HomeView.xaml`, `HomeDashboardService`)
- Settings and theme (`SettingsService`, `ThemeService`, `SettingsWindow`)
- Tool registry and wiring (`ToolRegistry`, `App.xaml` DataTemplates)
- Infrastructure (`ViewModelBase`, `RelayCommand`, `AsyncRelayCommand`)
- 6 tool ViewModels in depth: StorageMaster, Downloader, LocalSecretVault, SecureFileShredder, HostsFileEditor, StartupManager
- 5 additional tool ViewModels: PasswordGenerator, NetworkSpeedTest, PortScanner, BulkFileRenamer, ScreenshotAnnotator
- Service layer: `HomeDashboardService`, `DownloadCoordinatorService`, `TextFormatConversionService`
- Theme files: `DarkTheme.xaml`, `LightTheme.xaml`, `AuroraTheme.xaml`
- Resource files: `Styles.xaml`, `InputStyles.xaml`
- 3 tool XAML views in depth: `StorageMasterView`, `DownloaderView`, `LocalSecretVaultView`
- Cross-checks: all 33 DataTemplates in `App.xaml` vs ViewModel classes and ToolRegistry entries
- Test directory: all 47 test files, coverage mapped to tools

### Analysis performed

- Static analysis of initialization sequences and service access patterns
- Event subscription and disposal tracing
- Async/await and threading pattern review
- WPF binding correctness, DataContext propagation, command wiring
- Theme brush usage — `DynamicResource` vs `StaticResource` vs hardcoded
- DPI and layout scaling — fixed pixel dimensions identified
- UX flow review — empty states, error feedback, keyboard usability, interaction consistency
- Test coverage mapping — which tools and services have no coverage

---

## 3. Severity Legend

| Level | Meaning |
|---|---|
| **Critical** | Can cause crash, data loss, silent security failure, or irreversible user impact |
| **High** | Causes visible breakage, memory leaks, incorrect behavior, or serious UX harm |
| **Medium** | Degrades reliability, UX, or maintainability in meaningful ways |
| **Low** | Minor defects, minor friction, or suboptimal patterns with low immediate impact |
| **Improvement** | No defect, but a clear enhancement worth doing |

---

## 4. Findings by Area

---

### 4.1 Architecture / Project Structure

---

#### F-01: Static null-forgiving service properties create initialization order crash risk
**Severity:** Critical  
**Area:** Architecture / Startup  
**Confidence:** Confirmed

**Description:** `App.xaml.cs` declares all services as `public static IXxxService Xxx { get; private set; } = null!;`. The `null!` suppresses the nullable warning but does not provide safety — any code that accesses these properties before `OnStartup()` has initialized them will throw `NullReferenceException`.

**Why it matters:** WPF DataTemplate instantiation, resource dictionary loading, and design-time tooling can trigger ViewModel constructors or property access earlier than expected. The pattern also makes it impossible for the compiler or runtime to detect missing initialization.

**Evidence:** `App.xaml.cs` lines ~20–50 (static property declarations); `HomeViewModel.cs` lines ~140–141 with `?? App.NavigationService` fallback — which silently uses the static accessor rather than injected service.

**Recommended fix:**  
- Guard every static accessor with a null check that throws a descriptive `InvalidOperationException`: `ThemeService ?? throw new InvalidOperationException("App services not initialized.")`.  
- Long-term: move to `Microsoft.Extensions.DependencyInjection` with a proper composition root. The existing factory pattern in `ToolRegistry` is already close to this model.

**Implementation risk:** Medium (touches startup and all callsites of `App.*`)

---

#### F-02: NavigationService back-stack never disposes previous ViewModels
**Severity:** High  
**Area:** Architecture / Resource Safety  
**Confidence:** Confirmed

**Description:** When navigating to a new tool, `NavigationService` pushes the previous `CurrentViewModel` onto `_backStack`. It never calls `Dispose()` on the outgoing ViewModel, even if it implements `IDisposable`. ViewModels that hold timers, event subscriptions, or `CancellationTokenSource` instances accumulate in the back-stack for the entire session.

**Why it matters:** Extended use of the app (opening many tools) progressively leaks memory and keeps background workers or timer callbacks alive for tools that are no longer visible.

**Evidence:** `NavigationService.cs` — `Navigate()` and `GoBack()` methods have no disposal calls.

**Recommended fix:** In `Navigate()`, before pushing to back-stack:
```csharp
if (CurrentViewModel is IDisposable d) d.Dispose();
```
Then add `IDisposable` to ViewModels that have cleanup needs.

**Implementation risk:** Medium (requires `IDisposable` to be added to several ViewModels)

---

#### F-03: Constructor parameter injection in HomeViewModel is effectively dead code
**Severity:** Low  
**Area:** Architecture  
**Confidence:** Confirmed

**Description:** `HomeViewModel` accepts `INavigationService? navigation` and `IHomeDashboardService? dashboard` as constructor parameters, but both always arrive as `null` when instantiated via the WPF DataTemplate system. The code immediately falls back to `App.NavigationService` and `App.HomeDashboardService`. The constructor parameters provide no actual injection path.

**Why it matters:** Misleads readers into thinking DI is working here; creates false confidence in testability. The ViewModel cannot be tested in isolation without App being initialized.

**Recommended fix:** Either wire up explicit injection via the factory in `ToolRegistry`, or remove the parameters and reference `App.*` directly with a comment explaining the limitation.

**Implementation risk:** Low

---

### 4.2 Startup / Initialization

---

#### F-04: MainWindow reads settings before service null-safety is guaranteed
**Severity:** High  
**Area:** Startup  
**Confidence:** Confirmed

**Description:** `MainWindow.xaml.cs` constructor calls `App.SettingsService.Load()` to restore window position. If `SettingsService` is not yet assigned (e.g., exception during `OnStartup()`, or designer instantiation), this is a `NullReferenceException`.

**Why it matters:** Any failure during `App.OnStartup()` before `SettingsService` is assigned could cause a secondary crash that hides the original error.

**Evidence:** `MainWindow.xaml.cs` lines ~27–34.

**Recommended fix:** Add a null check or use `App.SettingsService?.Load() ?? new AppSettings()`.

**Implementation risk:** Low

---

#### F-05: ThemeService subscribed to SystemEvents but never unsubscribed on exit
**Severity:** Medium  
**Area:** Startup / Resource Safety  
**Confidence:** Confirmed

**Description:** `ThemeService` subscribes to `SystemEvents.UserPreferenceChanged` when the `System` theme is selected. This is a static .NET event. If `ThemeService` is not disposed at app shutdown, the delegate remains registered.

**Why it matters:** While typically benign on app exit, it can cause issues in test harness scenarios or if the service is ever recreated. It's also a pattern that, if copied to other services, could cause serious leaks.

**Evidence:** `Services/ThemeService.cs` lines ~77–92.

**Recommended fix:** Make `ThemeService` implement `IDisposable`, unsubscribe in `Dispose()`, and call `App.ThemeService.Dispose()` in `App.OnExit()`.

**Implementation risk:** Low

---

### 4.3 Navigation / Tool Launching

---

#### F-06: All tool ViewModels registered in ToolRegistry and DataTemplates — no wiring gaps
**Severity:** N/A (verified sound)  
**Area:** Navigation  
**Confidence:** Confirmed

All 33 tools have a matching DataTemplate in `App.xaml` and a corresponding factory in `ToolRegistry`. No orphaned templates or missing mappings were found. This area is correctly implemented.

---

#### F-07: MainWindowViewModel event subscriptions never unsubscribed
**Severity:** Medium  
**Area:** Navigation / Resource Safety  
**Confidence:** Confirmed

**Description:** `MainWindowViewModel` subscribes to `_navigation.Navigated`, `_theme.ThemeChanged`, and `notifications.NotificationRequested` in its constructor. These subscriptions are never removed. While the ViewModel appears to be a singleton for the app lifetime, the pattern is unsafe if multiple instances are ever created.

**Why it matters:** If the ViewModel were ever recreated (e.g., during testing or a future refactor), each old instance would remain alive and continue processing events from the services.

**Evidence:** `ViewModels/MainWindowViewModel.cs` lines ~109–120.

**Recommended fix:** Implement `IDisposable` and unsubscribe in `Dispose()`.

**Implementation risk:** Low

---

### 4.4 WPF / MVVM / Bindings

---

#### F-08: AsyncRelayCommand uses async void — unhandled exceptions crash the app
**Severity:** High  
**Area:** WPF / MVVM  
**Confidence:** Confirmed

**Description:** `AsyncRelayCommand.Execute()` is implemented as `async void`. Exceptions thrown in the async task that are not caught by the provided `_onException` handler propagate to the `SynchronizationContext`, which in WPF causes an unhandled exception that terminates the application.

**Why it matters:** Any command that does async I/O (file operations, network calls, registry access) and encounters an unexpected exception (permission denied, file locked, timeout) will crash the app rather than displaying an error.

**Evidence:** `Commands/AsyncRelayCommand.cs` lines ~58–78.

**Recommended fix:** Wrap the entire `async void Execute` body in a top-level try/catch that logs and surfaces errors gracefully, even when no `_onException` handler is provided:
```csharp
catch (Exception ex)
{
    if (_onException != null) _onException(ex);
    else App.LoggingService?.LogError("Unhandled async command error", ex);
}
```

**Implementation risk:** Low

---

#### F-09: ViewModelBase.OnPropertyChanged is not UI-thread-safe
**Severity:** Medium  
**Area:** WPF / MVVM  
**Confidence:** Likely

**Description:** `ViewModelBase.OnPropertyChanged()` raises `PropertyChanged` directly. WPF requires `PropertyChanged` to be raised on the UI thread for bound controls to update correctly. Several background operations in StorageMaster, PortScanner, and NetworkSpeedTest call `SetProperty()` from background threads.

**Why it matters:** Raising `PropertyChanged` from a non-UI thread causes intermittent `InvalidOperationException` ("The calling thread cannot access this object because a different thread owns it") that may not reproduce consistently.

**Evidence:** `ViewModels/ViewModelBase.cs`; `StorageMasterViewModel.cs` lines with progress callbacks; `PortScannerViewModel.cs` lines ~206, 210.

**Recommended fix:** Dispatch to UI thread in `OnPropertyChanged()`:
```csharp
protected virtual void OnPropertyChanged(string? propertyName = null)
{
    var dispatcher = Application.Current?.Dispatcher;
    if (dispatcher != null && !dispatcher.CheckAccess())
        dispatcher.Invoke(() => PropertyChanged?.Invoke(this, GetPropertyChangedEventArgs(propertyName)));
    else
        PropertyChanged?.Invoke(this, GetPropertyChangedEventArgs(propertyName));
}
```

**Implementation risk:** Low (single change in `ViewModelBase`)

---

#### F-10: HomeView.xaml multi-level RelativeSource bindings are silently fragile
**Severity:** Medium  
**Area:** WPF / Bindings  
**Confidence:** Confirmed

**Description:** Multiple places in `HomeView.xaml` use:
```xaml
Command="{Binding DataContext.NavigateCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
```
inside `DataTemplate` sections (inside `ItemsControl`). If the visual tree is not fully constructed, or if the `DataContext` on the `UserControl` is briefly `null` during navigation transitions, the binding silently fails and buttons become inoperative.

**Why it matters:** Tool cards on the home page may become unclickable after certain navigation transitions without any visible error. The failure is invisible in production.

**Evidence:** `Views/HomeView.xaml` lines ~56–57, 205, 319–320, 401.

**Recommended fix:** Use `BindingProxy` or routed command pattern to avoid deep `RelativeSource` binding. Alternatively, add `FallbackValue={x:Null}` with command null-guards to fail quietly rather than silently.

**Implementation risk:** Medium

---

#### F-11: SettingsWindow implements its own INotifyPropertyChanged instead of using ViewModelBase
**Severity:** Low  
**Area:** WPF / MVVM  
**Confidence:** Confirmed

**Description:** `SettingsWindow.xaml.cs` implements `INotifyPropertyChanged` directly in code-behind rather than extracting a SettingsViewModel inheriting from `ViewModelBase`. This bypasses all infrastructure (caching, tracing) and creates an inconsistent pattern.

**Why it matters:** Settings changes made externally (e.g., via `ThemeService`) while the settings window is open are not reflected in the UI.

**Recommended fix:** Extract a `SettingsViewModel` class inheriting from `ViewModelBase`. Keep settings window code-behind minimal.

**Implementation risk:** Medium

---

### 4.5 UI / UX / Convenience

---

#### F-12: HostsFileEditor has no unsaved-changes warning on navigation away
**Severity:** High  
**Area:** UX  
**Confidence:** Confirmed

**Description:** `HostsFileEditorViewModel` correctly tracks `IsModified`, but there is no prompt or confirmation when the user navigates to another tool while edits are pending. All unsaved changes are silently discarded.

**Why it matters:** Editing the hosts file is a meaningful, non-trivial action. Silent discard of uncommitted entries is a data-loss scenario that users will find frustrating and confusing.

**Evidence:** `HostsFileEditorViewModel.cs` — `IsModified` flag set but never consulted during navigation events.

**Recommended fix:** Subscribe to a navigation-leaving event (or override a `CanDeactivate` hook) and prompt the user to save or discard.

**Implementation risk:** Medium

---

#### F-13: HostsFileEditor adds invalid IP entries to the list before validating
**Severity:** Medium  
**Area:** UX / Logic  
**Confidence:** Confirmed

**Description:** The `AddEntry()` method adds a new `HostsEntry` to the `Entries` collection and marks `IsModified = true` before validating the IP address format. If the user types `999.999.999.999`, the entry appears in the list and is only rejected at save time. The entry then lingers in the list in a visually ambiguous state.

**Why it matters:** Creates confusing state: the user sees an entry they believe they added, then gets a save error with no clear indication of which entry is invalid.

**Evidence:** `HostsFileEditorViewModel.cs` `AddEntry()` method.

**Recommended fix:** Validate IP (and hostname) in `AddEntry()` before adding to the collection. Show an inline validation message instead.

**Implementation risk:** Low

---

#### F-14: StartupManager silently omits HKLM entries without elevation notice
**Severity:** Medium  
**Area:** UX  
**Confidence:** Confirmed

**Description:** When loading startup entries, the `HKLM` registry key open fails silently (the `catch` block is empty). The list only shows `HKCU` entries without any indication that system-level (HKLM) startup items were skipped due to insufficient privilege.

**Why it matters:** Users who expect to see and manage all startup items (including system-installed ones) will see an incomplete list and may assume entries don't exist, or may assume the tool is malfunctioning.

**Evidence:** `StartupManagerViewModel.cs` lines ~146–166.

**Recommended fix:** Display a non-blocking info banner: "System startup entries (HKLM) require administrator privileges and are not shown." Conditionally visible based on whether the HKLM read failed.

**Implementation risk:** Low

---

#### F-15: StorageMaster result list silently truncated at 2000 items
**Severity:** Medium  
**Area:** UX  
**Confidence:** Confirmed

**Description:** `FilteredFiles.Add()` is guarded with `.Take(2000)`, capping the displayed result at 2000 items. No UI indicator tells the user results are truncated.

**Why it matters:** On a large drive, a scan might return 100,000 files. After filtering, only 2000 appear. The user has no way to know results are incomplete. This looks like either a filter bug or a missing feature.

**Evidence:** `StorageMasterViewModel.cs` line ~506.

**Recommended fix:** Display a banner or count: "Showing 2,000 of {total} results. Refine your filter to see more."

**Implementation risk:** Low

---

#### F-16: Downloader URL fields are read-only but not selectable
**Severity:** Low  
**Area:** UX  
**Confidence:** Confirmed

**Description:** In `DownloaderView.xaml`, the `SourceUrl`, `ResolvedUrl`, and `OutputFilePath` fields are `IsReadOnly="True"` TextBoxes. While this prevents editing, WPF read-only TextBoxes also make text selection and `Ctrl+C` copy non-obvious (the fields have no selection highlight styling).

**Why it matters:** Users wanting to copy a URL or file path out of the detail panel cannot do so intuitively.

**Evidence:** `DownloaderView.xaml` lines ~157–165.

**Recommended fix:** Add `IsReadOnly="True"` TextBoxes with explicit `IsTabStop="True"` and a small "Copy" icon button alongside each field. Or use `SelectableTextBlock` behavior.

**Implementation risk:** Low

---

#### F-17: LocalSecretVaultView PasswordBox not synchronized on secret selection change
**Severity:** Medium  
**Area:** UX / Correctness  
**Confidence:** Confirmed

**Description:** The vault edit panel shows either a `TextBox` or `PasswordBox` based on `IsValueVisible`. The `PasswordBox` is not two-way bound (WPF limitation). When the user selects a different secret, the `TextBox` updates from the binding, but the `PasswordBox` shows stale content from the previous selection because it has no binding update trigger.

**Why it matters:** Users in masked mode will see the previous secret's value in the PasswordBox when switching between entries, until they interact with the field. This is a significant correctness issue for a security tool.

**Evidence:** `LocalSecretVaultView.xaml` lines ~130–141.

**Recommended fix:** In code-behind, listen for `SelectedSecret` changes (via `DataContextChanged` or explicit subscription) and imperatively set `EditValuePasswordBox.Password = vm.EditValue`.

**Implementation risk:** Low

---

#### F-18: NetworkSpeedTest upload test is simulated, not real
**Severity:** High  
**Area:** UX / Correctness  
**Confidence:** Confirmed

**Description:** The upload speed test in `NetworkSpeedTestViewModel` uses `Task.Delay()` to simulate a measurement rather than performing an actual upload. The displayed result is not a real network measurement.

**Why it matters:** Users relying on this tool to assess upload bandwidth are receiving fabricated results. This is a functional correctness failure for the stated purpose of the tool.

**Evidence:** `NetworkSpeedTestViewModel.cs` lines ~250–283.

**Recommended fix:** Implement a real upload test (POST a payload to a measurement endpoint, or use a local server-reflector). If a real test is not feasible, clearly label the value as "Not measured" or remove the upload result entirely.

**Implementation risk:** Medium

---

### 4.6 Theme / Layout / DPI / Responsiveness

---

#### F-19: DiffToolView uses hardcoded colors for diff highlighting
**Severity:** High  
**Area:** Theme  
**Confidence:** Confirmed

**Description:** `DiffToolView.xaml` hard-codes `"Green"` and `"Red"` for diff-line foreground and `#1A00AA00` / `#1AAA0000` for diff-line background highlights. These colors are not theme-aware.

**Why it matters:** On the dark or Aurora theme, these colors produce poor contrast or visual confusion. The green-on-dark is acceptable, but the hardcoded transparency colors do not integrate with the theme's color system.

**Evidence:** `DiffToolView.xaml` lines ~99, 101, 121, 124, 137, 141.

**Recommended fix:** Define semantic brush tokens in all three theme files (`DiffAddedBrush`, `DiffRemovedBrush`, `DiffAddedBackgroundBrush`, `DiffRemovedBackgroundBrush`) and reference them with `DynamicResource`.

**Implementation risk:** Low

---

#### F-20: SecureFileShredderView warning banner hardcodes light-theme-only colors
**Severity:** High  
**Area:** Theme  
**Confidence:** Confirmed

**Description:** The warning banner at the top of `SecureFileShredderView.xaml` uses:
- Background: `#FFEBEE` (very light red)
- Border: `#EF9A9A` (light pink)
- Foreground/icon: `#C62828` (dark red)

These colors are invisible on dark themes (light-on-light) and clash with Aurora theme colors.

**Why it matters:** The warning banner is the most important UX element in this tool. It must be legible in all themes.

**Evidence:** `SecureFileShredderView.xaml` lines ~12–19.

**Recommended fix:** Use `DynamicResource WarningBrush`, `WarningBorderBrush`, `WarningForegroundBrush` tokens (or equivalent) defined per theme. If these tokens don't exist, add them to all three theme files.

**Implementation risk:** Low

---

#### F-21: ColorPickerView RGB channel labels use hardcoded semantic colors
**Severity:** Low  
**Area:** Theme  
**Confidence:** Confirmed

**Description:** The R, G, B labels in `ColorPickerView.xaml` use hardcoded `#EF4444` (red), `#22C55E` (green), `#3B82F6` (blue) as label foregrounds. These are intentional semantic colors for the RGB channels, but they are fixed and may conflict with theme backgrounds.

**Why it matters:** Lower severity than F-19 and F-20 since these colors are semantically appropriate (red for red channel, etc.), but they should still be defined as named resources for maintainability.

**Recommended fix:** Define `ColorPickerRedChannelBrush`, etc., as named `SolidColorBrush` resources in a shared resource dictionary rather than inlining hex values.

**Implementation risk:** Low

---

#### F-22: 252+ fixed-pixel widths across tool views reduce DPI scaling quality
**Severity:** Medium  
**Area:** DPI / Layout  
**Confidence:** Confirmed

**Description:** Across tool views, there are over 252 instances of fixed-pixel `Width="N"` or `Height="N"` attributes. Examples:
- `PortScannerView.xaml`: fields at `Width="160"`, `Width="80"`
- `NetworkSpeedTestView.xaml`: progress bars at `Width="120"`
- `SecureFileShredderView.xaml`: ComboBox at `Width="200"`
- `DiffToolView.xaml`: text panels at `Height="160"`

**Why it matters:** At 125% or 150% DPI (common on modern displays), fixed-pixel controls do not scale with text. Labels outgrow their containing controls, content clips, and layout alignment breaks.

**Evidence:** Grep on `Width="[0-9]` across Views/ returns 252 matches.

**Recommended fix:** Replace fixed widths with relative sizing (`Width="*"`, `MinWidth`, `MaxWidth`). For controls that genuinely need a minimum, use `MinWidth` instead of `Width`. Audit the 252 matches and fix the ones in visible, user-facing areas first.

**Implementation risk:** Medium (many files, but each change is isolated)

---

### 4.7 Settings / Persistence / State

---

#### F-23: SettingsService swallows deserialization exceptions silently
**Severity:** Medium  
**Area:** Persistence  
**Confidence:** Confirmed

**Description:** In `SettingsService.Load()`, a `try/catch` around deserialization falls back to `new AppSettings()` on any exception. The logging attempt itself is also wrapped in a `catch { }`. If the settings file is corrupt, the error is silently discarded and the user's configuration is reset to defaults without any notification.

**Why it matters:** Users lose all their settings (window position, theme preference, favorites, recently used tools) after a corrupt-settings event, with no explanation.

**Evidence:** `Services/SettingsService.cs` lines ~32–36, 49–51.

**Recommended fix:**
1. Log to `Debug.WriteLine` at minimum, even if `App.LoggingService` is unavailable.
2. On corrupt settings, notify the user once: "Your settings file was unreadable and has been reset to defaults. The original has been saved as `settings.corrupt.json`."
3. Preserve the corrupt file for debugging.

**Implementation risk:** Low

---

#### F-24: HomeDashboardService.Persist() has no I/O error handling
**Severity:** Medium  
**Area:** Persistence  
**Confidence:** Confirmed

**Description:** `HomeDashboardService.Persist()` writes favorites and recent-tools lists to `SettingsService` without any try/catch. If the underlying settings write fails (disk full, permission denied, file locked), the exception propagates to the caller — likely a command execution — where it becomes an unhandled async exception.

**Why it matters:** Persistence failures should be handled gracefully, not crash the command execution path.

**Evidence:** `Services/HomeDashboardService.cs` lines ~109–112.

**Recommended fix:** Wrap the Persist call in try/catch, log the failure, and surface it as a non-blocking notification if the save fails.

**Implementation risk:** Low

---

### 4.8 Async / Threading / Resource Safety

---

#### F-25: StorageMasterViewModel CancellationTokenSource replaced without disposal
**Severity:** High  
**Area:** Async / Resource Safety  
**Confidence:** Confirmed

**Description:** In `StorageMasterViewModel.StartScanAsync()`, a new `CancellationTokenSource` is created and assigned to `_scanCts` without first disposing the previous one:
```csharp
_scanCts = new CancellationTokenSource(); // previous CTS leaked
```
The `finally` block disposes and nulls `_scanCts` correctly after the scan, but if the user triggers a second scan before the first completes, the first CTS is replaced and leaked.

**Why it matters:** Each leaked CTS holds OS-level cancellation handles. Repeated scans in a session progressively consume these resources.

**Evidence:** `StorageMasterViewModel.cs` lines ~405, 444–445.

**Recommended fix:**
```csharp
_scanCts?.Dispose(); // dispose previous
_scanCts = new CancellationTokenSource();
```

**Implementation risk:** Low

---

#### F-26: DownloadCoordinatorService spawns fire-and-forget Tasks with no join on shutdown
**Severity:** High  
**Area:** Async / Resource Safety  
**Confidence:** Confirmed

**Description:** `DownloadCoordinatorService.QueueLoopAsync()` spawns individual download tasks with:
```csharp
_ = Task.Run(() => ExecuteJobAsync(nextJob, handle, cancellationToken), cancellationToken);
```
The returned `Task` is discarded. When the service is stopped, there is no mechanism to await or cancel all in-flight tasks before the service is torn down. Downloads can continue running against already-disposed resources.

**Why it matters:** Can produce `ObjectDisposedException`, file handle conflicts, partially written output files, or orphaned network connections after the user stops the queue or closes the tool.

**Evidence:** `Services/Downloader/DownloadCoordinatorService.cs` line ~541.

**Recommended fix:** Track spawned tasks in a `List<Task>` and `await Task.WhenAll(...)` in the shutdown path (after signaling cancellation).

**Implementation risk:** Medium

---

#### F-27: DownloaderViewModel DispatcherTimer not stopped or disposed on ViewModel cleanup
**Severity:** High  
**Area:** Async / Resource Safety  
**Confidence:** Confirmed

**Description:** `DownloaderViewModel` holds a `DispatcherTimer` (`_clipboardTimer`). There is no `IDisposable` implementation on the ViewModel. When the user navigates away from the Downloader tool, the timer continues ticking and the ViewModel stays alive via the navigation back-stack.

**Why it matters:** Clipboard polling continues indefinitely, keeping the ViewModel in memory and consuming CPU cycles unnecessarily for the remainder of the session.

**Evidence:** `DownloaderViewModel.cs` line ~28.

**Recommended fix:** Implement `IDisposable`. In `Dispose()`, call `_clipboardTimer.Stop()`. Ensure `NavigationService` calls `Dispose()` when removing ViewModels (F-02).

**Implementation risk:** Low

---

#### F-28: SecureFileShredder batch deletion blocks UI thread
**Severity:** High  
**Area:** Async / Threading  
**Confidence:** Confirmed

**Description:** The cleanup deletion loop in `StorageMasterViewModel` (and the shred loop in `SecureFileShredderViewModel`) iterates synchronously over selected files and deletes/shreds each one without offloading to a background thread. For large selections, this freezes the UI thread for seconds to minutes.

**Why it matters:** The app appears hung. Users may force-kill it, potentially leaving files in a partially shredded/deleted state.

**Evidence:** `StorageMasterViewModel.cs` lines ~656–683; `SecureFileShredderViewModel.cs` lines ~172–193.

**Recommended fix:** Wrap the deletion loop in `await Task.Run(() => { ... })` with progress reporting via `IProgress<T>` or `Dispatcher.InvokeAsync`.

**Implementation risk:** Medium

---

#### F-29: SecureFileShredder Application.Current.Dispatcher.Invoke can NullReferenceException on shutdown
**Severity:** Medium  
**Area:** Async / Threading  
**Confidence:** Confirmed

**Description:** `SecureFileShredderViewModel` calls `Application.Current.Dispatcher.Invoke(...)` from background threads. If the application is in the process of shutting down, `Application.Current` returns `null`, causing `NullReferenceException`.

**Why it matters:** If the user closes the app while a shred operation is running, the background thread crashes with a hard exception.

**Evidence:** `SecureFileShredderViewModel.cs` lines ~172–176, 182–185.

**Recommended fix:** Use null-conditional: `Application.Current?.Dispatcher?.Invoke(...)`. Or check `!Application.Current.Dispatcher.HasShutdownStarted`.

**Implementation risk:** Low

---

#### F-30: PortScanner allows up to 500 concurrent socket connections
**Severity:** High  
**Area:** Async / Resource Safety  
**Confidence:** Confirmed

**Description:** `PortScannerViewModel` uses a `SemaphoreSlim` initialized with a `Concurrency` value derived from user settings, allowing up to 500 concurrent TCP connections. No upper bound enforcement or system-resource check is performed.

**Why it matters:** 500 concurrent socket connections can exhaust local port availability, trigger firewall responses, or degrade system network performance. It may also cause the tool to appear to hang when the OS starts refusing socket creation.

**Evidence:** `PortScannerViewModel.cs` line ~79.

**Recommended fix:** Cap the semaphore at a safe maximum (e.g., 50–100). Display the concurrency setting with a warning if the user sets it above a safe threshold.

**Implementation risk:** Low

---

#### F-31: TextFormatConversionService async methods missing ConfigureAwait(false)
**Severity:** Low  
**Area:** Async  
**Confidence:** Confirmed

**Description:** All `await` calls in `TextFormatConversionService` lack `.ConfigureAwait(false)`. In a library service called from ViewModel commands (which run on the UI synchronization context), this means continuations resume on the UI thread unnecessarily, adding latency to the UI thread.

**Why it matters:** Minor performance issue. Does not cause correctness problems in WPF (unlike ASP.NET contexts), but is inconsistent with library best practices.

**Evidence:** `Services/TextConversion/TextFormatConversionService.cs` lines ~162, 213, 227, 236, 251.

**Recommended fix:** Add `.ConfigureAwait(false)` to all `await` calls in service-layer methods (not in ViewModel methods, where UI thread context is needed).

**Implementation risk:** Low

---

### 4.9 File / Permission / Environment Edge Cases

---

#### F-32: LocalSecretVault retains decryption key in memory indefinitely
**Severity:** High  
**Area:** Security / Memory Safety  
**Confidence:** Confirmed

**Description:** `LocalSecretVaultViewModel` stores the derived AES key in `_derivedKey` (a `byte[]`) for the entire ViewModel lifetime. The key is never zeroed after use. Intermediate plaintext byte arrays from `EditValue` encoding are also not cleared.

**Why it matters:** A memory dump of the process (e.g., via Task Manager, crash dump, or debugging tool) reveals the plaintext encryption key and all secret values. This is a meaningful security risk for a tool explicitly intended to store credentials securely.

**Evidence:** `LocalSecretVaultViewModel.cs` lines ~47–48, 230–231.

**Recommended fix:**
1. Use `Array.Clear(_derivedKey, 0, _derivedKey.Length)` after each encrypt/decrypt operation, or implement a "lock vault" command that zeroes the key.
2. Use `System.Security.SecureString` for `_derivedKey` storage where feasible.
3. Zero intermediate byte arrays explicitly: `Array.Clear(plaintextBytes, 0, plaintextBytes.Length)`.

**Implementation risk:** Low

---

#### F-33: LocalSecretVault has no brute-force protection on unlock
**Severity:** High  
**Area:** Security  
**Confidence:** Confirmed

**Description:** The `UnlockCommand` in `LocalSecretVaultViewModel` processes each password attempt immediately with no rate limiting, lockout, or delay after failed attempts.

**Why it matters:** An attacker with access to the running app (or able to automate UI commands) can attempt passwords at processor speed. AES-256 key derivation has a cost, but without any application-layer throttling, this reduces to a pure CPU attack.

**Evidence:** `LocalSecretVaultViewModel.cs` `UnlockCommand` handler.

**Recommended fix:** After each failed attempt, apply an exponential delay (`await Task.Delay(failedAttempts * 500ms)`). After 10 failures, lock the vault and require app restart or a cooldown period.

**Implementation risk:** Low

---

#### F-34: SecureFileShredder file rename has no error handling and a path-switch bug
**Severity:** High  
**Area:** File Operations  
**Confidence:** Confirmed

**Description:** The shred operation renames the file to a random name before overwriting. If `File.Move()` fails (file locked, different volume, permissions), the code falls through and attempts to open the original `path` — which now may not exist or may still be at its original name. This creates an inconsistent and potentially crashy state.

**Why it matters:** File shredding is a trust-critical operation. A partially-executed shred (rename succeeded but overwrite failed, or vice versa) leaves data in an ambiguous security state.

**Evidence:** `SecureFileShredderViewModel.cs` lines ~220+.

**Recommended fix:** Explicitly handle the `File.Move()` result. If rename fails, continue operating on the original path (without rename). Track which path is active and use it consistently through the entire operation.

**Implementation risk:** Low

---

#### F-35: HostsFile backup write not wrapped in try/catch
**Severity:** Medium  
**Area:** File Operations  
**Confidence:** Confirmed

**Description:** `HostsFileEditorViewModel` writes a backup before saving. The `WriteAllTextAsync` for the backup file is not wrapped in exception handling. If the backup write fails (permission, disk full), the exception propagates and the save is aborted — but the user's edit is not preserved and no useful error message is shown.

**Why it matters:** The hosts file is a system file. Errors in this path should be handled carefully and communicated clearly.

**Evidence:** `HostsFileEditorViewModel.cs` lines ~228–237.

**Recommended fix:** Wrap the backup write in try/catch. If the backup fails, warn the user and offer to proceed without backup or abort.

**Implementation risk:** Low

---

### 4.10 Testing / Validation Gaps

---

#### F-36: 27 of 33 tool ViewModels have zero test coverage
**Severity:** High  
**Area:** Testing  
**Confidence:** Confirmed

**Description:** Only 6 ViewModels have tests: `BulkFileRenamer`, `PasswordGenerator`, `PingTool`, `RegexTester`, `TextFormatConverter`, `ViewModelBase`. All other 27 tools — including those with confirmed issues (F-28, F-29, F-30, F-32, F-33) — have no automated tests.

**Untested tools with confirmed issues:**
- `NetworkSpeedTestViewModel` — fake upload, dispatcher assumptions
- `PortScannerViewModel` — uncapped concurrency
- `LocalSecretVaultViewModel` — security gaps
- `SecureFileShredderViewModel` — threading, rename bug
- `StorageMasterViewModel` — CTS leaks, UI blocking, truncation
- `DownloaderViewModel` — timer leak, no disposal
- `HostsFileEditorViewModel` — data loss on navigate, invalid state

**Why it matters:** No regression safety net. Any refactor touching these tools risks introducing bugs that are not caught until manual testing (or user reports).

**Recommended fix:** Prioritize tests for high-risk tools. Start with `LocalSecretVaultViewModel` (security), `PortScannerViewModel` (resource safety), and `StorageMasterViewModel` (complex state management). Use seam patterns to test without real file system or network.

**Implementation risk:** Low per test, Medium overall effort

---

#### F-37: NetworkSpeedTestViewModel has a static shared HttpClient with potential misconfiguration
**Severity:** Medium  
**Area:** Testing / Runtime Safety  
**Confidence:** Confirmed

**Description:** `NetworkSpeedTestViewModel` holds a `static HttpClient` field. Static `HttpClient` instances are generally recommended to avoid socket exhaustion — but they must also be configured correctly (timeout, headers) for each use. If the static instance has a custom timeout that was appropriate for one operation but blocks another, all operations share that state.

**Why it matters:** Shared mutable static state in a service that performs multiple distinct network operations (latency check, download test, upload test) can cause one operation's configuration to bleed into another.

**Evidence:** `NetworkSpeedTestViewModel.cs` line ~28.

**Recommended fix:** Use `IHttpClientFactory` if DI is introduced. Or explicitly reset timeout before each timed operation rather than relying on the default.

**Implementation risk:** Low

---

### 4.11 Maintainability / Technical Debt

---

#### F-38: DownloadCoordinatorService is a ~800-line god service
**Severity:** Medium  
**Area:** Maintainability  
**Confidence:** Confirmed

**Description:** `DownloadCoordinatorService.cs` and `WebScraperService.cs` are both 700–800 line files with wide responsibility surfaces. Changes to the download pipeline require reading and modifying large files with high merge conflict probability.

**Why it matters:** Large files slow review, increase regression risk, and make it hard to test individual behaviors in isolation.

**Recommended fix:** Follow the decomposition plan already outlined in `IMPLEMENTATION_REFACTOR_PLAN.md`. Extract queue management, job lifecycle, and engine routing into separate focused classes.

**Implementation risk:** High (architecture refactor, do after test coverage improves)

---

#### F-39: ToolRegistry factory lambdas capture static App.* services creating hidden coupling
**Severity:** Medium  
**Area:** Maintainability  
**Confidence:** Confirmed

**Description:** Tool factories in `App.xaml.cs` are lambdas like `() => new SomeViewModel(App.SomeService)`. Each factory captures a static service reference at the point of invocation. This makes it impossible to test tool factories in isolation without a fully initialized `App`.

**Why it matters:** Tightly couples the tool registry to the application startup sequence. Makes unit testing of individual tool ViewModels require a live `App`.

**Recommended fix:** Pass service dependencies into the registry registration call explicitly, or use a proper DI container that resolves at factory invocation time.

**Implementation risk:** High (architectural, do as part of DI migration)

---

## 5. Priority Fix Plan

### Phase 1 — Urgent Safety and Stability (do now)

| # | Finding | Change |
|---|---|---|
| 1 | F-08 | Fix `AsyncRelayCommand` to catch all exceptions at the top level |
| 2 | F-19 | Replace hardcoded diff colors with `DynamicResource` theme tokens |
| 3 | F-20 | Replace hardcoded shredder warning banner colors with `DynamicResource` |
| 4 | F-25 | Fix CTS disposal before replacement in `StorageMasterViewModel` |
| 5 | F-29 | Add null-guard on `Application.Current.Dispatcher` in shredder |
| 6 | F-32 | Zero sensitive key bytes in `LocalSecretVaultViewModel` after use |
| 7 | F-34 | Fix file rename error handling and path consistency in shredder |
| 8 | F-18 | Either implement real upload test or clearly label result as "Not measured" |

### Phase 2 — High-Value UX and Runtime Fixes

| # | Finding | Change |
|---|---|---|
| 9 | F-09 | Marshal `PropertyChanged` to UI thread in `ViewModelBase` |
| 10 | F-12 | Add unsaved-changes prompt in `HostsFileEditorViewModel` on navigation |
| 11 | F-13 | Validate IP at `AddEntry()` time, not at save time |
| 12 | F-14 | Show elevation notice in `StartupManagerViewModel` for missing HKLM entries |
| 13 | F-15 | Show truncation notice in `StorageMasterViewModel` filtered results |
| 14 | F-17 | Fix `PasswordBox` sync in `LocalSecretVaultView` on secret selection change |
| 15 | F-27 | Add `IDisposable` to `DownloaderViewModel`, stop timer on dispose |
| 16 | F-02 | Have `NavigationService` call `Dispose()` on outgoing ViewModels |
| 17 | F-33 | Add exponential backoff on failed unlock attempts in `LocalSecretVault` |
| 18 | F-30 | Cap `PortScanner` concurrency at a safe maximum |

### Phase 3 — Maintainability Hardening and Cleanup

| # | Finding | Change |
|---|---|---|
| 19 | F-01 | Add null-guard accessors on all `App.*` static service properties |
| 20 | F-23 | Improve `SettingsService` error handling — preserve corrupt file, notify user |
| 21 | F-24 | Add try/catch to `HomeDashboardService.Persist()` |
| 22 | F-05 | Make `ThemeService` disposable; unsubscribe `SystemEvents` on exit |
| 23 | F-07 | Implement `IDisposable` on `MainWindowViewModel` |
| 24 | F-11 | Extract `SettingsViewModel` from `SettingsWindow` code-behind |
| 25 | F-22 | Audit and fix DPI-scaling for top-offender views |
| 26 | F-26 | Track and join spawned download tasks on shutdown |
| 27 | F-36 | Add tests for `LocalSecretVault`, `PortScanner`, `StorageMaster` |
| 28 | F-28 | Move deletion loops to background threads with progress reporting |

---

## 6. Quick Wins

These are low-risk, high-value changes achievable in under an hour each:

- **F-25** — Dispose CTS before replacement in StorageMaster (2 lines)
- **F-29** — Null-guard `Application.Current.Dispatcher` calls (3 lines)
- **F-14** — Show elevation notice in StartupManager (add a `bool HklmSkipped` property + info TextBlock)
- **F-15** — Show truncation notice in StorageMaster filtered list (1 property + 1 TextBlock)
- **F-13** — Move IP validation to `AddEntry()` in HostsFileEditor
- **F-21** — Extract `ColorPickerView` RGB label colors to named resources
- **F-16** — Add Copy button alongside read-only URL fields in DownloaderView
- **F-31** — Add `.ConfigureAwait(false)` to all awaits in `TextFormatConversionService`
- **F-04** — Add null-conditional to `App.SettingsService?.Load()` in `MainWindow`

---

## 7. Strategic Improvements

These require more planning and should be sequenced carefully:

- **Proper DI container** — Replace `App.*` static accessors with `Microsoft.Extensions.DependencyInjection`. This is the single change that resolves F-01, F-03, F-39, and makes F-36 (testing) dramatically easier.
- **Downloader decomposition** — Break `DownloadCoordinatorService` and `WebScraperService` into focused units per the existing `IMPLEMENTATION_REFACTOR_PLAN.md`.
- **Task join on downloader shutdown** — Requires task tracking collection and coordinated cancellation (F-26).
- **`IDisposable` ViewModel pattern** — Establish a `IActivatable`/`IDeactivatable` hook in `NavigationService` and enforce disposal, resolving F-02, F-07, F-27 in one architectural move.
- **ViewModel test coverage** — 27 untested ViewModels is a significant liability. Seam patterns (interfaces on file system, registry, crypto) are the prerequisite for making these testable.
- **CI enforcement** — No CI pipeline exists. A minimal GitHub Actions workflow (build + test on push/PR) would catch regressions immediately. This is listed in the existing `IMPLEMENTATION_REFACTOR_PLAN.md` and remains unaddressed.

---

## 8. Final Recommendation

The codebase is in good operational shape with a clean architecture and no systemic design failures. However, it has a cluster of safety issues that should be addressed before it can be considered hardened for enterprise use.

**Start with Phase 1.** The security gaps in `LocalSecretVault` (F-32, F-33), the theme breakage in `DiffTool` and `SecureFileShredder` (F-19, F-20), and the `AsyncRelayCommand` crash risk (F-08) are the highest-priority items and all have low implementation risk. They can be shipped in a single focused hardening pass.

**Then Phase 2.** The UX issues (F-12, F-13, F-14, F-15, F-17) collectively represent the gap between a "mostly working" tool and a polished product. Each is independently fixable. The disposal chain (F-02, F-27) should be done as a pair since they depend on each other.

**Do not start Phase 3 until Phase 1 is complete.** The architectural work (DI, decomposition, CI) provides long-term benefits but does not unblock the safety and UX improvements that matter most to users now.

**Critical note on testing:** The 27-tool test gap (F-36) is the most significant strategic risk. Any refactoring work without test coverage is flying blind. Adding even basic happy-path + error-path tests for the high-risk tools should be treated as a parallel track to all other work.
