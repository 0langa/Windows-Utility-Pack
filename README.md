# Windows Utility Pack

Windows Utility Pack is a modular WPF/.NET desktop toolkit that bundles system, file, network, security, developer, image, and pentesting utilities into one application shell.

## Download

Download the Windows x64 installer or portable ZIP from
[Releases](https://github.com/0langa/Windows-Utility-Pack/releases).
Both include the .NET desktop runtime. The installer is per-user; the portable
ZIP must be extracted in full before running `WindowsUtilityPack.exe`.
Release binaries are unsigned. Checksums accompany the downloads.

Close older copies before upgrading, and keep a backup of both
`%LOCALAPPDATA%\WindowsUtilityPack` and `%APPDATA%\WindowsUtilityPack`.
Uninstall retains user data. See [state and recovery](docs/STATE_AND_RECOVERY.md)
for vault migration, encrypted backups and operation limits.

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

For the bounded local validation sequence (restore, build, tests with coverage, and
real WPF tool creation in all three themes), use PowerShell 7:

```powershell
pwsh ./scripts/Test-Local.ps1
```

The script writes logs and reports under `output/validation` and prints only on
failure. The WPF probe uses its own disposable app profile; it opens each tool but
does not run destructive system operations. This is composition coverage, not a
replacement for end-to-end workflow tests. If .NET's NuGet requests hang on this
machine, add `-DisableIPv6`; that switch affects only the child processes.


## Run the App

```powershell
dotnet run --project src/WindowsUtilityPack/WindowsUtilityPack.csproj
```

A separate test profile can be selected without touching saved application preferences:

```powershell
dotnet run --project src/WindowsUtilityPack/WindowsUtilityPack.csproj -- --profile-directory C:\absolute\path\to\test-profile
```

This isolates app-owned files, not Windows or the files/tools you operate on.
See [state and recovery](docs/STATE_AND_RECOVERY.md) before testing destructive actions.

## Publish a portable Windows build

```powershell
dotnet publish src/WindowsUtilityPack/WindowsUtilityPack.csproj -c Release -r win-x64 --self-contained true -o output/portable-win-x64
```

Keep the whole output directory, including `ThirdPartyNotices/Magick.NET.txt`.
Run `WindowsUtilityPack.exe` from that directory. This is a portable folder build,
not an installer. App-managed yt-dlp/gallery-dl/FFmpeg remain separate optional
downloads; the core tools do not require those executables.

## Native Windows acceptance

The external UI Automation runner drives the published executable, native file
dialogs, keyboard input and screen pixels. Its full 52-tool run changes disposable
Windows state and is restricted to Windows Sandbox. Enable Windows Sandbox before
running these commands from PowerShell 7:

```powershell
./scripts/Submit-NativeAcceptance.ps1 -PublishApp
./scripts/Start-NativeAcceptance.ps1 -Watch
```

The Sandbox stays open for further runs. Submit another run with
`./scripts/Submit-NativeAcceptance.ps1`, or use `-Filter 'tool-key,other-tool-key'`
while developing a case. Reports, action traces, control trees and screenshots go
to `output/revival/native-results/<request-id>`. Only that results folder is mapped
with write access. Close the Sandbox window when finished to discard guest changes.

See [native acceptance](docs/NATIVE_ACCEPTANCE.md) for the 52 scenarios, recorded
artifacts, repairs and remaining test boundaries.

## Build and exercise the installer

Use an Inno Setup 7 compiler supplied explicitly; the build script does not install
a toolchain or download dependencies itself:

```powershell
./scripts/Build-Installer.ps1 -Compiler C:\tools\InnoSetup\ISCC.exe
```

The resulting setup installs per user, bundles the .NET desktop runtime and creates
a Start Menu shortcut. Rerun setup to update. Close the app before updating or
uninstalling. Uninstall retains the user's profile and files it did not install.

To exercise the lifecycle in a fresh Sandbox, also build a synthetic older package
and submit the installer job before starting the guest:

```powershell
./scripts/Build-Installer.ps1 -Compiler C:\tools\InnoSetup\ISCC.exe -Version 0.9.0
./scripts/Submit-NativeAcceptance.ps1 -Installer
./scripts/Start-NativeAcceptance.ps1 -Watch
```

That case covers cancellation, installation, update, running-app guards, uninstall,
retained data and reinstall. The older package uses current source with an older
version; it does not establish compatibility with every historical database.

## Architecture Notes

- UI follows WPF + MVVM.
- Services are composed in `src/WindowsUtilityPack/App.xaml.cs`.
- Tool registrations are centralized in `src/WindowsUtilityPack/ToolBootstrapper.cs`.
- ViewModel-to-View templates are defined in `src/WindowsUtilityPack/App.xaml`.
- Image conversion, resizing, and annotations use Magick.NET without a license key; see [image processing behavior and packaging](docs/IMAGE_PROCESSING.md).

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

- Current repairs and remaining acceptance gates: [October 2026 assessment](docs/REVIVAL_ASSESSMENT_2026-10-26.md)
- Per-tool automated/native evidence: [all 52 workflow rows](docs/REVIVAL_WORKFLOW_MATRIX.md)
- Profiles, protected state, backups and operation limits: [state and recovery](docs/STATE_AND_RECOVERY.md)
- Audit remediation tracking: `docs/audits/Audit_Remediation_Report.md`
- Comprehensive audit baseline: `docs/Comprehensive_Codebase_Audit.md`
