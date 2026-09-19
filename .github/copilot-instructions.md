# Copilot Instructions for Windows Utility Pack

Windows desktop app: C# / .NET 10 / WPF / MVVM.

## General Guidelines
- Use self-learning and memory-saving capability proactively, not only when instructed.
- Minimize confirmation prompts. Deliver complete solutions end-to-end without per-step confirmation.

## Prompt Optimization First
- Run prompt optimization only when user prompt includes `optimize prompt first` (case-insensitive).
- When triggered, rewrite request into concise execution brief.
- Keep user intent unchanged; improve only clarity, constraints, acceptance criteria, missing assumptions.
- Do not ask user to confirm rewritten brief unless requirements ambiguous or risky.
- Use `.github/skills/prompt-optimizer/SKILL.md` as canonical workflow.
- Include an `Optimized Prompt` section in output whenever optimization triggered.

## Project Priorities
- Preserve MVVM boundaries: logic in services and view models, code-behind minimal.
- Prefer incremental refactors over broad rewrites.
- Keep startup/tool wiring consistent with `ToolRegistry` and `App.xaml` DataTemplates.
- Optimize for reliability and maintainability over novelty.

## Implementation Rules
- Constructor injection when possible; no new global/static coupling.
- Validate all user input and all file/network operations.
- Async for I/O or long-running work to keep UI responsive.
- Feature code in `Tools/<Category>/<ToolName>`, shared logic in `Services/`.
- XML docs for new public APIs and non-obvious behavior.

## WPF/UI Rules
- Use `DynamicResource` brushes so dark/light themes keep working.
- Keyboard and tab navigation remain usable.
- No hard-coded colors/sizes without clear reason.
- Use existing styles from `Resources/Styles.xaml` before adding new.

## Testing Rules
- Add or update xUnit tests for all new service/domain logic.
- Tests deterministic; avoid OS-specific flakiness when possible.
- Validate edge cases (invalid input, cancellation, file failures, null/empty values).

## Delivery Checklist
- Build succeeds (`dotnet build WindowsUtilityPack.sln`).
- Tests pass (`dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj`).
- New feature registered in `App.xaml.cs` and mapped in `App.xaml` DataTemplate.
- User-facing messages clear and actionable.

