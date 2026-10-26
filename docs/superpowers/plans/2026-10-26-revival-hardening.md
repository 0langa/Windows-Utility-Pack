# Windows Utility Pack Revival Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Make the existing application reliable enough for routine use, preserving equal review coverage across all 52 tools.

**Architecture:** Retain WPF/MVVM and existing service interfaces. Put failure handling, bounded work, and recoverable publication in services; keep dispatcher state and navigation lifetimes in view models. Use real local fixtures and isolated application profiles for verification.

**Tech Stack:** C#, .NET 10, WPF, SQLite, Magick.NET, xUnit, PowerShell.

**Spec:** `docs/REVIVAL_ASSESSMENT_2026-10-26.md`, including the complete tool matrix and R01–R24 findings.

## Global Constraints

- Give all seven categories and all 52 tools the same review dimensions.
- Preserve the earlier revival patch and Magick.NET migration.
- Use free/local tooling; do not incur charges.
- Keep real user data, registry settings, hosts, startup entries, and unrelated processes out of destructive tests. Use temporary fixtures or injected adapters.
- No unsolicited commit, push, release, or deployment. Keep changes reviewable on `codex/revival-hardening`.
- Report failures and unresolved limitations; do not narrate successful checks.
- Regression tests exercise behavior rather than mirroring implementation; low-impact copy/layout changes only need direct verification.

## Review Focus

- A canceled operation must not overwrite an existing file or leave the UI busy; task 4 covers cancellation while waiting for a slot.
- A skipped, filtered, inaccessible, or linked directory must not be offered as empty; task 2 covers each scan outcome.
- A saved rule must retain independent identity and dispatch only its exact target; task 1 covers multiple new rules and concurrent evaluation.
- A refused navigation must not construct/retain a new worker; task 3 covers factory invocation, history, and disposal.
- Persisted or exported state must not silently retain credentials or lose unsaved data; tasks 3 and 5 cover dirty guards and persistence failures.

## Task 1: Automation evaluation and dispatch

**Files:** `Services/IAutomationRuleService.cs`, `Services/BackgroundTaskService.cs`, `App.xaml.cs`, `Models/AutomationRule.cs`, and automation service tests, all under their existing source/test roots.
**Interfaces:** Preserve `IAutomationRuleService`. Make one evaluation path own notification/action dispatch; support deterministic snapshots internally. Keep dry runs side-effect free.

- [x] Add failing regressions for distinct IDs when creating multiple rules, disabled rules in dry runs, new trigger simulations, and concurrent cooldown claiming.
- [x] Replace competing evaluators with one owned, cancellable loop; marshal navigation/notifications to the dispatcher.
- [x] Replace substring process termination with exact, identity-revalidated targeting; give cleanup a real analysis/preview path rather than reporting an empty placeholder scan.
- [x] Run automation tests, then the suite; record remaining real-machine boundaries in the ledger.

## Task 2: Storage cleanup evidence and file-operation recovery

**Files:** `Models/StorageItem.cs`, `Services/Storage/ScanEngine.cs`, `CleanupRecommendationService.cs`, cleanup policy service, file rename/shred and metadata services, associated tests.
**Interfaces:** `ScanAsync` retains its signature and returns explicit completeness evidence; cleanup consumes that evidence rather than inferring emptiness from displayed counts.

- [x] Reproduce filtered/depth-limited directories misclassified as empty and temp-prefix lookalikes classified as safe.
- [x] Track enumeration completeness, avoid following junctions, preserve cancellation, and use normalized path boundaries.
- [x] Reproduce rename partial failures and shredder recovery/reporting faults using disposable files; add preflight and per-file recovery where possible.
- [x] Preserve frame/format semantics when stripping metadata, or reject unsupported edits explicitly.
- [x] Run affected storage/file tests and the suite.

## Task 3: Navigation, editors, and persisted shell state

**Files:** `Services/NavigationService.cs`, `App.xaml.cs`, `HomeViewModel.cs`, `MarkdownEditorViewModel.cs`, environment editor, system-info view model, and tests.
**Interfaces:** Preserve public navigation and command APIs; use registered tool keys/types for identity. Dirty guards use `INavigationGuard`.

- [x] Add failing tests for navigation-veto factory creation, invalid navigation/history changes, saved Home expansion state, and stale output after invalid input.
- [x] Fix identity lookup, disposal/refresh ownership, and dirty editor New/Open/navigation handling.
- [x] Make Markdown preview labeling match delivered behavior; remove misleading unsupported options and regenerate dependent output when formats change.
- [x] Run affected tests and inspect the available native shell, System Info and Automation interactions; remaining view/keyboard acceptance is recorded in task 7.

## Task 4: Network, processes, and cancellation

**Files:** PortScanner, DNS, CertificateManager, HTTP/speed-test services, process runner, downloader direct engine, pentesting transport/crawler, API mock server and their tests.
**Interfaces:** Preserve existing request/result contracts where possible; cancellation must propagate and terminal UI state must be set in `finally`.

- [x] Reproduce cancellation during semaphore scheduling, bad URI/port handling, child processes surviving cancellation, off-origin redirects, and unbounded responses against local fixtures.
- [x] Add bounded responses/time budgets, validate redirects and scope at every hop, and own all worker/request lifetimes.
- [x] Bind download resumption to entity identity, isolate job targets/segments, and keep client settings stable during concurrent jobs.
- [x] Reproduce mock-server stop/restart/port-release faults and repair them with owned task draining.
- [x] Run loopback integration tests and the suite; no bulk public bandwidth tests or unauthorized scans.

## Task 5: Durable state, secrets, and Windows editors

**Files:** SettingsService, LocalSecretVault, HTTP Request Builder persistence, downloader settings, registry/hosts/environment/startup services and their tests.
**Interfaces:** Surface save failure to the caller; publish serialized state atomically. Retain compatible loading/migration and typed registry values.

- [x] Add failing tests for failed writes preserving originals, stale settings snapshots, plaintext persisted credentials, invalid later registry nodes, and multi-string round trips.
- [x] Add protected/opt-in sensitive persistence, vault backup/recovery and lock lifetime, and safe settings updates.
- [x] Preflight whole registry/hosts edits, preserve encoding/types, and roll back recoverable partial changes.
- [x] Exercise temporary file paths and disposable registry subtrees only; run the suite.

## Task 6: Bounded text work and accurate reporting

**Files:** Diff, RegexTester, LogFileAnalyzer, EventLogViewer, task scheduler, SSH helper, SystemInfo, Timestamp and associated tests.
**Interfaces:** Keep result models usable by the existing views; distinguish error/cancel/empty instead of returning fabricated empty success.

- [x] Reproduce large-input UI blocking, locale-dependent parsing, command quoting, and inaccurate hardware/output values.
- [x] Bound algorithms and retained results, move CPU/I/O work off the dispatcher, make cancellation/lifetime explicit, and fix misleading labels/statuses.
- [x] Verify Unicode, German locale, malformed input, long input, cancellation and repeated navigation with local fixtures.
- [x] Run focused tests and the suite.

## Task 7: Native workflows and delivery

**Files:** `tests/WindowsUtilityPack.UiSmoke`, `tests/WindowsUtilityPack.NativeTests`, test fixtures, local/native/installer scripts, `packaging/WindowsUtilityPack.iss`, README, revival assessment and a workflow evidence matrix.
**Interfaces:** Retain isolated profile support; composition, service integration, and actual native interactions are separate evidence levels.

- [x] Retry native interaction with the existing app; cover representative real workflows in every category and record each of the 52 tools individually.
- [x] Use the assessment matrix to add normal/error/cancel/persistence cases, including image and QR exports with independent decoding. Per-tool combinations beyond the bounded scenarios remain explicit in the matrix.
- [x] Add and exercise per-user install/update/uninstall/reinstall, running-app guards and retained workspace data in a disposable Windows Sandbox.
- [x] Verify both Windows publish forms, notices/assets and isolated published-profile launch. Native interaction resumed, including image/QR exports, downloader provisioning and detached-window shutdown; long-running resource acceptance remains open.
- [x] Record any unavailable clean-machine, privileged, mixed-DPI, or external-engine gates honestly; do not equate view composition with end-to-end behavior.
- [x] Run the full local validation entry point and review the whole patch with one fresh reviewer as required by executing-plans. Address material findings before concluding.

## Execution ledger

Progress, concrete tests, deviations, and evidence paths are recorded in `output/revival/hardening-progress.md`. This plan is executed continuously under the user's instruction to resume the revival.

## Current delivery boundary

Tasks 1–6 core repairs are implemented. Task 7 includes both publish forms, a 52-tool external native suite, independent output verification, per-user installer lifecycle, published-alpha vault migration, the workflow matrix and final review dispositions. `docs/NATIVE_ACCEPTANCE.md` records scenarios, artifacts and repairs; `docs/REVIVAL_RETEST_2026-10-26.md` retains earlier public-provider and mixed-DPI evidence. Other historical schema combinations, signed distribution, additional per-tool input/provider/display combinations and soak profiling remain separate work. The user subsequently authorized commit, push and a new release; see `docs/releases/v1.0.0.md`.
