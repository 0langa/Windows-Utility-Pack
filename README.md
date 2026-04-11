# Windows Utility Pack

Windows Utility Pack is a Windows desktop toolbox built with WPF and .NET 10 that brings system utilities, file tooling, security helpers, network diagnostics, developer utilities, and image workflows into a single local-first application.

The current codebase ships a unified shell, a personalized home dashboard, 32 integrated tools across six categories, persisted settings, multiple themes, a benchmark project, and an actively maintained test suite.

## Overview

The application is designed as a multi-tool desktop workspace rather than a collection of disconnected windows. Users land on a Home dashboard with search, favorites, recent tools, quick actions, and live system vitals, then launch individual tools through a shared navigation and theming system.

The repo is also structured for ongoing hardening work: the solution builds cleanly, tests pass, feature-specific docs exist for newer areas such as Downloader Studio and QR Code Generator, and the repository contains an explicit engineering roadmap for architecture and CI improvements.

## At a Glance

- 32 integrated tools plus the Home dashboard
- 6 feature areas: System Utilities, File & Data Tools, Security & Privacy, Network & Internet, Developer & Productivity, Image Tools
- 4 theme modes: Dark, Light, Aurora, and System-follow
- Local settings and history persisted under the user's Windows profile
- xUnit test suite with 255 passing tests in the current validated state
- Optional benchmark project for targeted performance measurements

## Modules and Tool Areas

| Area | Focus | Integrated tools |
| --- | --- | --- |
| System Utilities | Machine inspection, startup behavior, environment editing, storage workflows | Storage Master, Startup Manager, System Info Dashboard, Environment Variables Editor, Hosts File Editor |
| File & Data Tools | Bulk file operations, file integrity, metadata cleanup, file splitting | Bulk File Renamer, File Hash Calculator, Secure File Shredder, Metadata Viewer/Editor, File Splitter / Joiner |
| Security & Privacy | Passwords, hashes, secret storage, certificate inspection | Password Generator, Hash Generator, Local Secret Vault, Certificate Inspector |
| Network & Internet | Connectivity tests, HTTP inspection, speed tests, download orchestration | Ping Tool, DNS Lookup, Port Scanner, HTTP Request Tester, Network Speed Test, Downloader Studio |
| Developer & Productivity | Text conversion, validation, encoding, IDs, QR, diffing, color workflows | Regex Tester, Text Format Converter, QR Code Generator, Color Picker, Timestamp Converter, UUID / ULID Generator, Base64 / URL Encoder-Decoder, Diff Tool, JSON / YAML Validator |
| Image Tools | Batch processing and screenshot workflows | Image Resizer & Compressor, Image Format Converter, Screenshot Annotator |

## Key Features

### Home dashboard and shell

- Personalized landing page with favorites, recent tools, category browsing, inline search, and recent-search history
- Live vitals strip showing CPU, RAM, disk free space, and network status
- Quick actions for password generation, UUID generation, clipboard inspection, and pinging a host detected from clipboard content
- Theme-aware shell with persisted appearance settings and system theme following

### Storage and system administration

- Storage Master for drive analysis, recursive scans, duplicate detection, cleanup recommendations, snapshots, and report export
- Startup Manager for user and machine startup entry inspection and toggling
- System Info Dashboard for OS, CPU, memory, GPU, runtime, and drive summaries
- Environment Variables Editor for user and machine scope environment variables
- Hosts File Editor with backup and restore support

### File and data workflows

- Bulk renaming with live preview, prefix/suffix rules, find-and-replace, and conflict detection
- File hash calculation and verification workflows
- Secure deletion via overwrite-and-delete flow
- Metadata inspection and stripping for supported image and audio formats
- File splitting and joining with manifest-based checksum validation

### Security and privacy tooling

- Cryptographically secure password generation
- Text and file hashing utilities
- Local AES-256 encrypted secret vault persisted on disk
- Certificate inspection from live TLS endpoints, local files, or pasted PEM data

### Network and download workflows

- Ping, DNS, port scanning, and HTTP request inspection tools
- Network Speed Test for latency plus download/upload measurement
- Downloader Studio for queue management, direct downloads, media downloads, asset discovery, site crawl staging, scheduling, history, and diagnostics

### Developer and content tooling

- Regex testing with debounced execution and timeout protection
- Text Format Converter for HTML, XML, Markdown, RTF, PDF, DOCX, and JSON workflows with preview/export services
- QR Code Generator with styling, scannability checks, logo overlay, clipboard helpers, and multi-format export
- JSON / YAML validation and formatting
- Base64, URL, and HTML encode/decode support
- UUID and ULID generation with bulk copy support
- Color sampling and reusable palette building
- Text diffing and timestamp conversion utilities

### Image workflows

- Batch image resizing and compression
- Batch format conversion across common desktop image formats
- Screenshot capture with annotation, blur, and redaction support

## Current Functionality Status

### Fully implemented and wired

The current source tree includes:

- 32 registered tools in `ToolRegistry`
- Matching WPF `DataTemplate` mappings in `App.xaml` for every registered tool ViewModel
- A Home dashboard that is actively used for discovery and personalization
- Shared services for navigation, settings, logging, clipboard access, dialogs, theming, downloader orchestration, text conversion, QR generation, file tools, image tools, storage analysis, and structured data validation
- A benchmark project and an automated xUnit test project included in the solution

No placeholder-only tool registrations were found in the current application startup wiring. The tools that are registered in `App.xaml.cs` are backed by concrete ViewModels and Views.

### Partially implemented or deliberately constrained areas

These are present in the repo and explicitly identifiable as current constraints rather than missing features:

- Downloader Studio scheduler currently supports one-shot start and pause scheduling, not recurring schedules
- Downloader segmented resume state is not fully restored across app restarts for partial segmented runs
- Downloader media option handling is intentionally simplified to presets and settings-backed defaults instead of exposing a full advanced matrix
- Home dashboard quick ping uses a host detected from clipboard content rather than a canonical "last-used host" shared across tools
- The repository does not currently include a CI workflow under `.github/workflows`

### Explicitly tracked next-step work in the repo

The repository contains an engineering roadmap in `docs/IMPLEMENTATION_REFACTOR_PLAN.md`. It focuses on codebase hardening rather than new end-user modules:

- CI enforcement for build and test runs
- Reduced reliance on static `App.*` service access
- Decomposition of the largest service and ViewModel files
- Better alignment between the root shim project and the real WPF application project
- Additional integration-style test coverage

## Tech Stack

| Layer | Current stack |
| --- | --- |
| Language | C# with nullable reference types enabled |
| Runtime | .NET 10 |
| UI | WPF |
| Architecture | MVVM with service-oriented feature domains |
| Test framework | xUnit |
| Benchmarking | BenchmarkDotNet |
| Major libraries | HtmlAgilityPack, Newtonsoft.Json, QRCoder, PDFsharp, PdfPig, DocumentFormat.OpenXml, ReverseMarkdown, DnsClient, SixLabors.ImageSharp, TagLibSharp, YamlDotNet |

## Architecture Summary

The application uses a pragmatic WPF MVVM structure with centralized startup composition.

- `App.xaml.cs` acts as the current composition root and service host
- `ToolRegistry` is the source of truth for tool metadata, categories, and navigation keys
- `NavigationService` creates fresh tool ViewModels per navigation request
- `App.xaml` binds each tool ViewModel to its corresponding View through `DataTemplate` registration
- Shared services live under `src/WindowsUtilityPack/Services/` and are organized by domain
- Theme and styling are driven by merged resource dictionaries and semantic brush/style resources

At a high level, the runtime flow is:

1. `App` initializes shared services
2. `App` registers tool metadata and ViewModel factories in `ToolRegistry`
3. `NavigationService` is populated from the registry
4. `MainWindow` hosts a `ContentControl` bound to the current ViewModel
5. WPF resolves the correct View from the registered `DataTemplate`

## Repository Layout

```text
Windows-Utility-Pack/
|- src/WindowsUtilityPack/              Main WPF application project
|  |- App.xaml(.cs)                     Startup, resources, tool registration, composition
|  |- MainWindow.xaml(.cs)              Shell window and notification/status surfaces
|  |- Commands/                         RelayCommand and AsyncRelayCommand
|  |- Controls/                         Shared WPF controls
|  |- Converters/                       WPF value converters
|  |- Models/                           Shared models and tool metadata
|  |- Resources/                        Shared styles, icons, scrollbar and input resources
|  |- Services/                         Shared application and domain services
|  |- Themes/                           Dark, Light, and Aurora theme dictionaries
|  |- Tools/                            Tool-specific ViewModels and Views by category
|  |- ViewModels/                       Shell and shared ViewModel infrastructure
|  |- Views/                            Cross-tool views such as Home and Settings
|- tests/WindowsUtilityPack.Tests/      xUnit test project
|- BenchmarkSuite1/                     Optional BenchmarkDotNet project
|- docs/                                Audits, architecture notes, and feature references
|- WindowsUtilityPack.sln               Primary solution entry point
|- WindowsUtilityPack.csproj            Root shim project for restore and dependency tooling
```

## Installation and Setup

### Prerequisites

- Windows 11 or Windows 10 with desktop runtime support
- .NET 10 SDK
- Optional: Visual Studio 2022 or later with .NET desktop development workload

### Clone the repository

```powershell
git clone https://github.com/0langa/Windows-Utility-Pack.git
cd Windows-Utility-Pack
```

### Restore dependencies

```powershell
dotnet restore WindowsUtilityPack.sln
```

## Build, Test, and Run

### Build the solution

```powershell
dotnet build WindowsUtilityPack.sln
```

### Run the automated tests

```powershell
dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj
```

### Run the application

```powershell
dotnet run --project src/WindowsUtilityPack/WindowsUtilityPack.csproj
```

### Run benchmarks (optional)

```powershell
dotnet run -c Release --project BenchmarkSuite1/BenchmarkSuite1.csproj
```

### Important project-file note

The root-level `WindowsUtilityPack.csproj` is a tooling shim for restore and dependency scanning. It is not the actual desktop app project. Use the solution or `src/WindowsUtilityPack/WindowsUtilityPack.csproj` for normal development.

## Configuration and Environment Requirements

The app is local-first and does not require a `.env` file or external application configuration to start. Several tools do interact with the local machine, the network, or external executables, so effective behavior depends on environment and permissions.

### Runtime requirements and expectations

- WPF means the application itself runs on Windows only
- Registry-editing, hosts-file, and machine-scope environment-variable tasks may require elevation
- Downloader Studio can automatically manage external helper tools such as `yt-dlp`, `gallery-dl`, and `ffmpeg` under the local app data profile
- Network-dependent tools require internet or LAN connectivity to provide meaningful output

### Data and persistence locations

| Location | Purpose |
| --- | --- |
| `%LOCALAPPDATA%\WindowsUtilityPack\settings.json` | App settings, theme preference, home dashboard state, downloader settings, QR preferences |
| `%LOCALAPPDATA%\WindowsUtilityPack\app.log` | General application log |
| `%LOCALAPPDATA%\WindowsUtilityPack\downloader-history.json` | Downloader history |
| `%LOCALAPPDATA%\WindowsUtilityPack\logs\downloader.log` | Downloader diagnostics/event log |
| `%LOCALAPPDATA%\WindowsUtilityPack\tools\` | Downloader helper binaries such as `yt-dlp`, `gallery-dl`, and `ffmpeg` |
| `%APPDATA%\WindowsUtilityPack\vault.json` | Local Secret Vault encrypted data |
| `%APPDATA%\WindowsUtilityPack\hosts.backup` | Hosts file backup created by Hosts File Editor |

## Usage Guide

### First launch

1. Start the application.
2. Land on the Home dashboard.
3. Use the search box, favorites, recent tools, or category cards to open a tool.
4. Use the theme toggle in the shell header if you want Light, Dark, or Aurora mode.

### Typical workflow

1. Open a tool from Home.
2. Complete the task within that tool's dedicated workspace.
3. Return to Home via the shell branding button.
4. Reopen frequently used tools from Favorites or Recently Used.

### Recommended starting points by need

- Disk cleanup and storage analysis: `Storage Master`
- Startup and machine configuration: `Startup Manager`, `Environment Variables Editor`, `Hosts File Editor`
- File operations: `Bulk File Renamer`, `File Hash Calculator`, `File Splitter / Joiner`, `Metadata Viewer/Editor`
- Secure local utilities: `Password Generator`, `Hash Generator`, `Local Secret Vault`, `Certificate Inspector`
- Network diagnostics: `Ping Tool`, `DNS Lookup`, `Port Scanner`, `HTTP Request Tester`, `Network Speed Test`
- Download workflows: `Downloader Studio`
- Developer text and data workflows: `Text Format Converter`, `Regex Tester`, `JSON / YAML Validator`, `Diff Tool`, `Base64 / URL Encoder-Decoder`
- Visual or image tasks: `QR Code Generator`, `Color Picker`, `Image Resizer & Compressor`, `Image Format Converter`, `Screenshot Annotator`

## Known Limitations and Unfinished Areas

The application is functional and buildable in its current form, but the repo explicitly documents some open limitations:

- No CI workflow is currently checked into `.github/workflows`
- The codebase still relies on static `App.*` service access in some shell-facing paths
- Several feature areas have large hotspot files that are candidates for decomposition rather than redesign
- Downloader scheduling is one-shot only today
- Downloader segmented resume persistence across restarts is intentionally incomplete
- Some repository docs are historical or audit-oriented and should be read as engineering references, not product guarantees

## Documentation Map

The `docs/` folder contains useful reference material for both maintainers and reviewers.

| Document | Purpose |
| --- | --- |
| `docs/README.md` | Documentation index |
| `docs/FULL_AUDIT_REPORT.md` | Architecture and codebase audit summary |
| `docs/REPOSITORY_AUDIT_REPORT.md` | Latest repo-wide audit with current findings |
| `docs/IMPLEMENTATION_REFACTOR_PLAN.md` | Active engineering roadmap |
| `docs/DOWNLOADER_ARCHITECTURE.md` | Downloader Studio architecture and current constraints |
| `docs/QR_CODE_GENERATOR.md` | QR Code Generator implementation notes |
| `docs/homepage-personalisation.md` | Home dashboard behavior and metadata usage |
| `docs/UI_UX_OVERHAUL_REPORT.md` | Current design-system and theme direction |

## Development Notes for Contributors

### General expectations

- Build the solution before finishing changes: `dotnet build WindowsUtilityPack.sln`
- Run tests after code changes: `dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj`
- Use the main WPF project under `src/WindowsUtilityPack/`
- Treat the root `WindowsUtilityPack.csproj` as tooling-only unless you are working on dependency or restore workflows

### Architectural conventions in the current codebase

- Keep business logic in services and ViewModels, not in WPF code-behind
- Register tools through `ToolRegistry` in `App.xaml.cs`
- Add a matching `DataTemplate` in `App.xaml` for any newly introduced tool ViewModel
- Reuse shared styles and theme resources from `Resources/` and `Themes/`
- Preserve theme-awareness by preferring `DynamicResource` over hardcoded colors

### Tests and benchmarks

- The solution includes a dedicated xUnit test project
- The solution also includes `BenchmarkSuite1` for performance experiments
- Current roadmap items in the repo prioritize broader integration coverage and CI enforcement

## Roadmap

The repo contains a documented engineering roadmap, but not an explicit end-user feature expansion roadmap. The tracked next steps are primarily about stabilization and maintainability:

1. Add CI for restore, build, and test validation
2. Reduce static service-location coupling
3. Break up the largest service and ViewModel hotspots into smaller units
4. Align shim and app project dependency metadata more clearly
5. Expand integration coverage for shell and downloader flows

If you are looking for product scope, the implemented feature surface is already the primary source of truth: the current focus is hardening what exists rather than advertising unbuilt modules.

