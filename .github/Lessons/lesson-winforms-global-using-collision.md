# Lesson: Remove WinForms global usings in mixed WPF shells

## Metadata

- PatternId: LESSON-WINFORMS-GLOBAL-USING-COLLISION
- PatternVersion: 1
- Status: active
- Supersedes:
- CreatedAt: 2026-04-11
- LastValidatedAt: 2026-04-11
- ValidationEvidence: Build failed with 77 ambiguous type errors after enabling WinForms; passed after removing implicit `System.Windows.Forms` and `System.Drawing` global usings.

## Task Context

- Triggering task: Tray/background mode implementation.
- Date/time: 2026-04-11
- Impacted area: `WindowsUtilityPack.csproj`, WPF UI and shared type resolution.

## Mistake

- What went wrong: Enabling WinForms support introduced broad type ambiguities (`UserControl`, `KeyEventArgs`, `Color`, `Timer`, etc.) across the WPF codebase.
- Expected behavior: Only tray-integration files should depend on WinForms types.
- Actual behavior: Global usings caused namespace collisions in many unrelated files.

## Root Cause Analysis

- Primary cause: SDK implicit global usings from WinForms support leaked into all compile units.
- Contributing factors: Existing WPF code uses similarly named types from different namespaces.
- Detection gap: Collision surfaced only at build stage.

## Resolution

- Fix implemented: Kept WinForms enabled, but removed implicit global usings for `System.Windows.Forms` and `System.Drawing` in the project file.
- Why this fix works: Restricts WinForms type usage to files that explicitly opt in via alias/using.
- Verification performed: `dotnet build WindowsUtilityPack.sln` and `dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj` succeeded.

## Preventive Actions

- Guardrails added: When adding WinForms interop to WPF projects, explicitly remove conflicting global usings.
- Tests/checks added: Immediate full build after SDK-level property changes.
- Process updates: Treat SDK property changes as cross-cutting risk and validate whole solution.

## Reuse Guidance

- How to apply this lesson in future tasks: For hybrid WPF/WinForms scenarios, prefer explicit per-file aliases and remove conflicting global usings.