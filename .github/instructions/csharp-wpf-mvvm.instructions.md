---
description: C# and XAML guidance for this WPF MVVM app
applyTo: "src/WindowsUtilityPack/**/*.cs,src/WindowsUtilityPack/**/*.xaml"
---

# C# WPF MVVM Instructions

- Keep views declarative and light; avoid business logic in code-behind.
- Place workflow logic in view models/services and keep commands explicit.
- Use existing patterns (`RelayCommand`, `AsyncRelayCommand`, `ViewModelBase`).
- Prefer file-scoped namespaces and project style used in nearby files.
- When introducing options/models, keep them immutable or narrowly scoped where practical.
- For cross-feature logic, use service interfaces in `Services/`.
- Preserve `ToolRegistry` + `NavigationService` conventions for new tools.
- Ensure `App.xaml` has matching DataTemplate for every new tool ViewModel.
