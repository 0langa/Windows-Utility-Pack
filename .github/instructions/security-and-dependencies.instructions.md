---
description: Dependency and security guidance
applyTo: "**/*.csproj,src/WindowsUtilityPack/**/*.cs"
---

# Security and Dependency Instructions

- Prefer stable, maintained NuGet packages with clear licenses.
- Validate file paths and user-controlled content before processing.
- Do not log secrets or sensitive values.
- Handle exceptions with user-safe messages and non-fatal fallbacks where appropriate.
- Document new dependencies in feature docs when behavior is non-obvious.
