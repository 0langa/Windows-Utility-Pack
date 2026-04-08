You are working inside an existing Windows desktop application repository built with C# / .NET 10 / WPF / MVVM.

Your task is to completely redesign and re-implement the existing Downloader module so it becomes a serious, production-grade, premium-level download manager and asset acquisition tool while still being understandable and efficient to use.

This is a full rewrite / re-architecture task, not a small patch.
Do not preserve weak legacy structure just because it exists.
Preserve only what is actually sound, useful, and aligned with the new architecture.

The final Downloader must feel much closer in capability and polish to established desktop download managers such as Internet Download Manager and the more advanced link-crawling / package-oriented workflows of JDownloader, while also leveraging specialized engines already relevant to this project’s direction (for example video/media download engines and gallery extraction tools). The final product must be modern, robust, highly versatile, and deeply integrated into the existing app.

==================================================
PRIMARY OBJECTIVE
==================================================

Fully rewrite the Downloader feature so that it can:

1. Handle standard direct file downloads extremely well
2. Handle website media extraction/download workflows cleanly
3. Handle deep asset discovery / crawling / scraping use cases
4. Support advanced download management workflows
5. Provide strong visibility through progress, status, history, and optional logging
6. Offer comprehensive settings for many kinds of websites and download scenarios
7. Stay intuitive enough for common tasks like:
   - downloading a file from a direct link
   - downloading video/audio from supported media pages
   - downloading all images/documents/assets from a page or a crawl scope
   - queueing many downloads and controlling them professionally
8. Integrate cleanly into the existing application architecture, navigation, styles, and settings system

This is not a prototype. Implement a complete feature that is immediately usable after completion.

==================================================
PRODUCT DIRECTION / QUALITY TARGET
==================================================

The result should feel like a premium Windows utility.

Think in terms of:
- strong UX for beginners
- deep controls for advanced users
- clean MVVM architecture
- reliable background work
- stable long-running operations
- rich queue management
- intelligent engine selection
- excellent observability
- safe and predictable error handling

The current Downloader is not enough.
You are replacing it with a comprehensive download workstation.

==================================================
REPOSITORY / ARCHITECTURE CONTEXT
==================================================

Adapt the implementation to the existing repository patterns and structure.

Follow the app’s established conventions:
- WPF
- MVVM
- ViewModelBase
- services for business logic
- tool registration via ToolRegistry / App startup wiring
- DataTemplate-based navigation
- centralized logging/settings/dialog/file-picker patterns already used elsewhere in the project
- modern card/panel based UI styling consistent with the rest of the app

Use the existing downloader-related pieces only as reference where useful.
Refactor, replace, or split them as needed.

Keep the result cohesive and maintainable.
Avoid hacks, overgrown ViewModels, or business logic in code-behind.

==================================================
MANDATORY HIGH-LEVEL FEATURE SET
==================================================

The new Downloader must support multiple acquisition modes in one unified tool.

Implement a modern multi-mode downloader with modes such as:

1. Quick Download
   - simple URL input
   - auto detect best workflow
   - fast default download path
   - minimal friction

2. Media Download
   - for supported video/audio/media pages
   - format and quality choices
   - subtitles/thumbnails/metadata options
   - playlist/channel handling where supported

3. Asset Grabber / Page Scan
   - scan a page for downloadable assets
   - show found items before download
   - filter/select assets
   - download selected assets

4. Site Crawl / Deep Extraction
   - crawl same-site paths or selected subdirectories
   - collect downloadable assets across multiple pages
   - enforce crawl limits and safety settings
   - present results in a manageable selection UI

5. Queue Manager
   - manage many downloads
   - pause/resume/retry/reorder/categorize/prioritize
   - batch workflows
   - scheduling

These modes should feel unified, not like separate disconnected tools.

==================================================
CORE CAPABILITIES TO IMPLEMENT
==================================================

A. INPUT / LINK INGESTION
--------------------------------
Support multiple ways to add work items:
- single URL
- multiple URLs (multi-line paste)
- bulk paste of mixed text containing URLs
- clipboard monitoring option
- drag and drop of URLs / link lists / container-like text files if practical
- import from text file
- manual add dialog with advanced options
- add current URL text quickly and stage it before starting

Normalize and validate input:
- trim whitespace
- remove duplicates
- tolerate common paste noise
- detect malformed URLs cleanly
- classify URL types when possible
- support URL grouping into a batch

B. INTELLIGENT ENGINE SELECTION
--------------------------------
The downloader must intelligently choose the best engine/path per item.

Design an extensible engine strategy layer that can decide among:
- direct HTTP/HTTPS file download engine
- media extraction engine (for video/audio-heavy supported sites)
- gallery / collection extraction engine
- built-in page asset scraper/crawler
- manifest/stream workflow where appropriate
- fallback generic downloader

Do not hardcode everything into one class.
Create a strategy/engine model such as:
- IDownloadEngine
- DownloadEngineResolver
- DownloadJob orchestration
- capability detection
- probe/analysis stage before download when useful

For each added item, optionally perform:
- content/URL classification
- metadata probe
- filename discovery
- size detection if possible
- engine suitability scoring
- warnings or required user choices

C. DOWNLOAD JOB MODEL
--------------------------------
Create a strong internal model for download jobs and packages.

Each job should track things like:
- job ID
- source URL
- resolved URL if redirected
- display title
- output path
- output file name
- package/group ID
- category
- engine type
- status
- progress percent
- downloaded bytes
- total bytes if known
- speed
- ETA
- creation time
- start time
- completion time
- retry count
- priority
- segment count if applicable
- resumable yes/no
- selected format/profile
- authentication/cookie profile if relevant
- error summary
- detailed log path if enabled

Also support package/group concepts:
- group related links together
- allow package-level actions
- package title
- package output folder
- package asset count
- package aggregate progress

D. PREMIUM-LEVEL DOWNLOAD MANAGEMENT
--------------------------------
Implement professional download management behavior:
- start
- pause
- resume
- cancel
- retry
- retry failed only
- restart from scratch
- open containing folder
- open source URL
- copy URL
- remove from list
- clear completed
- clear failed
- reorder queue
- move to top / bottom
- priority system (high/normal/low)
- max simultaneous downloads setting
- queue limits
- optional auto-start on add
- optional staged link collection before starting

Support resume/error recovery for capable download types.
Use part/incomplete files safely where appropriate.
Preserve temporary state cleanly.

E. DIRECT DOWNLOAD ENGINE
--------------------------------
Implement a high-quality direct download engine for ordinary files.

It should support:
- HEAD or metadata probing where useful
- follow redirects safely
- resumable downloads via range requests where supported
- chunk/segment download design where appropriate and safe
- configurable segment count for large downloads
- configurable retry/backoff
- rate limiting / bandwidth throttling
- timeout settings
- overwrite / auto-rename behavior
- file hash verification extension point
- last modified / content type handling
- partial file handling and cleanup rules

IMPORTANT:
Dynamic multi-connection/segmented downloading should be implemented thoughtfully.
Do not create a fake “premium downloader” illusion.
If the server supports ranged requests, use robust segmented downloading for qualifying file types/sizes.
If not supported, gracefully fall back to single-stream download.
Ensure segment merging and resume behavior are safe and correct.

F. MEDIA DOWNLOAD CAPABILITIES
--------------------------------
For media-heavy supported sites, provide a premium media workflow.

Capabilities should include as many meaningful options as practical:
- video/audio format selection
- quality/resolution choices
- audio-only extraction
- subtitle download
- thumbnail download
- metadata embedding where feasible
- playlist support
- per-item media info preview
- container/codec aware option presentation
- “best”, “balanced”, and “smallest reasonable” presets
- save captions/subtitles separately
- optional auto-convert/extract audio
- post-processing pipeline where relevant
- output template control
- authenticated download support using user-supplied cookies/profile when lawful and needed for content the user has access to

Do not add anything aimed at bypassing DRM or defeating access controls.
Keep the feature focused on legitimate downloading of accessible content.

G. PAGE SCAN / ASSET GRABBER
--------------------------------
Implement a LinkGrabber / Grabber style workflow inspired by advanced download managers.

User flow:
1. enter page URL
2. scan/probe page
3. extract candidate downloadable assets and linked resources
4. show staged results in a dedicated panel/list
5. allow filtering, sorting, grouping, previewing metadata
6. user selects what to download
7. selected items are added to queue/packages
8. optional auto-start after confirmation

The scan result UI should expose:
- asset name
- asset URL
- asset type
- extension
- detected size if known
- source page
- package/group
- selected checkbox
- availability status if checked
- warnings if suspicious/unavailable

Filtering should include:
- images
- video
- audio
- archives
- documents
- executables
- code/text/data
- fonts
- all
- custom extension filters
- host/domain filters
- size filters if known

Also allow:
- select all
- select visible
- deselect all
- invert selection
- download only unique assets
- group by type or source page

H. SITE CRAWL / DEEP EXTRACTION
--------------------------------
Implement controlled site crawling for deeper extraction use cases.

Capabilities:
- crawl same domain only by default
- optional subpath-only restriction
- configurable max depth
- configurable max pages
- configurable concurrent crawl workers
- duplicate URL prevention
- visited URL tracking
- optional robots/etag/last-modified awareness where useful
- user agent control
- domain allow/block lists
- file type targeting
- crawl preview counters
- stop scan gracefully
- streaming results into UI while scan continues

Extraction should inspect:
- HTML standard asset tags
- src/srcset
- audio/video/source
- links to documents/archives/etc.
- inline scripts where asset URLs exist
- embedded JSON where safe and practical
- common manifest/media references
- CSS references where useful
- lazy-load attributes
- canonicalized relative URLs

Do not let the crawler run uncontrolled.
Implement strong scope limits and cancellation.

I. RULES / PACKAGING / AUTO-ORGANIZATION
--------------------------------
Implement a rules system inspired by package/category automation workflows.

Support rules that can automatically:
- assign category based on extension/domain/URL pattern/content type
- set output folder
- rename file/package
- strip noisy text from file names
- group by domain / page / media type / date
- auto-apply tags
- choose engine preference
- auto-start or stage only
- attach comments/notes

Provide a UI for rule management if practical.
At minimum provide a clean architecture and a few default useful rules/presets.

J. CATEGORIES / DESTINATION MANAGEMENT
--------------------------------
Allow download categories similar to premium download managers.

Each category can define:
- name
- icon/glyph if useful
- default save folder
- extensions/patterns
- default behavior
- post-download actions

Example categories:
- Videos
- Audio
- Images
- Documents
- Archives
- Software
- Web Assets
- Mixed / General

Support:
- auto-category assignment
- custom categories
- per-category default directories
- domain-based save folders if user prefers

K. PROGRESS / STATUS / TELEMETRY
--------------------------------
The downloader must have excellent observability.

Implement:
- per-job progress bars
- per-package progress bars
- total/aggregate progress
- speed display
- ETA display
- bytes downloaded / total bytes
- active connections/segments if applicable
- current stage (probing, downloading, merging, processing, retrying, paused, verifying, extracting, etc.)
- status badges
- recent events view
- summary counters:
  - queued
  - active
  - paused
  - completed
  - failed
  - skipped

Progress bars must be smooth and meaningful, not fake.
Indeterminate states should be used honestly only when total progress is truly unknown.

L. OPTIONAL LOGGING / DIAGNOSTICS
--------------------------------
Implement comprehensive but optional logging features.

Logging should be designed in layers:
1. normal app log integration
2. downloader event log
3. per-job verbose diagnostic log (optional)
4. scan/crawl result summaries
5. failure diagnostics

Make logging configurable:
- Off
- Errors only
- Normal operational log
- Verbose / diagnostic

Possible logged events:
- job added
- probe started/completed
- engine selected
- redirect chain
- retries
- segment events
- pause/resume
- failures
- file completed
- scan summary
- extracted asset count
- final duration/statistics

Requirements:
- logging must never crash the app
- logs must rotate or stay bounded
- sensitive values should be handled carefully
- logs should be readable and useful for troubleshooting

M. HISTORY / RECENT DOWNLOADS
--------------------------------
Implement a useful history model:
- completed download history
- failed download history
- reopened source
- open file/folder
- redownload
- duplicate detection against history
- search/filter history
- clear history controls
- optional persistence across sessions using app settings or local data storage

N. SCHEDULER / AUTOMATION
--------------------------------
Implement a first-class scheduler inspired by premium download managers.

Capabilities:
- start queue at a specified time
- stop/pause at specified time
- periodic queue execution if practical
- “download then close app” option if suitable
- optional “download then sleep/shutdown” extension point if safe and aligned with app conventions
- scheduled site sync / re-scan extension point for future use

At minimum:
- one-time scheduled start
- scheduled stop/pause
- queue selection
- clear UI feedback about pending schedules

O. SETTINGS / ADVANCED OPTIONS
--------------------------------
Provide a comprehensive settings panel, but keep it structured and understandable.

Organize settings into tabs/groups such as:
- General
- Queue
- Connections
- Media
- Scan/Crawl
- File Handling
- Logging
- Advanced

Suggested settings include:
General:
- auto-start on add
- clipboard monitoring
- stage links before download
- default download folder
- duplicate handling mode
- close completed notifications
- theme-friendly density mode if useful

Queue:
- max concurrent downloads
- max retries
- retry delay/backoff
- priority defaults
- start next item automatically
- queue behavior on failure

Connections:
- segments per download
- timeout
- max redirects
- bandwidth limit
- proxy support architecture
- per-host connection cap
- user agent override
- cookie/profile selection architecture
- custom headers support

Media:
- preferred video format
- preferred audio format
- subtitle defaults
- thumbnail defaults
- output templates
- metadata preferences
- playlist behavior

Scan/Crawl:
- same-domain only default
- same-subpath only
- max depth
- max pages
- asset type filters
- content-type probing behavior
- scan linked scripts/json
- dedupe settings

File Handling:
- use .part files
- resume partial files
- overwrite / rename / skip
- preserve timestamps if possible
- sanitize filenames
- create category/domain subfolders
- filename templates

Logging:
- log level
- verbose per-job logs
- keep logs for N days/items
- open log folder
- export diagnostics

Advanced:
- developer diagnostics
- raw command preview for engine-backed workflows if appropriate
- custom engine arguments in a controlled/safe manner
- experimental features toggle

Persist settings properly.
Load and save them safely.

==================================================
UI / UX REQUIREMENTS
==================================================

The interface must be fully redesigned.
It should look like a serious premium desktop downloader, not a rough utility panel.

Build a layout that makes sense for both simple and advanced use.

Recommended overall layout:
- header with title + quick actions + summary stats
- top quick add / smart input bar
- mode tabs or segmented mode navigation
- main content area split intelligently
- queue/download list
- details / inspector panel
- optional scan result panel
- bottom status / diagnostics strip

The main UI should support:

1. Quick Add Area
- URL input
- paste/import buttons
- smart add
- scan page
- add to queue
- immediate download button

2. Download List / Queue
- DataGrid or highly usable list
- sortable columns
- filtering
- multi-select actions
- status icons
- progress bars inside rows
- speed/ETA columns
- category/engine columns
- context menu actions

3. Details / Inspector Pane
When a row is selected show:
- source URL
- resolved name
- engine
- destination
- segments
- retries
- logs
- metadata
- media format info
- errors/warnings
- file actions

4. Scan / LinkGrabber Panel
- staged discovered assets
- filters/search
- grouped results
- select/download actions

5. Settings Drawer or Advanced Panel
- collapsible advanced settings
- easy defaults
- preserve workspace clarity

UX expectations:
- powerful but not cluttered
- progressive disclosure of advanced controls
- disabled states when unavailable
- keyboard friendly
- good tab order
- clear button labeling
- strong visual hierarchy
- good empty states
- robust long text handling
- theme support
- high DPI support
- resizable layout
- minimal unnecessary scrolling

==================================================
ENGINEERING / IMPLEMENTATION REQUIREMENTS
==================================================

A. ARCHITECTURE
--------------------------------
Design the downloader as a set of focused services and models, for example:
- IDownloadCoordinatorService
- DownloadCoordinatorService
- IDownloadEngineResolver
- DirectHttpDownloadEngine
- MediaDownloadEngine
- GalleryDownloadEngine
- ScraperDownloadEngine
- CrawlService / AssetDiscoveryService
- DownloadQueueService
- DownloadPersistenceService
- DownloadSchedulerService
- DownloadRulesService
- DownloadLoggingService
- DownloadSettingsService
- DownloadHistoryService

These are examples, not mandatory names.
The important point is proper separation of responsibilities.

B. THREADING / ASYNC
--------------------------------
All long-running work must be asynchronous and responsive.
The UI must remain smooth during:
- probing
- downloads
- scans
- crawl operations
- log updates
- queue changes

Use cancellation tokens properly.
Support graceful cancellation.
Avoid race conditions when many jobs update simultaneously.

C. PERFORMANCE
--------------------------------
This tool may be used heavily.
Optimize for:
- many queued items
- large files
- long-running sessions
- many discovered assets
- large crawl results
- high-frequency progress updates without UI thrash
- bounded memory usage
- efficient deduplication and URL bookkeeping

D. ROBUSTNESS
--------------------------------
Handle failures professionally:
- network interruptions
- timeout
- redirect loops
- unsupported URLs
- unavailable content
- permission issues
- file locks
- invalid output paths
- insufficient disk space where detectable
- tool dependency missing
- downstream engine failure
- partial scan failure
- mixed success batch scenarios

Every failure should produce:
- correct status
- actionable message
- optional detailed diagnostics
- safe cleanup behavior

E. DEPENDENCIES
--------------------------------
You may use reliable libraries/tools when justified.

Select mature, actively maintained dependencies where they genuinely improve the implementation.

Potential dependency areas:
- advanced HTTP handling
- HTML parsing
- media download/extraction
- gallery extraction
- manifest/media handling
- persistence
- structured logging if useful

Requirements:
- do not add dependencies casually
- document every added dependency
- wire dependencies correctly
- keep the project buildable
- preserve licensing sanity
- add abstraction layers so the app is not tightly coupled to CLI output parsing everywhere

F. SECURITY / ETHICAL BOUNDARIES
--------------------------------
This downloader is for lawful downloading and user-authorized extraction.

Do not implement anything aimed at:
- bypassing DRM
- evading authentication systems illegitimately
- defeating anti-bot protections in abusive ways
- ignoring user permissions
- unauthorized access escalation

However, it is valid to support:
- user-provided cookies/session data for content the user is legitimately allowed to access
- authenticated downloads where the user supplies credentials/tokens through supported workflows
- respectful rate limits and concurrency controls
- domain scoping and safe crawling constraints

G. BROWSER / SYSTEM INTEGRATION (OPTIONAL IF CLEAN)
--------------------------------
If it can be added cleanly, support architectural hooks for:
- clipboard monitoring
- browser-captured URLs via paste/import workflows
- future browser extension/local helper integration
- context-menu / protocol integration extension points

At minimum, design the system so these can be added later without major rewrites.

==================================================
REWRITE EXPECTATIONS FOR CURRENT MODULE
==================================================

Treat the current Downloader implementation as insufficient baseline material.
You may reuse isolated pieces only when they are still good after review.

You should especially improve:
- architecture
- intuitiveness
- scan workflow
- queue handling
- status model
- progress reporting
- settings depth
- multi-download workflow
- logging/diagnostics
- extensibility
- file handling safety
- advanced crawling/scraping organization

This should be a full modernization, not cosmetic cleanup.

==================================================
TESTING / VALIDATION
==================================================

Before finishing, verify as much as possible.

At minimum validate:
- project compiles cleanly
- new downloader tool registers and navigates correctly
- direct download works
- multiple URL add works
- invalid URLs are handled well
- queue operations work
- pause/resume/cancel/retry behave correctly
- progress updates are correct
- scan/page asset discovery works on representative pages
- filtered asset selection works
- crawl constraints are respected
- completed downloads are saved correctly
- duplicate handling works
- settings persist
- history/logging work
- no obvious memory/resource leaks in normal use
- UI remains responsive during active operations
- theme/layout still works correctly

If the repository has or supports tests, add meaningful tests especially around:
- URL normalization
- engine selection
- filename sanitization
- category assignment
- duplicate detection
- retry policy logic
- settings serialization
- rules evaluation
- queue transitions
- scheduler logic
- crawl scope filtering
- progress math where unit-testable

==================================================
DOCUMENTATION
==================================================

Add clear XML docs and concise inline comments in critical areas.

Also create or update developer-facing docs describing:
- downloader architecture
- new services/components
- dependencies added
- settings model
- job lifecycle
- engine selection logic
- known limitations
- future extension points

Include a concise operator-facing summary if useful:
- how to use Quick Download
- how to use Media mode
- how to use Asset Grabber
- how to use Crawl mode
- where logs/history/settings live

==================================================
DELIVERY REQUIREMENTS
==================================================

Implement the full feature directly in the repository.

Make all necessary changes:
- views
- view models
- services
- models
- settings
- startup wiring
- tool registration
- documentation
- tests if applicable
- dependency references
- migration/refactor of existing downloader code

Do not leave the downloader half-finished.
Do not stop at “basic downloading works”.
Do not leave core functionality as TODOs.
Do not keep a confusing UI just because it already exists.

==================================================
FINAL RESULT EXPECTATION
==================================================

The final Downloader should feel like:
- a premium Windows download manager
- an advanced asset grabber
- a modern media downloader frontend
- a controlled website extraction tool
- a robust queue/scheduler system
- a maintainable enterprise-quality module inside this app

It should be strong enough that a user can rely on it for:
- normal file downloads
- large downloads
- many queued downloads
- media extraction workflows
- scanning pages for downloadable content
- crawling sites for assets within safe limits
- troubleshooting failures with real diagnostics
- organizing downloads intelligently

==================================================
FINAL REPORT
==================================================

At the end, provide a concise but useful implementation report covering:
- files added/changed
- major services/components introduced
- dependencies added
- features implemented
- what replaced the old downloader architecture
- settings added
- limitations or intentionally deferred items
- build/test/runtime validation performed