# Memory: WPF + WinForms tray interop configuration

## Metadata

- PatternId: MEMORY-WPF-WINFORMS-TRAY-INTEROP
- PatternVersion: 1
- Status: active
- Supersedes:
- CreatedAt: 2026-04-11
- LastValidatedAt: 2026-04-11
- ValidationEvidence: Tray mode implementation compiled cleanly after removing conflicting implicit global usings.

## Source Context

- Triggering task: Tray/background mode foundation with NotifyIcon.
- Scope/system: Shell integration and project configuration.
- Date/time: 2026-04-11

## Memory

- Key fact or decision: Keep `UseWindowsForms=true` for tray icon support, but remove implicit global usings for `System.Windows.Forms` and `System.Drawing`.
- Why it matters: Prevents widespread WPF type ambiguities while preserving explicit tray interop.

## Applicability

- When to reuse: Any WPF feature requiring WinForms interop.
- Preconditions/limitations: Requires explicit WinForms aliases/usings in implementation files.

## Actionable Guidance

- Recommended future action: After any SDK/global-using change, run full solution build before additional feature edits.
- Related files/services/components: `src/WindowsUtilityPack/WindowsUtilityPack.csproj`, shell code-behind files using tray APIs.