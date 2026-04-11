# Lesson: Guard queue cancellation against disposed CTS

## Metadata

- PatternId: LESSON-DOWNLOADER-QUEUE-CTS-DISPOSAL-RACE
- PatternVersion: 1
- Status: active
- Supersedes:
- CreatedAt: 2026-04-11
- LastValidatedAt: 2026-04-11
- ValidationEvidence: Full tests failed with `ObjectDisposedException` in `StopQueueAsync`; resolved with guarded `Cancel()` handling.

## Task Context

- Triggering task: Automation rules template/dry-run implementation validation run.
- Date/time: 2026-04-11
- Impacted area: `DownloadCoordinatorService` queue lifecycle.

## Mistake

- What went wrong: Queue stop logic called `CancellationTokenSource.Cancel()` after the source could already be disposed by queue loop teardown.
- Expected behavior: Stop should be idempotent and safe across rapid stop/restart cycles.
- Actual behavior: `ObjectDisposedException` intermittently surfaced in restart tests.

## Root Cause Analysis

- Primary cause: Race between `StopQueueAsync` and queue-loop `finally` disposal of `_queueCts`.
- Contributing factors: Concurrent stop/restart path exercised by regression tests.
- Detection gap: Not visible until full suite run under timing-sensitive conditions.

## Resolution

- Fix implemented: Snapshot `_queueCts` reference and guard `Cancel()` with `ObjectDisposedException` handling.
- Why this fix works: Makes stop resilient if teardown already disposed the token source.
- Verification performed: Full `dotnet test` passed.

## Preventive Actions

- Guardrails added: Treat cancellation/disposal paths as race-prone and code for idempotency.
- Tests/checks added: Existing restart regression test now validated as stable.
- Process updates: Run full-suite tests after touching queue lifecycle code.

## Reuse Guidance

- How to apply this lesson in future tasks: In async queue managers, avoid unguarded `Cancel()` on mutable CTS fields that may be disposed concurrently.