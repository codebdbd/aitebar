# Harden main-panel focus, overflow, navigation, and feedback

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

This work improves the everyday behavior of the main AiteBar edge panel after the 1.13.0 release candidate was preserved in commit `3aab32a` and draft PR 28. A panel revealed only by mouse hover must not steal keyboard focus from the application where the user is typing. A panel with more buttons than three layout bands can display must keep every action reachable through an explicit overflow button instead of clipping buttons outside the window. Keyboard arrows must follow the visible two-dimensional layout, and failed drop, clipboard, location, or settings actions must provide understandable feedback.

## Progress

- [x] (2026-08-02 04:05Z) Published the clean 1.13.0 baseline as commit `3aab32a` on `agent/aitebar-1.13.0-release` and opened draft PR 28.
- [x] (2026-08-02 04:12Z) Reviewed focus activation, the 30 ms hover timer, settings cloning, three-band clipping, keyboard navigation, context and drag affordances, reorder boundaries, and silent errors.
- [x] (2026-08-02 05:02Z) Stopped timer-driven hover activation from taking foreground focus, reduced each tick to one settings snapshot, and disabled polling outside the hidden/idle/enabled state.
- [x] (2026-08-02 05:18Z) Replaced clipped excess buttons with a localized, keyboard-accessible “More (N)” menu backed by a tested capacity helper.
- [x] (2026-08-02 05:31Z) Added tested spatial arrow navigation, Home/End navigation, a focusable context button, and localized drag-handle accessibility metadata.
- [x] (2026-08-02 05:39Z) Added owned feedback dialogs for drop, clipboard, missing-location, and settings-save failures, and constrained reorder previews to the persisted button group.
- [x] (2026-08-02 07:54Z) Passed 9 focused helper tests, warning-free Release build, 1081 non-WPF tests, 72 isolated WPF tests (1153 total), rebuilt the installer, and smoke-started the published executable for 15 seconds.

## Surprises & Discoveries

- Observation: Hover opening and explicit opening share the same animation completion, which always calls `ForceForegroundWindow` and `Activate`.
  Evidence: The activation timer calls parameterless `ShowDock()`, and `Toggle` activates after every show animation regardless of its source.

- Observation: One 30 ms timer pass reads `AppSettings` six times, and every read deep-clones the full settings graph.
  Evidence: `MainWindow.AppSettings` delegates to `AppSettingsService.Settings`; the getter returns `CloneAppSettings(_appSettings)`. The timer separately reads monitor index twice, delay, edge, zone size, and hover enablement.

- Observation: `OverflowWrapPanel` caps measured cross bands at three but arranges every child, while its parent clips to bounds and keyboard navigation retains all children.
  Evidence: `PanelLayoutHelper.MaxUserBands` is three, `UnifiedButtonsPanel` has `ClipToBounds="True"`, and `GetAllFocusableButtons` appends the complete `_unifiedButtons` list.

- Observation: Existing stable panel metrics can calculate the visible capacity without altering the four-edge geometry contract.
  Evidence: Horizontal metrics expose user width and height; vertical metrics additionally expose the leading and overflow reserves already consumed by `OverflowWrapPanel`.

## Decision Log

- Decision: Preserve explicit opening behavior and suppress foreground activation only for timer-driven hover opening.
  Rationale: Hotkey, tray, and position-indicator commands are deliberate. Hover is incidental and must never interrupt typing in another application.
  Date/Author: 2026-08-02 / Codex

- Decision: Use one settings snapshot per hover tick and run the timer only while the panel is hidden, animation is idle, and hover activation is enabled.
  Rationale: The timer exists only to reveal a hidden panel. Running it in every other state wastes CPU and allocates cloned settings graphs without user benefit.
  Date/Author: 2026-08-02 / Codex

- Decision: Represent excess actions with one final “More (N)” button and an existing styled context menu.
  Rationale: A popup preserves the panel’s compact geometry, works on all four edges, avoids wheel conflicts with context switching, and gives keyboard users a visible destination instead of focusing clipped controls.
  Date/Author: 2026-08-02 / Codex

- Decision: Keep utilities and user buttons as separate reorder groups and make that boundary explicit in drag behavior.
  Rationale: `UnifiedButtonService` always renders utilities before user buttons and persists their orders separately. Pretending they can cross groups creates misleading previews and dropped changes.
  Date/Author: 2026-08-02 / Codex

## Outcomes & Retrospective

Implementation is complete and layered after commit `3aab32a`. The release-equivalent suite passes 1153/1153 tests, including the WPF orientation coverage, and the published executable remains alive during the 15-second startup smoke test. The rebuilt unsigned installer is `artifacts/installer/AiteBar-Setup.exe`, 79,448,300 bytes, SHA-256 `023A59088D5423962F620C950BBDD24E028BF1F0F9479F566F25966802B1A067`. Interactive feel on a real desktop remains part of the human Top/Bottom/Left/Right release check; automated geometry and orientation coverage is green.

## Context and Orientation

`AiteBar/MainWindow.xaml` defines the compact edge panel, fixed controls, `OverflowWrapPanel`, context badge, drag handle, and application-settings button. `AiteBar/MainWindow.xaml.cs` owns activation, animation, settings snapshots, context menus, button execution, orientation, and positioning. `AiteBar/MainWindow.KeyboardNavigationHandler.cs`, `.DragAndDropHandler.cs`, `.DropHandler.cs`, and `.PanelDragHandler.cs` contain the interaction-specific partial class code.

`AiteBar/PanelLayoutHelper.cs` calculates fixed panel dimensions for up to three user-button bands. `AiteBar/OverflowWrapPanel.cs` arranges buttons inside those dimensions. `UnifiedButtonService` creates a logical list containing utilities first on the primary context and then user buttons. Tests for pure layout live in `AiteBar.Tests/PanelLayoutHelperTests.cs`; WPF orientation tests live in `MainWindowIconConverterOrientationTests.cs`. New behavior tests must be isolated in the existing `WpfTestCollection` and added to both WPF class arrays in `.github/workflows/release.yml` if a new test class is introduced.

## Plan of Work

First separate hover opening from explicit opening. Track whether the current show animation may activate the window. The activation timer passes false; hotkey, tray, and position-indicator paths retain explicit activation. Replace repeated timer property reads with one local `AppSettings` snapshot. Add a method that starts or stops the timer from startup, settings changes, and animation completion according to the hidden/idle/enabled state.

Next compute the current panel’s visible button capacity from `PanelLayoutMetrics`, including vertical leading and overflow reserves. Build only the actions that fit, reserving the last slot for a non-draggable overflow button when necessary. Store the remaining logical actions separately and build a localized menu that executes the same action methods. Keyboard enumeration must contain displayed action buttons and the overflow button only.

Then replace linear primary-axis navigation with spatial navigation. Use each visible button’s rectangle relative to the panel and select the nearest candidate whose center lies in the pressed direction. Tab continues to follow visual enumeration, Escape hides, Enter/Space invoke, and Home/End select the first/last visible command. Convert or wrap the context badge so it can receive focus and open the context list; add tooltip and automation metadata to the drag handle.

Finally make drop, clipboard, location, and settings failures visible through localized owned dialogs or a lightweight panel notification. During reorder preview, reject targets from the opposite group and reset transforms cleanly. Add regression tests for every corrected behavior before full validation.

## Concrete Steps

Run from `D:\01_Codebdbd\01_projects\aitebar`:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~MainWindow|FullyQualifiedName~PanelLayoutHelper|FullyQualifiedName~PanelPositionHelper|FullyQualifiedName~ActivationZoneHelper|FullyQualifiedName~CommandButtonStyleTests"
    dotnet build .\AiteBar.sln -c Release

For final release-equivalent validation, run the non-WPF exclusion filter from `.github/workflows/release.yml` and each WPF class in its own host. Only after all tests pass should the installer be rebuilt.

## Validation and Acceptance

While typing in another application, move the pointer into the panel activation zone. The panel appears without changing the foreground application. Clicking a panel button still executes it, and opening with the global hotkey focuses the panel for keyboard navigation.

Configure enough buttons to exceed three bands on each edge. Exactly the buttons that fit remain visible, followed by “More (N)”; no button is clipped, and every hidden action is available in the menu. Tab and arrows never focus a control outside the panel. Arrow keys move geometrically between rows or columns.

Hover disabled means no activation timer polling and no opening. Re-enabling it from settings restores activation without restart. Failed drops and missing file locations produce localized feedback. Dragging a utility over a user button does not display a false reorder destination.

## Idempotence and Recovery

The published baseline is recoverable from commit `3aab32a` and PR 28. All new work remains after that commit and must not amend it. Source edits use `apply_patch`; tests and builds are repeatable. Do not rewrite or force-push the published baseline.

## Artifacts and Notes

Published baseline:

    branch: agent/aitebar-1.13.0-release
    commit: 3aab32a
    draft PR: https://github.com/codebdbd/aitebar/pull/28

## Interfaces and Dependencies

No new library is required. Overflow uses existing WPF `Button`, `ContextMenu`, `MenuItem`, and `AppContextMenuFactory`. The panel must retain `PanelLayoutHelper.MaxUserBands = 3`; overflow changes reachability, not the compact size contract. Any helper introduced for capacity or directional navigation should be pure and tested outside WPF where possible.

Plan revision note (2026-08-02 04:12Z): Created after publishing the baseline and completing the full main-panel UX review.

Plan revision note (2026-08-02 05:39Z): Recorded implemented focus, overflow, navigation, accessibility, failure-feedback, and reorder-boundary milestones before full release validation.

Plan revision note (2026-08-02 07:54Z): Recorded the final 1153-test release matrix, installer artifact, hash, and publish startup smoke result.
