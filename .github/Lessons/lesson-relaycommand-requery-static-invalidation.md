# Lesson: Use static requery invalidation for RelayCommand

## Metadata

- PatternId: LESSON-RELAYCOMMAND-STATIC-REQUERY
- PatternVersion: 1
- Status: active
- Supersedes:
- CreatedAt: 2026-04-11
- LastValidatedAt: 2026-04-11
- ValidationEvidence: Command enable-state updates worked after switching to `RelayCommand.RaiseCanExecuteChanged()`.

## Task Context

- Triggering task: Multi-window shell command integration.
- Date/time: 2026-04-11
- Impacted area: `MainWindowViewModel` command state updates.

## Mistake

- What went wrong: Treated requery method as instance-level command API.
- Expected behavior: CanExecute should refresh after navigation changes.
- Actual behavior: Needed project-specific static invalidation call.

## Root Cause Analysis

- Primary cause: This codebase’s `RelayCommand` wraps WPF `CommandManager` with static `RaiseCanExecuteChanged()`.
- Contributing factors: Muscle memory from instance-based command implementations in other repos.
- Detection gap: Caught during integration review prior to build validation.

## Resolution

- Fix implemented: Replaced instance-style usage with `RelayCommand.RaiseCanExecuteChanged()`.
- Why this fix works: Triggers WPF command manager requery across bound commands.
- Verification performed: Build/tests succeeded and command behavior remained stable.

## Preventive Actions

- Guardrails added: Check local command implementation before applying framework-generic patterns.
- Tests/checks added: Full suite executed after shell command changes.
- Process updates: Review command helper APIs in repository-specific command layer first.

## Reuse Guidance

- How to apply this lesson in future tasks: For this repository, use static requery invalidation whenever command availability depends on changing shell state.