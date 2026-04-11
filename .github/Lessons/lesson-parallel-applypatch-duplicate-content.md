# Lesson: Avoid parallel file creation with apply_patch

## Metadata

- PatternId: LESSON-PATCH-PARALLEL-DUPLICATE
- PatternVersion: 1
- Status: active
- Supersedes:
- CreatedAt: 2026-04-11
- LastValidatedAt: 2026-04-11
- ValidationEvidence: Build error CS1529 and XAML MC3000 resolved by deduplicating repeated file bodies.

## Task Context

- Triggering task: Continue autonomous roadmap delivery (SSH + Certificate Manager).
- Date/time: 2026-04-11
- Impacted area: New tool files under Services/Models/Tools.

## Mistake

- What went wrong: Multiple newly added files ended up with duplicated full file bodies.
- Expected behavior: Each file should contain one definition block.
- Actual behavior: Duplicate content produced invalid C# and XAML parse/build errors.

## Root Cause Analysis

- Primary cause: Parallelized `apply_patch` add/update operations introduced duplicate content in some files.
- Contributing factors: Several related new files were created concurrently.
- Detection gap: Only surfaced at compile time.

## Resolution

- Fix implemented: Deleted and recreated affected files with single clean definitions.
- Why this fix works: Removes repeated namespaces/root elements and restores valid syntax.
- Verification performed: `dotnet build WindowsUtilityPack.sln` and `dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj` passed.

## Preventive Actions

- Guardrails added: Prefer sequential file creation for newly added related files.
- Tests/checks added: Keep immediate build after large file-creation batches.
- Process updates: If duplicate-content symptoms appear, inspect for accidental full-body repetition first.

## Reuse Guidance

- How to apply this lesson in future tasks: Avoid parallel `apply_patch` when adding multiple brand-new source files in the same feature area.