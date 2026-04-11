# Memory: Safe patch sequencing for new feature files

## Metadata

- PatternId: MEMORY-PATCH-SEQUENCING
- PatternVersion: 1
- Status: active
- Supersedes:
- CreatedAt: 2026-04-11
- LastValidatedAt: 2026-04-11
- ValidationEvidence: Certificate Manager wave required deduplication after concurrent patch adds; sequential recreation was stable.

## Source Context

- Triggering task: Continue implementation wave (SSH + Certificate Manager).
- Scope/system: Windows Utility Pack code generation workflow.
- Date/time: 2026-04-11

## Memory

- Key fact or decision: For brand-new related C#/XAML files, prefer sequential add/edit operations instead of parallel `apply_patch`.
- Why it matters: Reduces risk of accidental repeated file bodies and avoids avoidable compile/parser errors.

## Applicability

- When to reuse: Any feature wave adding several new files at once.
- Preconditions/limitations: Especially important when files are created in parallel tool calls.

## Actionable Guidance

- Recommended future action: Batch reads in parallel, but perform writes for new files sequentially and run build immediately after creation.
- Related files/services/components: New tool implementations under `src/WindowsUtilityPack/Tools`, `Services`, and matching tests.