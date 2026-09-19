# CLAUDE.md

## Project identity
Windows Utility Pack: C# / .NET 10 / WPF / MVVM desktop app with xUnit tests.

Goals for all work:
- preserve build stability
- preserve existing behavior unless task explicitly changes it
- prioritize reliability, responsiveness, maintainability, polished desktop UX
- prefer incremental refactors over broad rewrites

## Repository shape
Locations:
- `src/WindowsUtilityPack/` = app source
- `src/WindowsUtilityPack/Tools/` = feature/tool implementations
- `src/WindowsUtilityPack/Services/` = shared services
- `src/WindowsUtilityPack/ViewModels/` = MVVM view models
- `src/WindowsUtilityPack/Views/` = WPF views
- `src/WindowsUtilityPack/Resources/` and `Themes/` = styles and theme resources
- `tests/WindowsUtilityPack.Tests/` = automated tests
- `docs/` = feature and audit docs
- `.github/instructions/` and `.github/skills/` = existing repo guidance

## Working style
- Analyze relevant code path before editing.
- Reuse existing patterns and infrastructure; no parallel systems.
- Fix root causes, not visible symptoms.
- Keep changes coherent and production-safe.
- Avoid speculative rewrites.
- Keep outputs concise unless user explicitly asks for long explanation.
- Do not dump full file contents unless needed.
- Ask questions only when ambiguity would cause incorrect or risky implementation.

## Build and validation
Validate affected work with:
- `dotnet build WindowsUtilityPack.sln`
- `dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj`

Build the solution before finishing any change that touches code, XAML, or project files. Doc- and comment-only changes do not need a build.
Do not claim success if build or tests fail.

## Architecture rules
- Preserve MVVM boundaries.
- Keep business logic out of code-behind.
- Keep views declarative and lightweight.
- Put workflow logic in view models and services.
- Use constructor injection where practical.
- Avoid new global/static coupling unless established project pattern exists.
- Prefer extending existing services over duplicating.
- Keep naming and file organization consistent with nearby code.

## Tool registration rules
When adding or changing tools:
- use `ToolRegistry` as source of truth
- keep startup and navigation wiring consistent
- register new tools in `App.xaml.cs` if required by existing patterns
- ensure matching `DataTemplate` mappings exist in `App.xaml`
- do not create duplicate tool metadata for homepage or one feature

## WPF and UI rules
- Use `DynamicResource` for theme-sensitive brushes and styles.
- Preserve dark/light theme compatibility.
- Avoid hard-coded colors, spacing, sizes unless strong reason.
- Reuse styles from `Resources/Styles.xaml`, `InputStyles.xaml`, and related theme resources before adding new ones.
- Keep keyboard navigation, tab order, and focus behavior usable.
- Avoid fragile popup, dropdown, flyout, or overlay behavior.
- Design desktop-first; layout stays stable under resize, DPI scaling, and varied display settings.
- Reduce wasted space and visual clutter.
- Prefer compact, high-value UI over oversized decorative containers.

## Homepage and dashboard rules
- Keep homepage tool-first.
- Favorites, recently used, category access, and search rely on shared tool metadata, not duplicated definitions.
- Homepage personalization must persist safely and fail gracefully on missing or corrupt settings data.
- Do not break existing navigation when changing homepage UX.
- Avoid horizontal scrolling for core category discovery when cleaner visible layout is practical.

## Async and responsiveness rules
- Use async for I/O and long-running operations.
- Do not block UI thread with heavy work.
- Marshal UI-bound updates back to UI thread where required.
- Handle cancellation where relevant.
- Avoid fire-and-forget unless failure handled safely and intentional.

## Safety and robustness
- Validate all user input.
- Validate file paths and external content before processing.
- Handle null, empty, invalid, and missing-data cases explicitly.
- Fail gracefully on file I/O, permissions, parsing, serialization, and environment-dependent operations.
- Admin-sensitive features degrade safely when elevation or access unavailable.
- Do not log secrets or sensitive values.
- Prefer user-safe error messages with actionable guidance over raw exception text.

## State, settings, and persistence
- Preserve existing settings behavior unless task explicitly changes it.
- Keep persisted data formats backward-compatible where practical.
- Treat corrupt or missing settings/state files as recoverable.
- Do not silently discard important user state without reason.

## Testing rules
- Add or update tests for new service logic, validation, parsing, and non-trivial state behavior.
- Keep tests deterministic; avoid machine-specific assumptions where possible.
- Test invalid inputs, error paths, cancellation, and persistence edge cases when relevant.
- If change is hard to test, create seam instead of skipping tests.

## Code quality
- Follow nullable reference safety.
- Prefer simple, explicit code over clever abstractions.
- Remove dead code left by refactor.
- Avoid duplicated logic.
- Add XML docs for new public APIs and non-obvious behavior.
- Keep comments useful and durable; do not narrate obvious code.

## Delivery standard
Before finishing any task:
- ensure solution builds
- run relevant tests
- check for nearby regressions
- verify bindings and runtime flow in affected WPF areas
- verify theme and layout behavior if UI was touched

Final responses concise, include:
- what changed
- important validation performed
- any remaining risks or blockers
