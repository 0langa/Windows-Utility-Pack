# Platform Foundations Phase 1

Date: 2026-04-11

## Summary

Phase 1 introduces shared platform primitives required for the next roadmap wave:
- SQLite-backed local data store with schema migration support.
- Centralized activity logging.
- Workspace profile persistence.
- Global shell command palette service and UI.
- Shared background task framework and first migrations.
- Clipboard Manager tool with persistent history.

This phase is intentionally incremental and architecture-safe: it extends current composition style while avoiding broad rewrites of existing tools.

## New Services

### AppDataStoreService

Files:
- `src/WindowsUtilityPack/Services/IAppDataStoreService.cs`
- `src/WindowsUtilityPack/Services/AppDataStoreService.cs`

Responsibilities:
- Owns shared database path under LocalApplicationData.
- Ensures directory and database creation.
- Applies versioned schema migrations via `PRAGMA user_version`.
- Exposes opened per-operation connections for higher-level services.

Initial schema (migration v1):
- `activity_log`
- `workspace_profiles`

Migration v2:
- `clipboard_history`

### ActivityLogService

Files:
- `src/WindowsUtilityPack/Services/IActivityLogService.cs`
- `src/WindowsUtilityPack/Services/ActivityLogService.cs`

Responsibilities:
- Persists auditable events with category/action/details/sensitive flag.
- Supports filtered and bounded recent-event queries.

Current shell integration:
- Tool navigation events are logged.
- Command palette execution events are logged.

### WorkspaceProfileService

Files:
- `src/WindowsUtilityPack/Services/IWorkspaceProfileService.cs`
- `src/WindowsUtilityPack/Services/WorkspaceProfileService.cs`

Responsibilities:
- Persists named workspace profiles with startup tool and pinned tool keys.
- Supports create/update/delete/list operations.
- Stores pinned-tool arrays as JSON payloads.

### CommandPaletteService

File:
- `src/WindowsUtilityPack/Services/ICommandPaletteService.cs`

Responsibilities:
- Produces searchable command items for tools and shell actions.
- Provides lightweight relevance scoring across title/subtitle/category/key/keywords.

### BackgroundTaskService

Files:
- `src/WindowsUtilityPack/Services/IBackgroundTaskService.cs`
- `src/WindowsUtilityPack/Services/BackgroundTaskService.cs`
- `src/WindowsUtilityPack/Models/BackgroundTaskModels.cs`

Responsibilities:
- Tracks cancellable long-running tasks with IDs and lifecycle states.
- Publishes progress snapshots and completion/failure transitions.
- Provides bounded finished-task history for diagnostic and UX surfaces.

Current integrations:
- Storage Master scan lifecycle.
- Port Scanner scan lifecycle.

### ClipboardHistoryService

File:
- `src/WindowsUtilityPack/Services/IClipboardHistoryService.cs`

Responsibilities:
- Stores clipboard history entries in local SQLite store.
- Prevents adjacent duplicate entries.
- Supports delete and clear operations.

### Clipboard Manager Tool

Files:
- `src/WindowsUtilityPack/Tools/DeveloperProductivity/ClipboardManager/ClipboardManagerViewModel.cs`
- `src/WindowsUtilityPack/Tools/DeveloperProductivity/ClipboardManager/ClipboardManagerView.xaml`
- `src/WindowsUtilityPack/Tools/DeveloperProductivity/ClipboardManager/ClipboardManagerView.xaml.cs`

Capabilities:
- Persistent clipboard history list.
- Auto-monitoring and capture of text clipboard changes.
- Search, copy selected, delete selected, clear all.

## Shell Integration

Main shell updates:
- `Ctrl+K` opens command palette.
- `Escape` closes palette.
- `Enter` executes selected result.
- Search supports both tool navigation and shell actions (home/settings).

Files:
- `src/WindowsUtilityPack/ViewModels/MainWindowViewModel.cs`
- `src/WindowsUtilityPack/MainWindow.xaml`
- `src/WindowsUtilityPack/MainWindow.xaml.cs`
- `src/WindowsUtilityPack/App.xaml.cs`

## Dependency Changes

Added:
- `Microsoft.Data.Sqlite` in `src/WindowsUtilityPack/WindowsUtilityPack.csproj`.

Reason:
- Required for shared local persistence/migration foundation.

## Test Coverage Added

New tests:
- `tests/WindowsUtilityPack.Tests/Services/AppDataStoreServiceTests.cs`
- `tests/WindowsUtilityPack.Tests/Services/ActivityLogServiceTests.cs`
- `tests/WindowsUtilityPack.Tests/Services/WorkspaceProfileServiceTests.cs`
- `tests/WindowsUtilityPack.Tests/Services/CommandPaletteServiceTests.cs`
- `tests/WindowsUtilityPack.Tests/Services/BackgroundTaskServiceTests.cs`
- `tests/WindowsUtilityPack.Tests/Services/ClipboardHistoryServiceTests.cs`

Coverage focus:
- Migration/table initialization.
- Activity event persistence and category filtering.
- Workspace profile CRUD/update behavior.
- Command palette baseline search behavior.
- Background task lifecycle and cancellation semantics.
- Clipboard history persistence and dedup behavior.