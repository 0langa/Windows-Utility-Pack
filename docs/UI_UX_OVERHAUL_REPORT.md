# UI/UX Overhaul Report (circa 2025–2026)

> **Note:** This document has no explicit date. It describes completed design improvements (theme expansion, semantic tokens, shell redesign). Treat as a reference for the current visual design approach.

## Major problems found

- Theme architecture only supported a narrow dark/light switch with a small token set and limited semantic roles.
- Shared styles covered only part of the control surface, leaving many views visually inconsistent or close to native-default.
- The shell used rigid layouts that did not scale gracefully across narrower windows or higher DPI scenarios.
- Navigation cards, category buttons, menus, and status surfaces did not communicate hierarchy or affordance strongly enough.
- Complex tools such as Downloader Studio, Storage Master, and QR Code Generator were functionally strong but visually dense and locally styled.
- Interaction quality was inconsistent across list-like and table-like surfaces, including limited context-menu support and uneven selection/focus treatment.

## Core design-system changes

- Expanded the theme model to support `Dark`, `Light`, `Aurora`, and `System`.
- Rebuilt theme dictionaries around semantic tokens for:
  - app backgrounds and shell surfaces
  - elevated and alternate surfaces
  - primary, secondary, tertiary, and on-accent text
  - accent, hover, pressed, focus, and validation states
  - primary, secondary, and destructive action colors
  - list, tab, menu, table, scrollbar, and input states
- Reworked shared resource dictionaries so future pages inherit the same styling rules instead of relying on page-local colors and borders.

## Theme changes made

- Refined the existing dark theme with deeper contrast balance, stronger surfaces, and more legible secondary text.
- Refined the light theme so it feels intentional rather than “dark mode inverted”.
- Added a new premium `Aurora` theme using restrained blue/violet gradients and elevated surfaces while preserving readability and utility-first contrast.
- Updated theme switching infrastructure so the new theme participates cleanly in runtime theme changes and persisted settings.

## Shell and page improvements

- Redesigned the main window shell with:
  - a premium hero header
  - adaptive horizontal category navigation
  - clearer theme/status visibility
  - improved notification treatment
  - a more cohesive footer/status area
- Upgraded the home screen into a more guided product landing surface with stronger hierarchy and better card composition.
- Redesigned the settings window to present appearance choices and shell behavior more clearly, including the new `Aurora` option.
- Refined `CategoryMenuButton` to use richer dropdown presentation and stronger affordances.
- Improved high-visibility tool pages:
  - `Downloader Studio`: better header treatment and added right-click context menus for queue, discovery, and history grids.
  - `QR Code Generator`: improved panel hierarchy, hero introduction, warning treatment, and preview-side organization.
  - `Storage Master`: upgraded its header and elevated surface treatment to align with the new shell.

## Shared component improvements

- Modernized shared button families and introduced clearer primary, secondary, and destructive action language.
- Reworked shared surfaces, cards, focus visuals, and status containers.
- Updated text inputs, combo boxes, checkboxes, radio buttons, tabs, sliders, progress bars, menus, list items, and data-grid presentation to follow one visual system.
- Improved scrollbar styling and general control consistency across themes.

## Responsiveness and scaling improvements

- Replaced rigid shell navigation layout with a horizontally scrollable adaptive layout instead of a forced single-row uniform grid.
- Increased shell sizing resilience and improved surface spacing for large and medium windows.
- Reduced hardcoded visual assumptions in key pages so controls and panels scale more predictably.
- Used shared templates and semantic resources to keep theme and layout behavior consistent across DPI and resize scenarios.

## Interaction and affordance upgrades

- Added or improved context-menu support in the downloader’s queue, discovery, and history surfaces.
- Strengthened focus, hover, selected, pressed, and disabled states through the shared control templates.
- Improved visual distinction between informational, warning, and error surfaces.
- Made action grouping clearer in shell and feature entry surfaces.

## Remaining minor limitations

- Some older pages still carry dense legacy compositions internally even though they now inherit the improved global control system; they are materially better, but a future pass could continue decomposing them into more reusable page sections.
- A few low-usage control types still rely more on brush-level styling than full custom templates.

## Guidance for future pages

- Use semantic theme keys and shared styles from `Themes/*.xaml`, `Resources/Styles.xaml`, `Resources/InputStyles.xaml`, and `Resources/ScrollBarStyles.xaml`.
- Prefer `ElevatedSurfaceStyle`, `ToolPanelBorderStyle`, `HeroSurfaceStyle`, and the shared heading/body text styles instead of local borders and typography.
- Use shared action button styles for intent:
  - `PrimaryActionButtonStyle`
  - `SecondaryActionButtonStyle`
  - `DangerActionButtonStyle`
- Favor adaptive layouts, scrollable overflow regions, and reusable status surfaces over fixed-size grids and page-local color values.
