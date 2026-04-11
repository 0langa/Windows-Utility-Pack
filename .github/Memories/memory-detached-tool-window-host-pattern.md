# Memory: Detached tool window host pattern

## Metadata

- PatternId: MEMORY-DETACHED-TOOL-WINDOW-HOST
- PatternVersion: 1
- Status: active
- Supersedes:
- CreatedAt: 2026-04-11
- LastValidatedAt: 2026-04-11
- ValidationEvidence: Full build/test pass after adding detached window host service and shell integration.

## Source Context

- Triggering task: Continue implementation wave for multi-window hosting.
- Scope/system: Shell and navigation extensibility.
- Date/time: 2026-04-11

## Memory

- Key fact or decision: Use `ToolRegistry` + `ToolWindowHostService` to open/activate one detached window per tool key.
- Why it matters: Enables multi-window workflows without changing tool view models or data templates.

## Applicability

- When to reuse: Any tool requiring long-running side-by-side monitoring while user navigates elsewhere.
- Preconditions/limitations: Home dashboard remains non-detachable by design.

## Actionable Guidance

- Recommended future action: Route additional shell actions through command palette keys (e.g., detach selected tool) to keep behavior discoverable.
- Related files/services/components: `Services/IToolWindowHostService.cs`, `ViewModels/MainWindowViewModel.cs`, `MainWindow.xaml`.