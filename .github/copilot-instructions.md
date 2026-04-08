# Copilot Instructions for Windows Utility Pack

This repository is a Windows desktop app built with C# / .NET 10 / WPF / MVVM.

## Project Priorities
- Preserve MVVM boundaries: keep logic in services and view models, keep code-behind minimal.
- Prefer incremental refactors over broad rewrites.
- Keep startup/tool wiring consistent with `ToolRegistry` and `App.xaml` DataTemplates.
- Optimize for reliability and maintainability over novelty.

## Implementation Rules
- Use constructor injection when possible; avoid introducing new global/static coupling.
- Validate all user input and all file/network operations.
- Use async for I/O or long-running work to keep UI responsive.
- Keep feature code in `Tools/<Category>/<ToolName>` and shared logic in `Services/`.
- Add XML docs for new public APIs and non-obvious behavior.

## WPF/UI Rules
- Use `DynamicResource` brushes so dark/light themes keep working.
- Ensure keyboard and tab navigation remain usable.
- Avoid hard-coded colors/sizes unless there is a clear reason.
- Use existing styles from `Resources/Styles.xaml` before adding new styles.

## Testing Rules
- Add or update xUnit tests for all new service/domain logic.
- Keep tests deterministic and avoid OS-specific flakiness when possible.
- Validate edge cases (invalid input, cancellation, file failures, null/empty values).

## Delivery Checklist
- Build succeeds (`dotnet build WindowsUtilityPack.sln`).
- Tests pass (`dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj`).
- New feature is registered in `App.xaml.cs` and mapped in `App.xaml` DataTemplate.
- User-facing messages are clear and actionable.
