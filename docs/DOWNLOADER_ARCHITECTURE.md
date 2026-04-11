# Downloader Rewrite (2026)

## What Was Replaced

The old Downloader implementation was replaced by a service-oriented architecture with clear separation between:

- queue orchestration
- engine selection and execution
- asset discovery/crawl
- settings persistence
- scheduler
- diagnostics/event logging
- history persistence
- UI state orchestration

## Core Components

### Models (`Tools/NetworkInternet/Downloader/Models`)

- `DownloadJob`: full job lifecycle + telemetry (status, progress, speed, ETA, bytes, retries, segments, output).
- `DownloadPackage`: grouped package view with aggregate progress.
- `DownloadAssetCandidate`: selectable scan/crawl result items.
- `DownloadHistoryEntry`: persisted history for completed/failed/cancelled jobs.
- `DownloadEventRecord`: diagnostics/event stream items.
- `DownloaderEnums`: modes, engine types, statuses, priorities, filters, scheduler actions.

### Services (`Services/Downloader`)

- `DownloadCoordinatorService`: queue lifecycle, retries, cancellation/pause semantics, package aggregation, history recording.
- `DownloadEngineResolverService`: strategy resolver for selecting best engine per job.
- `DirectHttpDownloadEngine`: direct HTTP/HTTPS with probe, resume, segmented download, throttling, and safe file handling.
- `MediaDownloadEngine`: yt-dlp-based media acquisition pipeline.
- `GalleryDownloadEngine`: gallery-dl-based collection download pipeline.
- `FallbackDownloadEngine`: safe fallback to direct download path.
- `AssetDiscoveryService`: page scan / crawl orchestration with size/reachability probing.
- `DownloaderSettingsService`: downloader settings persistence via app settings.
- `DownloadHistoryService`: bounded JSON history persistence.
- `DownloadEventLogService`: event stream + rotating log file output.
- `DownloadSchedulerService`: one-shot queue start/pause scheduling.
- `DownloadInputParserService`: noisy text parsing and URL normalization.
- `DownloadCategoryService`: category/domain extension rules.
- `DownloaderFileDialogService`: import/cookie/diagnostics dialogs.

## Wiring and Navigation

- `App.xaml.cs` now composes the downloader service graph.
- Tool registration entry updated to **Downloader Studio**.
- Existing DataTemplate navigation remains intact through `DownloaderViewModel` + `DownloaderView`.

## UI/Workflow Summary

`DownloaderView` now provides:

- quick add + immediate download actions
- mode selection (Quick, Media, Asset Grabber, Site Crawl)
- queue manager with inspector and batch actions
- queue search and status filtering for large/busy job sets
- asset discovery/crawl panel with filtering and staged selection
- history tab with redownload action
- grouped settings + scheduler + diagnostics export
- event stream panel for live observability

Help/tutorial content is centralized via:

- `DownloaderHelpContentProvider`: single source for help topic list + detailed workflow guidance text.
- `DownloaderViewModel`: binds help topics from provider and supports searching by both topic title and full help content.

Queue/discovery presentation helpers:

- `DownloaderJobFilterMatcher`: testable queue-search/status filtering logic used by the Queue Manager view.
- `DownloaderWorkflowPredictor`: centralized workflow prediction for quick/media/discovery URL intent messaging.
- `BuildRouteReason` (`DownloaderViewModel.HelpAndRouting.cs`): mode-aware route explanation text so the UI can show why a URL was classified a certain way and what operation will happen next.

## Refinement Additions (Phase 2+)

- Route transparency:
  - Quick, Media, Asset Grabber, and Site Crawl now expose explicit route-reason strings in the workspace.
  - Messaging is mode-aware and prevents “mystery fallback” behavior.
- Media intent safety:
  - Media analysis messaging reinforces video-first defaults and explicitly explains when audio-only routing is active.
- Discovery clarity:
  - Asset Grabber/Site Crawl now surface routing intent alongside staged-selection summary to keep scan-first/crawl-first behavior obvious before queueing.

## Settings Model

Downloader settings are persisted in `AppSettings.DownloaderSettings` and include:

- general behavior
- queue limits/retry behavior
- connection and segmentation settings
- media defaults
- scan/crawl limits and probing
- file handling controls
- logging level
- advanced options
- category rules

## Diagnostics and Runtime Data

- Event logs: `%LOCALAPPDATA%\WindowsUtilityPack\logs\downloader.log`
- History: `%LOCALAPPDATA%\WindowsUtilityPack\downloader-history.json`

## Dependencies

No new NuGet packages were introduced for this rewrite.
The implementation reuses existing repository dependencies, notably:

- `HtmlAgilityPack` and `Newtonsoft.Json` for discovery/scraping support
- built-in .NET HTTP/process APIs for engine execution and direct download pipeline

## Extension Points

- add new `IDownloadEngine` implementations and register via resolver composition
- extend category/rule matching in `DownloadCategoryService`
- extend scheduler actions beyond one-shot start/pause
- add per-job hash verification/post-processing stages in coordinator/engine pipeline
- add richer per-site capability probing in resolver

## Known Deliberate Constraints

- segmented direct download resume is safe for fresh segmented runs; partial segmented resume restoration is not yet persisted across app restarts.
- advanced media option matrix is intentionally simplified to robust presets + settings-backed defaults.
- queue scheduling currently supports one-shot start/pause (not recurring schedules).
