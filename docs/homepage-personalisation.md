# Homepage Personalisation — Developer Guide

This document describes how the home dashboard works, how personalisation
(favourites, recents, search) is implemented, and how to extend it when
adding new tools.

---

## Architecture overview

| Layer | File(s) | Responsibility |
|-------|---------|---------------|
| **Source of truth** | `Tools/ToolRegistry.cs` | Static registry of all `ToolDefinition` entries. Categories, icons, and descriptions are registered here at startup. |
| **Persistence** | `Services/SettingsService.cs`, `Services/ISettingsService.cs` | JSON settings file at `%LOCALAPPDATA%\WindowsUtilityPack\settings.json`. Stores `FavoriteToolKeys` and `RecentToolKeys` (lists of tool key strings). |
| **Dashboard service** | `Services/HomeDashboardService.cs`, `Services/IHomeDashboardService.cs` | Manages favourites and recents in memory, resolves keys to `ToolDefinition` objects via `ToolRegistry.GetByKey()`, persists changes through `ISettingsService`. |
| **ViewModel** | `ViewModels/HomeViewModel.cs` | Exposes `FavoriteTools`, `RecentTools`, `AllTools`, `Categories`, `SelectedCategory`, `SelectedCategoryTools`, `SearchQuery`, `SearchResults`, and all related commands. |
| **View** | `Views/HomeView.xaml` | Declarative XAML dashboard with search bar, favourites, recents, category tabs, category tool panel, and all-tools grid. |
| **Converter** | `Converters/FavoriteCheckConverter.cs` | Multi-value converter that checks if a tool key exists in the current favourites list (for star toggle binding). |
| **Converter** | `Converters/EqualityToTagConverter.cs` | Multi-value converter that returns `"Selected"` when two values are reference-equal (for category tab visual state). |

---

## How favourites work

1. Each tool card in the All Tools and Browse-by-Category sections has a
   `ToggleButton` bound to `ToggleFavoriteCommand` with the tool's `Key` as
   the command parameter.
2. `ToggleFavoriteCommand` calls `IHomeDashboardService.ToggleFavorite(key)`.
3. The service adds or removes the key from an in-memory `List<string>`,
   persists the updated list to `AppSettings.FavoriteToolKeys`, and raises
   the `Changed` event.
4. `HomeViewModel` listens to `Changed` and refreshes `FavoriteTools` from
   the service, causing the UI to update.

**Storage format:** `settings.json` → `FavoriteToolKeys: ["password-generator", "ping-tool"]`

---

## How recents work

1. `App.xaml.cs` subscribes to `NavigationService.Navigated`. When a tool
   ViewModel is navigated to, the handler resolves the tool key and calls
   `IHomeDashboardService.RecordToolLaunch(key)`.
2. The service moves the key to index 0 of the recents list, deduplicates,
   caps at `MaxRecentTools` (currently 10), persists, and raises `Changed`.
3. `HomeViewModel` refreshes `RecentTools` on `Changed`.
4. `ClearRecentCommand` calls `IHomeDashboardService.ClearRecent()`.

**Storage format:** `settings.json` → `RecentToolKeys: ["regex-tester", "dns-lookup"]`

---

## How search works

1. The search `TextBox` in `HomeView.xaml` is bound to
   `HomeViewModel.SearchQuery` with `UpdateSourceTrigger=PropertyChanged`.
2. On every change, `UpdateSearchResults()` filters `AllTools` by matching
   the query against `Name`, `Description`, and `Category` (case-insensitive
   `Contains`).
3. `SearchResults` and `ShowSearchResults` update, and the search results
   section becomes visible when there are matches.
4. `ClearSearchCommand` resets `SearchQuery` to empty.

---

## How category selection works

1. Category tabs are rendered from `HomeViewModel.Categories` (built by
   `ToolRegistry.GetCategories()`).
2. Each tab button's `Command` is bound to `SelectCategoryCommand` with the
   `CategoryItem` as the parameter.
3. Clicking a tab sets `SelectedCategory`, which updates
   `SelectedCategoryTools` with that category's `Tools` list.
4. Clicking the already-selected tab deselects it (toggle behaviour).
5. The tab visual state is driven by an `EqualityToTagConverter` that sets
   `Tag="Selected"` when the button's `DataContext` matches
   `SelectedCategory`.

---

## How category descriptions are configured

In `App.xaml.cs RegisterTools()`, each category has a description registered
via `ToolRegistry.RegisterCategoryDescription(category, description)`.
These descriptions are stored in a static dictionary and used when building
`CategoryItem` objects.

Current descriptions:
- **System Utilities** → "Manage startup, environment, storage, and system info"
- **File & Data Tools** → "Rename, hash, shred, split, and inspect files"
- **Security & Privacy** → "Passwords, hashes, secrets, and certificates"
- **Network & Internet** → "Ping, DNS, ports, HTTP, speed, and downloads"
- **Developer & Productivity** → "Regex, encoding, colour, QR, diff, and more"
- **Image Tools** → "Resize, convert, and annotate images"

---

## Adding a new tool

When you add a new tool to the app, it **automatically** integrates with
favourites, recents, search, and category browsing. Follow these steps:

1. **Register the tool** in `App.xaml.cs RegisterTools()` with a unique
   `Key`, `Name`, `Category`, `Description`, `IconGlyph`, and `Factory`.
2. **Register its DataTemplate** in `App.xaml` mapping the ViewModel type to
   the View.
3. **Category handling:** If the tool belongs to an existing category, it
   appears automatically. For a new category, also call
   `ToolRegistry.RegisterCategoryIcon()` and
   `ToolRegistry.RegisterCategoryDescription()`.
4. **No extra homepage work needed.** The homepage reads from
   `ToolRegistry.GetDisplayTools()` and `ToolRegistry.GetCategories()` so
   new tools appear in the All Tools grid, category panel, and search
   automatically.

---

## Key files changed in this redesign

- `Views/HomeView.xaml` — Full homepage layout
- `ViewModels/HomeViewModel.cs` — Search, category selection, all commands
- `MainWindow.xaml` — Removed horizontal scrolling category bar
- `Resources/Styles.xaml` — Added `SearchBoxStyle`, `CategoryTabButtonStyle`, `CategoryToolCardStyle`
- `Converters/EqualityToTagConverter.cs` — New converter for category tab state
- `App.xaml` — Registered `EqualityToTagConverter`
