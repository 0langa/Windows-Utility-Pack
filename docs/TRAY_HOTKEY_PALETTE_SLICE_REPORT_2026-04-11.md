# Tray / Hotkey / Command Palette Slice Report (2026-04-11)

## Scope Delivered

This slice upgrades shell behavior for tray mode, global hotkeys, and command-palette access while preserving existing MVVM and service-based architecture.

## Key Changes

### 1) Global hotkey platform (true background-capable)
- Added Win32 `RegisterHotKey` infrastructure:
  - `IGlobalHotkeyService`
  - `GlobalHotkeyService`
- Hotkeys are now system-wide while the app is running (including hidden-to-tray state).
- Registration lifecycle:
  - start on app startup
  - refresh on settings/bindings changes
  - unregister on disposal
- Failures are captured as `HotkeyRegistrationIssue` entries for user visibility.

### 2) Tray platform service and richer tray menu
- Added `ITrayIconService` + `TrayIconService`.
- Tray menu now includes:
  - Open Main Window
  - Open Command Palette
  - Quick Screenshot
  - Open Screenshot Annotator
  - Open Clipboard Manager
  - Enable/Disable Global Hotkeys
  - Exit
- `MainWindow` now consumes tray service abstractions instead of directly owning `NotifyIcon` wiring details.

### 3) Command palette shell actions and relevance
- Extended command palette shell actions:
  - Quick Screenshot
  - Open Screenshot Annotator
  - Toggle Main Window
  - Open Clipboard Manager
- Added execution-aware ranking via `RecordExecution`.
- Added shortcut hint support in palette items.
- UI now shows shortcut hint badges where available.

### 4) Quick screenshot global workflow
- Added:
  - `IQuickScreenshotService`
  - `QuickScreenshotService`
  - `IQuickCaptureStateService`
  - `QuickCaptureStateService`
- Global quick screenshot now captures immediately even when app is hidden.
- Supports behavior setting:
  - Capture to file + clipboard
  - Capture to file + open annotator
- Latest quick capture path is handed off to Screenshot Annotator.

### 5) Settings and behavior controls
- Extended `AppSettings` with tray/global shell options:
  - `CloseToTray`
  - `StartMinimizedToTray`
  - `RestoreMainWindowOnGlobalAction`
  - `QuickScreenshotBehavior`
  - `QuickScreenshotOutputDirectory` (future-facing)
- Updated `SettingsWindowViewModel` and `SettingsWindow.xaml` to manage these options.
- `TrayModeCoordinator` now respects `CloseToTray` and supports start-minimized logic.

### 6) Hotkey manager integration upgrades
- `HotkeyManagerViewModel` now optionally integrates with `IGlobalHotkeyService`.
- Save now refreshes global registrations immediately.
- UI surfaces registration issues to explain collisions/blocked gestures.

## New / Updated Defaults

Default hotkeys now include:
- Open command palette: `Ctrl+K`
- Quick screenshot: `Ctrl+Shift+S`
- Toggle main window: `Ctrl+Shift+Space`
- Open settings: `Ctrl+OemComma`
- Navigate home: `Ctrl+H`
- Open activity log: `Ctrl+Shift+L`
- Open task monitor: `Ctrl+Shift+M`

## Notable UX Decisions

- Command palette remains in the main shell overlay for consistency, but is now globally summonable via true global hotkey by restoring the main window when configured.
- Global actions are routed through central shell execution in `MainWindow` to keep behavior predictable for tray/background use.
- Tray “toggle hotkeys” is immediate and updates registration state through the shared hotkey/global-hotkey services.

## Validation

Mandatory commands run:
- `dotnet build WindowsUtilityPack.sln`
- `dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj`

Both passed in this delivery.

## Known Limitations

- Quick screenshot output directory is currently settings-backed but does not yet expose a dedicated folder picker in the settings window.
