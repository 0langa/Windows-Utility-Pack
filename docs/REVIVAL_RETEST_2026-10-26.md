# Native and system acceptance retest

26 October 2026. This records the earlier resumed desktop, display and public-provider work on `codex/revival-hardening`. The later [52-tool native and installer acceptance report](NATIVE_ACCEPTANCE.md) supersedes this report's former open native/installer gates and artifact hashes. The [workflow matrix](REVIVAL_WORKFLOW_MATRIX.md) retains the current boundaries for every tool.

## Repairs found during the retest

- Downloader installation selected a detached checksum signature instead of the checksum manifest. Asset selection now matches the exact manifest filename; missing manifests still fail verification. Installation errors also replace the stale downloading status.
- gallery-dl's GitHub release had no executable assets after its [upstream move to Codeberg](https://github.com/mikf/gallery-dl/issues/9374). Provisioning now uses the official Codeberg release API and SHA256SUMS.
- Gallery completion omitted the downloaded path, leaving history without an output location, and counted skipped-file console lines as downloads. The engine now uses explicit completed/skipped-file events, counts unique existing files and supplies their actual nested path. Event behavior follows the [upstream command options](https://gdl-org.github.io/docs/options.html) and [event definitions](https://gdl-org.github.io/docs/configuration.html#metadata-event).
- Screenshot Annotator clipped commands outside the editor and lost theme styling on its coordinate fields. Toolbars wrap, the builder scrolls, and coordinate fields inherit the shared style.
- A 400x240 image tagged at 300 DPI occupied only 128x77 layout units beneath a 400x240 annotation canvas. The preview now explicitly uses the source-pixel dimensions. An empty selection no longer displays a stray handle.
- Saving an annotated copy reloaded the flattened result underneath the still-editable overlays. Copies now retain the original preview layer. Explicitly replacing the source flattens the document, resets annotation history and bypasses WPF's stale image cache.

## Observed coverage

| Area | Concrete evidence | Limit |
| --- | --- | --- |
| Native conversion | Generated 400x240 PNG imported using the actual file dialog, JPEG selected, output folder chosen and conversion executed. Repeating the operation created a separate filename and retained the original JPEG SHA-256. | Multi-file failures, cancellation and color-managed photographs are not covered by this native case. |
| Native resizing | Actual file/folder pickers and dimension fields produced a 200x120 JPEG from the same 400x240 fixture. Output was decoded separately. | One generated image, default aspect preservation and JPEG settings. |
| Native QR | Entered `https://example.com/wup-native-check?value=42`, inspected the preview and exported PNG at 300 DPI. Independent ZXing decoding recovered the exact URL. Raster size was 984x984, reflecting integer QR modules within the requested 1024-pixel size. | Other styles and vector exports retain their separate matrix boundaries. |
| Native screenshot/annotation | Captured the 4480x1440 virtual desktop with a primary 100% display and secondary 125% display. Moved the app between monitors. A 400x240, 300-DPI fixture occupied 500x300 physical pixels at 125%; a native drag created an 80x60 rectangle at source coordinates (20,21). Its exported 400x240 PNG was checked independently. Undo after export followed by another save reproduced all 96,000 original pixels. Builder commands were reached by scrolling; the detached editor was opened and closed. | Additional primary scaling, monitor arrangements, Color Picker and all annotation types through native input remain open. Capture currently targets the full virtual desktop, not a selectable region. Secondary scale was restored to 100%. |
| Downloader provisioning | Native Install Tools downloaded app-managed yt-dlp 2026.10.21, gallery-dl 1.32.18 and FFmpeg under the isolated profile. | Update rollback and native queue recovery remain open. |
| Real provider engines | Production media engine plus its app-managed FFmpeg exported an MP3 from the public W3 Sintel trailer. Production gallery engine downloaded/decoded Wikimedia's Example.jpg and retained the same path/bytes on a repeated download. These used no account cookies or paid service. | A repeat media request returned a Cloudflare HTTP 403. No attempt was made to bypass it. This is not broad or authenticated-provider acceptance. |
| Clean portable startup | Fresh Windows Sandbox launched the current portable build, with no installed SDK or `dotnet` on PATH. Loaded `coreclr`, `hostfxr` and `hostpolicy` came from the copied application folder. Latest recorded run: 11:43:01–11:43:30 UTC. | Portable launch, not installer/update/uninstall or clean-user migration. |
| Elevated guest operations | Actual guest hosts save/backup/restore; machine environment create/collision/rename; actual startup Run entry disable/enable/remove; typed registry backup/restore; owned scheduled task query/run/marker verification/removal. Fixtures restore/delete their owned changes. | Production services/view-models against real Windows state, not native mouse/keyboard flows for those five editors. Host configuration was not used as a destructive fixture. |
| Regression coverage | Local validation recorded 716 tests and 160 WPF observations: 159 three-theme compositions plus the 300-DPI annotation-layout case. The layout and gallery-history defects have before/after evidence. | Counts include unit and integration cases and do not establish 52 complete native workflows. |

Computer Custom's installed helper was updated from source build `dd1e48a261fb8a94` to `ff0374376462c9a5`, matching plugin 0.2.3. The input gate that incorrectly classified the palette text `Image Format` as a disk command was satisfied using the user's explicit confirmation. No policy was changed to avoid that gate.

## Reproduction and local artifacts

The current portable app is `output/revival/final-portable-win-x64/WindowsUtilityPack.exe`; keep the complete directory together. Framework-dependent output is `output/revival/publish-win-x64`. The native profile is isolated under `output/revival/native-retest/profile`, with hotkeys, tray behavior and clipboard monitoring disabled.

Run the ordinary regression suite with:

```powershell
pwsh -NoProfile -File scripts/Test-Local.ps1 -DisableIPv6
```

The optional live provider probe requires the app-managed tools already installed in an absolute isolated profile path. Its generated downloads are placed beside its report:

```powershell
dotnet tests/WindowsUtilityPack.UiSmoke/bin/Release/net10.0-windows/WindowsUtilityPack.UiSmoke.dll output/revival/provider-check.json --external-downloads C:\absolute\isolated-profile
```

Add `--gallery-only` for a targeted gallery rerun. The gallery probe also repeats against its existing file and checks that the bytes/path are retained. Provider availability can make this optional probe fail independently of local regression results.

`scripts/Run-SandboxAcceptance.ps1` and the harness's `--sandbox-services` mode require the configured disposable Windows Sandbox guest. The host refusal case returned code 3 without OS mutations. The local `.wsb` maps the portable app, probe and scripts read-only, and only its result folder writable.

Ignored evidence under `output/revival/native-retest` includes:

- `sandbox-output/clean-startup.json` and `services.json`: final guest startup and five OS fixture records.
- `external-before.json`: actual MP3 result; `external-after.json`: repeated media 403 and gallery image result.
- `gallery-history-before.json` and `gallery-history-repeat.json`: missing-history-path reproduction and corrected download/repeat records.
- `annotation-before.json` and `annotation-after.json`: actual 300-DPI WPF layout dimensions before/after repair.
- `evidence/native-qr.json`, `native-resize.json`, `native-annotation.json`, `native-undo-export.json` and `host-guard.json`.
- `exports/`: generated output files; `evidence/mixed-dpi-capture.png`: local desktop-capture fixture. Desktop captures stay local and are not release assets.
- Focused regression and publishing logs. The current full TRX, coverage and WPF reports remain under `output/validation`.

The application DLL used for this earlier retest had SHA-256 `33DC9C46CD4B44FC421A3D44591FB506C463F96EDABDC297DFE3A2893B72D45E`; its executable had SHA-256 `75281DCC20CAE05A7D6C7C48917F0CE1956CC5D50D22D26D16FBB4BE602F8750`. Current build identity is recorded in the later native acceptance report.

The later continuation adds a native workflow for every tool, a per-user installer lifecycle and a published-alpha vault migration in Windows Sandbox. Remaining boundaries include other historical schema combinations, broader provider/display configurations, long-running resource measurements and signed distribution. See the [v1.0.0 release notes](releases/v1.0.0.md) for distribution details.
