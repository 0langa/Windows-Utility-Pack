# Windows Utility Pack — Status & Roadmap
_Portfolio audit: 2026-07-11_

## What this is
A modular WPF/.NET 10 desktop toolkit that bundles roughly thirty utilities into one
application shell, organized into seven categories: System Utilities, File & Data Tools,
Security & Privacy, Network & Internet, Developer Productivity, Image Tools, and
Pentesting. Architecture is MVVM with centralized tool registration
(`src/WindowsUtilityPack/Tools/ToolRegistry.cs`, `ToolBootstrapper.cs`), an xUnit test
project, and a GitHub Actions build-and-test pipeline on `windows-latest`.

## Current state
This is the flagship of the portfolio: 161 commits in an intense 16-day burst
(2026-03-29 to 2026-04-13), 313 source files, 104 test files, CI in place, and a deep
`docs/` folder (23 documents: audits, remediation reports, a feature roadmap, per-tool
design docs). The working tree is clean and `v0.1.0-alpha` was tagged 2026-04-11.

It has been untouched for ~3 months. Notable loose ends:

- Four unmerged remote branches (`claude/add-claude-documentation-LpmQ1`,
  `copilot/create-full-audit-report`, `copilot/enhance-http-request-tester`,
  `copilot/reconcile-technical-review-report`) plus ~10 already-merged stale branches.
- No distribution story: no installer, MSIX, or published release zip — the only way to
  run it is `dotnet run` from source.
- Two stray build artifacts tracked in git (`output/pdf/windows-utility-pack-summary.pdf`,
  `tmp/pdfs/...page-1.png`).
- `docs/WINDOWS_UTILITY_PACK_FEATURE_ROADMAP.md` (2026-04-11) describes a much larger
  platform vision (unified data store, command palette, background job system) that was
  never started.

No TODO/FIXME markers in source — the codebase is tidy for its size.

## Definition of "finished"
"Finished" for this project should mean **a shippable v1.0 of the current tool set**,
not the roadmap's platform vision. Concretely: the app builds and tests green in CI, has
a one-click distributable (self-contained publish zip or installer), a README with
screenshots, a triaged branch list, and a tagged `v1.0.0` GitHub release. The roadmap
items (command palette, unified store, telemetry) are explicitly deferred to post-1.0.

## Roadmap

### Phase 1 — Now (next 1-2 weeks)
1. Triage the four unmerged remote branches: merge what is wanted, delete the rest;
   prune all merged branches.
2. Remove the tracked `output/` and `tmp/` artifacts and extend `.gitignore`.
3. Verify a clean `dotnet build` + `dotnet test` on current SDK; fix anything rotted.
4. Do a manual smoke pass across all seven categories and log per-tool status
   (works / broken / stub) in a short checklist doc.

### Phase 2 — Next (2-6 weeks)
1. Add a `dotnet publish` self-contained release configuration and a CI job that
   attaches the zip to a GitHub Release.
2. Fix or hide any tools flagged broken in the smoke pass; polish the top 10 most-used.
3. Rewrite README as a product page: hero screenshot, tool list, download link.
4. Tag `v1.0.0` and publish the release.

### Phase 3 — Later (optional/stretch)
- Pick at most one roadmap platform primitive (global command palette is the highest
  leverage) and ship it as v1.1.
- Consider MSIX/winget packaging for real distribution.
- Archive the older audit reports in `docs/` into `docs/archive/` to reduce noise.

## Effort to "finished"
**M** — the code and CI are largely done; the remaining work is triage, packaging, a
release pipeline, and README polish rather than feature development.
