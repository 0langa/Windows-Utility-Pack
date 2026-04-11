namespace WindowsUtilityPack.Tools.NetworkInternet.Downloader;

/// <summary>
/// Centralized help and tutorial content for Downloader Studio.
/// Keeps help topics in one place so UI and behavior can evolve together.
/// </summary>
public static class DownloaderHelpContentProvider
{
    private static readonly IReadOnlyList<string> s_topics =
    [
        "Start Here: Workflow Overview",
        "Quick direct file download",
        "YouTube quality tab",
        "Download a YouTube video as video",
        "Extract audio intentionally",
        "Scan a page for assets",
        "Crawl a site safely",
        "Use queue manager",
        "Queue states and what they mean",
        "Use history and redownload",
        "Use categories and rules",
        "Scheduler basics",
        "Diagnostics and logging",
        "Troubleshooting common issues",
        "Feature validation checklist",
    ];

    private static readonly IReadOnlyDictionary<string, string> s_content = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Start Here: Workflow Overview"] = "Downloader Studio has eight workspaces:\n\n1) Quick Download\nDirect file and mixed URL input. Fast add/start with auto engine selection.\n\n2) Queue Manager\nMain control center for live jobs: start/pause/stop, retry, reorder, priority, inspector.\n\n3) Media Download\nExplicit media workflow. Video is the default output. Audio-only must be selected manually.\n\n4) YouTube Video\nFocused YouTube flow with clear quality controls for video resolution/FPS/codec and audio quality/codec.\n\n5) Asset Grabber\nScan one page, review staged assets, select items, then queue/download selected.\n\n6) Site Crawl\nControlled multi-page discovery with scope limits (domain, depth, max pages, workers).\n\n7) History & Diagnostics\nReview completed/failed jobs, redownload, open source/file/folder, inspect recent events.\n\n8) Settings, Rules & Help\nPersist behavior, manage category rules, scheduler, and troubleshooting guidance.\n\nExpected result:\n- You always know what action will happen in each tab.\n- Discovery tabs stage results first; they do not silently do fallback direct downloads.",
        ["Quick direct file download"] = "Steps:\n1) Open Quick Download.\n2) Paste one or multiple URLs.\n3) Click Analyze Input to preview detected workflow.\n4) Click Add to Queue for staged processing or Download Now for immediate start.\n\nWhat works here:\n- Multi-line and noisy input parsing.\n- URL normalization (including www.* links).\n- Auto engine routing for quick mode.\n\nExpected result:\n- Status bar confirms added item count.\n- New jobs appear in Queue Manager with clear Engine/Status/Plan.",
        ["YouTube quality tab"] = "Steps:\n1) Open YouTube Video.\n2) Paste one or more YouTube links.\n3) Choose video quality, FPS, video codec, audio quality, audio codec, and container.\n4) Click Analyze YouTube.\n5) Click Add to Queue or Download Now.\n\nWhat this tab guarantees:\n- YouTube-only routing (non-YouTube links are ignored).\n- Explicit quality plan shown before start.\n- Output stays video-focused with selected quality profile.\n\nExpected result:\n- Plan text clearly states selected video/audio quality profile.\n- Queue entry carries an explicit YouTube format expression.",
        ["Download a YouTube video as video"] = "Steps:\n1) Open Media Download.\n2) Paste a YouTube/media URL.\n3) Keep Output = Video (default).\n4) Choose Video Profile and Container.\n5) Click Analyze Media.\n6) Click Add to Queue or Download Now.\n\nImportant:\n- Media defaults to VIDEO output.\n- No hidden audio-only carry-over is applied.\n\nExpected result:\n- Media analysis text shows a video plan.\n- Queue item shows plan like 'Video: MP4 (...)'.",
        ["Extract audio intentionally"] = "Steps:\n1) Open Media Download.\n2) Set Output = AudioOnly.\n3) Choose Audio Format.\n4) Analyze Media.\n5) Add to Queue or Download Now.\n\nSafety behavior:\n- Audio extraction only happens when AudioOnly is explicitly selected.\n- Video mode does not auto-switch to audio mode.\n\nExpected result:\n- Analysis text explicitly says audio-only.\n- Queue job plan indicates audio-only output.",
        ["Scan a page for assets"] = "Steps:\n1) Open Asset Grabber.\n2) Paste one page URL.\n3) Click Scan Page.\n4) Filter/search results.\n5) Use Select All / Select Visible / Invert.\n6) Click Add Selected to Queue or Download Selected Now.\n\nResult columns:\n- Name, Type, Extension, Size, Source Page, Host, Reachable, Warning.\n\nExpected result:\n- Discovered assets appear in staged list.\n- Only selected assets are queued/downloaded.",
        ["Crawl a site safely"] = "Steps:\n1) Open Site Crawl.\n2) Paste root URL.\n3) Set crawl limits (same domain/subpath, depth, max pages, workers).\n4) Click Crawl Site.\n5) Review staged results and select items.\n6) Add selected items to queue or start now.\n\nSafety controls:\n- Domain and scope constraints.\n- Deduplication and probe options.\n\nExpected result:\n- Crawl summary reports discovered/staged counts.\n- Selected assets flow into queue as normal download jobs.",
        ["Use queue manager"] = "Primary controls:\n- Start/Pause/Stop Queue\n- Start/Pause/Resume/Retry/Remove Selected\n- Retry Failed, Clear Completed, Clear Failed\n- Move selected Top/Up/Down/Bottom\n- Set selected priority High/Normal/Low\n\nInspector shows:\n- Source URL, resolved URL, output, status message, error\n- Quick actions: open source, open folder, copy URL\n\nExpected result:\n- Queue remains manageable for batch workflows.\n- Ordering and priority changes affect execution order.",
        ["Queue states and what they mean"] = "State meanings:\n- Staged: collected but not queued for execution yet.\n- Queued: waiting for execution.\n- Probing: metadata/engine probe in progress.\n- Downloading: active transfer.\n- Processing: post-download step (merge/finalize).\n- Paused: stopped intentionally and resumable via queue actions.\n- Completed: successful finish.\n- Failed: retries exhausted or terminal error.\n- Cancelled: cancelled by user/system action.\n- Skipped: intentionally not written (e.g. duplicate policy).\n\nExpected result:\n- Status values in queue map directly to real lifecycle transitions.",
        ["Use history and redownload"] = "Steps:\n1) Open History & Diagnostics.\n2) Select a finished or failed item.\n3) Use Open File / Open Folder / Open Source URL / Copy URL.\n4) Use Redownload Selected to enqueue same source again.\n5) Use Clear History when you want a clean history store.\n\nExpected result:\n- History provides auditability and quick re-run workflows.",
        ["Use categories and rules"] = "Where:\n- Settings -> Rules tab.\n\nWhat you can define:\n- Rule name\n- Default folder\n- Extension patterns\n- Domain match patterns\n- Priority override\n\nUsage:\n1) Add or edit rules.\n2) Save Settings.\n3) New incoming jobs are classified by extension/domain.\n\nExpected result:\n- Category and destination are auto-assigned without manual per-job edits.",
        ["Scheduler basics"] = "Capabilities:\n- One-time schedule start.\n- One-time schedule pause.\n\nSteps:\n1) Go to Settings tab.\n2) Set date/time for start and pause.\n3) Apply Schedule Start / Schedule Pause.\n4) Check scheduler status text.\n5) Use Clear Schedule to remove pending actions.\n\nExpected result:\n- Queue starts/pauses at planned times without manual interaction.",
        ["Diagnostics and logging"] = "Diagnostics surfaces:\n- Live recent events panel in History & Diagnostics.\n- Export Diagnostics action for external troubleshooting.\n- Optional log-level settings in downloader settings model.\n\nHow to use:\n1) Reproduce issue.\n2) Inspect recent events for failure context.\n3) Export diagnostics file.\n4) Share diagnostics with issue details.\n\nExpected result:\n- Failures are observable and actionable instead of silent.",
        ["Troubleshooting common issues"] = "1) 'Media downloaded as MP3 unexpectedly'\n- Ensure Media tab Output = Video.\n- Analyze Media should show video plan before start.\n\n2) 'Asset/Crawl URL downloaded as random .bin'\n- Use Asset Grabber or Site Crawl actions (Scan Page / Crawl Site).\n- Discovery modes stage first; they are not direct download actions.\n\n3) 'Nothing starts'\n- Check queue state (Staged vs Queued).\n- Click Start Queue or Start Selected.\n\n4) 'Media tools missing'\n- Install Tools from header.\n- Retry media workflow.\n\n5) 'File already exists'\n- Check duplicate handling mode in settings (Skip/AutoRename/Overwrite).",
        ["Feature validation checklist"] = "Quick smoke test sequence:\n\nA) Quick mode\n- Paste 2 URLs -> Add to Queue -> verify queue rows appear.\n\nB) YouTube quality tab\n- Paste YouTube URL -> choose quality profile -> Analyze YouTube -> Add.\n- Verify plan text reflects selected video/audio quality constraints.\n\nC) Media video default\n- Paste YouTube URL in Media tab with Output=Video -> Analyze -> Add.\n- Verify plan indicates video, not audio-only.\n\nD) Audio explicit\n- Switch Output=AudioOnly -> Analyze -> Add.\n- Verify plan indicates audio-only.\n\nE) Asset grabber\n- Scan one page -> select subset -> Add Selected to Queue.\n- Verify only selected items are added.\n\nF) Site crawl\n- Crawl with domain/depth limits -> staged results appear.\n\nG) Queue controls\n- Reorder selected up/down and change priority.\n- Verify execution order/status changes.\n\nH) History/diagnostics\n- Complete a job -> appears in history.\n- Export diagnostics file.\n\nIf all steps pass, core downloader workflows are operating correctly.",
    };

    /// <summary>Ordered list of help topics shown in the UI.</summary>
    public static IReadOnlyList<string> Topics => s_topics;

    /// <summary>
    /// Gets help content for a topic.
    /// Returns a fallback string when the topic is unknown.
    /// </summary>
    public static string GetContent(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return "Select a topic to see workflow steps and troubleshooting guidance.";
        }

        if (s_content.TryGetValue(topic, out var content))
        {
            return content;
        }

        return $"No detailed guide is available yet for '{topic}'.";
    }
}
