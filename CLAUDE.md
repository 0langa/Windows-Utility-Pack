# CLAUDE.md

## Project identity
Windows Utility Pack is a C# / .NET 10 / WPF / MVVM desktop application with xUnit tests.

Primary goals for all work:
- preserve build stability
- preserve existing behavior unless the task explicitly changes it
- prioritize reliability, responsiveness, maintainability, and polished desktop UX
- prefer incremental refactors over broad rewrites

---

## Repository shape

```
Windows-Utility-Pack/
├── src/WindowsUtilityPack/        # Main WPF application
├── tests/WindowsUtilityPack.Tests/ # xUnit test suite
├── BenchmarkSuite1/               # Performance benchmarks
├── docs/                          # Feature and audit documentation
├── .github/                       # Instructions, skills, agents, lessons, memories
├── WindowsUtilityPack.sln         # Solution file (3 projects)
├── WindowsUtilityPack.csproj      # Root CI shim (net10.0, non-Windows)
└── nuget.config                   # NuGet fallback package folder
```

### App source layout (`src/WindowsUtilityPack/`)

| Path | Purpose |
|------|---------|
| `App.xaml` / `App.xaml.cs` | Application entry point; service instantiation; tool registration |
| `MainWindow.xaml` | Shell window with navigation region |
| `Assets/` | Icons and logo images |
| `Behaviors/` | XAML-attached behaviors (e.g., `BindableRichTextBox`) |
| `Commands/` | `RelayCommand`, `AsyncRelayCommand` |
| `Controls/` | Custom WPF controls (e.g., `CategoryMenuButton`) |
| `Converters/` | 11 XAML value converters |
| `Models/` | 45+ shared data models (`ToolDefinition`, `StorageItem`, etc.) |
| `Resources/` | `Styles.xaml`, `InputStyles.xaml`, `Icons.xaml`, `ScrollBarStyles.xaml` |
| `Services/` | 100+ service files (see below) |
| `Themes/` | `DarkTheme.xaml`, `LightTheme.xaml`, `AuroraTheme.xaml` |
| `Tools/` | 46 tools across 6 categories (see below) |
| `ViewModels/` | `ViewModelBase`, `MainWindowViewModel`, `HomeViewModel`, `SettingsWindowViewModel` |
| `Views/` | `HomeView`, `SettingsWindow` |

### Tool categories and tools

Each tool lives in `Tools/<Category>/<ToolName>/` and contains its own ViewModel and View.

| Category | Tools |
|----------|-------|
| **System Utilities** | ActivityLog, AutomationRules, BackgroundTaskMonitor, EnvVarsEditor, EventLogViewer, HostsFileEditor, HotkeyManager, ProcessExplorer, RegistryEditor, StartupManager, StorageMaster, SystemInfoDashboard, TaskSchedulerUi, WorkspaceProfiles |
| **File & Data Tools** | BulkFileRenamer, FileHashCalculator, FileSplitterJoiner, MetadataEditor, SecureFileShredder |
| **Security & Privacy** | CertificateInspector, CertificateManager, HashGenerator, LocalSecretVault, PasswordGenerator |
| **Network & Internet** | DnsLookup, Downloader, HttpRequestTester, NetworkSpeedTest, PingTool, PortScanner, SshRemoteTool |
| **Developer & Productivity** | ApiMockServer, Base64Encoder, ClipboardManager, ColorPicker, DiffTool, JsonYamlValidator, LogFileAnalyzer, MarkdownEditor, QrCodeGenerator, RegexTester, TextFormatConverter, TimestampConverter, UuidGenerator |
| **Image Tools** | ImageFormatConverter, ImageResizer, ScreenshotAnnotator |

### Services layout (`src/WindowsUtilityPack/Services/`)

| Subdirectory | Purpose |
|---|---|
| *(root)* | Core cross-cutting services: `ActivityLogService`, `AppDataStoreService`, `BackgroundTaskService`, `ClipboardService`, `FileDialogService`, `FolderPickerService`, `HomeDashboardService`, `LoggingService`, `NavigationService`, `NotificationService`, `SettingsService`, `StartupDiagnosticsService`, `SystemInfoReportService`, `SystemVitalsService`, `ThemeService`, `UserDialogService`, `WorkspaceProfileService`, + interface-only files for tool-specific services |
| `Downloader/` | 27 files: coordinator, scheduler, history, input parser, event log, known hosts, settings, web scraper, YouTube plan builder; `Engines/` subdir has 5 download engines (Direct, Media, Gallery, Fallback, Base) |
| `Storage/` | 16 files: scan engine, snapshot, duplicate detection, drive analysis, cleanup recommendations, automation policy, shell file operations, elevation, report |
| `QrCode/` | 13 files: QR code generation, styling presets, export (PNG/SVG), scannability report |
| `TextConversion/` | 11 files: format conversion, preview document builder, preview window service, export, STA thread invoker |
| `ImageTools/` | Image processing service |
| `FileTools/` | File split/join service |
| `Identifier/` | ULID generator |
| `StructuredData/` | JSON/YAML validation service |

### Tests (`tests/WindowsUtilityPack.Tests/`)

75+ xUnit test classes organized under:
- `Services/` — one test class per service (50+ files)
- `StorageMaster/` — storage-specific tests (8 files)
- `ViewModels/` — ViewModel unit tests (17+ files)

### Key documentation (`docs/`)
Notable files: `DOWNLOADER_ARCHITECTURE.md`, `QR_CODE_GENERATOR.md`, `UI_UX_OVERHAUL_REPORT.md`, `FULL_AUDIT_REPORT.md`, `homepage-personalisation.md`, `WINDOWS_UTILITY_PACK_FEATURE_ROADMAP.md`.

### AI guidance (`.github/`)
- `instructions/` — coding standards (C#/WPF/MVVM, security, testing)
- `skills/` — reusable delivery workflows
- `agents/` — specialized agent configs
- `Lessons/` — documented bug post-mortems (race conditions, static invalidation, etc.)
- `Memories/` — architectural decisions (tray interop, detached tool windows, safe patching)

---

## Architecture

### Service locator via `App.xaml.cs`
All services are instantiated manually in `App.OnStartup()` and exposed as static properties on `App`. There is no DI container — constructor injection is done by hand using these static references:
```csharp
App.ThemeService, App.NavigationService, App.SettingsService, App.LoggingService, ...
```
When adding a new service, follow this pattern exactly: add the static property, instantiate in `OnStartup()` in dependency order.

### Tool registration via `ToolRegistry`
`Tools/ToolRegistry.cs` is the single source of truth for all tool metadata.  
Tools are registered in `App.OnStartup()` using `ToolRegistry.Register(new ToolDefinition { ... })`.  
Each registration provides: `Key`, `Name`, `Category`, `Description`, `Icon`, and a `Factory` delegate `() => new ToolViewModel(...)`.  
A matching `DataTemplate` for the ViewModel type must exist in `App.xaml` to wire the view.

### MVVM conventions
- `ViewModelBase` is in `ViewModels/ViewModelBase.cs` — all ViewModels inherit from it.
- Tool-specific ViewModels and Views live **inside the tool's folder** under `Tools/`, not in the top-level `ViewModels/` or `Views/` directories.
- Commands use `RelayCommand` (sync) or `AsyncRelayCommand` (async) from `Commands/`.
- Business logic belongs in services; ViewModels orchestrate and expose state.
- Code-behind must stay minimal and declarative.

### Themes
Three themes ship: **Dark**, **Light**, **Aurora**. All theme-sensitive brushes and styles must use `DynamicResource`. Never hardcode colors. Reuse keys from the theme files before adding new ones.

### Persistence
- `AppDataStoreService` — SQLite-backed (via `Microsoft.Data.Sqlite`) for structured data.
- `SettingsService` — application settings (JSON, file-based).
- `WorkspaceProfileService` — workspace profile snapshots.
- Treat missing or corrupt files as recoverable; never crash silently.

---

## Build and validation

Always validate with:
```
dotnet build WindowsUtilityPack.sln
dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj
```
Do not claim success if either fails. Build even for narrow changes.

---

## Key NuGet packages

| Package | Use |
|---------|-----|
| `Newtonsoft.Json` | JSON serialization |
| `YamlDotNet` | YAML parsing |
| `HtmlAgilityPack` | HTML scraping |
| `DnsClient` | DNS lookups |
| `SixLabors.ImageSharp` (+Drawing) | Image processing |
| `QRCoder` | QR code generation |
| `Markdig` | Markdown rendering |
| `ReverseMarkdown` | HTML→Markdown conversion |
| `TagLibSharp` | Media file metadata |
| `Microsoft.Data.Sqlite` | SQLite persistence |
| `PDFsharp` / `PdfPig` | PDF read/write |
| `DocumentFormat.OpenXml` | Office document support |
| `xunit` 2.9.3 | Unit testing |

---

## Working style
- Analyze the relevant code path before editing.
- Reuse existing patterns and infrastructure instead of adding parallel systems.
- Fix root causes, not just visible symptoms.
- Keep changes coherent and production-safe.
- Avoid speculative rewrites.
- Keep outputs concise unless the user explicitly asks for a long explanation.
- Do not dump full file contents unless needed.
- Ask questions only when ambiguity would likely cause an incorrect or risky implementation.

---

## Architecture rules
- Preserve MVVM boundaries.
- Keep business logic out of code-behind.
- Keep views declarative and lightweight.
- Put workflow logic in view models and services.
- Use constructor injection where practical (wired manually in `App.OnStartup()`).
- Avoid introducing new global/static coupling unless there is already an established project pattern for it.
- Prefer extending existing services over creating duplicate ones.
- Keep naming and file organization consistent with nearby code.

---

## Tool registration rules
When adding or changing tools:
- use the existing `ToolRegistry` as the source of truth
- keep startup and navigation wiring consistent
- register new tools in `App.xaml.cs` following existing patterns
- ensure a matching `DataTemplate` mapping exists in `App.xaml`
- do not create duplicate tool metadata just for the homepage or one specific feature

---

## WPF and UI rules
- Use `DynamicResource` for all theme-sensitive brushes and styles.
- Preserve dark/light/aurora theme compatibility.
- Avoid hard-coded colors, spacing, and sizes unless there is a strong reason.
- Reuse existing styles from `Resources/Styles.xaml`, `InputStyles.xaml`, and related theme resources before adding new styles.
- Keep keyboard navigation, tab order, and focus behavior usable.
- Avoid fragile popup, dropdown, flyout, or overlay behavior.
- Design for desktop use first, but ensure layout remains stable under resize, DPI scaling, and different display settings.
- Reduce wasted space and visual clutter.
- Prefer compact, high-value UI over oversized decorative containers.

---

## Homepage and dashboard rules
- Keep the homepage tool-first.
- Favorites, recently used, category access, and search should rely on shared tool metadata rather than duplicated definitions.
- Homepage personalization must persist safely and fail gracefully if settings data is missing or corrupt.
- Do not break existing navigation when changing homepage UX.
- Avoid horizontal scrolling for core category discovery when a cleaner visible layout is practical.

---

## Async and responsiveness rules
- Use async for I/O and long-running operations.
- Do not block the UI thread with heavy work.
- Marshal UI-bound updates safely back to the UI thread where required.
- Handle cancellation where it is relevant.
- Avoid fire-and-forget unless failure is safely handled and intentional.

---

## Safety and robustness
- Validate all user input.
- Validate file paths and external content before processing.
- Handle null, empty, invalid, and missing-data cases explicitly.
- Fail gracefully on file I/O, permissions, parsing, serialization, and environment-dependent operations.
- Admin-sensitive features must degrade safely when elevation or access is unavailable.
- Do not log secrets or sensitive values.
- Prefer user-safe error messages with actionable guidance over raw exception text.

---

## State, settings, and persistence
- Preserve existing settings behavior unless the task explicitly changes it.
- Keep persisted data formats backward-compatible where practical.
- Treat corrupt or missing settings/state files as recoverable conditions.
- Do not silently discard important user state without reason.

---

## Testing rules
- Add or update tests for new service logic, validation, parsing, and non-trivial state behavior.
- Keep tests deterministic and avoid machine-specific assumptions where possible.
- Test invalid inputs, error paths, cancellation, and persistence edge cases when relevant.
- If a change is hard to test, create a seam instead of skipping testing entirely.

---

## Code quality
- Follow nullable reference safety.
- Prefer simple, explicit code over clever abstractions.
- Remove dead code created by a refactor.
- Avoid duplicated logic.
- Add XML docs for new public APIs and non-obvious behavior.
- Keep comments useful and durable; do not narrate obvious code.

---

## Delivery standard
Before finishing any task:
- ensure the solution builds
- run relevant tests
- check for nearby regressions
- verify bindings and runtime flow in affected WPF areas
- verify theme and layout behavior if UI was touched

Final responses should be concise and include:
- what changed
- important validation performed
- any remaining risks or blockers
