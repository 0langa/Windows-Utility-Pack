# GitHub Copilot 5-Session Execution Plan

Date: 2026-03-30

This document converts the findings in `docs/TECHNICAL_REVIEW_REPORT.md` into a five-session prompt plan for GitHub Copilot running directly against the repository on GitHub. The goal is to let Copilot work autonomously, but only within guardrails that preserve a clean, buildable, reviewable solution at every step.

## 1. How to Use This Plan

- Run the sessions in order.
- Start each session from a clean branch based on the latest successful result of the previous session.
- Treat Session 1 as the operating template for the remaining sessions.
- Require Copilot to re-check the repository state before each implementation step instead of blindly following the report.
- Do not let Copilot batch unrelated architectural changes into one session just because they were mentioned in the report.

## 2. Non-Negotiable Operating Contract For Every Session

Every session prompt below assumes these rules:

1. Read `docs/TECHNICAL_REVIEW_REPORT.md` first, then inspect the current repository state before planning changes.
2. Verify that the targeted issue still exists in the current code. If it no longer exists, skip it and explain why.
3. Only implement changes that are justified by the current repository state, not by speculation.
4. Keep placeholder dropdown entries with `ToolKey=""` untouched unless they cause collateral defects.
5. Prefer the smallest coherent refactor that improves correctness, testability, accessibility, or maintainability.
6. Preserve the current architecture direction unless the report explicitly marks a larger change as optional and the session calls for it.
7. Never leave the branch in a broken state. Build and test sequentially after each coherent change set.
8. Do not run `build` and `test` in parallel. A prior parallel run caused a temporary file-lock artifact.
9. Do not introduce new warnings or errors. If touched code produces warnings, clean them up before finishing the session.
10. Before writing code, run a validity gate:
   - Does this issue still exist?
   - Is this the smallest useful fix?
   - Does it fit the current manual startup / MVVM structure?
   - Can it be validated with build and tests?
   - Will it keep the repo understandable for the next engineer?
11. Before finalizing, run a self-review gate:
   - Are the changes internally consistent?
   - Do constructors, registrations, bindings, and XAML resources still line up?
   - Did any docs become stale because of the change?
   - Did the change accidentally widen scope?
12. If Copilot discovers a recommended change is not valid or no longer makes sense in context, it should not force it. It should document the reason and move on to the next valid item.

## 3. Standard Validation Commands

Use these commands sequentially from the repository root:

```powershell
dotnet restore WindowsUtilityPack.sln
dotnet build WindowsUtilityPack.sln -c Release --no-restore
dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj -c Release --no-build --no-restore
```

During inner loops, Copilot may use project- or test-level validation where appropriate, but the session is not complete until the full solution build and test commands above pass.

## 4. Session Overview

| Session | Primary theme | Main outcomes | Depends on |
| --- | --- | --- | --- |
| 1 | Foundation stabilization | Repo hygiene, WPF-boundary adapters, validation/busy-state quick wins, doc alignment | Technical review only |
| 2 | Composition and shell metadata | Constructor/factory cleanup, no `App.*` fallback in VMs, shell/home driven from `ToolRegistry`, better status text | Session 1 |
| 3 | Accessibility and shell UX | Keyboard-friendly navigation, notification presenter, shared button styles, layout/accessibility improvements | Session 2 |
| 4 | Tool services and responsiveness | `IDriveInfoService`, `IRegexEvaluationService`, `IPingService`, debounce/cancel/stop flows, password generator hardening | Session 3 |
| 5 | Bulk rename hardening and final confidence | Safe batch rename engine, richer per-file results, expanded tests, cleanup, final doc sync | Session 4 |

## 5. Session 1 Master Prompt

This is the most detailed prompt. The remaining sessions can follow the same execution style with a narrower scope.

```text
You are GitHub Copilot operating directly on the Windows Utility Pack repository.

Your job is to make a senior-level, production-minded stabilization pass on the repo while keeping the solution buildable and testable at all times. Work autonomously, but do not make speculative changes. Re-check the code before every implementation decision. If a planned change no longer makes sense after inspection, skip it and explain why in your final summary.

Primary source of truth:
- docs/TECHNICAL_REVIEW_REPORT.md

Secondary context to inspect before coding:
- README.md
- docs/PROJECT_STATUS_AND_QUICKSTART.md
- docs/BUILD_REPAIR_NOTES.md
- src/WindowsUtilityPack/App.xaml.cs
- src/WindowsUtilityPack/MainWindow.xaml.cs
- src/WindowsUtilityPack/ViewModels/HomeViewModel.cs
- src/WindowsUtilityPack/Tools/FileDataTools/BulkFileRenamer/BulkFileRenamerViewModel.cs
- src/WindowsUtilityPack/Tools/FileDataTools/BulkFileRenamer/BulkFileRenamerView.xaml
- src/WindowsUtilityPack/Tools/SecurityPrivacy/PasswordGenerator/PasswordGeneratorViewModel.cs
- src/WindowsUtilityPack/Tools/SecurityPrivacy/PasswordGenerator/PasswordGeneratorView.xaml
- src/WindowsUtilityPack/Tools/SystemUtilities/DiskInfo/DiskInfoViewModel.cs
- src/WindowsUtilityPack/Tools/SystemUtilities/DiskInfo/DiskInfoView.xaml
- src/WindowsUtilityPack/Tools/NetworkInternet/PingTool/PingToolView.xaml
- src/WindowsUtilityPack/Services/
- tests/WindowsUtilityPack.Tests/

Operating rules:
1. Inspect the current repository state first. Do not assume the report is still perfectly current.
2. Keep placeholder dropdown entries with ToolKey="" out of scope unless they are causing collateral defects.
3. Prefer small, coherent changes over large rewrites.
4. Preserve the current lightweight MVVM/manual-startup architecture. Do not introduce a DI container.
5. Do not leave direct WPF dependencies in view-model logic when a small adapter can remove them cleanly.
6. Do not introduce new warnings, TODO debt, dead code, or partially wired abstractions.
7. If you touch docs, make them describe the code truthfully as it exists after your changes.
8. Build and test sequentially, never in parallel.

Pre-change validity gate:
Before editing, verify each candidate change against the current code and only implement it if it still makes sense.
- Repo hygiene: confirm whether generated obj files are tracked in Git.
- WPF-boundary adapters: confirm whether Bulk File Renamer and Password Generator still directly use OpenFolderDialog, MessageBox, or Clipboard.
- Ping validation: confirm whether PingCount is still a free-form TextBox or otherwise weakly validated.
- Busy states: confirm whether IsLoading / IsBusy state already exists in view models and is still not surfaced in XAML.
- Docs: confirm stale claims before editing them.

Session 1 scope:
Implement only the following, keeping the solution green after each coherent step:

A. Repository hygiene
- If generated obj files are still tracked in Git, remove them from version control without deleting legitimate source files.
- Do not change placeholder menu entries.

B. WPF-boundary service adapters
- Add small abstractions for:
  - IFolderPickerService
  - IUserDialogService
  - IClipboardService
- Add concrete WPF implementations in the Services layer.
- Register and construct them using the existing manual startup/composition style.
- Refactor BulkFileRenamerViewModel and PasswordGeneratorViewModel to use these adapters instead of direct WPF static APIs or dialogs.
- Keep behavior equivalent unless a small usability improvement is obviously safe.

C. Validation and busy-state quick wins
- Improve PingCount input handling so invalid numeric input is prevented or clearly validated in a WPF-appropriate way.
- Surface existing IsLoading / IsBusy state in Disk Info and Bulk File Renamer views with simple, consistent busy/disabled feedback.
- Keep the UI clean and minimal; do not over-design the views.

D. Documentation alignment
- Update stale documentation affected by this session.
- If docs currently claim behavior that is still not implemented, correct the docs instead of inventing extra features just to satisfy old text.

Preferred implementation style:
- Small interfaces.
- Constructor/factory injection compatible with the existing App startup path.
- No service locator fallback added to new code.
- No code-behind growth unless it is strictly view-only behavior and clearly justified.
- No unnecessary package additions.

Quality bar:
- Senior-quality C# and XAML.
- Stable WPF behavior.
- Clean naming.
- Minimal duplication.
- No silent breaking changes.
- No speculative architecture churn.

Self-review before finishing:
1. Re-read your diff as a reviewer. Challenge whether each file change is necessary.
2. Confirm the new services are actually used and no old call sites remain.
3. Confirm constructor wiring is complete and the app can still instantiate affected view models.
4. Confirm bindings, command states, and resources compile correctly.
5. Confirm docs now match reality.
6. If any sub-change feels weak, overcomplicated, or unjustified after review, simplify or remove it before finalizing.

Validation steps:
1. dotnet restore WindowsUtilityPack.sln
2. dotnet build WindowsUtilityPack.sln -c Release --no-restore
3. dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj -c Release --no-build --no-restore

Expected final summary:
- What you changed
- Why each change was valid after re-checking the repo
- What you intentionally did not change and why
- Validation results
- Any residual risks that should carry into Session 2
```

## 6. Session 2 Prompt

```text
You are GitHub Copilot continuing work on the Windows Utility Pack repository after Session 1.

Read docs/TECHNICAL_REVIEW_REPORT.md first, then inspect the current code before planning changes. Do not blindly trust the previous plan if the repo has moved.

Goal:
Improve composition and remove shell duplication without introducing a DI container or a broad architectural rewrite.

Target outcomes:
1. Remove remaining view-model fallback to App.NavigationService or other App.* service-locator usage where practical, starting with HomeViewModel and any newly touched code.
2. Keep the current manual startup, but move toward constructor/factory-based creation that is easier to test and reason about.
3. Drive shell navigation and home cards from ToolRegistry metadata instead of duplicating tool definitions across App.xaml.cs, MainWindow.xaml, and HomeView.xaml.
4. Replace type-name-derived status text with ToolDefinition.Name or equivalent metadata-driven user-facing labels.
5. Keep the solution behaviorally stable and visually familiar.

Rules:
- Re-check every intended refactor against the current code.
- Preserve current public interfaces where the technical review marked them as Preserve.
- Do not introduce Microsoft.Extensions.DependencyInjection in this session.
- Do not rework placeholder entries beyond what is needed to preserve the existing navigation layout.
- Keep XAML and view-model changes coherent and reviewable.

Suggested workflow:
1. Inspect App.xaml.cs, App.xaml, MainWindow.xaml, MainWindowViewModel.cs, HomeViewModel.cs, ToolRegistry.cs, ToolDefinition.cs, HomeView.xaml, and the category navigation controls.
2. Design the smallest registry-driven projection that can power both shell navigation and home cards.
3. Remove duplicated hard-coded tool lists only once the registry-driven path is working.
4. Re-check every constructor/factory change before applying it to avoid broken startup wiring.
5. Update docs if shell composition or extension steps changed.

Validation:
- dotnet build WindowsUtilityPack.sln -c Release --no-restore
- dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj -c Release --no-build --no-restore

Final summary:
- Changes made
- Why the refactor is valid and lower-maintenance
- Any intentionally deferred shell issues for Session 3
```

## 7. Session 3 Prompt

```text
You are GitHub Copilot continuing work on the Windows Utility Pack repository after Session 2.

Read docs/TECHNICAL_REVIEW_REPORT.md first, then inspect the current repo state to verify the accessibility and shell UX findings still apply.

Goal:
Make the shell meaningfully more accessible and maintainable without changing the app's overall design direction.

Target outcomes:
1. Replace the current hover-only category navigation behavior with a keyboard-friendly pattern that works for both pointer and keyboard users.
2. Surface the existing NotificationService in the shell with a simple, clear presenter such as a banner or toast region.
3. Add missing AutomationProperties, focus behavior, and tab-order improvements where they materially improve accessibility.
4. Consolidate repeated inline button templates and shared action styling into Resources/Styles.xaml.
5. Improve the most rigid shell/tool layout patterns where small adaptive changes provide immediate value.

Rules:
- Confirm each issue still exists before changing it.
- Do not over-engineer the notification UI.
- Prefer standard WPF patterns over fragile custom popup logic.
- Preserve the nav concept; improve the implementation.
- Keep styling consistent with the existing theme dictionaries.

Suggested workflow:
1. Inspect CategoryMenuButton.xaml, CategoryMenuButton.xaml.cs, MainWindow.xaml, shared styles, and the shell-level services/view models.
2. Replace the most fragile accessibility blockers first.
3. Wire NotificationRequested to a visible shell element.
4. Add AutomationProperties.Name / HelpText to meaningful actions and inputs.
5. Move duplicated templates/styles into shared resources only when the new shared style is genuinely reused.

Validation:
- dotnet build WindowsUtilityPack.sln -c Release --no-restore
- dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj -c Release --no-build --no-restore

Final summary:
- Accessibility and shell UX improvements completed
- Any remaining layout/accessibility items better suited to later work
- Validation results
```

## 8. Session 4 Prompt

```text
You are GitHub Copilot continuing work on the Windows Utility Pack repository after Session 3.

Read docs/TECHNICAL_REVIEW_REPORT.md first, then inspect the actual implementation before deciding what to change.

Goal:
Move the most expensive or hard-to-test tool behavior behind thin services, improve responsiveness, and tighten correctness in the tools that are already implemented.

Target outcomes:
1. Extract IDriveInfoService for Disk Info and move expensive drive enumeration off the UI thread while keeping UI property updates safe.
2. Extract IRegexEvaluationService for Regex Tester and add debounce and cancellation where it provides real UX value.
3. Extract IPingService for Ping Tool, add cancellation/Stop support, and improve validation and summary behavior if still needed.
4. Improve Password Generator correctness:
   - guarantee at least one character from each selected class
   - route clipboard operations through the adapter from Session 1
   - add copy feedback through the notification path if available
   - add an exclude-ambiguous-characters option if it fits cleanly
5. Keep all new services testable without real UI dependencies.

Rules:
- Verify each target issue still exists before implementing it.
- Do not add cancellation everywhere just because you can; add it where it improves UX materially.
- Preserve current public shapes where possible; add adapters instead of large rewrites.
- Keep Regex timeout protection intact.
- Keep Ping behavior deterministic and easy to test.

Suggested workflow:
1. Inspect DiskInfoViewModel, RegexTesterViewModel, PingToolViewModel, PasswordGeneratorViewModel, and related views/tests.
2. Design small DTO/service boundaries first.
3. Move CPU/I/O work into services, then wire view models back to them.
4. Add or update tests for extracted logic as you go.
5. Re-check bindings and busy/cancel states after each tool update.

Validation:
- dotnet build WindowsUtilityPack.sln -c Release --no-restore
- dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj -c Release --no-build --no-restore

Final summary:
- Tool-service extractions completed
- Responsiveness/cancellation improvements completed
- Remaining Bulk Rename work that carries into Session 5
```

## 9. Session 5 Prompt

```text
You are GitHub Copilot completing the final planned stabilization session for the Windows Utility Pack repository.

Read docs/TECHNICAL_REVIEW_REPORT.md first, then inspect the current repository to verify what is still outstanding after Sessions 1-4.

Goal:
Finish the highest-risk remaining tool work, expand confidence with tests, and leave the repo in a clean, coherent state.

Target outcomes:
1. Implement a safer Bulk Rename engine behind an IBulkRenameService:
   - support swap/chain rename scenarios with temporary staging names
   - return richer per-file results
   - preserve destination-boundary safety
   - keep the view model UI-friendly and testable
2. Expand automated coverage for the now-extracted services and still-untested implemented tools.
3. Improve error surfacing for settings/logging failures and add simple log-rotation or log-hygiene improvements if justified by the current implementation.
4. Clean up stale or unused code/docs that are still confirmed unused after re-checking:
   - ThemeToIconConverter
   - CategoryItem
   - stale documentation claims
5. Leave the solution, tests, and docs in a stable handoff state.

Rules:
- Do not force cleanup items that became useful in earlier sessions.
- Re-check whether each cleanup target is still unused before deleting it.
- Keep the Bulk Rename algorithm correct and reviewable; correctness is more important than cleverness.
- Add tests around the rename planner/service and other extracted services instead of trying to test raw WPF UI.

Suggested workflow:
1. Inspect the current Bulk Rename implementation and existing safeguards.
2. Design a clear two-phase rename plan with temporary staging names and explicit result reporting.
3. Add tests that cover collisions, swap renames, invalid names, and mixed success/failure cases.
4. Re-check settings/logging failure handling and wire user-facing error surfacing where it is now possible.
5. Update docs to match the final codebase state after this session.

Validation:
- dotnet build WindowsUtilityPack.sln -c Release --no-restore
- dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj -c Release --no-build --no-restore

Final summary:
- What was completed
- What was intentionally left as optional or mid-term work
- Final validation results
- Any recommended follow-up branch or milestone work
```

## 10. Items Explicitly Deferred Beyond This 5-Session Plan

These were recommended in the technical review, but they are better treated as optional or mid-term decisions unless the team explicitly reprioritizes them:

- Introduce a full DI host such as `Microsoft.Extensions.DependencyInjection`
- Decide and implement packaging/distribution strategy
- Add localization/resource infrastructure
- Add a full UI smoke-test layer

These items should be revisited only after the five sessions above are complete and stable.

## 11. Review Standard To Append To Any Copilot Run

If the team wants one short review clause appended to every prompt, use this:

```text
Before making each change, re-check that the issue still exists and that the proposed fix is the smallest sensible improvement. Before finishing, review your own diff like a strict senior code reviewer: remove weak changes, fix warnings, confirm bindings/registrations/resources still line up, and do not leave the repository unless build and tests pass sequentially.
```
