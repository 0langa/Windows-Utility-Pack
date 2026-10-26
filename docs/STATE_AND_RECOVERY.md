# State, recovery and operation limits

## Profiles and saved settings

Normal data remains under the application's Windows Utility Pack local/roaming app-data directories. For an isolated instance:

```powershell
.\WindowsUtilityPack.exe --profile-directory C:\absolute\path\to\test-profile
```

The override routes application-owned profile files to that directory. It does not virtualize Windows, the registry, hosts, startup entries, external processes, downloads or file dialogs. Use generated data and leave machine integrations off when testing. The WPF smoke host preconfigures hotkeys, tray and clipboard monitoring off.

Settings serialize first, then publish through a sibling temporary file. Changes from separate loaded settings snapshots merge by field; unrelated preferences are not overwritten by an older snapshot. Changes to the same field still follow the last writer. Lists are whole fields, not independently merged entries.

Saved HTTP request fields and downloader custom headers are protected using Windows current-user DPAPI. They are readable by the app running as that Windows user and are not a portable secret backup for another machine/user. General activity/download history is outside this encrypted-field guarantee. Save failures are surfaced through notifications or `TrySave` at callers that require an explicit outcome.

Malformed JSON settings are moved to a uniquely named `settings.corrupt-<timestamp>-<id>.json` recovery file before defaults are used. Other load failures, including inaccessible protected data, keep the original and may create a diagnostic copy. Such failures must not be treated as consent to overwrite the original. Preserve the original file before attempting manual repair.

## Vault migration and backup

`vault.json` version 2 uses authenticated AES-256-GCM. A newly created or migrated vault derives its key with PBKDF2-SHA256, a fresh 32-byte salt and 600,000 iterations. The encrypted document and individual values carry authentication tags. The app clears its owned key buffers and displayed draft on lock/navigation/disposal; managed strings and the Windows clipboard do not provide a guarantee of physical memory erasure.

Opening a compatible legacy vault decrypts and validates all entries before a single atomic migration. The preceding encrypted file is retained as `vault.json.bak`. Normal saves likewise retain an encrypted previous version. Concurrent instances reject stale writes instead of overwriting another instance's changes. Wrong passwords, tampering and failed publication preserve the current file and saved UI entries.

There is no password reset or recovery key. To recover manually from an encrypted backup, close all app instances, copy both the current file and `.bak` to a separate safe location, then restore a copy of the backup as `vault.json`. Unlock it using the password that encrypted that backup. Keep the original copies until its contents are inspected. The current UI does not implement a backup picker or automatic secret-clipboard expiry.

## Windows editor recovery

- Registry import validates every node/value before writing and records previous values for rollback. A rollback can itself fail due to changed permissions or concurrent external edits; that failure is reported. Existing legacy multi-string backups cannot recover literal pipe separators already lost by the old format. New backups use JSON string arrays.
- Hosts edits require a backup, compare the current on-disk bytes with the loaded copy, and replace the file atomically. Unchanged lines preserve original spacing/comments/aliases. Unicode BOM formats and strict UTF-8 are supported; undecodable legacy ANSI is rejected. Restoring a backup first saves a `.before-restore` copy of current content.
- Environment renames and startup moves reject destination collisions and restore recoverable intermediate changes. They are not full machine snapshots. Elevated/system-wide changes still require acceptance in a disposable Windows environment.
- Batch rename stages the selected batch and attempts rollback on failure. Errors include recovery paths when rollback is incomplete. Shredding is irreversible once overwriting begins, rejects hard links/reparse points, and cannot promise SSD/COW/back-up sanitization.
- Cleanup automation opens Storage Master for review. Cleanup recommendations are proposals; only complete scan evidence and live revalidation are eligible for a subsequent explicit deletion action. Duplicate cleanup requires a currently identical copy outside the full deletion selection, including directory descendants. Its surviving file stays open against writes/deletion until that removal finishes. This can reject cleanup when another process holds incompatible file access.

## Deliberate work limits

| Operation | Limit/behavior |
| --- | --- |
| HTTP response text | 4 MiB by declared and actual byte count, with per-operation time budgets. |
| Structured JSON/YAML | 1 million characters, 64 levels; YAML event/alias caps and recursive-alias rejection; duplicate keys rejected. |
| Diff | 1 million characters per side, 20,000 combined lines, 4 million LCS cells for nonidentical inputs. Oversized comparisons return an error instead of allocating an unbounded matrix. |
| Regex | Input 1 million characters, pattern 4096; 250 ms per-match timeout, 2-second overall evaluation budget, 1,000 retained matches with bounded previews. |
| Logs | Input file 256 MiB, line 65,536 characters, first 5,000 matching rows retained; total match count is reported separately. |
| Child processes | Default 2-minute budget and 8-million-character stdout/stderr caps. Explicit long-running media operations have their own lifecycle. Cancellation targets only the child tree owned by the runner. |
| Images | 256 frames, 32,768 pixels per side, 64 million decoded pixels total; see IMAGE_PROCESSING.md. |
| QR export | 120–4096 requested pixels; 72–1200 DPI. Integer modules and optional frame/caption determine final dimensions. |
| Dependency installers | Release metadata 4 MiB/30 seconds; download/extracted executable 512 MiB; download deadline 15 minutes; published checksums required. |

These limits are application safeguards, not hard real-time execution guarantees for every native codec, Windows API or filesystem operation. A canceled write is checked before publication; already completed irreversible overwrites cannot be undone.
