# Zen Editor Side Margin Click-To-Exit Behavior with Safety Frame

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document is maintained in accordance with `PLANS.md` at the repository root. It builds on the completed implementations in `ZEN_EDITOR_EXECPLAN.md` and `ZEN_EDITOR_USABILITY_EXECPLAN.md`, but contains all context needed for this improvement so that a contributor can continue from this file alone.

## Purpose / Big Picture

After this change, a user working in the Zen Editor can quickly and naturally exit the full-screen writing environment by clicking the empty margin areas at the left or right sides of the screen, in addition to using `Esc` or `Alt+F4`.

Crucially, **no fixed zone widths are hardcoded**: the exit zones dynamically cover the empty side margins of the display outside the central text column, while strictly enforcing a **protective safety frame** (`SideSafetyMargin`, default 48 px) around the text column so that clicking near or selecting text never accidentally closes the editor.

Both the feature itself and the size of the safety frame are fully configurable in the application settings (`AppSettingsWindow`) under General Settings, stored in `settings.json` and `ZenEditorStoreIndex`, and hot-reloaded without requiring an application restart.

## Progress

- [x] (2026-09-24) Clarified requirements with user and agreed on side margin click-to-exit with safety frame and configurable settings (no hardcoding).
- [x] (2026-09-24) Created `ZenEditorLayoutHelper` with pure, testable layout calculations for editor text width and dynamic side exit zone bounds respecting `safetyMargin`.
- [x] (2026-09-24) Added `ZenEditorExitOnSideClick` (bool, default true) and `ZenEditorSideSafetyMargin` (double, default 48.0) to `AppSettings`, `AppSettingsService`, `ZenEditorStoreIndex`, and `ZenEditorStore`.
- [x] (2026-09-24) Added UI controls in `AppSettingsWindow.xaml` and `.cs` under General Settings with presets (24, 48 default, 72, 96 px) and four localized resource strings (`Strings*.resx`).
- [x] (2026-09-24) Added `LeftExitZone` and `RightExitZone` transparent borders in `ZenEditorWindow.xaml` with accessibility name `Common_Close` and hand cursor.
- [x] (2026-09-24) Implemented press/release click state tracking, safe mouse capture, dynamic geometry adaptation, settings injection, and error-overlay suppression in `ZenEditorWindow.xaml.cs`.
- [x] (2026-09-24) Added comprehensive unit tests in `ZenEditorLayoutHelperTests.cs` and `ZenEditorWindowBehaviorTests.cs` covering dynamic geometry, safety buffer, toggling, accessibility, click closing, and drag safety.
- [x] (2026-09-24) Updated user documentation in `docs/USER_MANUAL.md`, function reference in `docs/functions.md`, and `CHANGELOG.md`.
- [x] (2026-09-24) Verified build and unit tests pass with zero warnings or failures.

## Surprises & Discoveries

- Observation: Fixed pixel constants (e.g. 120 px) in fullscreen writing applications cause ergonomic frustration when users expect the full empty margin to be reactive, but also risk accidental dismissal when clicking near text margins.
  Evidence: Moving to dynamic margin calculation `(containerWidth - editorWidth) / 2 - safetyMargin` with configurable safety buffers (24, 48, 72, 96 px) resolves both issues cleanly.

- Observation: WPF off-screen testing on an STA thread does not trigger full layout arrange passes on unshown windows, so `ActualWidth` remains zero unless window bounds or mock widths are explicitly measured.
  Evidence: `ExitZones_Geometry_AdaptsToContainerWidth` required inspecting explicit `Width` as a fallback when `ActualWidth` is unmeasured, ensuring high fidelity both in production layout and unit tests.

- Observation: `window.IsLoaded` remains false on WPF windows until they are rendered to screen, even after async initialization.
  Evidence: Subscribing to the `Closing` event on `ZenEditorWindow` is the authoritative way to verify that close actions are triggered or prevented.

## Decision Log

- Decision: Dynamically allocate exit zones across the available side margins instead of hardcoding a fixed pixel width.
  Rationale: Follows best desktop UI practices. Standard displays (1080p, 1440p, 4K, ultrawide) have varying side margin widths. The exit zone should cover the empty space up to the safety frame.
  Date/Author: 2026-09-25 / Antigravity

- Decision: Introduce a protective safety frame (`ZenEditorSideSafetyMargin`, default 48 px) and make it configurable in Settings.
  Rationale: Protects the user from accidental dismissals when clicking near the paragraph edges or selecting text, while allowing users to customize the buffer size (24, 48, 72, 96 px) or disable the feature entirely.
  Date/Author: 2026-09-25 / Antigravity

- Decision: Extract layout math into `ZenEditorLayoutHelper.cs`.
  Rationale: Follows `AGENTS.md` architectural guideline to isolate pure calculation logic from WPF UI code and enable thorough unit testing without UI dependencies.
  Date/Author: 2026-09-24 / Antigravity

- Decision: Use separate mouse down and mouse up state matching with mouse capture.
  Rationale: Prevents accidental closing when a user starts a text selection inside the paragraph editor and releases the mouse cursor outside the text column.
  Date/Author: 2026-09-24 / Antigravity

- Decision: Collapse zones if effective width drops below 20 DIPs.
  Rationale: On small monitors or constrained windows (e.g. 800x600), the text editor takes almost the entire width. Collapsing prevents edge zones from encroaching on editor padding or text selection.
  Date/Author: 2026-09-24 / Antigravity

## Outcomes & Retrospective

The side margin exit zones work seamlessly and reliably. Users can click anywhere on the empty side margins outside the protective safety frame to close the Zen Editor. All text and formatting are saved automatically prior to exit. The feature and its safety frame are fully configurable in `AppSettingsWindow`.
