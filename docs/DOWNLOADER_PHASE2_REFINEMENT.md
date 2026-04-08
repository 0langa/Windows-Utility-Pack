
IMPORTANT REPOSITORY REFERENCES

You must read and use ALL of the following documents before making changes:

- docs/DOWNLOADER_REWRITE_PROMPT.md (original architecture + feature scope)
- docs/DOWNLOADER_PHASE2_REFINEMENT.md (UX fixes, behavior corrections, and polish)
- docs/DOWNLOADER_ARCHITECTURE.md (system design, services, and responsibilities)

These documents serve different purposes:

- REWRITE → defines what the downloader must be capable of
- PHASE 2 → defines what is currently broken and how to fix it
- ARCHITECTURE → defines how the system is structured

Treat ALL of them as required specifications.
Do not ignore them.

If conflicts occur:
1. preserve architecture
2. fix behavior
3. improve UX


==================================================
ARCHITECTURE PRESERVATION RULE
==================================================

You must NOT:
- collapse services into ViewModels
- bypass DownloadCoordinator / EngineResolver
- duplicate logic across engines
- tightly couple UI with engines

You MUST:
- extend architecture cleanly
- refactor only when necessary
- keep responsibilities separated


==================================================
EXECUTION REQUIREMENTS (MANDATORY)
==================================================

Before coding, you MUST:

1. Summarize:
   - current downloader issues
   - root causes
   - affected components

2. Define implementation plan:
   - behavior fixes
   - routing fixes
   - UI restructuring
   - customization improvements
   - help system

Only then begin implementation.

DO NOT skip phases.
DO NOT mix phases randomly.


==================================================
CRITICAL BUGS (FIX FIRST)
==================================================

1. Media mode outputs MP3 instead of video
2. Mode routing is broken (Asset/Crawl → direct download)
3. Asset selection workflow unclear
4. UI overcrowded and poorly structured

Fix ALL before adding features.


==================================================
TASK TYPE
==================================================

This is a focused second-phase overhaul of the Downloader Studio feature.

You are not building the downloader from zero anymore.
You are taking the current rewritten downloader and pushing it from:
“architecturally strong but confusing / partially incorrect in real use”
to:
“intuitive, mode-correct, polished, and reliable enough to feel like a premium downloader UI.”

This is both:
1. a UX redesign task
2. a functional correctness / workflow completion task

Do not just add more controls.
Fix the actual user experience and the broken or misleading behavior.

==================================================
CURRENT REAL-WORLD PROBLEMS TO SOLVE
==================================================

The current downloader rewrite has a much better architecture already, but testing revealed important problems in behavior and usability.

You must explicitly address all of these:

1. MEDIA MODE BEHAVIOR IS WRONG / CONFUSING
- Pasting a YouTube link in Media Download mode currently results in an MP3 file in situations where the user expects a normal video download.
- The default media workflow must be video-first unless the user explicitly chooses audio-only.
- Media mode must never silently inherit an audio-only state in a way that surprises the user.
- Users must clearly understand:
  - what will be downloaded
  - in what format
  - with what quality/profile
  - whether they are downloading video or audio only

2. TOP INPUT FIELD BEHAVIOR IS TOO AMBIGUOUS
- When Asset Grabber or Site Crawl mode is selected, using the top input currently can trigger a direct/fallback download flow and produce a useless .bin file.
- This is unacceptable.
- Mode-specific user intent must be respected.
- If the selected mode is Asset Grabber, the primary action must be scan/discover/stage assets.
- If the selected mode is Site Crawl, the primary action must be crawl/discover/stage assets.
- The system must not fall back to direct file download just because a URL was entered in a generic field.

3. ASSET GRABBER / CRAWL RESULTS ARE NOT INTUITIVE TO ACT ON
- Users can discover assets, but the next step is not clear enough.
- It must be obvious how to:
  - review results
  - filter them
  - select them
  - add them to queue
  - download them now
- The selection/download workflow must feel like a real staged LinkGrabber workflow, not an unfinished side panel.

4. UI IS TOO DENSE / IMPORTANT PANELS ARE TOO SMALL
- The current workspace is overloaded.
- Important areas like the actual download list are too small.
- Too many unrelated controls share the same visible space.
- The next version must use stronger separation of concerns and clearer tab/workspace structure.

5. CUSTOMIZABILITY IS STILL TOO SHALLOW IN PRACTICE
- Many settings/models exist but are not surfaced cleanly enough or not fully wired into actual behavior.
- The downloader needs much more visible, usable customization across:
  - media
  - crawling
  - queue behavior
  - file handling
  - logging
  - categories/rules
  - scheduler
  - advanced engine behavior

6. HELP / LEARNABILITY IS INSUFFICIENT
- The downloader has become powerful enough that users need guidance.
- Add a detailed built-in tutorial/help system so users can understand every mode and workflow without guessing.

==================================================
PRIMARY OBJECTIVE
==================================================

Redesign and refine Downloader Studio so it becomes:

- intuitive for common tasks
- mode-correct in behavior
- explicit in what each action will do
- less cluttered
- more customizable
- easier to learn
- more consistent with premium downloader workflows
- more robust in actual day-to-day use

==================================================
HIGH-LEVEL PRODUCT DIRECTION
==================================================

The final downloader should feel like a hybrid of:

- a premium direct download manager
- a dedicated media downloader workspace
- a JDownloader-style staged LinkGrabber
- a controlled site crawl / extraction workstation
- a professional queue manager
- a diagnosable and teachable power-user tool

Do not aim for “everything visible at once”.
Aim for:
- mode clarity
- progressive disclosure
- larger, more useful primary work areas
- fewer ambiguous buttons
- obvious next steps
- strong defaults
- advanced controls where they belong

==================================================
REDESIGN STRATEGY (MANDATORY)
==================================================

Redesign the UI around dedicated workflows, not one crowded workspace.

Implement a tabbed workspace with a structure close to this unless you find a clearly better version:

1. QUICK DOWNLOAD
Purpose:
- fast everyday downloads
- direct links
- quick paste
- quick import
- immediate start or add to queue

Contents:
- single focused quick-input area
- clear engine prediction / detected type
- destination/category controls
- “Add to Queue” and “Download Now”
- recent quick actions
- no deep crawl/media-specific clutter

2. QUEUE MANAGER
Purpose:
- this is the main operational control center
- it must have the most space

Contents:
- large queue/download grid
- larger details/inspector pane
- package/group awareness
- reorder controls
- priority controls
- start/pause/stop/retry/remove actions
- aggregate progress / stats
- selected item details

This tab must prioritize readability and management over miscellaneous settings.

3. MEDIA DOWNLOAD
Purpose:
- dedicated flow for YouTube and similar sites
- explicit media analysis and choice

Contents:
- media URL input
- “Analyze” / “Fetch Formats” action
- rich metadata preview
- explicit format/profile selection
- explicit choice between:
  - video download
  - audio-only extraction
- quality/container options
- subtitle/thumbnail/metadata toggles
- playlist handling options
- cookie/profile/auth controls if supported
- output template / media naming settings relevant to media only

IMPORTANT:
Media mode must no longer be a vague preset wrapper.
It must clearly show what is about to happen before the download starts.

4. ASSET GRABBER
Purpose:
- scan a single page and stage downloadable items

Contents:
- page URL input
- scan action
- staged results grid/list
- filters/search/views
- group by type/source
- selection controls
- “Add Selected to Queue”
- “Download Selected Now”

This tab should behave like a true staged selection workflow.

5. SITE CRAWL
Purpose:
- deeper multi-page asset discovery with scope controls

Contents:
- crawl root URL
- crawl settings
- same-domain and subpath restrictions
- depth/pages/workers
- live discovery counters
- cancellation
- staged crawl result list
- same selection/download workflow as Asset Grabber
- clear explanation of scope before crawl starts

6. HISTORY & DIAGNOSTICS
Purpose:
- completed/failed download history
- troubleshooting
- redownload and inspection

Contents:
- history grid
- history search/filter/sort
- redownload actions
- open file/folder/source actions
- event log viewer
- diagnostics export
- if practical, per-job log preview or log link

7. SETTINGS, RULES & HELP
Purpose:
- move configuration and teaching material out of operational tabs

Contents:
- settings sub-tabs or grouped sections
- scheduler
- category rules
- folder rules
- logging settings
- advanced engine settings
- help/tutorial pane
- contextual explanations
- workflow guides

==================================================
FUNCTIONAL CORRECTNESS REQUIREMENTS
==================================================

You must audit and fix the actual logic, not just the UI.

A. MEDIA MODE MUST BECOME EXPLICIT AND SAFE
--------------------------------
Fix all behavior that can cause a normal video URL to end up as MP3 unless the user explicitly chose audio-only.

Requirements:
- default action for media URLs = download video
- audio-only must require explicit selection
- clearly separate:
  - video presets
  - audio extraction presets
- do not silently reuse stale or hidden audio-only settings
- display effective download plan before start
- if media metadata/formats can be probed, do that before final execution
- show selected output type in a human-readable way:
  - “Video: MP4 1080p”
  - “Audio only: MP3”
  - etc.

B. MODE-SPECIFIC INPUT AND ACTIONS MUST BE RESPECTED
--------------------------------
Fix the root cause of mode confusion.

Requirements:
- Quick Download tab/input triggers queue/direct behavior
- Media Download tab/input triggers media probe/download behavior
- Asset Grabber tab/input triggers asset scan behavior
- Site Crawl tab/input triggers crawl behavior
- Do not let a generic “Download Now” button produce fallback .bin downloads when the user is in discovery-oriented modes
- Each tab must have actions whose labels match the actual operation:
  - Analyze
  - Scan Page
  - Crawl Site
  - Add Selected to Queue
  - Download Selected
  - Start Queue
  - etc.

C. REMOVE “MYSTERY OUTCOMES”
--------------------------------
The user should always know:
- which engine is chosen
- what kind of item is being processed
- where it will be saved
- whether it will be scanned first or downloaded immediately
- whether results will be staged or downloaded directly

If engine selection is automatic, explain it in the UI.
Show detected workflow like:
- Direct file
- Media extraction
- Gallery/collection
- Asset scan
- Site crawl
- Fallback direct

D. FIX FALLBACK / DIRECT DOWNLOAD MISBEHAVIOR
--------------------------------
Investigate why generic or discovery-oriented URLs currently become useless .bin downloads.

Audit and fix:
- content-disposition handling
- content-type handling
- direct/fallback engine routing
- file name derivation
- mode-aware engine resolution
- whether scan/crawl paths are bypassing proper staging
- whether “Download Now” is incorrectly calling direct-download coordinator logic across all modes

For direct downloads:
- derive better file names
- avoid meaningless “download.bin” where better metadata is available
- when filename remains unknown, present a preview/rename opportunity if appropriate
- expose resolved content type and final file name in inspector/details

==================================================
UX / UI REQUIREMENTS
==================================================

A. USE SPACE MUCH BETTER
--------------------------------
Important panels must get real room.

Requirements:
- Queue Manager tab should dedicate substantial area to the queue grid
- Inspector/details should be visible but not dominate
- Avoid stacking too many short panels vertically
- Reduce persistent chrome where possible
- Do not waste large amounts of space on controls that are only occasionally used

B. PROGRESSIVE DISCLOSURE
--------------------------------
Do not overload users with all settings at once.

Use:
- collapsible advanced sections
- mode-specific panels
- settings grouped away from main workflows
- flyouts or secondary panes only where they actually improve clarity

C. BETTER EMPTY STATES AND NEXT-STEP GUIDANCE
--------------------------------
Every tab should explain itself when empty.

Examples:
- Queue tab: “No downloads queued yet. Add a direct link in Quick Download, or send selected assets from Asset Grabber.”
- Media tab: “Paste a media page URL, analyze available formats, then choose video or audio.”
- Asset Grabber: “Paste a single page URL and scan it for downloadable assets.”
- Crawl: “Paste a root URL and define crawl scope before starting.”
- Help tab: walkthroughs and troubleshooting guides.

D. STRONGER BUTTON LABELS
--------------------------------
Replace generic or ambiguous text where necessary.

Prefer:
- Analyze Media
- Scan Page
- Crawl Site
- Add to Queue
- Download Selected Now
- Start Queue
- Pause Queue
- Retry Failed
- Open Containing Folder
- View Diagnostics
- Save Settings
- Restore Defaults

E. LARGE HELPFUL INSPECTOR / DETAILS PANELS
--------------------------------
Selected items should show meaningful details:
- source URL
- resolved URL
- selected engine
- target path
- selected format/profile
- category
- priority
- resumable support
- segment count
- content type
- file size if known
- subtitles/thumbnails/metadata choices
- last error

==================================================
HELP / TUTORIAL FEATURE (MANDATORY)
==================================================

Add a built-in help / tutorial system.

This should not be a tiny tooltip-only feature.
Implement a real help experience such as:

- a Help & Tutorials section/tab
- concise mode explanations
- step-by-step workflows
- “How do I…” guides
- troubleshooting guide
- glossary of downloader terms
- examples of common tasks

Must cover at least:
1. Quick direct file download
2. Downloading a YouTube video as video
3. Extracting audio intentionally
4. Scanning a page for images/files
5. Crawling a site safely
6. Using the queue manager
7. Using history and redownload
8. Using categories/rules
9. Scheduler basics
10. Diagnostics/logging basics

Also add contextual help:
- small info text or help buttons near complex settings
- warnings for risky crawl scope / unsupported behavior
- clear explanation of cookies/auth usage where relevant

If suitable, provide:
- markdown-backed help content
- a structured in-app help view
- expandable sections
- search/filter for help topics if practical

==================================================
CUSTOMIZABILITY REQUIREMENTS
==================================================

Make customization meaningfully available, not just technically present in models.

A. MEDIA CUSTOMIZATION
--------------------------------
Expose and wire clearly:
- video presets
- audio-only presets
- container preference
- resolution preference
- subtitle behavior
- subtitle language/format if feasible
- thumbnail download
- metadata embedding
- playlist behavior
- output template
- cookie file/profile
- custom engine args only in advanced section
- reset-to-default profile

B. ASSET GRABBER / CRAWL CUSTOMIZATION
--------------------------------
Expose and wire clearly:
- same-domain only
- same-subpath only
- max depth
- max pages
- crawl workers
- file-type targeting
- custom extension filters
- host/domain filters
- unique assets only
- probe sizes / reachability
- script/json asset extraction toggle
- dedupe behavior

C. QUEUE CUSTOMIZATION
--------------------------------
Expose and wire:
- max concurrent downloads
- priority defaults
- retry policy
- start next automatically
- continue on failure
- queue ordering
- selected queue/package controls

D. FILE HANDLING CUSTOMIZATION
--------------------------------
Expose and wire:
- overwrite / rename / skip
- preserve timestamps
- sanitize filenames
- file naming templates
- category/domain subfolders
- part-file behavior
- cookie file selection
- output folder strategy

E. LOGGING / DIAGNOSTICS CUSTOMIZATION
--------------------------------
Expose and wire:
- log level
- per-job verbose logs
- log retention
- open log folder
- export diagnostics
- raw engine command preview in advanced mode if appropriate
- developer diagnostics toggle

F. CATEGORY / RULE / PACKAGIZER-LIKE CUSTOMIZATION
--------------------------------
Add a usable rules editor or at minimum a clearly usable management surface for:
- category name
- extensions/patterns
- domain patterns
- default folder
- automatic naming/grouping behavior
- engine preference
- auto-start vs stage
- notes/comments if relevant

This must not remain a hidden settings model only.

==================================================
QUEUE MANAGER IMPROVEMENTS
==================================================

Make Queue Manager feel like a true control center.

Add and/or improve:
- reorder selected items up/down/top/bottom
- change priority
- package/group actions
- package progress
- better selection workflows
- start selected
- pause selected
- retry selected
- remove selected
- open source / copy source / open target folder
- search/filter within queue if practical
- better status badges / row visuals
- clear distinction between queued, active, paused, complete, failed, cancelled, staged

==================================================
HISTORY & DIAGNOSTICS IMPROVEMENTS
==================================================

Strengthen the post-download and troubleshooting workflow.

Add and/or improve:
- history search
- history filters
- sort options
- open file
- open folder
- open source URL
- copy URL
- redownload selected
- better failed-item detail
- event log filtering
- diagnostics export that is actually useful
- log file discoverability
- optional per-job diagnostic trace if available

==================================================
DISCOVERY WORKFLOW IMPROVEMENTS
==================================================

Asset Grabber and Site Crawl must feel complete.

Requirements:
- discovered results appear in a dedicated, spacious staging list/grid
- clear columns such as:
  - selected
  - name
  - type
  - extension
  - size
  - source page
  - host
  - availability
  - warning
- group by:
  - type
  - source page
  - host
- bulk selection actions:
  - select all
  - select visible
  - deselect all
  - invert selection
- clear next actions:
  - Add Selected to Queue
  - Download Selected Now
- if practical, keep staged results until dismissed instead of losing them too easily
- provide better scan/crawl status and summary
- show why some items are skipped or filtered

==================================================
ARCHITECTURAL EXPECTATIONS
==================================================

Continue using the new downloader architecture, but refine it where needed.

You may refactor and extend:
- view models
- tab organization
- mode-specific child view models if needed
- coordinator logic
- engine resolver behavior
- media engine execution planning
- direct/fallback download naming/content-type logic
- asset discovery orchestration
- settings wiring
- rules/category services
- help content provider / help view

Prefer clean separation such as:
- workflow-specific sub-viewmodels
- tab-specific user controls
- reusable inspector/detail components
- reusable selection and staging components
- help content models/services if helpful

Do not turn the main DownloaderViewModel into an even larger god object if splitting would improve maintainability.

==================================================
TESTING / VALIDATION (MANDATORY)
==================================================

Do not stop at visual changes.
Validate actual behavior.

At minimum verify:
- Media mode defaults to video, not accidental MP3
- Explicit audio-only still works correctly
- Asset Grabber input triggers scan, not fallback download
- Site Crawl input triggers crawl, not fallback download
- discovered assets can be clearly selected and actually downloaded
- direct downloads still work
- queue operations still work
- settings changes are reflected in runtime behavior
- help/tutorial content is reachable and accurate
- history and diagnostics actions work
- layout remains usable and readable
- major work areas are now appropriately sized
- build compiles cleanly
- tests pass

Add or update meaningful tests where appropriate, especially around:
- mode-aware input routing
- media execution plan selection
- accidental audio-only regression
- discovery vs direct-download routing
- filename derivation / fallback naming
- settings-to-execution wiring
- queue reorder / selection behavior
- help content availability if testable
- history search/filter logic if implemented
- category/rule matching if extended

==================================================
QUALITY BAR
==================================================

This is not just “add some polish”.
This is the pass that should make the downloader feel actually good to use.

Your goal is to eliminate:
- confusing behavior
- accidental wrong outputs
- hidden important actions
- overcrowded UI
- settings that exist but do not feel real
- workflows that make the user guess

The result should feel cohesive, teachable, and professional.

==================================================
DELIVERY REQUIREMENTS
==================================================

Implement the improvements directly in the repository.

Make all necessary changes across:
- XAML / views
- view models
- services
- models
- settings
- help/tutorial content
- tests
- docs if needed

At the end, provide a concise implementation report covering:
- which UX/workflow issues were fixed
- which root-cause behavior bugs were fixed
- how the tab redesign is structured
- what new customization surfaces were added
- how help/tutorial was implemented
- files added/changed
- tests added/updated
- build/test/runtime validation results