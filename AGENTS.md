# AGENTS.md

Agent onboarding for this repository.

## Stack
- C# / .NET 10 / WPF / MVVM
- xUnit tests

## AI Context Files
- Copilot-wide instructions: `.github/copilot-instructions.md`
- Path instructions: `.github/instructions/*.instructions.md`
- Skills: `.github/skills/*/SKILL.md`
- Agent profile: `.github/agents/csharp-wpf-feature-builder.agent.md`

## Mandatory Validation
- `dotnet build WindowsUtilityPack.sln`
- `dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj`

## Architecture Constraints
- Keep business logic out of code-behind.
- Register tools in `App.xaml.cs` + DataTemplate in `App.xaml`.
- Preserve theme/resource compatibility.
