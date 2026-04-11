# Audit Summary

Date: 2026-04-11

## What Was Broken

- The shell command palette keyboard handler existed in [MainWindow.xaml.cs](/C:/Users/juliu/source/repos/Windows-Utility-Pack/src/WindowsUtilityPack/MainWindow.xaml.cs) but was not wired in [MainWindow.xaml](/C:/Users/juliu/source/repos/Windows-Utility-Pack/src/WindowsUtilityPack/MainWindow.xaml), so `Esc`, `Enter`, `Up`, and `Down` did not reliably drive the in-window palette.
- Opening the in-window command palette also triggered the detached command palette host window, creating overlapping command-palette surfaces and competing focus paths.
- The dimming command-palette backdrop did not dismiss the overlay, leaving a visible modal-style surface without a matching close interaction.

## What Was Fixed

- Wired `PreviewKeyDown` in the shell window so the existing command-palette keyboard interaction path is active.
- Reworked the shell command-palette request flow so the in-window palette now focuses its search box instead of opening the detached palette window.
- Added backdrop click dismissal for the in-window command palette.

## Interaction Issues Found

- Command-palette keyboard navigation and execution were effectively broken because the shell never received preview key events.
- The shell had duplicate command-palette UX paths for a single action:
  the in-window overlay and the detached topmost palette could open from the same request.
- The command-palette overlay lacked a direct pointer-based dismiss action on its backdrop.

## Binding And Command Issues Found

- The main shell command path was partially wired:
  the XAML defined the palette UI and the code-behind defined the palette key handling, but the event hookup was missing.
- The command-palette focus request path was routed to the detached palette host instead of the shell overlay that owned the visible bound state.

## Tests Added

- Shell regression coverage for `MainWindowViewModel`:
  command-palette focus request event, shell-action execution paths, clipboard-manager navigation, and detached-window command enablement/status flow.
- XAML wiring regression tests for shell preview-key handling and command-palette backdrop dismissal.
- `CommandPaletteWindowViewModel` tests for fresh activation reset and command enablement tracking.
- `HomeViewModel` tests for copy-name, description fallback, and synonym-based search behavior.
- `ToolWindowHostService` tests for unknown tools and `CloseAll()` cleanup.

## Validation

- `dotnet build WindowsUtilityPack.sln`
- `dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj`

## Future Improvement Areas

- More tool-specific UI interaction coverage is still warranted for currently untested viewmodels such as `StartupManagerViewModel`, `SystemInfoViewModel`, `WorkspaceProfilesViewModel`, and the image/file utility shells.
- Several debounced `async void` scheduling helpers remain in tool viewmodels; they are intentional UI bridges today, but they are still worth a focused hardening pass if these tools continue growing.
- Broader automated UI verification for window-level flows would add confidence around tray mode, global hotkeys, and detached tool-window behavior.
