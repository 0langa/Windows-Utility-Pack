# Repository Recovery Audit

Date: 2026-04-10

## Scope
This audit covers the current state of `WindowsUtilityPack.sln` and the primary app project at `src/WindowsUtilityPack/WindowsUtilityPack.csproj`.

## Current Build/Runtime Blockers

1. Incomplete tool registration
- `App.xaml.cs` currently registers only a subset of implemented tools.
- Result: many implemented tools are unreachable from navigation/home.

2. Incomplete DataTemplate mapping
- `App.xaml` maps only a subset of ViewModels to views.
- Result: navigation to non-mapped tools will fail to render correctly.

3. Missing views for existing ViewModels
- `MetadataEditor` has `MetadataEditorViewModel` but no `MetadataEditorView`.
- `CertificateInspector` has `CertificateInspectorViewModel` but no `CertificateInspectorView`.

4. Empty scaffold directories for required tools
- `DeveloperProductivity/JsonYamlValidator`
- `FileDataTools/FileSplitterJoiner`
- `ImageTools/ImageResizer`
- `ImageTools/ImageFormatConverter`
- `ImageTools/ScreenshotAnnotator`

5. Category/UI wiring gap
- `Image Tools` category exists in source tree but category icon/wiring is not configured in tool registration.

## Implemented Tools (present with View + ViewModel)

### System Utilities
- Startup Manager
- System Info Dashboard
- Environment Variables Editor
- Hosts File Editor
- Storage Master

### File & Data Tools
- Bulk File Renamer
- File Hash Calculator
- Secure File Shredder

### Network & Internet
- Ping Tool
- DNS Lookup
- Port Scanner
- HTTP Request Tester
- Network Speed Test
- Downloader Studio

### Security & Privacy
- Password Generator
- Hash Generator
- Local Secret Vault

### Developer & Productivity
- Regex Tester
- Text Format Converter
- QR Code Generator
- Color Picker
- Timestamp Converter
- UUID Generator
- Base64 Encoder
- Diff Tool

## Partially Implemented Tools

1. Metadata Viewer/Editor
- ViewModel exists and supports filesystem + image metadata.
- Audio metadata currently not fully implemented (placeholder note indicates external library requirement).
- Missing view wiring.

2. Certificate Inspector
- ViewModel exists with URL/file/PEM inspection logic.
- Missing view wiring.

3. DNS Lookup
- Currently focused on A/AAAA/CNAME; MX/TXT support missing.

4. UUID Generator
- UUID generation implemented; ULID support missing.

5. Base64 Encoder
- Supports Base64/URL/HTML encode-decode but naming/surface should reflect unified Base64 + URL encoder/decoder scope.

## Missing Tools (not implemented yet)

- File Splitter / Joiner
- JSON / YAML Validator
- Image Resizer & Compressor
- Image Format Converter
- Screenshot Annotator

## Registration / Navigation / Template Gaps

### App startup registrations missing
All currently present but unregistered tools must be registered in `App.xaml.cs` so they can be navigated.

### DataTemplates missing
All registered ViewModels must have matching DataTemplates in `App.xaml`.

### Category support
`Image Tools` category icon and registration path must be added.

## Dependency and Package Findings

- Existing packages are generally stable for current implemented features.
- New functionality likely requires:
  - `TagLibSharp` (`taglib`) for robust audio metadata/ID3.
  - `YamlDotNet` for dedicated YAML validation/formatting.
  - `SixLabors.ImageSharp` (+ `SixLabors.ImageSharp.Drawing` and `SixLabors.ImageSharp.Webp`) for image resize/convert/annotation workflows including WebP.

## Test Coverage Gaps (for touched areas)

- No tests for new/missing tools because they are not implemented.
- Sparse coverage for registry/template consistency with expanded tool set.
- No deterministic service tests for splitter/joiner, JSON/YAML validation, image processing, and ULID generation.

## Remediation Plan

### Phase 1 - Skeleton repair
1. Register all existing tools in `App.xaml.cs`.
2. Add all missing DataTemplates to `App.xaml`.
3. Add `Image Tools` category icon mapping.
4. Verify shell navigation can resolve all registered tools.

### Phase 2 - Missing tool implementation
1. Add `MetadataEditorView` and `CertificateInspectorView`.
2. Implement `JsonYamlValidator` tool (validation + pretty-format + error reporting).
3. Implement `FileSplitterJoiner` tool with checksum verification.
4. Implement `ImageResizer` and `ImageFormatConverter` tools.
5. Implement `ScreenshotAnnotator` with capture + basic annotation primitives.

### Phase 3 - Capability completion
1. Extend DNS tool for MX/TXT.
2. Extend UUID tool for ULID support.
3. Upgrade Metadata Editor with robust audio metadata via TagLib#.

### Phase 4 - Validation and delivery
1. Add/update xUnit coverage for services and deterministic viewmodel logic.
2. Run required validation:
   - `dotnet build WindowsUtilityPack.sln`
   - `dotnet test tests/WindowsUtilityPack.Tests/WindowsUtilityPack.Tests.csproj`
3. Commit and push; if direct `main` push blocked, push branch and report blocker.
