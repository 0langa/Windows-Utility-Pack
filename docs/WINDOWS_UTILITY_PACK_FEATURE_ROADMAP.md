# Windows Utility Pack Feature Roadmap

Date: 2026-04-11

## Baseline Snapshot

The current repository already includes a broad tool surface and solid navigation wiring.

Implemented categories and major tools:
- System Utilities: Storage Master, Startup Manager, System Info Dashboard, Environment Variables Editor, Hosts File Editor.
- File and Data Tools: Bulk File Renamer, File Hash Calculator, Secure File Shredder, Metadata Viewer/Editor, File Splitter/Joiner.
- Security and Privacy: Password Generator, Hash Generator, Local Secret Vault, Certificate Inspector.
- Network and Internet: Ping Tool, DNS Lookup, Port Scanner, HTTP Request Tester, Network Speed Test, Downloader Studio.
- Developer and Productivity: Regex Tester, Text Format Converter, QR Code Generator, Color Picker, Timestamp Converter, UUID/ULID Generator, Base64/URL Encoder-Decoder, Diff Tool, JSON/YAML Validator.
- Image Tools: Image Resizer and Compressor, Image Format Converter, Screenshot Annotator.

Platform observations:
- Tool registration and DataTemplate mapping are centralized and broadly consistent.
- Settings persistence exists as JSON in LocalApplicationData.
- Service location still relies on static App properties.
- Shared operation persistence is fragmented across per-feature settings/history files.
- Global command palette, profile/workspace orchestration, and unified background jobs are not yet platform-level primitives.

## Gap Map Against Next Major Evolution

High-priority platform gaps:
- Unified local data store with migration support for cross-tool activity/history/profile data.
- Central audit and activity logging with exportable history surfaces.
- Global command palette with keyboard-first execution flow.
- Workspace/profile snapshots beyond simple shell preferences.
- Shared background task abstraction with cancellation/progress semantics reusable across tools.

High-priority enhancement gaps:
- Storage Master: richer duplicate preview workflows, stronger visualization, policy automation depth.
- Startup Manager: integrated autorun plus scheduled task control center and impact modeling.
- System Info Dashboard: richer realtime telemetry and export/report maturity.
- Network and Port tools: fingerprinting, profiles, stronger diagnostics workspaces.
- HTTP tester and secret vault: stronger collection/profile handling and lifecycle hardening.

New first-class tool gaps:
- Process Explorer, Registry Editor, Event Log Viewer.
- Clipboard Manager, Cron/Task Scheduler UI, Log File Analyzer, Markdown Editor, API Mock Server.
- SSH/Remote Tool and Certificate Manager.

## Delivery Phases

### Phase 1: Platform Foundations
- Add SQLite-backed local app data service with versioned migrations.
- Add centralized activity log service and event categories.
- Add command palette service and shell overlay with Ctrl+K.
- Add workspace profile persistence service.
- Add reusable background task service primitives.

### Phase 2: Existing Tool Upgrades
- Upgrade Storage Master, Startup Manager, System Info, Network stack, Port Scanner, HTTP tester, Secret Vault, Screenshot Annotator, Diff Tool, Regex Tester, and Text Converter using shared foundation services.

### Phase 3: New Tools
- Add prioritized new tools using the established category and registration patterns.
- Reuse shared persistence, export, and task orchestration services.

### Phase 4: Hardening and Documentation
- Expand tests for service logic, parsing, persistence, and regression-prone interaction flows.
- Deliver architecture notes, migration notes, security notes, and feature docs.

## Acceptance Gates

- `dotnet build WindowsUtilityPack.sln` succeeds.
- `dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj` succeeds.
- Any new tool is registered in both `App.xaml.cs` and `App.xaml` with `ToolRegistry` participation.
- Theme compatibility remains intact using DynamicResource-backed styles.
- Error handling, cancellation, and input validation are explicit in all new shared services.