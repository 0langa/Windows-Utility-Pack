# Memory: Downloader queue stop/restart race handling

## Metadata

- PatternId: MEMORY-DOWNLOADER-QUEUE-STOP-RESTART-RACE
- PatternVersion: 1
- Status: active
- Supersedes:
- CreatedAt: 2026-04-11
- LastValidatedAt: 2026-04-11
- ValidationEvidence: Restart regression test passed after guarded cancel handling in `StopQueueAsync`.

## Source Context

- Triggering task: Continue implementation wave with full-suite validation.
- Scope/system: Downloader queue orchestration.
- Date/time: 2026-04-11

## Memory

- Key fact or decision: `_queueCts.Cancel()` in stop flow must tolerate concurrent disposal from queue-loop finalization.
- Why it matters: Prevents intermittent `ObjectDisposedException` during stop/restart operations.

## Applicability

- When to reuse: Any service using mutable CTS fields across concurrent stop and shutdown paths.
- Preconditions/limitations: Relevant when teardown also disposes CTS in parallel.

## Actionable Guidance

- Recommended future action: Snapshot CTS references locally before cancel and guard disposal races.
- Related files/services/components: `src/WindowsUtilityPack/Services/Downloader/DownloadCoordinatorService.cs`.