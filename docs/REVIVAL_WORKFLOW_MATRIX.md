# Revival workflow evidence matrix

26 October 2026. This inventory gives every registered tool the same evidence categories. A row is not a completion badge. “Native” means actual desktop control, not a view-model call.

All rows have source review and real WPF composition/layout in Dark, Light and Aurora. The native column records the completed 52-tool external UI Automation run; see [native acceptance](NATIVE_ACCEPTANCE.md) for its exact scope, run IDs and artifacts. The final column describes additional combinations beyond this bounded suite.

Fixtures and assertions live under `tests/WindowsUtilityPack.Tests/Services`, `ViewModels`, and `StorageMaster`. Reproduction commands are in `scripts/Test-Local.ps1`; local results and the execution ledger are under `output/validation` and `output/revival`. The latter are ignored artifacts, not source-controlled release material.

## System Utilities (14)

| Tool | Automated behavior evidence | Native evidence | Remaining acceptance boundary |
| --- | --- | --- | --- |
| Storage Master (`storage-master`) | Scan completeness, temp boundaries, live candidate and duplicate-survivor validation with a retained survivor handle, snapshots/duplicates/report services, storage VM fixtures. | Scan, duplicates, snapshot, CSV, canceled cleanup, recycle with retained duplicate survivor, Windows Recycle Bin recovery. | Large-tree memory, interrupted cleanup and denied access. Changing duplicate survivors retain separate regression fixtures. |
| Startup Manager (`startup-manager`) | Disposable HKCU moves preserve raw ExpandString and reject collisions. Windows Sandbox exercised an actual Run entry through disable/enable/remove while retaining its registry type. | Owned Run entry: disable/enable with raw ExpandString retained, CSV and removal. | Denied writes and startup-folder entries beyond the owned registry fixture. |
| System Info Dashboard (`system-info-dashboard`) | Report fixtures and physical-core enumeration; refresh/lifetime code. | Guest machine identity, logical CPU count and parsed JSON export. | Drive changes, remote sessions and sustained refresh. |
| Activity Log (`activity-log`) | SQLite-backed add/filter/retention/export service fixtures. | Actual app activity, CSV export, empty filter and canceled clear. | History redaction and larger retention/size budgets. |
| Background Task Monitor (`background-task-monitor`) | Tracker state/cancellation-token and monitor VM fixtures. | Completed storage task, detached monitor, cancellation stops a real port-scanner worker. | Native cancellation of storage workers and sustained concurrent workloads beyond the scanner case. |
| Event Log Viewer (`event-log-viewer`) | VM filtering/result fixtures; service has background execution, deadline and explicit errors. | Owned Windows event, source/ID filtering, CSV content and invalid-ID feedback. | Denied logs, large exports and deadline/cancellation combinations. |
| Hotkey Manager (`hotkey-manager`) | Binding/collision/service and VM fixtures. | Enable/save, global shortcut from another process, export/import, disable and OS registration release. | Conflicts, custom rebinding and additional keyboard layouts/chords. |
| Automation Rules (`automation-rules`) | SQLite independent IDs/schema migration, trigger validation, concurrent evaluation, dry-run, exact-target dispatch recorder. | Add/save, low-disk dry-run, reload and delete. | Elapsed cooldown and live action dispatch remain service-level coverage; native case uses dry-run. |
| Process Explorer (`process-explorer`) | Service/VM filters, identity-preserved selection, pinned process targeting, timer disposal. | Owned child selection/PID/details, canceled termination, actual termination. | Access-denied processes and sustained refresh under churn. |
| Registry Editor (`registry-editor`) | Disposable HKCU values, whole-import preflight, raw expandable/multi-string round trips. Windows Sandbox repeated typed backup/restore against actual guest registry keys. | Owned HKCU key: save, backup, edit, restore and delete; editor action height. | All registry value kinds retain service fixtures; native workflow uses an owned string value. Denied-write rollback remains additional acceptance. |
| Task Scheduler UI (`task-scheduler-ui`) | Local query and structured Unicode rows under German culture. Windows Sandbox created an owned task, queried/ran it through the production service, verified its marker and removed it. | Owned task without a next run: details, canceled run, execution marker and cleanup. | Denied task operations and unusual task configurations. |
| Workspace Profiles (`workspace-profiles`) | SQLite profile CRUD, capture/apply; settings-write failure propagation. | Save/apply, navigation/reopen, retained Unicode description and delete; action button bounds. | Missing tool keys and additional startup-layout transitions. |
| Environment Variables Editor (`env-vars-editor`) | Injected rename/collision/rollback and dirty-navigation fixtures. Windows Sandbox exercised create/collision/rename/delete of an owned machine environment variable. | User and machine scope, Unicode value, rename, copy, canceled delete and delete. | Denied machine writes and interrupted rename/edit operations. |
| Hosts File Editor (`hosts-file-editor`) | Unicode/BOM/aliases/unchanged lines, exact-byte backup and failed-write fixtures. Elevated Windows Sandbox exercised the actual hosts file through save/backup/restore and verified original bytes. | Invalid IP, owned entry save, backup restore and original-byte equality. | Denied ACLs and interrupted replacement; legacy ANSI input is explicitly rejected. |

## File & Data Tools (5)

| Tool | Automated behavior evidence | Native evidence | Remaining acceptance boundary |
| --- | --- | --- | --- |
| Bulk File Renamer (`bulk-renamer`) | Disposable rename collision, reserved-name, case-only/staging/rollback fixtures and VM tests. | Folder picker, preview, canceled apply, renamed bytes and confirmed invalid-prefix apply retaining the original path/bytes. | Large folders, interrupted multi-file rollback and user-driven undo. |
| File Hash Calculator (`file-hash-calculator`) | Known file vectors, stale-selection discard, streamed read and lifetime cancellation. | Known file digest, matching and mismatched expected hash. | Large/locked files and native cancel/retry combinations. |
| Secure File Shredder (`secure-file-shredder`) | Generated files: delete, pre-cancel, locked/missing input and hard-link rejection with both paths unchanged. | Generated fixture only: canceled confirmation, then actual removal. | Mid-write cancellation cannot restore overwritten bytes. SSD/COW physical erasure is not promised. |
| Metadata Viewer/Editor (`metadata-editor`) | Image strip/TIFF page preservation; tagged synthetic PCM-WAV is reopened and actual sample bytes compared; invalid-audio preservation. | Generated PNG metadata, stripped output and unchanged source. | Native audio import/export, other containers and photographic metadata; service fixtures cover tagged WAV and TIFF pages. |
| File Splitter / Joiner (`file-splitter-joiner`) | Real byte round trips, manifests/checksums, empty input, collisions, aliases, missing/corrupt parts, cancel and destination preservation. | Three parts, manifest/checksum checks, collision preservation, exact joined bytes. | Large inputs and mid-operation native interruption/retry; corruption/preservation have service fixtures. |

## Security & Privacy (5)

| Tool | Automated behavior evidence | Native evidence | Remaining acceptance boundary |
| --- | --- | --- | --- |
| Password Generator (`password-generator`) | VM tests cover classes, length bounds, generation and clipboard adapter. | Generated length, digits-only output and all enabled character classes. | Additional settings and clipboard workflows; the strength estimate remains a heuristic. |
| Hash Generator (`hash-generator`) | Independent known digest/CRC vectors across short reads, cancellation; streamed text/file implementation. | Independently known SHA-256 and MD5 text digests, clear. | Other algorithms/file modes, invalid encoding, large inputs and cancellation beyond service fixtures. |
| Local Secret Vault (`local-secret-vault`) | Real temporary encrypted files: create/unlock/lock, wrong password, tamper, legacy migration+backup, stale writer and failed write. | Create/save/lock, wrong password, unlock, copy, encrypted-file check and delete. | Published v0.1.0-alpha vault migration, retained encrypted copies and process restart have a separate native scenario. Other recovery combinations and automatic clipboard expiry remain open. |
| Certificate Inspector (`certificate-inspector`) | Endpoint parsing/deadlines reviewed; view composition only, no complete certificate fixture suite. | Generated certificate file, PEM/thumbprint agreement and invalid file. | TLS endpoint chain/hostname behavior, expired/future certificates, IPv6/custom ports and network cancellation. |
| Certificate Manager (`certificate-manager`) | PEM-format and VM tests with injected certificate rows. | Temporary current-user certificate, store filtering, independent PEM identity and empty filter. | Denied stores and additional certificate/store types. |

## Network & Internet (7)

| Tool | Automated behavior evidence | Native evidence | Remaining acceptance boundary |
| --- | --- | --- | --- |
| Ping Tool (`ping-tool`) | VM defaults, count bounds and command state tests; no actual ICMP fixture in the suite. | Actual loopback ICMP replies, stop and restart. | Unreachable hosts and varied ICMP/network policies. |
| DNS Lookup (`dns-lookup`) | Source review plus view composition; parallel bounded queries and cancellation added. | Owned DNS server provides A/AAAA/CNAME/MX/TXT, copied records, NXDOMAIN and clear. | Partial failures, unusual DNS packets and resolver/network changes. Controlled records and NXDOMAIN are covered. |
| Port Scanner (`port-scanner`) | Owned loopback open/closed ports and cancellation while workers are scheduled; VM state fixtures. | Owned loopback listener plus closed port, copied result, invalid range and stop. | Unreachable networks and sustained concurrent scans beyond owned loopback fixtures. |
| HTTP Request Tester (`http-request-tester`) | VM formatting/request fixtures plus shared bounded HTTP-body/cancellation logic. | Local POST echo preserves Unicode and headers; cancel and restart. | Native chunked/oversized responses and timeout combinations beyond bounded-body service fixtures. |
| Network Speed Test (`network-speed-test`) | Small local/stub request fixtures verify errors instead of false completed upload; state/latency helpers. | Owned download/upload endpoints, displayed throughput and cancellation. | Public endpoint availability and throughput calibration; native test measures owned loopback transfers. |
| SSH Remote Tool (`ssh-remote-tool`) | SQLite profile CRUD, command validation; real PowerShell argument capture preserves metacharacters/Unicode; TCP helper tests. | Profile save/reopen/delete, owned TCP reachability and command copy. This does not claim SSH authentication. | Authenticated SSH sessions and platform-specific terminal behavior; TCP reachability is not authentication. |
| Downloader Studio (`downloader`) | Loopback ranges/resume/identity/redirects/concurrent-name fixtures and queue/history tests. Actual app-managed engines produced a public MP3 and decoded Wikimedia image; repeat gallery download retained its file/history path. Repeat media request returned provider Cloudflare 403. | Default staged direct download, Start Selected, exact output bytes and history after reopening. | Authenticated/broader providers, interrupted engine updates and recovery. The earlier repeated public media request received provider HTTP 403. |

## Pentesting (5)

| Tool | Automated behavior evidence | Native evidence | Remaining acceptance boundary |
| --- | --- | --- | --- |
| HTTP Request Builder (`pentest-http-request-builder`) | Loopback HTTP transport/header/body/cancellation/response-bound fixtures; current-user DPAPI settings persistence; header/query edits and deletions across reopen, load suppression and failed-save draft guard. | Local JSON echo, response copy, cancellation and restart. | Additional authentication, multipart and network/error combinations beyond the defined scenario. |
| Header & Cookie Analyzer (`pentest-header-cookie-analyzer`) | Repeated headers/cookies, parsing and heuristic findings fixtures. | Shared response import, duplicate values in parsed output, both cookie names and Secure/HttpOnly/SameSite flags, malformed lines. | Additional malformed inputs and standards edge cases beyond duplicate/cookie fixtures. |
| JWT Inspector (`pentest-jwt-inspector`) | Malformed token and unsigned/missing-expiry fixtures; strict decode and numeric-date handling reviewed. | Generated JWT header/payload, malformed-token recovery. | Token inspection does not establish signature validity or issuer trust. |
| Site Map Explorer (`pentest-site-map-explorer`) | Two owned origins, redirects, case-sensitive paths, sitemap/page/depth bounds and cancellation. | Owned crawl and sitemap, case-sensitive paths in CSV, cancellation and clear. | Public-site variation, authentication and large crawls; redirect/scope limits have service fixtures. |
| Response Diff Viewer (`pentest-response-diff-viewer`) | Repeated headers/cookies and changed values; body diff service fixtures. | Shared response import, identical/different response comparisons. | Large, asymmetric and binary response handling; comparison is text-oriented. |

## Developer & Productivity (13)

| Tool | Automated behavior evidence | Native evidence | Remaining acceptance boundary |
| --- | --- | --- | --- |
| Regex Tester (`regex-tester`) | Pattern/flags/error/matches plus result/preview caps and bounded evaluation fixtures. | Known match count, invalid pattern and recovery with another pattern. | Native catastrophic input and sustained editing/navigation; bounded evaluation has service fixtures. |
| Text Format Converter (`text-format-converter`) | Detection/support/format/conversion fixtures across text and document paths; best-effort support messages. | JSON-to-XML conversion, independent XML parsing, copy and export. | Native DOCX/PDF/RTF content/layout, malformed documents and larger document budgets. |
| Log File Analyzer (`log-file-analyzer`) | Generated log counts/filtering; 10,001 matches retain first 5,000; oversized line rejection, VM tests. | Generated log counts, filtering and copied matching lines. | Native cancellation/large exports and multiline/severity ambiguity. |
| Markdown Editor (`markdown-editor`) | Load/render/stats service and dirty New/Open/navigation/save-failure/async-render VM fixtures. | Edit/render, save/reopen and canceled unsaved-change prompt. | Sustained typing/rendering and app-close combinations; rendered output is HTML source. |
| API Mock Server (`api-mock-server`) | Real loopback GET/POST/HEAD/404, concurrent requests, stop/restart/port release and VM fixtures; invalid Save/Start retain draft/current endpoint state. | Save/start, external HTTP client receives route body and 404, request log, stop/restart. | Native route-grid editing/persistence combinations and app exit while clients are active. |
| QR Code Generator (`qr-code-generator`) | Independent ZXing decodes PNG/JPEG/BMP and transparent QR files; invalid-format preserves original; URL/style heuristics. | PNG export independently decoded by ZXing; invalid URL feedback. | SVG/PDF, logos and additional style/format combinations beyond independent raster decoding fixtures. |
| Clipboard Manager (`clipboard-manager`) | SQLite persistence, duplicate/sensitive filtering, current-user DPAPI payload tests. | Consent, actual Windows clipboard event, filter/copy and delete. | Clipboard contention, pin/clear, sensitive filtering and cross-tool combinations beyond persistence/service fixtures. |
| Color Picker (`color-picker`) | Source review and view composition; no dedicated color/capture test suite. | HEX/RGB agreement, palette, known screen swatch and Escape cancellation. | Additional monitor/DPI arrangements, invalid HEX handling and long-lived palettes. |
| Timestamp Converter (`timestamp-converter`) | Out-of-range input regression clears stale custom output. | Negative Unix time, date conversion and out-of-range feedback. | Milliseconds, DST, culture and live-clock combinations. |
| UUID / ULID Generator (`uuid-generator`) | ULID generator cases; source reviewed format updates and separate generation paths. | UUID parsing, ULID alphabet/range, distinct bulk values and copying. | Additional formats/count limits and clipboard combinations. |
| Base64 / URL Encoder-Decoder (`base64-url-encoder`) | Strict encoding and debounce lifetime reviewed; view composition only, no full round-trip suite. | Unicode Base64 and URL round trips, literal plus, malformed input, clear. | ASCII/alternate encoding, form-versus-URL semantics and rapid mode changes. |
| Diff Tool (`diff-tool`) | Known Unicode edit results, matrix-budget rejection and cancellation fixtures. | Known differences, ignore-case behavior and clear; actual view content exists. | Large/asymmetric documents and navigation during comparison beyond budget/cancellation service fixtures. |
| JSON / YAML Validator (`json-yaml-validator`) | Valid/invalid documents, duplicate keys, oversize input, recursive aliases and ordinary alias preservation. | JSON formatting and independent parsing, invalid input, YAML recovery. | Scalar/normalization semantics and more alias/oversized cases beyond service fixtures. |

## Image Tools (3)

| Tool | Automated behavior evidence | Native evidence | Remaining acceptance boundary |
| --- | --- | --- | --- |
| Image Resizer & Compressor (`image-resizer`) | WIC fixtures and independent dimensions/aspect/alpha; cancellation/collision/invalid/bounded input cases. | File/folder pickers, independently decoded resized dimensions and empty-batch state. | Multi-file malformed/cancel/reopen cases and color-managed photographs. |
| Image Format Converter (`image-format-converter`) | Encoded format/frame/alpha/HEIC/same-path/collision/failure service matrix. | PNG-to-JPEG, independently decoded dimensions, collision suffix and preserved first output. | Native multi-file error/cancel/reopen cases and color-managed photographs. |
| Screenshot Annotator (`screenshot-annotator`) | Annotation types and exported pixels; save-copy/replace-source layer state, undo/redo and 300-DPI WPF layout regression. | Load, add rectangle, export, changed pixels and undo restoring original pixels. | More monitor/primary DPI arrangements, annotation types and sustained editing. Capture uses the virtual desktop. |

## Shell and delivery

- Home and command palette were used natively. Fresh Home exposes categories. Automated navigation cases cover veto-before-construction, invalid targets, history keys, activation/deactivation and detached-window identity.
- The actual self-contained published app used an absolute `--profile-directory` with hotkeys, tray behavior and clipboard monitoring disabled. File Hash Calculator used the real file dialog and a generated three-byte file. Its expected SHA-256 matched.
- Both Windows x64 publish forms include Magick.NET notices. Native portable and installer runs use bundled .NET/Desktop runtime in Windows Sandbox. Per-user install, update, running-app guards, uninstall, retained data and reinstall are recorded in the native acceptance report.
- The shared grid style is composed in all themes. Screenshot Annotator's detached window was opened/closed; broader selected-row and detached-editor workflows remain open.
- Actual hosts, User/Machine environment, startup, registry, scheduler, process termination and certificate fixtures ran through native controls only in disposable Windows Sandbox. Host configuration was preserved.
