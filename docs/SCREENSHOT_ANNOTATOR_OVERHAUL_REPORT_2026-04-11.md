# Screenshot Annotator Overhaul Report (2026-04-11)

## What Was Broken / Missing
- No reliable on-canvas interaction model (create/select/move/resize) that could be audited and tested end-to-end.
- Several user interactions were either not implemented or were fragile:
  - Drag-to-create previews and commit/cancel flows were not consistently modeled as state.
  - Selection + manipulation (move/resize) behaviors were missing or incomplete.
  - Keyboard interaction (Escape / Delete / arrow nudging) was not wired to ViewModel behavior.
- Limited automated coverage around interaction flows and state transitions.

## What Was Fixed / Implemented
### Interaction Model (ViewModel)
- Added an explicit interaction state machine for on-canvas authoring:
  - Drag-to-create with a ghost `DragPreviewAnnotation`.
  - Selection, move, resize (with resize handles), cancel/commit.
  - Delete selected annotation.
  - Keyboard nudging with clamping.
- Hardened bounds/clamping and undo snapshot logic so repeated interactions do not corrupt state.
- Completed “feature-finish” behaviors:
  - Arrow annotations now use true start/end endpoints (not “rectangle diagonal” semantics).
  - Arrow-specific endpoint handles for resizing.
  - In-place Text editing with commit/cancel and focus behavior.
  - Copy/paste of annotations via clipboard (Ctrl+C / Ctrl+V), including safe clamping to preview bounds.
  - Keyboard shortcuts for undo/redo (Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z).

### UI Wiring (View + Thin Code-Behind)
- Wired the preview surface to the ViewModel interaction entry points for:
  - Create (drag on empty space)
  - Select + move (mouse down on an annotation)
  - Resize (mouse down on a resize handle)
- Added selection visuals + resize handles and a dashed ghost preview rectangle.
- Added keyboard routing on the view:
  - `Esc` cancels current interaction
  - `Delete` removes selection
  - Arrow keys nudge selection (Shift = bigger step)
  - `Ctrl+Z` / `Ctrl+Y` / `Ctrl+Shift+Z` undo/redo
  - `Ctrl+C` / `Ctrl+V` copy/paste

### Rendering Support
- Added/updated an arrowhead converter used by the arrow rendering template to keep the rendering logic declarative and testable.

## Interaction Issues Found (And Addressed)
- “Looks enabled but does nothing”: interactions are now routed through consistent ViewModel methods.
- “State not reflected in UI”: selection visuals and the ghost preview are bound to ViewModel state.
- “Actions silently fail”: interaction methods now return success/failure and maintain a coherent interaction mode.
- “Rapid/repeated interactions”: move/resize/nudge use a single undo snapshot per interaction to avoid ballooning history and to ensure stable cancel semantics.
- “Arrows behave wrong”: arrows now behave like a proper line tool with endpoint manipulation and correct export rendering.
- “Text annotations can’t be edited in-place”: double-click editing with commit/cancel and LostFocus commit is now implemented.

## Binding / Command Issues Found (And Addressed)
- Removed/avoided brittle binding paths by exposing clear ViewModel state required for overlays (selection/preview).
- Consolidated interaction logic into ViewModel methods instead of ad-hoc code-behind behavior.

## Tests Added / Expanded
Added/expanded xUnit tests focused on deterministic, non-UI automation coverage of interaction logic:
- Drag-to-create: commit creates an annotation; tiny drags are rejected.
- Move/resize: modifies geometry correctly and stays within bounds.
- Cancel flows: reverting interaction state.
- Delete selected annotation.
- Keyboard nudging behavior (including clamping).
- Undo/redo behavior for interaction-driven edits.
- Arrow: endpoints + bounding box creation and endpoint resize behavior.
- Text: begin/cancel edit behavior.
- Clipboard: copy/paste duplicates with offset.
- Image export: integration test verifying an arrow drawn using explicit endpoints affects output pixels.

## Areas Still Worth Improving Later
- Optional: multi-select + grouping, if the tool is intended to support batch manipulation.
- Optional: higher-confidence pixel assertions for image export tests (currently smoke-level verification to keep it stable across rendering differences).
- Optional: UI automation for the highest-value interaction smoke test (only if the repo adopts a stable WPF UI automation harness).
