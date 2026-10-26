# Native Windows acceptance

The runner in `tests/WindowsUtilityPack.NativeTests` launches the published app as
a separate process. It has no reference to the application assembly. It uses
Windows UI Automation, actual file dialogs, keyboard input and screen pixels;
it checks resulting files, Windows state, network responses and exported images
independently. The complete suite requires Windows Sandbox.

The guest receives read-only application, runner, script and installer folders.
Only its report folder is writable on the host. System edits, process termination,
global hotkeys, clipboard monitoring, certificate-store changes, DNS changes and
installer operations stay in the disposable guest. Registry, environment, hosts,
certificate and DNS fixtures additionally restore their state in `finally` blocks.

## Tool scenarios

These are concrete workflow boundaries, not every possible input, feature
combination or Windows configuration. All 52 registered tools have a native case.

| Tool key | Native scenario and independent observation |
| --- | --- |
| base64-url-encoder | Unicode Base64 and URL round trips, literal plus, malformed input, clear. |
| json-yaml-validator | JSON formatting and independent parsing, invalid input, YAML recovery. |
| regex-tester | Known match count, invalid pattern and recovery with another pattern. |
| diff-tool | Known differences, ignore-case behavior and clear; actual view content exists. |
| timestamp-converter | Negative Unix time, date conversion and out-of-range feedback. |
| uuid-generator | UUID parsing, ULID alphabet/range, distinct bulk values and copying. |
| hash-generator | Independently known SHA-256 and MD5 text digests, clear. |
| password-generator | Generated length, digits-only output and all enabled character classes. |
| pentest-jwt-inspector | Generated JWT header/payload, malformed-token recovery. |
| http-request-tester | Local POST echo preserves Unicode and headers; cancel and restart. |
| port-scanner | Owned loopback listener plus closed port, copied result, invalid range and stop. |
| ping-tool | Actual loopback ICMP replies, stop and restart. |
| network-speed-test | Owned download/upload endpoints, displayed throughput and cancellation. |
| pentest-http-request-builder | Local JSON echo, response copy, cancellation and restart. |
| pentest-header-cookie-analyzer | Shared response import, duplicate values in parsed output, both cookie names and Secure/HttpOnly/SameSite flags, malformed lines. |
| pentest-response-diff-viewer | Shared response import, identical/different response comparisons. |
| certificate-inspector | Generated certificate file, PEM/thumbprint agreement and invalid file. |
| file-hash-calculator | Known file digest, matching and mismatched expected hash. |
| bulk-renamer | Folder picker, preview, canceled apply, renamed bytes and confirmed invalid-prefix apply retaining the original path/bytes. |
| file-splitter-joiner | Three parts, manifest/checksum checks, collision preservation, exact joined bytes. |
| secure-file-shredder | Generated fixture only: canceled confirmation, then actual removal. |
| metadata-editor | Generated PNG metadata, stripped output and unchanged source. |
| log-file-analyzer | Generated log counts, filtering and copied matching lines. |
| markdown-editor | Edit/render, save/reopen and canceled unsaved-change prompt. |
| text-format-converter | JSON-to-XML conversion, independent XML parsing, copy and export. |
| system-info-dashboard | Guest machine identity, logical CPU count and parsed JSON export. |
| env-vars-editor | User and machine scope, Unicode value, rename, copy, canceled delete and delete. |
| registry-editor | Owned HKCU key: save, backup, edit, restore and delete; editor action height. |
| hosts-file-editor | Invalid IP, owned entry save, backup restore and original-byte equality. |
| startup-manager | Owned Run entry: disable/enable with raw ExpandString retained, CSV and removal. |
| process-explorer | Owned child selection/PID/details, canceled termination, actual termination. |
| task-scheduler-ui | Owned task without a next run: details, canceled run, execution marker and cleanup. |
| event-log-viewer | Owned Windows event, source/ID filtering, CSV content and invalid-ID feedback. |
| activity-log | Actual app activity, CSV export, empty filter and canceled clear. |
| hotkey-manager | Enable/save, global shortcut from another process, export/import, disable and OS registration release. |
| automation-rules | Add/save, low-disk dry-run, reload and delete. |
| workspace-profiles | Save/apply, navigation/reopen, retained Unicode description and delete; action button bounds. |
| storage-master | Scan, duplicates, snapshot, CSV, canceled cleanup, recycle with retained duplicate survivor, Windows Recycle Bin recovery. |
| background-task-monitor | Completed storage task, detached monitor, cancellation stops a real port-scanner worker. |
| local-secret-vault | Create/save/lock, wrong password, unlock, copy, encrypted-file check and delete. |
| certificate-manager | Temporary current-user certificate, store filtering, independent PEM identity and empty filter. |
| ssh-remote-tool | Profile save/reopen/delete, owned TCP reachability and command copy. This does not claim SSH authentication. |
| downloader | Default staged direct download, Start Selected, exact output bytes and history after reopening. |
| pentest-site-map-explorer | Owned crawl and sitemap, case-sensitive paths in CSV, cancellation and clear. |
| api-mock-server | Save/start, external HTTP client receives route body and 404, request log, stop/restart. |
| qr-code-generator | PNG export independently decoded by ZXing; invalid URL feedback. |
| clipboard-manager | Consent, actual Windows clipboard event, filter/copy and delete. |
| color-picker | HEX/RGB agreement, palette, known screen swatch and Escape cancellation. |
| image-format-converter | PNG-to-JPEG, independently decoded dimensions, collision suffix and preserved first output. |
| image-resizer | File/folder pickers, independently decoded resized dimensions and empty-batch state. |
| screenshot-annotator | Load, add rectangle, export, changed pixels and undo restoring original pixels. |
| dns-lookup | Owned DNS server provides A/AAAA/CNAME/MX/TXT, copied records, NXDOMAIN and clear. |

## Repairs exposed by the native work

- Diff Tool had no initialized view content. Its code-behind now initializes the
  XAML, and composition checks reject empty tool views.
- Custom speed-test downloads retained the preset upload destination. Custom
  endpoints now require an explicit upload URL; stopping between phases also
  sets a terminal status. HTTP Request Tester distinguishes cancellation from errors.
- Color Picker sampled its own button. It now captures the desktop, accepts the
  selected screen pixel and supports Escape cancellation.
- Environment scope used the ComboBox item's display object as a value. User and
  Machine selection now binds the intended scope.
- Scheduled tasks without a next run lost their details through a null date call.
  Date formatting now handles absent next/last run times.
- Storage Master cleanup controls overlapped; Registry Editor and Workspace
  Profiles stretched action buttons vertically. Their layouts now constrain the
  controls, with native geometry assertions.
- DNS failures now include the query error detail. The malformed TXT response
  discovered during acceptance was a test-fixture length error, not a product
  decoder defect.

The final review tightened the header/cookie case to inspect result grids and the
rename case to actually confirm the rejected operation. A loopback test-server
race also needed correction: an EOF before complete headers is not an HTTP request.

## Installer lifecycle

`packaging/WindowsUtilityPack.iss` builds a per-user, self-contained x64 installer.
The lifecycle case exercises interactive cancellation, initial install and Start
Menu/registry registration, launch with bundled runtime, a saved workspace, blocked
update while running, update after exit, blocked uninstall while running,
uninstall, retained profile/unknown files, reinstall and reopened workspace.

The 0.9.0 package is a fixture built from current code with an older version.
It proves installer replacement and preservation of current-format state. It does
not prove migration from every historical schema or a previously shipped installer.
The app has no automatic update service: update means running the newer setup.
The local installer is unsigned; signing, public distribution and SmartScreen
reputation are separate delivery work.

## Published-alpha migration

A separate fresh-guest scenario launches the actual published `v0.1.0-alpha`
executable (SHA-256 `a09559b799425b827a1749e7476a2e55948ad6aea65d1b52eaa80a65b8eb60af`).
It creates a secret through the old native UI, then launches the current app with
the same guest account. It checks wrong-password preservation, migration to vault
format 2 in local app data, exact retained roaming and backup bytes, Unicode secret
and notes, and reopening in a new process. This covers the released alpha vault
format; it does not cover every historical/custom settings or database combination.

Run it in a fresh Sandbox after downloading the old asset to `output/installer`:

```powershell
gh release download v0.1.0-alpha --repo 0langa/Windows-Utility-Pack --pattern WindowsUtilityPack-0.1.0-alpha.exe --dir output/installer
./scripts/Submit-NativeAcceptance.ps1 -LegacyMigration
./scripts/Start-NativeAcceptance.ps1 -Watch
```

Recorded request: `4a72ccca-dba3-4a7b-bb66-3eaa97f4e60f`; report:
`output/revival/native-results/4a72ccca-dba3-4a7b-bb66-3eaa97f4e60f/legacy.json`.
It uses the same app assembly as the full native and installer runs below.

The compiler is Inno Setup 7.1.0. Its [upstream license](https://raw.githubusercontent.com/jrsoftware/issrc/is-7_1_0/license.txt)
allows use without a paid key. `Build-Installer.ps1` requires an explicit compiler
path and does not install software on the host.

## Reproduction and evidence

Commands are in the repository README. `Submit-NativeAcceptance.ps1` publishes the
runner and writes a new request ID. `Start-NativeAcceptance.ps1 -Watch` starts a
fresh disposable guest; subsequent requests reuse it. Restart the Sandbox after
changing guest bootstrap scripts or mappings. An installer run requires a fresh
guest without a prior installed copy.

Each tool produces a JSON action/assertion trace, final control tree and screenshot
under `output/revival/native-results/<request-id>`. Failures stop the sequence to
avoid continuing through an unexpected modal state. `native.json` contains the
tested application assembly SHA-256. Installer results are in `installer.json`,
including both setup hashes and the installed application hash. Setup logs and
screenshots of retained workspaces accompany that report.
These ignored artifacts can contain local paths and generated fixture secrets;
they are development evidence, not release payload.

Broader provider/authentication combinations, long-duration resource behavior,
additional Windows versions, hardware and DPI arrangements remain separate from
this bounded acceptance suite. Earlier public downloader and mixed-DPI evidence
is recorded in `REVIVAL_RETEST_2026-10-26.md`.

## Recorded run — 26 October 2026

- Full native request: `b20d910c-cc22-471a-9f7a-a64abe07d3d2`. All 52 cases have action traces, a final control tree and a screenshot in `output/revival/native-results/b20d910c-cc22-471a-9f7a-a64abe07d3d2`.
- Tested app assembly SHA-256: `A4A4B897A8FBD3956D3CF7316D8EF5DEDAB8BB172D75585C477A0EAA1F1802D6`.
- Installer lifecycle request: `2c7242bf-ea36-4285-884e-fe87ac3db594`; report: `output/revival/native-results/2c7242bf-ea36-4285-884e-fe87ac3db594/installer.json`.
- Tested setup SHA-256: `AB84CE3626AE5E2D4B9C8FC8537E0DF4A83DD1313F2EE6A740EF02DCF93B1621`. The installed application assembly matches the native/portable build above.
- Local regression/composition reports: `output/validation/tests/unit-tests.trx` and `output/validation/ui-smoke.json`.
- Local setup: `output/installer/WindowsUtilityPack-1.0.0-win-x64-setup.exe`. These acceptance runs preceded the separately authorized v1.0.0 publication; no host installation was performed.
