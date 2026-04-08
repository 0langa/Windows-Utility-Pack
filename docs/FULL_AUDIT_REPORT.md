# FULL AUDIT REPORT (2026-04-08)

## 1. Executive Summary

Windows Utility Pack is in a solid refactor-and-harden state. The application builds cleanly, the current automated test suite is broad, and the architecture has improved since prior audits (notably registry-driven shell/home generation and fixes in theme/traversal behavior).

This is not a rewrite candidate. The right strategy remains incremental modernization with clear priorities:

1. reduce static service-location coupling,
2. break down oversized hotspots,
3. add CI enforcement,
4. keep metadata/docs aligned with the real app project.

## 2. Verification Baseline

### 2.1 Snapshot

- Date: 2026-04-08
- Branch: `main`
- Commit: `df5df7e`

### 2.2 Validation commands run

| Command | Result |
| --- | --- |
| `dotnet build WindowsUtilityPack.sln` | Passed (0 warnings, 0 errors) |
| `dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj` | Passed (196/196) |

### 2.3 Repository signals

- `.github/workflows` directory: not present
- Test methods discovered via attributes (`[Fact]`/`[Theory]`): 180
- Runtime test executions from latest run: 196
- `.github` footprint: 894 files across 395 directories

## 3. Current Architecture Snapshot

### 3.1 What is working well

- MVVM structure is clear and consistent across most tool flows.
- `ToolRegistry` is now actively used to drive category navigation and home cards.
- Core services are split by domain (storage, downloader, text conversion, settings/theme, notifications).
- Tests cover key functional slices including navigation, storage services, text conversion, theme, and downloader viewmodel behavior.

### 3.2 Current composition model

- Startup initializes services in `src/WindowsUtilityPack/App.xaml.cs`.
- Services are exposed via static `App.*` properties.
- ViewModels are primarily factory-created via `ToolRegistry` and `NavigationService`.
- Some runtime paths still use static access/fallbacks (`HomeViewModel`, `SettingsWindow`, theme/settings persistence in shell).

## 4. Confirmed Open Findings (Current)

### 4.1 High - Static service location remains pervasive

`App.xaml.cs` still acts as a global service locator with many static properties. Several components use direct static access instead of explicit dependency flow.

Impact:

- hidden dependencies,
- more brittle testing/integration seams,
- harder future migration to a single composition root.

Primary evidence:

- `src/WindowsUtilityPack/App.xaml.cs`
- `src/WindowsUtilityPack/ViewModels/HomeViewModel.cs`
- `src/WindowsUtilityPack/ViewModels/MainWindowViewModel.cs`
- `src/WindowsUtilityPack/Views/SettingsWindow.xaml.cs`

### 4.2 High - Large hotspot files increase regression risk

Largest files in `src/WindowsUtilityPack` are still sizable and multi-responsibility:

| File | Lines |
| --- | ---: |
| `Services/TextConversion/TextFormatConversionService.cs` | 868 |
| `Services/Downloader/WebScraperService.cs` | 773 |
| `Tools/SystemUtilities/StorageMaster/StorageMasterViewModel.cs` | 760 |
| `Tools/SystemUtilities/StorageMaster/StorageMasterView.xaml` | 695 |
| `Tools/DeveloperProductivity/TextFormatConverter/TextFormatConverterViewModel.cs` | 668 |
| `Tools/NetworkInternet/Downloader/DownloaderViewModel.cs` | 659 |

Impact:

- high review/change cost,
- elevated merge conflict probability,
- behavior regression risk during feature work.

### 4.3 High - No CI gate for build/test

No workflow files are present under `.github/workflows`.

Impact:

- no automated enforcement on PR/push,
- regressions can land without fast feedback.

### 4.4 Medium - Root shim project still drifts from app project

`/WindowsUtilityPack.csproj` (shim for non-Windows restore/tooling) is documented well, but package versions and package identity differ from `src/WindowsUtilityPack/WindowsUtilityPack.csproj`.

Examples:

- `Newtonsoft.Json` version mismatch (`13.0.4` vs `13.0.3`)
- `UglyToad.PdfPig` in shim vs `PdfPig` in app project

Impact:

- contributor/tool confusion,
- potential dependency scan noise.

### 4.5 Medium - Shell/settings still contain static and code-behind coupling

`SettingsWindow` intentionally uses code-behind with static service access, and shell behavior still reaches into `App.SettingsService` for persistence.

Impact:

- acceptable now, but scales poorly as settings/shell complexity grows.

### 4.6 Medium - Operational noise from large non-product `.github` payload

The repository contains extensive agent/plugin/skill support content under `.github` relative to product runtime code.

Impact:

- increased maintenance surface,
- harder repo orientation for new contributors,
- audit signal-to-noise cost.

### 4.7 Medium - Diagnostics policy remains uneven

Some non-fatal paths still rely on silent/low-visibility behavior (for example debug-only navigation warnings).

Impact:

- weaker production diagnostics when edge conditions occur.

## 5. Previously Flagged Issues Now Resolved

The following prior findings are no longer accurate in the current codebase:

1. Theme system-follow mode subscription bug: resolved in `ThemeService.SetTheme(...)`.
2. Unsafe recursive traversal findings in duplicate/drive analysis: resolved with `EnumerationOptions` + `IgnoreInaccessible` paths.
3. Hard-coded shell/home metadata duplication: materially improved; shell/home are now registry-driven.
4. Prior low test-count finding: superseded by current passing baseline (196 tests) and expanded service/viewmodel coverage.

## 6. Test Posture (Current)

### 6.1 Strong coverage areas

- Navigation and tool registry behavior
- Storage services (scan, duplicates, drive analysis, reports, snapshots)
- Text conversion pipeline and preview services
- Theme service behavior
- Multiple tool viewmodels (including downloader)

### 6.2 Remaining weaker areas

- Shell-level integration flows (startup, navigation + settings + notifications in one path)
- External-process heavy download execution (`yt-dlp` / `gallery-dl`) under realistic integration conditions
- Full end-to-end UI workflow regression checks

## 7. Prioritized Recommendations

1. Establish a single explicit composition direction and reduce `App.*` usage in shell/settings/home edges first.
2. Decompose hotspot files incrementally by workflow boundary (not by arbitrary file slicing).
3. Add minimal CI pipeline for build + tests on PR/push.
4. Align shim/app package metadata or document and automate drift checks.
5. Add a small set of integration tests around shell startup/navigation/settings and downloader engine execution boundaries.

## 8. Final Conclusion

The project is in good operational shape and materially healthier than earlier audit artifacts suggested. The highest-value work now is architectural hardening and automation guardrails, not broad defect triage or rewrite planning.
