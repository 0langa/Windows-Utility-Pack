# Homepage Enhancements (2026-04)

## What Was Added
- Slim top vitals bar: CPU, RAM free/total, system-drive free space, network up/down.
- Time-aware greeting and date in the header.
- Compact quick-actions widget:
  - generate password + copy
  - generate UUID + copy
  - clipboard summary (length + content type)
  - ping detected host from clipboard (safe fallback for the requested “last-used host” action)
- Collapsible sections with persisted state:
  - Favorites
  - Recently Used
  - Browse by Category
- Search upgraded to inline dropdown behavior:
  - matches name, description, category, key, and keyword tags
  - supports synonym lookup (`guid`, `hash`, `dns`, `qr`, `json`, etc.)
  - shows recent searches when focused and query is empty
- Empty-state polish for Favorites/Recents with dashed placeholder cards.
- Category cards now show:
  - tool count
  - most-used tool (from launch data)
  - hover preview of top tools
- Tool presentation upgrades:
  - usage-frequency indicator (dot density)
  - “New” badge for metadata-marked recent tools
  - right-click context menu:
    - Open
    - Add/Remove Favorite
    - Copy tool name
    - View description

## Metadata Extensions
- `ToolDefinition` metadata is now actively used for homepage discovery:
  - `Keywords` participate in search matching.
  - `DateAdded` drives the “New” badge via `IsNewToolConverter`.
- Added keyword/date metadata to newer/high-value tools (QR, UUID/ULID, JSON/YAML, Image tools, DNS/hash calculator).

## Persisted Preferences
Stored in `AppSettings` and persisted through `ISettingsService`:
- `HomeViewIsCompact`
- `FavoritesExpanded`
- `RecentsExpanded`
- `CategoryBrowserExpanded`
- `HomeRecentSearches`
- `ToolLaunchCounts`

## Notes / Deferred
- “Ping last-used host” was implemented as a clean fallback (`clipboard-detected host`) because current navigation state does not store a canonical “last-used host” across tools.
- No command-palette shortcut hint was added because there is no existing shortcut registry to reuse safely without introducing parallel state.
