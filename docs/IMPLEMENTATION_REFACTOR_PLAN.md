# IMPLEMENTATION AND REFACTOR PLAN (2026-04-08)

> **Status (2026-04-11):** Active roadmap — workstreams not yet started. Cross-reference `docs/REPO_RECOVERY_AUDIT.md` (2026-04-10) for the most recent list of incomplete tool registrations that should be resolved alongside this plan.

## 1. Objective

This plan defines the next implementation steps for Windows Utility Pack based on the current validated baseline:

- build passing,
- tests passing (196/196),
- several prior critical defects already resolved.

The focus now is architecture hardening, maintainability, and delivery safety automation.

## 2. Current Baseline State

### Already completed (do not re-prioritize)

- Theme system-follow behavior fix in `ThemeService`.
- Safer recursive traversal in duplicate/drive analysis paths.
- Registry-driven generation of shell categories and home cards.
- Expanded unit-test surface including theme/downloader/storage service coverage.

### Remaining top risks

1. Static service-location coupling (`App.*`) is still widespread.
2. Large hotspot files are expensive and risky to evolve.
3. No CI workflow currently enforces build/tests on PR/push.
4. Root shim/app project package metadata drift remains.

## 3. Guiding Principles

- Keep changes incremental and reversible.
- Preserve behavior unless explicitly fixing a defect.
- Add/extend tests before splitting risky hotspots.
- Prefer explicit dependencies over hidden global access.
- Avoid framework churn unless it unlocks measurable benefit.

## 4. Workstreams

### Workstream A - Composition hardening (High)

Goal:

- Reduce hidden dependencies and static service access in shell-facing code.

Primary scope:

- `src/WindowsUtilityPack/App.xaml.cs`
- `src/WindowsUtilityPack/ViewModels/HomeViewModel.cs`
- `src/WindowsUtilityPack/ViewModels/MainWindowViewModel.cs`
- `src/WindowsUtilityPack/Views/SettingsWindow.xaml.cs`

Implementation steps:

1. Define one explicit composition pattern for shell/home/settings construction.
2. Remove fallback static navigation dependency in `HomeViewModel` if feasible.
3. Move settings/theme persistence paths toward injected services.
4. Keep compatibility layers only where WPF constraints require them.

Validation:

- Build + existing tests.
- Manual startup/navigation/settings/theme checks on Windows.

---

### Workstream B - Hotspot decomposition (High)

Goal:

- Lower change risk and review cost in oversized files.

Primary scope:

- `Services/TextConversion/TextFormatConversionService.cs`
- `Services/Downloader/WebScraperService.cs`
- `Tools/SystemUtilities/StorageMaster/StorageMasterViewModel.cs`
- `Tools/DeveloperProductivity/TextFormatConverter/TextFormatConverterViewModel.cs`
- optional XAML extraction where it improves maintainability

Implementation steps:

1. Add behavior tests around each target workflow before extraction.
2. Split by workflow boundary (coordinator/service/helper), not by arbitrary line count.
3. Ship each extraction in isolated PR-sized increments.
4. Re-run targeted tests after every extraction slice.

Validation:

- Build + targeted test suites at each slice.
- Regression checks for affected tool flows.

---

### Workstream C - CI enforcement (High)

Goal:

- Establish automated quality gates for every PR/push.

Primary scope:

- `.github/workflows/*.yml` (new)

Implementation steps:

1. Add workflow to run restore/build/test.
2. Ensure workflow uses the same solution/test entry points used locally.
3. Fail fast on build/test regressions.
4. Add caching only after baseline workflow is stable.

Validation:

- Open test PR and verify required checks execute and block on failure.

---

### Workstream D - Shim/app metadata alignment (Medium)

Goal:

- Remove ambiguity between the shim project and app project dependencies.

Primary scope:

- `/WindowsUtilityPack.csproj`
- `src/WindowsUtilityPack/WindowsUtilityPack.csproj`
- supporting documentation (`README.md`/`docs`)

Implementation steps:

1. Decide authoritative package-version policy.
2. Align or explicitly justify differences (`Newtonsoft.Json`, `PdfPig` package identity).
3. Document intended shim behavior and non-goals clearly.

Validation:

- `dotnet restore` in both Windows and non-Windows-capable environments where possible.

---

### Workstream E - Integration coverage for remaining risk zones (Medium)

Goal:

- Improve confidence in cross-component behavior not fully covered by unit tests.

Primary scope:

- Shell startup/navigation/settings integration paths
- Downloader engine execution boundaries

Implementation steps:

1. Add focused integration-style tests for shell startup and navigation state transitions.
2. Add downloader execution tests around process-launch parsing/error handling seams.
3. Avoid brittle UI automation unless a unit/integration seam is impossible.

Validation:

- Stable test runs without environment-dependent flakiness.

---

### Workstream F - Repository hygiene and contributor clarity (Low)

Goal:

- Reduce non-product noise and improve contributor onboarding.

Primary scope:

- `.github` structure and related docs

Implementation steps:

1. Clarify what `.github` content is required for product engineering vs assistant tooling.
2. Consolidate or document high-noise directories.
3. Keep product-relevant contributor docs close to the app/test projects.

Validation:

- New contributor can locate build/test/app entry points quickly.

## 5. Recommended Execution Order

1. Workstream C - CI enforcement
2. Workstream A - Composition hardening (first slice)
3. Workstream D - Shim/app metadata alignment
4. Workstream B - Hotspot decomposition (iterative)
5. Workstream E - Integration coverage expansion
6. Workstream F - Repository hygiene

Reasoning:

- CI first prevents future regression drift while refactors proceed.
- Composition and metadata work remove hidden coupling and ambiguity early.
- Hotspot decomposition should run with CI and tests already in place.

## 6. Milestone Exit Criteria

### Milestone 1

- CI workflow live and green on PR.
- Build/test parity with local commands confirmed.

### Milestone 2

- Shell/home/settings paths rely less on static `App.*` access.
- No regression in startup/navigation/theme/settings behavior.

### Milestone 3

- At least two major hotspots decomposed with no behavior regressions.
- Added tests cover extracted workflow boundaries.

### Milestone 4

- Shim/app dependency drift resolved or explicitly policy-documented.
- Integration coverage added for shell and downloader execution seams.

## 7. Completion Criteria

This plan is complete when:

- build/tests are enforced in CI,
- static service-location coupling is materially reduced,
- hotspot files are broken into maintainable workflow units,
- shim/app metadata differences are intentional and documented,
- and integration-level confidence is improved in shell/downloader high-risk paths.
