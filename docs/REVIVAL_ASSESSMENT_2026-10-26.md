# Windows Utility Pack revival assessment

Date: 26 October 2026. Original baseline: `1ea169c`. Scope: all 52 tools in seven categories, shell, persistence, dependencies, and delivery. Implementation began on `codex/revival-hardening`; publication was subsequently authorized. See the [v1.0.0 release notes](releases/v1.0.0.md) for the distribution scope.

This revision records repairs and remaining boundaries. The [native acceptance report](NATIVE_ACCEPTANCE.md) defines a concrete workflow for each of the 52 tools and the per-user installer lifecycle, with external UI Automation, independent output checks and disposable Windows state. The [workflow evidence matrix](REVIVAL_WORKFLOW_MATRIX.md) records each tool separately. The earlier [native retest report](REVIVAL_RETEST_2026-10-26.md) retains display, elevated-service and public-provider evidence.

## Development environment

| Area | Current state and practical consequence |
| --- | --- |
| SDK/runtime | SDK 10.0.401 resolves `global.json` (10.0.100 with latestFeature roll-forward). Windows Desktop and .NET runtimes 10.0.12 are installed. SDK 8.0.425 is also available. Build the solution or `src/WindowsUtilityPack/WindowsUtilityPack.csproj`. |
| SDK diagnostic exception | The automation shell omitted `PROCESSOR_ARCHITECTURE`; this caused the SDK workload probe's `InstallerBase` initialization exception. `scripts/Test-Local.ps1` restores the machine value only in its owned child processes. No SDK reinstall or persistent environment change was needed. |
| NuGet connectivity | This machine's initial .NET requests stalled with IPv6. The optional `-DisableIPv6` switch sets `DOTNET_SYSTEM_NET_DISABLEIPV6=1` only for validation children. It does not change Windows networking. |
| Local runner | `pwsh ./scripts/Test-Local.ps1` writes diagnostics, restore/build logs, TRX/coverage, and three-theme WPF composition observations to `output/validation`; it prints only failures. |
| Downloader engines | The native Install Tools workflow provisioned yt-dlp 2026.10.21, gallery-dl 1.32.18 and FFmpeg in an isolated app profile. Production engines downloaded/decoded public media and a gallery image. A repeat media request received a provider Cloudflare 403; authenticated providers and broad provider compatibility remain separate. |
| Packaging | Framework-dependent and self-contained win-x64 output, plus a per-user Inno Setup installer. Windows Sandbox exercises bundled-runtime startup, install/update/uninstall/reinstall, running-app guards and retained workspaces. Local setup is unsigned; the older-version fixture uses current-format state. |
| Root project | The legacy root compatibility project remains a separate, divergent entry point. Use the solution/main application project shown in README. |

The current local portable build is `output/revival/final-portable-win-x64/WindowsUtilityPack.exe`; keep its whole folder together. Setup is `output/installer/WindowsUtilityPack-1.0.0-win-x64-setup.exe`. Current native evidence is under `output/revival/native-results`; earlier retest evidence remains under `output/revival/native-retest`. Exact build identities and run IDs are in the native acceptance report.

## Dependency replacement

Image processing now uses [Magick.NET-Q8-AnyCPU 15.0.0](https://www.nuget.org/packages/Magick.NET-Q8-AnyCPU/15.0.0), with its Apache 2.0 license and bundled native codecs. ImageSharp, ImageSharp.Drawing, and the transitive Six Labors font dependency were removed. No license key, subscription, account, or separate ImageMagick installation is needed. Keep `ThirdPartyNotices/Magick.NET.txt` with distributed builds. Details and format/frame limitations are in [IMAGE_PROCESSING.md](IMAGE_PROCESSING.md).

The revival patch also adds the upstream-compatible SQLitePCLRaw 3.0.3 native bundle, PKCS 8.0.2 for benchmark tooling, and transitive NuGet auditing. Dependency advisory detection is distinct from proving an exploitable application path. [SQLite bundle guidance](https://github.com/ericsink/SQLitePCL.raw#bundles), [SQLite advisory](https://github.com/advisories/GHSA-2m69-gcr7-jv3q), [PKCS advisory](https://github.com/advisories/GHSA-846f-f764-76mx).

The QR tests use [ZXing.Net 0.16.11](https://www.nuget.org/packages/ZXing.Net/0.16.11) as an independent reader, only in the test project. It does not become a runtime image-processing dependency.

## Repair inventory

These IDs retain the original assessment's references. “Repaired” describes the specific code path and regression boundary, not blanket certification of the entire tool.

| ID | Repair and remaining boundary |
| --- | --- |
| F01–F05 | First-run NaN settings serialization; JSON/YAML and Markdown read-only bindings; missing Process Explorer converter/Markdown style; theme foregrounds and Process Explorer selection. Later native inspection also exposed unthemed grid selection and unwanted raw row details, corrected in shared styles. Visual coverage at mixed DPI remains open. |
| F06 | Split/join publishes only a completed, validated temporary result; rejects aliases, duplicate/missing parts and collisions; handles empty input; cancellation/corruption preserves an existing destination. |
| F07–F08 | Direct downloads reject HTTP errors, invalid ranges and inconsistent lengths. Repeated response headers and cookies are retained without duplicate-key exceptions. |
| F09–F10 | Added real WPF composition/coverage collection and replaced ImageSharp. Raster resource limits, same-path output, frames, alpha, annotations, HEIC, cancellation and failure preservation have service fixtures. |
| R01–R02 | One automation evaluation/dispatch owner, atomic cooldown evaluation, persistent independent IDs, all trigger types, dispatcher navigation, exact-name/process-identity targeting. Cleanup automation opens Storage Master for explicit scan/review; it does not delete unattended. |
| R03 | Scan completeness distinguishes skipped, filtered, inaccessible and depth-limited directories. Reparse points are not followed. Temporary-root boundaries, live cleanup revalidation, and aborted recycle operations are handled. Extension-only suggestions are not treated as automatic safe deletion. Duplicate deletion retains full-hash group evidence, rejects survivors selected directly or beneath selected directories, and holds a verified surviving file against writes/deletion during cleanup; repeated categories are deduplicated. |
| R04 | Settings use atomic publication, stale-snapshot field merging, a save outcome contract, and current-user DPAPI for saved request fields/downloader custom headers. Request header/query edits and removals persist immediately; loading cannot write a partially initialized request, and a failed save guards navigation. General activity/download history is not claimed to be encrypted or comprehensively redacted. |
| R05 | Vault version 2 uses AES-256-GCM and PBKDF2-SHA256, atomic encrypted backups, migration from legacy CBC, stale-writer detection, save-before-UI-commit, lock on navigation/disposal, and bounded unlock retries. Manual backup recovery and secret clipboard retention remain separate product work. |
| R06 | Download resumes require strong entity identity, clients use immutable connection snapshots and leases, target locks and unique staging isolate jobs, concurrent same-name jobs reserve and retry distinct output names, and redirects do not forward custom credentials across origins. Bandwidth scheduling and live provider behavior need further acceptance. |
| R07 | Registry import preflights the whole tree and all value types before mutations. Multi-string arrays preserve pipes/whitespace; ExpandString retains its raw form. Recoverable changes roll back on failure/cancellation. Fixtures use only disposable HKCU subtrees. |
| R08 | Hosts edits preserve unchanged lines, aliases, supported Unicode/BOM/newline encodings, and original bytes for backup. Failed backup, external edits, invalid content, or failed atomic replacement prevent a misleading save result. Non-UTF8 legacy text without a Unicode BOM is rejected rather than rewritten as ASCII. |
| R09 | Batch rename validates Windows names/collisions and stages the batch with rollback. Shredding pins an exclusive handle, rejects linked files, reports each outcome, and supports cancellation. Its UI no longer promises physical sanitization or “DoD” erasure. An overwrite already started cannot be undone. |
| R10 | Startup moves retain raw registry kinds and reject collisions. Environment rename deletes the original only after creating the new entry and rolls back recoverable failures. Environment drafts have navigation guards. Native acceptance exercises real Run entries and User/Machine environment changes inside disposable Windows Sandbox; denied-write cases remain additional acceptance. |
| R11–R15 | Port scan scheduling/draining, DNS/HTTP/TLS budgets, bounded HTTP bodies, crawler same-origin redirects/counts, background event-log queries, structured locale-independent task queries, and owned child-process termination were repaired. OS permissions and public networks remain distinct test boundaries. |
| R16 | SSH is explicitly a profile/TCP/PowerShell-command helper. Literal argument quoting is exercised through a real PowerShell argument-capture fixture; TCP reachability does not imply SSH login/authentication. |
| R17–R19 | Registry keys identify navigation/pop-outs; veto happens before constructing another VM. Retained view timers/work deactivate, Home restores expansion state and shows categories on fresh profiles, and physical cores are reported separately from logical processors. |
| R20 | Diff matrices/text, regex matches/previews/time, log retention/lines, event queries and image work have bounds. CPU/file work moved off the dispatcher where applicable; both hash tools stream files and discard canceled/stale results. Very large storage trees still need profiling. |
| R21 | Metadata removal preserves TIFF pages and original audio container extension; writes stage before replacement. A generated tagged PCM-WAV fixture checks actual audio samples after stripping. Native 100%/125% screenshot and 300-DPI annotation/export checks are recorded in the retest report; additional configurations remain open. |
| R22 | Markdown dirty guards and atomic save, honest HTML-source label, stale timestamp clearing, removal of unsupported BLAKE2, UUID format updates, password length bounds, and strict Base64 decoding. |
| R23 | Mock-server start/stop/restart owns request tasks, bounds concurrency/body/log size and drains before releasing the listener. HEAD and concurrent loopback requests have fixtures. Invalid endpoint edits are caught at Save/Start, retain the draft and previous server configuration, and cannot escape the command as an application crash. |
| R24 | Local validation, repeatable composition, both publish forms and isolated-profile native launch added. The continuation adds all 52 native scenarios and installer lifecycle. Historical schema migration, broader DPI combinations and long-running soak remain additional acceptance. |
| R25 (new) | Independent decoding found default 300-DPI QR raster exports were cropped. Raster drawing now compensates for DPI, the selected quiet zone is applied once, invalid export parameters are rejected, and completed exports publish atomically. PNG/JPEG/BMP/transparent cases are decoded independently. |
| R26 (new) | JSON/YAML duplicate keys are rejected instead of silently discarding values. Input/node/depth/alias limits prevent unbounded normalization; recursive YAML aliases are rejected. |
| R27 (new) | Workspace application propagates a settings write failure. Process decoding errors retain their actual cause. Dependency downloads/extraction use bounded temporary files and checksums; canceled/missing FFmpeg archive content does not replace an installed executable. Updater stdout/stderr are drained together by the owned process runner. |
| R28 (retest) | Downloader provisioning selects exact checksum manifests and uses gallery-dl's moved official release source. Failed installation updates status. Gallery completion records real file paths/counts for history, including already-downloaded files. |
| R29 (retest) | Screenshot editor commands wrap/scroll; coordinate fields retain theme styling. Preview and annotations share source-pixel coordinates regardless of print DPI. Saving a copy keeps editable layers separate; replacing the source resets layers/history and invalidates the image cache. |
| R30 (native) | Diff Tool's missing InitializeComponent call left a blank view. The view initializes its XAML and composition rejects empty tool content. |
| R31 (native) | Custom speed tests require both endpoint URLs instead of retaining the preset upload service. Phase-boundary cancellation sets a stopped status; HTTP Request Tester distinguishes cancellation and timeout. |
| R32 (native) | Color Picker captures the desktop for pixel selection, with Escape cancellation, instead of sampling the clicked button. |
| R33 (native) | Environment scope binds the ComboBox content value, enabling the intended User/Machine target. |
| R34 (native) | Scheduled tasks with absent next/last run dates retain their details through nullable date formatting. |
| R35 (native) | Storage cleanup controls wrap without overlapping. Registry and workspace action buttons retain their intended height. Native geometry assertions cover the affected panels. |
| R36 (delivery) | Added per-user self-contained setup, application mutex guards, retained profile/unknown-file behavior and an installer lifecycle runner. The build script requires an explicitly supplied compiler. |

## Evidence and remaining acceptance gates

The test suite mixes pure units, view-model tests, generated filesystem fixtures, disposable HKCU state, real child processes, and owned loopback HTTP/TCP services. The WPF harness records 159 compositions (Home plus all 52 tools in Dark, Light and Aurora), plus a real 300-DPI annotation-layout regression. Current counts and artifact paths are in the retest report; test count is not a measure of end-to-end completeness.

The native runner launches the actual published executable and has no application assembly reference. Every tool has its own action/assertion trace, control tree and screenshot. Cases include real native dialogs, independently decoded image/QR exports, clipboard and global-hotkey input, loopback HTTP/DNS/ICMP, and guest OS edits with restoration. The recorded scenario table describes precisely which paths each case exercises.

The main remaining acceptance work is:

- Extend beyond each recorded native scenario to remaining input/format combinations, sustained keyboard use, three-theme selected rows and additional detached-editor lifetimes.
- Extend the disposable-VM hosts/environment/startup/registry/task fixtures to denied-permission and interrupted-write UI paths. The retest exercised actual guest OS services without changing host configuration.
- Extend the app-managed downloader acceptance to queue recovery, authenticated providers and concurrency/throttling. Public providers can block repeated requests independently of the app.
- Extend historical database/schema combinations and signed distribution. The installer update uses a synthetic prior-version package; a separate native run checks vault migration from the actual published v0.1.0-alpha executable.
- Extend the 100%/125% image checks to additional monitor arrangements and primary-monitor scaling; profile large storage trees and long-running task/navigation resource use. Certificate trust/expiry combinations and unusual resolver/network behavior need broader coverage.

Ignored local evidence lives under `output/revival` and `output/validation`, including `hardening-progress.md`, focused before/after logs, publish logs, TRX/coverage and composition JSON. Logs can contain local paths and process names; they are not release assets. The earlier whole-patch review raised four material findings (duplicate survivors, invalid mock endpoint saves, concurrent download names, request-row persistence), each repaired with a reproducing regression. A separate final review of this native/delivery continuation identified two weak native assertions and a stale cancellation status; those findings and their disposition are recorded in the ledger. This assessment records local acceptance; remote workflow and release records are maintained on GitHub.
