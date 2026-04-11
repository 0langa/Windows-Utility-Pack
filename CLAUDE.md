# CLAUDE.md

## Project identity
Windows Utility Pack is a C# / .NET 10 / WPF / MVVM desktop application with xUnit tests.

Primary goals for all work:
- preserve build stability
- preserve existing behavior unless the task explicitly changes it
- prioritize reliability, responsiveness, maintainability, and polished desktop UX
- prefer incremental refactors over broad rewrites

## Repository shape
Important locations:
- `src/WindowsUtilityPack/` = app source
- `src/WindowsUtilityPack/Tools/` = feature/tool implementations
- `src/WindowsUtilityPack/Services/` = shared services
- `src/WindowsUtilityPack/ViewModels/` = MVVM view models
- `src/WindowsUtilityPack/Views/` = WPF views
- `src/WindowsUtilityPack/Resources/` and `Themes/` = styles and theme resources
- `tests/WindowsUtilityPack.Tests/` = automated tests
- `docs/` = feature and audit documentation
- `.github/instructions/` and `.github/skills/` = existing repo guidance

## Working style
- Analyze the relevant code path before editing.
- Reuse existing patterns and infrastructure instead of adding parallel systems.
- Fix root causes, not just visible symptoms.
- Keep changes coherent and production-safe.
- Avoid speculative rewrites.
- Keep outputs concise unless the user explicitly asks for a long explanation.
- Do not dump full file contents unless needed.
- Ask questions only when ambiguity would likely cause an incorrect or risky implementation.

## Build and validation
Always validate affected work with:
- `dotnet build WindowsUtilityPack.sln`
- `dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj`

If a change touches only a narrow area, still build the solution before finishing.
Do not claim success if build or tests fail.

## Architecture rules
- Preserve MVVM boundaries.
- Keep business logic out of code-behind.
- Keep views declarative and lightweight.
- Put workflow logic in view models and services.
- Use constructor injection where practical.
- Avoid introducing new global/static coupling unless there is already an established project pattern for it.
- Prefer extending existing services over creating duplicate ones.
- Keep naming and file organization consistent with nearby code.

## Tool registration rules
When adding or changing tools:
- use the existing `ToolRegistry` as the source of truth
- keep startup and navigation wiring consistent
- register new tools in `App.xaml.cs` if required by existing patterns
- ensure matching `DataTemplate` mappings exist in `App.xaml`
- do not create duplicate tool metadata just for the homepage or one specific feature

## WPF and UI rules
- Use `DynamicResource` for theme-sensitive brushes and styles.
- Preserve dark/light theme compatibility.
- Avoid hard-coded colors, spacing, and sizes unless there is a strong reason.
- Reuse existing styles from `Resources/Styles.xaml`, `InputStyles.xaml`, and related theme resources before adding new styles.
- Keep keyboard navigation, tab order, and focus behavior usable.
- Avoid fragile popup, dropdown, flyout, or overlay behavior.
- Design for desktop use first, but ensure layout remains stable under resize, DPI scaling, and different display settings.
- Reduce wasted space and visual clutter.
- Prefer compact, high-value UI over oversized decorative containers.

## Homepage and dashboard rules
- Keep the homepage tool-first.
- Favorites, recently used, category access, and search should rely on shared tool metadata rather than duplicated definitions.
- Homepage personalization must persist safely and fail gracefully if settings data is missing or corrupt.
- Do not break existing navigation when changing homepage UX.
- Avoid horizontal scrolling for core category discovery when a cleaner visible layout is practical.

## Async and responsiveness rules
- Use async for I/O and long-running operations.
- Do not block the UI thread with heavy work.
- Marshal UI-bound updates safely back to the UI thread where required.
- Handle cancellation where it is relevant.
- Avoid fire-and-forget unless failure is safely handled and intentional.

## Safety and robustness
- Validate all user input.
- Validate file paths and external content before processing.
- Handle null, empty, invalid, and missing-data cases explicitly.
- Fail gracefully on file I/O, permissions, parsing, serialization, and environment-dependent operations.
- Admin-sensitive features must degrade safely when elevation or access is unavailable.
- Do not log secrets or sensitive values.
- Prefer user-safe error messages with actionable guidance over raw exception text.

## State, settings, and persistence
- Preserve existing settings behavior unless the task explicitly changes it.
- Keep persisted data formats backward-compatible where practical.
- Treat corrupt or missing settings/state files as recoverable conditions.
- Do not silently discard important user state without reason.

## Testing rules
- Add or update tests for new service logic, validation, parsing, and non-trivial state behavior.
- Keep tests deterministic and avoid machine-specific assumptions where possible.
- Test invalid inputs, error paths, cancellation, and persistence edge cases when relevant.
- If a change is hard to test, create a seam instead of skipping testing entirely.

## Code quality
- Follow nullable reference safety.
- Prefer simple, explicit code over clever abstractions.
- Remove dead code created by a refactor.
- Avoid duplicated logic.
- Add XML docs for new public APIs and non-obvious behavior.
- Keep comments useful and durable; do not narrate obvious code.

## Delivery standard
Before finishing any task:
- ensure the solution builds
- run relevant tests
- check for nearby regressions
- verify bindings and runtime flow in affected WPF areas
- verify theme and layout behavior if UI was touched

Final responses should be concise and include:
- what changed
- important validation performed
- any remaining risks or blockers
