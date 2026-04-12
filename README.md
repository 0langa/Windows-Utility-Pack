# Windows Utility Pack

Windows Utility Pack is a modular WPF/.NET desktop toolkit that bundles system, file, network, security, developer, image, and pentesting utilities into one application shell.

## Repository Layout

- `src/WindowsUtilityPack` - main WPF application
- `tests/WindowsUtilityPack.Tests` - xUnit test project
- `docs` - product and engineering documentation
- `BenchmarkSuite1` - benchmark playground

## Prerequisites

- Windows 11 (or Windows 10 with compatible .NET desktop workloads)
- .NET SDK `10.0.x` (pinned via [`global.json`](global.json))

## Build and Test

```powershell
dotnet restore WindowsUtilityPack.sln
dotnet build WindowsUtilityPack.sln
dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj
```

Required validation commands (per `AGENTS.md`) are the same as above.

## Run the App

```powershell
dotnet run --project src/WindowsUtilityPack/WindowsUtilityPack.csproj
```

## Architecture Notes

- UI follows WPF + MVVM.
- Services are composed in `src/WindowsUtilityPack/App.xaml.cs`.
- Tool registrations are centralized in `src/WindowsUtilityPack/ToolBootstrapper.cs`.
- ViewModel-to-View templates are defined in `src/WindowsUtilityPack/App.xaml`.

## Safety and Security Posture

- Clipboard history monitoring is opt-in and privacy-gated.
- Clipboard persistence uses encrypted at-rest storage.
- Runtime-downloaded downloader dependencies require SHA-256 verification against published release manifests.
- Pentesting tools are scoped for authorized defensive assessment workflows.

## Contributor Workflow

1. Create a feature branch.
2. Implement and keep changes incremental.
3. Run build + tests locally.
4. Update docs for behavior changes.
5. Open a PR with risk notes and validation evidence.

## Documentation

- Audit remediation tracking: `docs/audits/Audit_Remediation_Report.md`
- Comprehensive audit baseline: `docs/Comprehensive_Codebase_Audit.md`
