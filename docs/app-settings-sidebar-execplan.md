# Recompose program settings as sidebar navigation over one scrollable page

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

The Program Settings window will replace its four horizontal tabs with a fixed navigation column on the left and one vertically scrollable settings page on the right. Clicking a navigation item will scroll the right side to the corresponding section, and manually scrolling the page will update the selected navigation item. The user will continue to see and edit exactly the same settings, with the same initial values, validation, localization, save behavior, cancel behavior, and side effects; only the visual composition and navigation mechanism will change.

The result is visible by opening Program Settings. The left column must remain stationary while the right column scrolls through General, Contexts, Hotkeys, and Quick Tools in that order. The Save and Cancel buttons must remain visible at the bottom at every scroll position.

## Progress

- [x] (2026-07-15 11:38Z) Read `PLANS.md`, inspected all four current `TabItem` sections, all named controls, event handlers, nested scrolling, localization behavior, and existing tests.
- [x] (2026-07-15 11:38Z) Chose a fixed sidebar plus a single right-side `ScrollViewer`, with no new settings and no persistence of navigation position.
- [x] (2026-07-15 11:45Z) Added a baseline contract covering all 87 settings-bound names and the existing segmented/footer handlers; baseline passed before moving XAML.
- [x] (2026-07-15 11:45Z) Added and tested pure section-navigation calculations for target offsets and active-section selection.
- [x] (2026-07-15 11:50Z) Replaced the `TabControl` with a fixed four-item sidebar, four anchored cards in one right-side ScrollViewer, and a fixed footer; all 87 inventory assertions still pass.
- [x] (2026-07-15 11:50Z) Wired click-to-scroll, scroll-to-selection synchronization, keyboard-native ListBox selection, localization re-alignment, and dynamic context-safe coordinate recalculation.
- [x] (2026-07-15 12:10Z) Corrected the runtime section-coordinate calculation after UI Automation exposed an incorrect Contexts jump; the focused navigation/layout set passes 17/17.
- [x] (2026-07-15 12:10Z) Completed Release validation: build succeeded with zero warnings and errors, the full suite passed 707/707, all four sidebar transitions and reverse scroll synchronization passed in the running window, the installer was rebuilt, and the published executable stayed alive without changing `error.log`.

## Surprises & Discoveries

- Observation: the current window has four real categories, although the reference image shows seven example categories.
  Evidence: `AiteBar/AppSettingsWindow.xaml` contains exactly four `TabItem` elements using `AppSettingsWindow_General`, `AppSettingsWindow_ContextNames`, `AppSettingsWindow_Hotkeys`, and `AppSettingsWindow_QuickTools`.

- Observation: Hotkeys already contains its own `ScrollViewer`, while the other tabs do not.
  Evidence: the `AppSettingsWindow_Hotkeys` tab wraps its content in a `ScrollViewer` at the current XAML lines around 381–579. The new outer right-side scroll surface must replace this inner one; nested vertical scrolling would make mouse-wheel and keyboard behavior inconsistent.

- Observation: language selection is intentionally persisted immediately, before the main Save button is pressed, and it rebuilds localized dynamic context rows.
  Evidence: `SegLanguage_Click` writes `_settings.UiCulture`, saves through `AppSettingsService`, calls `LocalizationService.EnsureAppliedCulture()`, and invokes `RefreshLocalizedUi()`. The layout refactor must not convert this into deferred save behavior.

- Observation: context-name TextBoxes are generated in code rather than declared in XAML.
  Evidence: `BuildContextRows` in `AiteBar/AppSettingsWindow.xaml.cs` creates the eight rows inside `PanelContextsList`. That named host must be preserved unchanged inside the Contexts section.

- Observation: the current fixed height of 560 is already part of the working compact layout, but width 720 is insufficient after adding a sidebar without narrowing existing hotkey rows.
  Evidence: current hotkey rows contain a 140-unit label, four 62-unit modifiers with margins, and a 200-unit key ComboBox. The new plan widens the window while retaining its current height and local right-side scrolling.

- Observation: transforming an anchor to its ancestor during live scrolling did not provide a stable navigation target in the packaged runtime; selecting Contexts could clamp to the bottom of the page even though pure helper tests passed.
  Evidence: the first UI Automation pass reported Contexts at 100% scroll. All four anchors are direct children of `SettingsScrollContent`, so `LayoutInformation.GetLayoutSlot(section).Top` is the stable content-space coordinate. After that change, the four click targets were 0%, 33.4%, 67.5%, and 100%, with the expected item selected at every target.

## Decision Log

- Decision: retain exactly four navigation items and four content sections in the current order: General, Contexts, Hotkeys, Quick Tools.
  Rationale: the supplied screenshot is a composition reference, not a request to invent or split categories. Keeping the current categories is the safest way to honor the requirement that every setting remain unchanged.
  Date/Author: 2026-07-15 / Codex

- Decision: render all four sections in one right-side `ScrollViewer`; sidebar selection acts as an anchor rather than hiding non-selected content.
  Rationale: this implements “all on one page” literally and lets the user browse continuously while retaining direct navigation.
  Date/Author: 2026-07-15 / Codex

- Decision: use a WPF `ListBox` for the left navigation and style its four `ListBoxItem` elements as vertical navigation buttons.
  Rationale: `ListBox` provides selection state, arrow-key navigation, focus behavior, and screen-reader semantics without reimplementing those behaviors with plain Buttons.
  Date/Author: 2026-07-15 / Codex

- Decision: use immediate `ScrollToVerticalOffset` navigation rather than animated scrolling or `BringIntoView`.
  Rationale: immediate movement is predictable for keyboard users, avoids motion-preference concerns, and does not unexpectedly scroll horizontally or move focus. The target offset will be clamped to the ScrollViewer's valid range.
  Date/Author: 2026-07-15 / Codex

- Decision: widen `AppSettingsWindow` from 720 to 900 device-independent units while retaining height 560 and `ResizeMode="NoResize"`.
  Rationale: a 180-unit sidebar plus spacing leaves the right content approximately as wide as the current settings content. Retaining the established height keeps the window compact and makes only the right content area scroll.
  Date/Author: 2026-07-15 / Codex

- Decision: keep the native window frame, current dark palette, existing `CardStyle`, 36-unit form controls, and 44-unit footer command buttons.
  Rationale: the example image is not a literal visual specification. Custom title chrome, gradients, new colors, or redesigned controls would expand the task beyond composition and risk changing already approved styles.
  Date/Author: 2026-07-15 / Codex

- Decision: do not store the selected section or scroll offset in application settings.
  Rationale: persistence would add a new setting and violate the requirement that the settings model remain unchanged. Each newly opened window will start at General and offset zero.
  Date/Author: 2026-07-15 / Codex

- Decision: keep Save and Cancel in a footer outside the `ScrollViewer`, aligned under the right content column; do not add the reference image's Reset button.
  Rationale: this preserves the current action set and ensures actions never scroll out of view.
  Date/Author: 2026-07-15 / Codex

- Decision: require each section anchor to remain a direct child of `SettingsScrollContent` and calculate its target from `LayoutInformation.GetLayoutSlot`.
  Rationale: the StackPanel layout slot is expressed directly in scroll-content coordinates and is unaffected by the viewport's current transform. A structural test now enforces the direct-child invariant.
  Date/Author: 2026-07-15 / Codex

## Outcomes & Retrospective

All milestones are complete. The old tabs and nested Hotkeys ScrollViewer are gone; all four unchanged content blocks now exist on one page with a fixed sidebar and footer. Contract tests protect all 87 settings-bound names, original handlers, direct-child anchors, and the single-scroll-surface layout. The final focused navigation/layout run passed 17/17, the Release build completed with zero warnings and errors, and the full suite passed 707/707.

Runtime UI Automation confirmed a 900-by-560 window, four stationary 40-unit navigation rows, a separate right-side scrolling area, and visible 44-unit Save/Cancel buttons outside that area. Click navigation reached General, Contexts, Hotkeys, and Quick Tools at 0%, 33.4%, 67.5%, and 100%; direct scrolling at 0%, 50%, and 100% selected General, Contexts, and Quick Tools. The rebuilt `AiteBar-Setup.exe` is 77,493,910 bytes with SHA-256 `AE60E91331802CB82A53C89C11F8205A5225126D807EF338F84490F7F2C7FCEB`. Code signing was skipped because no certificate was supplied, matching the existing build behavior. The final published executable remained alive for the seven-second smoke and did not change `%APPDATA%\Codebdbd\Aite Bar\error.log`.

## Context and Orientation

`AiteBar/AppSettingsWindow.xaml` is the only window being recomposed. It currently declares a 720-by-560 non-resizable WPF window containing a top `TabControl` and a bottom Save/Cancel row. Each `TabItem` contains one existing card. The window uses shared styles from `AiteBar/SettingsWindowResources.xaml`, which in turn exposes form and settings resources such as `CardStyle`, `FieldLabelStyle`, segmented controls, form heights, and command-button styles.

`AiteBar/AppSettingsWindow.xaml.cs` owns loading, localization, validation, and saving. `LoadSettings` populates the controls. `BtnSave_Click` validates hotkeys and writes all settings. `BtnCancel_Click` closes without applying the ordinary staged edits. `SegLanguage_Click` is a deliberate exception that persists and applies language immediately. `BuildContextRows` creates the context controls dynamically in `PanelContextsList`. These methods and their semantics must remain intact.

The complete settings inventory that must survive the XAML move is as follows. General contains five language choices, four panel-edge choices, four panel-size choices, four activation-zone choices, four activation-delay choices, and the three checkboxes for the taskbar position indicator, secondary monitor, and update checks. Contexts contains `PanelContextsList`, which produces eight context-name inputs, eight colored number badges, and eight enabled checkboxes, with the primary context always enabled. Hotkeys contains nine bindings: show panel, next context, previous context, add button, File Sorter, Quick Note, Color Picker, Timer/Stopwatch, and QR Generator. Each binding retains four modifier ToggleButtons and one key ComboBox. Quick Tools contains the sixteen existing visibility checkboxes from Search through Copilot plus `ChkClipboardManagerPersistHistory` and its privacy hint. The existing `BtnCancel` and `BtnSave` remain the only footer actions.

In this plan, an “anchor” is a named section element in the scrollable page. A navigation click calculates that element's vertical position and moves the ScrollViewer to it. “Active section synchronization” means choosing the last section whose top edge has passed a small marker near the top of the viewport while the user scrolls. The final section is forced active when the ScrollViewer reaches the bottom, because its top may be impossible to align with the top of the viewport.

## Plan of Work

First create `AiteBar.Tests/AppSettingsLayoutContractTests.cs` before moving any controls. Parse `AiteBar/AppSettingsWindow.xaml` and assert that every settings-bound `x:Name` from the inventory above occurs exactly once. Assert the current segmented-control Click handlers, Save/Cancel Click handlers, the `PanelContextsList` host, and all nine hotkey ComboBoxes. This test must pass against the current tabbed layout. Its purpose is to make accidental deletion, duplication, or handler loss fail immediately during the move. Existing integration tests for immediate language persistence, Clipboard Manager, Icon Converter, localization, command-button styles, and form-control height remain in force.

Next add `AiteBar/AppSettingsSectionNavigationHelper.cs` as an internal pure helper with no WPF control dependencies. It will expose `GetTargetOffset(double sectionTop, double scrollableHeight)`, which clamps a section top to the range from zero through the ScrollViewer's maximum offset, and `GetActiveSectionIndex(IReadOnlyList<double> sectionTops, double verticalOffset, double viewportHeight, double extentHeight, double activationInset)`. The latter returns the last valid index whose section top is at or above `verticalOffset + activationInset`, except that reaching the bottom within a one-unit tolerance returns the final index. Invalid or empty inputs return a safe first/no-selection result documented by tests. Add `AiteBar.Tests/AppSettingsSectionNavigationHelperTests.cs` for first, middle, last, bottom-clamped, short-page, and empty-list cases.

Then recompose `AiteBar/AppSettingsWindow.xaml`. Change only the width to 900 and retain height 560, native window chrome, fixed resize mode, colors, form styles, and command styles. Replace the `TabControl` with a two-column Grid in the existing content row. Use a 180-unit left column, a 16-unit gap or separator, and a star-sized right column. Place a `ListBox` named `SettingsNavigationList` in the left column with exactly four items in the current order. Reuse the existing localization keys for their labels. Define local navigation styles in `Window.Resources`: transparent list chrome, 36- to 40-unit item rows, `CornerRadius="4"`, muted default text, `#252526` selected background, and an accent-colored left indicator. Do not copy the screenshot's gradient, custom title bar, Reset button, or category set.

In the right column add one `ScrollViewer` named `SettingsScrollViewer`, with vertical scrolling enabled and horizontal scrolling disabled. Its single child is a StackPanel named `SettingsScrollContent`. Move, without rewriting, the four existing content bodies into four named Border anchors: `GeneralSettingsSection`, `ContextSettingsSection`, `HotkeySettingsSection`, and `QuickToolsSettingsSection`. Keep their order and existing controls, names, bindings, tags, margins within rows, and event handlers. Add a visible section heading using the same localization key formerly used by each tab header, followed by the existing hint and controls. Retain `CardStyle` or a narrowly derived local section-card style. Remove only the Hotkeys section's inner `ScrollViewer`; the outer `SettingsScrollViewer` becomes the sole vertical scrolling surface. Keep adequate spacing between section cards and a small bottom spacer so the final card is fully visible.

Move the existing footer StackPanel to Grid row 1, column 1, outside the `SettingsScrollViewer`. Preserve `BtnCancel_Click`, `BtnSave_Click`, `IsCancel`, `IsDefault`, widths, margins, styles, and enabled-state behavior. The left navigation may span the content and footer rows visually, but no new Reset action is added.

Wire navigation in `AiteBar/AppSettingsWindow.xaml.cs`. Add a four-element ordered section accessor returning the named anchors. Add `_isSynchronizingNavigation` to prevent the ScrollChanged handler and SelectionChanged handler from recursively calling one another. On `Loaded`, select General and scroll to zero. In `SettingsNavigationList_SelectionChanged`, ignore synchronization-only changes, calculate the selected direct-child section's Y coordinate with `LayoutInformation.GetLayoutSlot`, clamp it through `AppSettingsSectionNavigationHelper.GetTargetOffset`, and call `SettingsScrollViewer.ScrollToVerticalOffset`. Do not move keyboard focus into the content section.

In `SettingsScrollViewer_ScrollChanged`, recompute all four section-top coordinates rather than caching them; localization and dynamic context rows can change heights. Pass the coordinates and ScrollViewer metrics to `GetActiveSectionIndex`, then update the ListBox selection under `_isSynchronizingNavigation`. This makes mouse-wheel, scrollbar, touchpad, Page Up/Down, and keyboard scrolling update the sidebar consistently. When localization refreshes the window, remember the current selected index only in memory, let `RefreshLocalizedUi` rebuild labels and context rows, and use `Dispatcher.BeginInvoke` at Loaded priority to scroll the same section back into view after layout. This preserves navigation context without adding persistent state.

After the XAML move, extend `AppSettingsLayoutContractTests`. Assert that there is no `TabControl` or `TabItem`, exactly one named vertical ScrollViewer, no ScrollViewer nested inside it, four ordered navigation items, four ordered section anchors, and that Save/Cancel are outside the scroll subtree. Re-run the baseline control-inventory assertions unchanged. Add source-level assertions that the navigation handlers and localization re-alignment are wired. If WPF construction of `AppSettingsWindow` can be performed without initializing tray/hotkey system integration, add a WPF layout test that measures the window at 900 by 560 and verifies the footer remains outside the scroll viewport. If construction requires unsafe system integration, keep the structural and pure-helper tests and record that limitation in `Surprises & Discoveries` rather than weakening production architecture for a test.

Finally perform Release validation and a manual smoke. The manual pass must verify every section, not just navigation. Rebuild publish and installer only after automated validation succeeds, stop only an `AiteBar` process whose executable is inside `artifacts/publish/win-x64`, start the new publish, and confirm that `%APPDATA%\Codebdbd\Aite Bar\error.log` does not gain new entries.

## Milestones

Milestone 1 freezes behavior before layout work. Add the inventory contract test and pure navigation helper tests while the old tabs still exist. Run the focused tests and expect them all to pass. At this point there is no visible UI change, but there is executable proof of which controls and handlers must survive.

Milestone 2 creates the one-page composition. Replace the TabControl with the sidebar, one right ScrollViewer, four anchors, and the fixed footer. Run the inventory and XAML structure tests. The window should build with every previous control still present exactly once, even before scroll synchronization is polished.

Milestone 3 completes interaction. Add click-to-scroll, manual-scroll synchronization, keyboard behavior, bottom clamping, and localization re-alignment using the pure helper. Run focused tests and manually navigate all four sections. The selected sidebar item must always agree with the visible section without focus jumps or recursive scrolling.

Milestone 4 validates unchanged settings behavior and release readiness. Run the full suite, inspect all supported languages and DPI conditions, exercise Save/Cancel and hotkey validation, rebuild the installer, launch the publish, and check the log. Completion requires both correct navigation and evidence that no setting disappeared or changed semantics.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Before changing XAML, add the baseline tests and run:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release -nr:false --filter "FullyQualifiedName~AppSettingsLayoutContractTests|FullyQualifiedName~AppSettingsSectionNavigationHelperTests|FullyQualifiedName~AppSettingsWindowIntegrationTests"

After each XAML/navigation milestone, repeat that command and also run the directly related contracts:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release -nr:false --filter "FullyQualifiedName~AppSettings|FullyQualifiedName~ClipboardManagerIntegrationTests|FullyQualifiedName~IconConverterIntegrationTests|FullyQualifiedName~CommandButtonStyleTests|FullyQualifiedName~FormControlHeightTests|FullyQualifiedName~RuntimeLocalizationWindowSourceTests"

For final repository validation, run sequentially because simultaneous WPF builds contend over generated `obj` files:

    dotnet build .\AiteBar.sln -c Release -nr:false
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release -nr:false --no-build
    .\installer\Build-Installer.ps1

Expected build output ends with zero warnings and zero errors. Expected test output has zero failed tests; record the actual passing count in `Progress` and `Outcomes & Retrospective` rather than hard-coding today's count in this plan. The installer command must create one current `AiteBar-Setup.exe` and `SHA256SUMS.txt` under `artifacts/installer`.

## Validation and Acceptance

Open Program Settings. The window must show a fixed left navigation with exactly General, Contexts, Hotkeys, and Quick Tools, and a scrollbar only in the right content area. All four section cards must be present on one continuous page. Clicking each left item must reveal its section. Scrolling with the mouse wheel and dragging the scrollbar must update the selected item. At the bottom, Quick Tools must become selected even if its heading cannot reach the exact top of the viewport. Arrow keys must move through sidebar items, and Tab must reach the settings controls and footer actions with visible keyboard focus cues.

The Save and Cancel buttons must remain visible while the right side scrolls. Save must retain existing hotkey-conflict validation and persist every existing field. Cancel must discard ordinary staged edits exactly as before. Changing language must still apply and persist immediately, must preserve typed custom context names, must relocalize default context names and all navigation/section labels, and must return to the same in-memory section after layout refresh. No navigation position is written to `settings.json`.

Verify the complete inventory manually. General retains language, edge, size, activation zone, delay, and three checkboxes. Contexts retains all eight names, badges, and enabled states. Hotkeys retains all nine bindings with four modifiers and a key selector each. Quick Tools retains all sixteen visibility choices and Clipboard Manager history persistence. No labels, options, defaults, tooltips, enablement rules, or handler semantics are changed.

Check Russian, English, German, and Ukrainian at 100%, 125%, and 150% display scaling where the environment permits. Long labels must wrap or trim according to existing styles without horizontal scrolling. The window must remain on screen at its fixed 900-by-560 size, the sidebar must not scroll, cards must not be clipped, and the footer must remain visible.

After starting the rebuilt publish, wait at least six seconds. The `AiteBar` process must remain alive and `error.log` length and timestamp must remain unchanged. If a new entry appears, inspect and resolve it before marking the plan complete.

## Idempotence and Recovery

The work is source-only and safe to repeat. Preserve the dirty worktree and do not reset unrelated changes. Move each existing XAML subtree once; the inventory contract test will expose duplicates and omissions. If the one-page XAML becomes temporarily invalid, restore only the incomplete local edit with `apply_patch`; do not use `git reset`, `git checkout --`, or broad file replacement.

If navigation oscillates during scrolling, keep `_isSynchronizingNavigation` around programmatic selection changes and recompute section offsets after layout instead of adding timers or arbitrary delays. If the last section never becomes active, correct the helper's bottom-of-scroll rule rather than adding blank content solely to force its heading to the top. If localization changes section heights, schedule one re-alignment at Dispatcher Loaded priority after `RefreshLocalizedUi`; do not cache coordinates across layout changes.

If WPF build files are inaccessible inside the sandbox, repeat build and tests outside it with approval and `-nr:false`. If publish files are locked, resolve the absolute artifact path and stop only the running process from `artifacts/publish/win-x64` before retrying. Do not stop editor language services or unrelated installed copies.

## Artifacts and Notes

The existing visual hierarchy to replace is:

    AppSettingsWindow
      Grid rows: content, footer
        TabControl
          General TabItem
          Context Names TabItem
          Hotkeys TabItem with nested ScrollViewer
          Quick Tools TabItem
        Save/Cancel footer

The target hierarchy is:

    AppSettingsWindow (900 x 560)
      Grid rows: content, footer; columns: sidebar, gap, right content
        SettingsNavigationList (fixed left column)
        SettingsScrollViewer (right content row only)
          SettingsScrollContent
            GeneralSettingsSection
            ContextSettingsSection
            HotkeySettingsSection
            QuickToolsSettingsSection
        Save/Cancel footer (fixed, right column, outside ScrollViewer)

The reference screenshot is intentionally not copied literally. It informs the sidebar/content/footer composition only. Current AiteBar colors, typography, form heights, native title bar, four categories, and action set remain authoritative.

## Interfaces and Dependencies

No NuGet package or external dependency is added. Use WPF types already referenced by the project: `ListBox`, `ListBoxItem`, `ScrollViewer`, `Border`, `StackPanel`, `LayoutInformation`, `ScrollChangedEventArgs`, `SelectionChangedEventArgs`, and `Dispatcher.BeginInvoke`.

Add `AiteBar/AppSettingsSectionNavigationHelper.cs` with an internal static class named `AppSettingsSectionNavigationHelper`. It must provide deterministic, UI-independent methods equivalent to:

    internal static double GetTargetOffset(double sectionTop, double scrollableHeight);

    internal static int GetActiveSectionIndex(
        IReadOnlyList<double> sectionTops,
        double verticalOffset,
        double viewportHeight,
        double extentHeight,
        double activationInset = 24);

`GetTargetOffset` returns a finite value clamped from zero through `scrollableHeight`. `GetActiveSectionIndex` returns `-1` for no sections, otherwise an index within the supplied list. When `verticalOffset + viewportHeight` is within one unit of `extentHeight`, it returns the final index. Otherwise it chooses the last section top not greater than `verticalOffset + activationInset`.

In `AppSettingsWindow.xaml`, the stable new names are `SettingsNavigationList`, `SettingsScrollViewer`, `SettingsScrollContent`, `GeneralSettingsSection`, `ContextSettingsSection`, `HotkeySettingsSection`, `QuickToolsSettingsSection`, `BtnCancel`, and `BtnSave`. Add `x:Name` to the footer buttons if they do not already have it, without changing their handlers or semantics. Every existing settings-bound control name remains unchanged.

Plan revision note (2026-07-15 11:38Z): created this self-contained implementation plan after inventorying the current four tabs, all settings controls, dynamic contexts, nested hotkey scrolling, localization behavior, save/cancel behavior, and relevant tests. The plan treats the supplied screenshot only as a composition reference and explicitly forbids settings-model or behavior changes.

Plan revision note (2026-07-15 11:50Z): recorded completion of the baseline, helper, XAML composition, and navigation/localization wiring milestones after the first 22-test focused pass.

Plan revision note (2026-07-15 12:10Z): recorded the runtime coordinate defect, the stable layout-slot correction, final UI Automation evidence, full 707-test validation, rebuilt installer metadata, and clean published startup smoke.
