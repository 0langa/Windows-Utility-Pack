# IMPLEMENTATION AND REFACTOR PLAN

## 1. Objective

This plan defines an incremental, implementation-ready path to improve Windows Utility Pack’s correctness, stability, maintainability, and testability **without rewriting the application**. It is based on a fresh repository audit plus validation of the external audit in `docs/EXTERNAL_AUDIT_SUMMARY.md`.

The goal is to sequence work so that high-risk correctness issues are addressed first, architecture is clarified second, and broader maintainability improvements happen only after the riskiest workflows are protected.

## 2. Guiding Principles

- **Correctness first.** Fix behavior that is currently wrong or unsafe before performing cosmetic or structural cleanup.
- **Preserve behavior unless clearly broken.** Refactors should not silently change user-visible semantics.
- **Incremental changes only.** Avoid large, multi-axis rewrites.
- **No rewrite strategy.** This codebase is already functional and has enough structure to evolve in place.
- **Improve testability as part of implementation.** When touching risky code, add or strengthen focused automated coverage.
- **Prefer explicit dependencies.** Reduce `App.*` service location in favor of constructor- or bootstrapper-driven dependencies.
- **Maintain UI responsiveness.** Do not introduce blocking work on the UI thread.
- **Preserve runtime safety.** Keep the existing defensive posture around regex evaluation, file-system handling, and OS integrations.
- **Avoid overengineering.** Introduce abstractions only where they remove proven duplication or reduce actual risk.

## 3. Priority Breakdown

### Priority 1: Critical bug fixes / correctness / safety
- Fix theming system-following bug.
- Fix downloader progress accounting and define partial-download cleanup behavior.
- Harden unsafe recursive enumeration in duplicate detection and drive analysis.
- Add focused regression tests for those fixes.

### Priority 2: High-value architecture cleanup
- Reduce static `App.*` coupling in shell/home/settings composition.
- Establish one clear composition-root pattern.
- Make tool metadata authoritative in one place and use it to drive shell/home UI.

### Priority 3: Maintainability and duplication reduction
- Decompose large viewmodels/services by workflow.
- Consolidate storage traversal behavior.
- Improve async-command error/reporting consistency.

### Priority 4: Documentation and polish
- Update README and project metadata.
- Clarify root project-file intent or remove it.
- Align architecture docs with the actual implementation.

### Priority 5: Future strategic improvements
- Adaptive layout/high-DPI improvements.
- Analyzer/style normalization.
- Broader integration-style coverage for shell/workflow scenarios.

## 4. Detailed Workstreams

### Workstream A — Theming and resource behavior

**Goal**
- Make `AppTheme.System` behave correctly and predictably, including OS-theme follow mode.

**Scope**
- Theme subscription logic, effective-theme application, settings persistence around theme choice.

**In-scope files/components**
- `src/WindowsUtilityPack/Services/ThemeService.cs`
- `src/WindowsUtilityPack/ViewModels/MainWindowViewModel.cs`
- `src/WindowsUtilityPack/Views/SettingsWindow.xaml.cs`
- any theme-related tests added under `tests/WindowsUtilityPack.Tests`

**Out-of-scope boundaries**
- Major theme redesigns, palette changes, or visual refreshes
- dynamic shell generation work except where required for test setup

**Risks**
- Breaking theme-change notifications
- Leaving stale `SystemEvents` subscriptions behind
- accidentally changing persisted theme semantics

**Prerequisites**
- Add a small test seam around theme resolution/subscription behavior if needed

**Step-by-step implementation sequence**
1. Reorder `ThemeService.SetTheme()` so system-event subscription state is updated even when the effective theme does not change.
2. Verify explicit dark/light modes still behave the same.
3. Verify switching explicit dark/light → system while OS matches current theme now keeps future follow behavior.
4. Ensure repeated calls do not create duplicate event subscriptions.
5. If practical, add tests for explicit-to-system transitions and no-op same-theme transitions.

**Validation requirements**
- Build the solution.
- Run targeted theme tests.
- Manual verification on Windows: toggle Dark/Light/System and then change OS app theme.

**Recommended tests**
- `ThemeService_SetTheme_SystemModeSubscribesEvenWhenEffectiveThemeMatches`
- `ThemeService_SetTheme_SameExplicitThemeDoesNotDoubleSubscribe`
- `ThemeService_OnSystemPreferenceChanged_UpdatesEffectiveTheme`

---

### Workstream B — Downloader correctness

**Goal**
- Make downloader progress, speed, cancellation, and failure behavior internally consistent and user-safe.

**Scope**
- Byte-count accounting, cancellation cleanup policy, failure-state handling, and focused tests.

**In-scope files/components**
- `src/WindowsUtilityPack/Tools/NetworkInternet/Downloader/DownloaderViewModel.cs`
- related models/tests under `tests/WindowsUtilityPack.Tests`

**Out-of-scope boundaries**
- Multi-download scheduler redesign
- retry/backoff framework
- download queue persistence

**Risks**
- Regressing progress updates
- leaving open file handles during cancellation
- changing UI state transitions unexpectedly

**Prerequisites**
- Decide explicit policy for partial files on cancel/failure

**Step-by-step implementation sequence**
1. Split the cumulative byte counter from the speed-sampling counter.
2. Keep progress based on total bytes written since the download started.
3. Implement explicit partial-file handling for cancel/failure.
4. Ensure `IsDownloading`, `Status`, `Speed`, and collection state are reset consistently.
5. Add tests covering success, cancellation, and failure outcomes.

**Validation requirements**
- Build the solution.
- Run targeted downloader tests.
- Manual verification with a known downloadable file and a forced cancellation scenario.

**Recommended tests**
- `DownloaderViewModel_ProgressRemainsCumulativeAfterSpeedUpdate`
- `DownloaderViewModel_CancelMarksItemCancelledAndAppliesPartialFilePolicy`
- `DownloaderViewModel_FailureMarksItemFailed`

---

### Workstream C — Storage traversal and scan safety

**Goal**
- Make all storage-related traversal code as safe and resilient as `ScanEngine`.

**Scope**
- Duplicate detection, folder-size calculation, top-folder analysis, and any shared traversal helper introduced.

**In-scope files/components**
- `src/WindowsUtilityPack/Services/Storage/ScanEngine.cs`
- `src/WindowsUtilityPack/Services/Storage/DuplicateDetectionService.cs`
- `src/WindowsUtilityPack/Services/Storage/DriveAnalysisService.cs`
- related tests under `tests/WindowsUtilityPack.Tests/StorageMaster`

**Out-of-scope boundaries**
- Storage Master feature redesign
- report/export formatting changes
- USN journal or advanced filesystem optimizations

**Risks**
- Changing which files are included/excluded
- altering hidden/system/reparse-point semantics by accident
- performance regressions on very large trees

**Prerequisites**
- Decide whether to extract a shared traversal helper or inject traversal through an interface

**Step-by-step implementation sequence**
1. Document the current `ScanEngine` traversal semantics as the baseline behavior.
2. Replace `SearchOption.AllDirectories` usage with explicit recursion or a shared traversal utility using `EnumerationOptions.IgnoreInaccessible`.
3. Preserve cancellation support and skip inaccessible paths without crashing the operation.
4. Add tests that simulate inaccessible or broken subtrees as far as the test environment allows.
5. Re-run existing storage tests to ensure aggregate calculations still match expectations.

**Validation requirements**
- Build the solution.
- Run storage-related tests.
- Manual verification on a folder tree containing protected/system areas when possible.

**Recommended tests**
- `DuplicateDetectionService_SkipsInaccessiblePathsWithoutFailingWholeScan`
- `DriveAnalysisService_GetFolderSizeAsync_SkipsUnreadableEntries`
- `DriveAnalysisService_GetTopFoldersBySize_IgnoresProtectedSubtrees`

---

### Workstream D — Dependency injection / `App.*` coupling reduction

**Goal**
- Replace hidden service-location dependencies with explicit dependency flow in the shell and UI entry points.

**Scope**
- Shell construction, home viewmodel construction, settings dialog wiring, service bootstrap strategy.

**In-scope files/components**
- `src/WindowsUtilityPack/App.xaml.cs`
- `src/WindowsUtilityPack/MainWindow.xaml.cs`
- `src/WindowsUtilityPack/ViewModels/HomeViewModel.cs`
- `src/WindowsUtilityPack/ViewModels/MainWindowViewModel.cs`
- `src/WindowsUtilityPack/Views/SettingsWindow.xaml.cs`
- `src/WindowsUtilityPack/Services/NavigationService.cs`

**Out-of-scope boundaries**
- Replacing every service in the app at once
- introducing plugin loading or MEF as part of the same change

**Risks**
- Breaking startup wiring
- accidentally introducing multiple instances of singleton-intended services
- making WPF DataTemplate instantiation incompatible with current navigation

**Prerequisites**
- Decide the target composition strategy: explicit bootstrapper object or `Microsoft.Extensions.DependencyInjection`

**Step-by-step implementation sequence**
1. Establish one authoritative composition root in startup.
2. Stop creating new dependencies implicitly from static access where a constructor dependency already exists.
3. Remove fallback `App.NavigationService` usage from `HomeViewModel` by ensuring home viewmodels are created through registered factories.
4. Move `MainWindow` and settings-window construction toward explicit dependency passing.
5. Leave a minimal compatibility layer only where WPF framework constraints require it.

**Validation requirements**
- Build the solution.
- Run navigation and affected viewmodel tests.
- Manual startup verification: app launch, home navigation, theme toggle, settings dialog.

**Recommended tests**
- `HomeViewModel_UsesInjectedNavigationService`
- `MainWindowViewModel_DoesNotRequireStaticSettingsAccess`
- integration-style startup/navigation tests if feasible

---

### Workstream E — Large class decomposition

**Goal**
- Reduce maintenance hotspots by extracting coherent workflow pieces without behavior changes.

**Scope**
- `StorageMasterViewModel`, `TextFormatConverterViewModel`, `TextFormatConversionService`, and potentially `StorageMasterView.xaml`.

**In-scope files/components**
- `src/WindowsUtilityPack/Tools/SystemUtilities/StorageMaster/StorageMasterViewModel.cs`
- `src/WindowsUtilityPack/Tools/DeveloperProductivity/TextFormatConverter/TextFormatConverterViewModel.cs`
- `src/WindowsUtilityPack/Services/TextConversion/TextFormatConversionService.cs`
- `src/WindowsUtilityPack/Tools/SystemUtilities/StorageMaster/StorageMasterView.xaml`

**Out-of-scope boundaries**
- Feature rewrites
- changing supported conversion matrix rules unless explicitly required

**Risks**
- Breaking command enablement/state transitions
- moving logic into abstractions that are harder to follow than the current code
- introducing partial refactors that increase indirection without reducing size meaningfully

**Prerequisites**
- Add or strengthen targeted tests around each hotspot before extraction

**Step-by-step implementation sequence**
1. Identify natural seams by workflow, not by file size alone.
2. For `StorageMasterViewModel`, split scan orchestration, duplicate analysis, cleanup orchestration, snapshot/export behavior, and summary/filter state.
3. For `TextFormatConverterViewModel`, split input-state management, conversion workflow, preview/export workflow, and messaging/state refresh logic.
4. For `TextFormatConversionService`, split by format family or pipeline stage while preserving the public interface.
5. For very large XAML, extract reusable sections/templates only after the backing state is stable.

**Validation requirements**
- Build the solution after each extraction step.
- Run existing text-conversion and storage tests after each stage.
- Avoid bundling multiple hotspot decompositions into one change.

**Recommended tests**
- Extend existing `TextFormatConverterViewModelTests` and `TextFormatConversionServiceTests`
- Add focused `StorageMasterViewModel` tests for scan/duplicate/cleanup/snapshot/export flows

---

### Workstream F — Tool metadata consolidation

**Goal**
- Eliminate shell/home metadata duplication and make tool registration the single source of truth.

**Scope**
- `ToolDefinition`, `ToolRegistry`, shell menu generation, home-card generation, and any required metadata enrichment.

**In-scope files/components**
- `src/WindowsUtilityPack/Models/ToolDefinition.cs`
- `src/WindowsUtilityPack/Tools/ToolRegistry.cs`
- `src/WindowsUtilityPack/App.xaml.cs`
- `src/WindowsUtilityPack/MainWindow.xaml` and related controls
- `src/WindowsUtilityPack/Views/HomeView.xaml`

**Out-of-scope boundaries**
- Dynamic plugin loading
- replacing explicit WPF DataTemplates with reflection-heavy auto-template generation unless it proves necessary

**Risks**
- Breaking menu ordering or discoverability
- introducing UI churn while solving a metadata problem
- overdesigning the registry model prematurely

**Prerequisites**
- Decide minimum metadata needed for shell/home generation (label, category, icon, description, ordering, visibility)

**Step-by-step implementation sequence**
1. Expand `ToolDefinition` only with fields needed by real UI surfaces.
2. Build a registry-backed model for category/grouped tool presentation.
3. Move home cards to generated items first.
4. Move category menus to generated items second.
5. Keep explicit DataTemplates unless there is a strong reason to change them separately.

**Validation requirements**
- Build the solution.
- Run navigation tests.
- Manual verification: all tools remain discoverable in shell and home UI, ordering is stable, keys navigate correctly.

**Recommended tests**
- `ToolRegistry_MetadataMatchesVisibleShellEntries`
- `HomeDashboard_UsesRegisteredToolsOnly`
- `CategoryGrouping_ReturnsExpectedToolSets`

---

### Workstream G — Async / cancellation hardening

**Goal**
- Make async command execution and user-facing error handling more consistent.

**Scope**
- `AsyncRelayCommand`, command consumers with inconsistent exception handling, and logging/status integration.

**In-scope files/components**
- `src/WindowsUtilityPack/Commands/AsyncRelayCommand.cs`
- viewmodels using `AsyncRelayCommand`
- optional logging/notification hooks if introduced

**Out-of-scope boundaries**
- complete command framework replacement
- broad event-bus/message-bus introduction

**Risks**
- Accidentally suppressing legitimate exceptions without reporting
- changing command reentrancy semantics
- introducing UI-thread deadlocks if awaiting is mishandled

**Prerequisites**
- Decide desired shared policy: log only, log + dialog, or log + status surface

**Step-by-step implementation sequence**
1. Add an `ExecuteAsync` path so tests and callers can observe command completion explicitly where useful.
2. Keep `Execute` as the `ICommand` bridge only.
3. Introduce optional shared exception callback/logging.
4. Adopt the shared policy gradually in the highest-risk commands first.
5. Revisit silent-ignore navigation behavior and convert it to at least log a warning.

**Validation requirements**
- Build the solution.
- Run command/viewmodel tests.
- Manually verify commands do not become reentrant unexpectedly.

**Recommended tests**
- `AsyncRelayCommand_ClearsExecutingStateAfterFailure`
- `AsyncRelayCommand_ReportsUnhandledExceptionViaConfiguredHook`
- `NavigationService_UnknownKey_LogsWarning`

---

### Workstream H — Test coverage improvements

**Goal**
- Add protection around the areas most likely to regress during the planned refactors.

**Scope**
- Theme behavior, downloader workflows, unsafe traversal services, shell/viewmodel interactions, cleanup/delete flows.

**In-scope files/components**
- `tests/WindowsUtilityPack.Tests/**/*`

**Out-of-scope boundaries**
- Full UI automation suite
- snapshot/golden-image visual testing unless explicitly adopted later

**Risks**
- Creating brittle tests tied to implementation details instead of behaviors
- adding tests that require unavailable platform features in CI

**Prerequisites**
- Establish small test seams for OS/event-driven behavior as needed

**Step-by-step implementation sequence**
1. Add tests for current correctness defects before large refactors.
2. Add integration-style tests only where unit seams are insufficient.
3. Prefer deterministic service/viewmodel tests over UI automation.
4. Reuse existing test-double patterns already present in viewmodel tests.
5. Keep new tests scoped to observable behavior, not internal method structure.

**Validation requirements**
- Run targeted tests during each workstream.
- Re-run broader solution tests at milestone boundaries.

**Recommended tests**
- Theme, downloader, drive-analysis, duplicate-detection, main-window/shell composition, cleanup-delete scenarios

---

### Workstream I — Documentation and metadata cleanup

**Goal**
- Make repository documentation and project metadata accurately describe the current application.

**Scope**
- README, project files, and architecture/refactor documentation as code changes land.

**In-scope files/components**
- `README.md`
- `WindowsUtilityPack.csproj`
- `src/WindowsUtilityPack/WindowsUtilityPack.csproj`
- relevant docs under `docs/`

**Out-of-scope boundaries**
- marketing rewrite
- non-engineering documentation not related to actual implementation state

**Risks**
- Leaving stale references after refactors
- documenting intended architecture before it actually exists

**Prerequisites**
- Decide the fate of the root-level project file

**Step-by-step implementation sequence**
1. Correct README tool list, build command, and architecture notes.
2. Clarify or remove the root-level `WindowsUtilityPack.csproj`.
3. Update docs when composition, traversal, or shell-generation changes are implemented.
4. Keep documentation aligned with shipped behavior rather than aspirational design.

**Validation requirements**
- Manual doc review for consistency with code.
- Build commands in docs should be executed before marking complete.

**Recommended tests**
- No automated tests required, but command accuracy should be verified manually.

## 5. Implementation Order

Recommended execution order:

1. **Workstream A — Theming and resource behavior**
2. **Workstream B — Downloader correctness**
3. **Workstream C — Storage traversal and scan safety**
4. **Workstream H — Test coverage improvements** for the above areas (iteratively alongside A-C)
5. **Workstream D — Dependency injection / `App.*` coupling reduction**
6. **Workstream F — Tool metadata consolidation**
7. **Workstream G — Async / cancellation hardening**
8. **Workstream E — Large class decomposition**
9. **Workstream I — Documentation and metadata cleanup** (ongoing, with a completion pass at the end)

This order front-loads correctness and safety, then removes architectural obstacles, and only then tackles larger decomposition work.

## 6. Validation Strategy

Future implementation should be verified with a combination of:

- **Build validation**
  - `dotnet build WindowsUtilityPack.sln`
- **Automated tests**
  - targeted tests for the workstream being changed
  - broader `dotnet test WindowsUtilityPack.sln --no-build` at milestone boundaries
- **Targeted manual verification on Windows**
  - theme switching and OS-theme follow behavior
  - downloader success/cancel/failure flows
  - storage scan/duplicate detection on representative folder trees
  - shell navigation/home card behavior after metadata consolidation
  - settings persistence and window-state restore behavior
- **Regression checks**
  - confirm existing text-conversion and storage tests still pass after shared-service or decomposition work
- **Edge-case checks**
  - inaccessible directories
  - cancellation during long-running operations
  - repeated theme toggles
  - unknown navigation keys
  - cleanup/delete partial failure scenarios

## 7. Refactoring Safety Rules

- **No partial refactors** that leave old and new patterns mixed inside the same workflow without a clear migration boundary.
- **No hidden behavior changes** unless they are explicitly documented and justified.
- **No duplicate abstractions** added just to mirror existing code.
- **No swallowed exceptions without reason**; if failure must be non-fatal, log or surface it appropriately.
- **No UI thread blocking** for file, network, or scan operations.
- **No speculative architecture** (plugins, event buses, reflection-heavy auto-wiring) unless a concrete problem requires it.
- **No careless contract changes** to public/internal service interfaces used by existing tests and workflows.
- **No large hotspot decomposition without tests** covering the behavior being extracted.
- **No metadata duplication reintroduced** after shell/home generation is centralized.

## 8. Completion Criteria

This plan can be considered successfully executed when all of the following are true:

- the theme system-following bug is fixed and covered by tests
- downloader progress/cancellation behavior is correct and covered by tests
- duplicate detection and drive analysis no longer rely on unsafe recursive enumeration patterns
- shell/home metadata is driven from a single authoritative source
- `App.*` service-location usage is materially reduced in shell-facing code
- major hotspot classes have been decomposed where needed without behavior regressions
- README and project metadata accurately describe the current repository state
- the repository builds cleanly and relevant automated tests pass after each completed workstream
- manual Windows verification confirms no regressions in startup, navigation, theming, storage workflows, and downloader workflows
