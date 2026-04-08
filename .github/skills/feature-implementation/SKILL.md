# Skill: Feature Implementation (WPF/.NET)

Use this skill when implementing a new tool or major feature in this repo.

## Steps
1. Identify where the feature belongs in `Tools/<Category>/<ToolName>`.
2. Add any reusable domain logic to `Services/` behind interfaces.
3. Implement ViewModel first, then View binding.
4. Register in `App.xaml.cs` `ToolRegistry` and add DataTemplate in `App.xaml`.
5. Add tests for service/viewmodel logic.
6. Run build and test commands before finalizing.

## Quality Gates
- No UI freeze in normal workflows.
- Input and file operations validated.
- Theme compatibility preserved.
- Errors shown clearly to users.
