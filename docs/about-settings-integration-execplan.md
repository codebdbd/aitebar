# Integrate About information into application settings

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. This document follows `PLANS.md` in the repository root.

## Purpose / Big Picture

After this change, users will find “About” as the last item in the left navigation of the existing application settings window. The tray “About” command will focus that section instead of opening a separate dialog, so version, update, support, license, data-folder, and program-folder actions live in one predictable place. The result is visible by opening the tray menu, choosing “About,” and observing the always-on-top settings window positioned at its final navigation section.

## Progress

- [x] (2026-07-17 18:10+03:00) Inspected the current `AboutWindow`, settings navigation, tray handlers, and window reuse behavior.
- [x] (2026-07-17 19:14+03:00) Added the fifth navigation item and About section to `AppSettingsWindow`.
- [x] (2026-07-17 19:16+03:00) Moved all About actions and version/localization behavior into `AppSettingsWindow`.
- [x] (2026-07-17 19:17+03:00) Routed tray Settings and About commands through the single reusable settings-window API.
- [x] (2026-07-17 19:20+03:00) Removed the obsolete standalone About window and updated contract tests and documentation.
- [x] (2026-07-17 19:25+03:00) Built Release with zero warnings and errors, passed all 781 tests, and rebuilt the installer.
- [x] (2026-07-17 20:36+03:00) Replaced diagonal text arrows with action-specific Fluent icons and removed the 500 ms topmost reassertion that covered Windows screenshot UI.
- [x] (2026-07-17 20:38+03:00) Rebuilt Release and passed all 782 tests after the visual and topmost refinements.
- [x] (2026-07-18 00:00+03:00) Replaced automatic settings topmost enforcement with an explicit, session-only pin toggle that is off by default; Release and all 782 tests pass.

## Surprises & Discoveries

- Observation: The settings window is a single scrollable document whose left navigation tracks section offsets rather than switching tab pages.
  Evidence: `GetSettingsSections()` and `AppSettingsSectionNavigationHelper` map four list items to four named section borders.
- Observation: The tray Settings item still directly constructs a modal settings window, while the main UI already has a reusable non-modal `ShowAppSettingsWindow()` method.
  Evidence: `MainWindow.TrayMenuHandler.cs` calls `new AppSettingsWindow(this).ShowDialog()`, while `MainWindow.xaml.cs` stores `_appSettingsWindow` and reactivates it.
- Observation: The localization contract treats the technology value `.NET 10 · WPF` as translatable UI content in `AppSettingsWindow`, even though the former About window used technology literals.
  Evidence: The first full test run passed 780 tests and failed only `XamlTextProperties_DoNotContainTranslatableLiteralText` with `AppSettingsWindow.xaml: .NET 10 · WPF`; the value was moved to `About_TechnologiesValue` in all four resource files.
- Observation: The 500 ms timer did more than keep settings above ordinary applications: each tick called `SetWindowPos(HWND_TOPMOST)`, repeatedly placing settings over the Windows screenshot overlay.
  Evidence: Removing `_topmostTimer` leaves the XAML `Topmost="True"` and activation-time enforcement intact while allowing newer system topmost UI to appear above settings.
- Observation: Even without the timer, combining XAML `Topmost="True"` with activation-time `SetWindowPos(HWND_TOPMOST)` still made settings feel unlike a normal Windows window.
  Evidence: The final interaction model uses XAML `Topmost="False"`; `NativeMethods.SetWindowPos` and `EnsureAlwaysOnTop` are absent, and only `BtnKeepOnTop.IsChecked` can set `Topmost` for the lifetime of that window.

## Decision Log

- Decision: Add About as the fifth scroll section instead of introducing a separate tab host.
  Rationale: This preserves the current settings navigation architecture and its capped, centered content column; the cap was later expanded from 700 to 1000 DIP by explicit user request.
  Date/Author: 2026-07-17 / Codex
- Decision: Route both tray entries through the reusable settings-window method and pass a typed section identifier.
  Rationale: This prevents duplicate settings windows and lets an existing settings window navigate to About reliably.
  Date/Author: 2026-07-17 / Codex
- Decision: Remove `AboutWindow.xaml` and `AboutWindow.xaml.cs` after their behavior is migrated.
  Rationale: Keeping an unreachable duplicate implementation would create localization and maintenance drift.
  Date/Author: 2026-07-17 / Codex
- Decision: Make `AppSettingsSection` public.
  Rationale: `MainWindow.ShowAppSettingsWindow` is already public and its optional typed section parameter cannot expose a less-accessible enum; a public enum keeps the API explicit without numeric indices.
  Date/Author: 2026-07-17 / Codex
- Decision: Use action-specific Fluent glyphs instead of generic diagonal arrow characters in About action rows.
  Rationale: Sync, globe, code, document, and folder icons communicate the destination directly and match the icon system already bundled with AiteBar.
  Date/Author: 2026-07-17 / Codex
- Decision: Supersede automatic topmost behavior with a visible, session-only pin toggle that is off by default.
  Rationale: A normal window participates naturally in Windows z-order and remains discoverable in the taskbar. Users who need persistent visibility can opt in explicitly, and closing the settings window resets the choice without adding another stored preference.
  Date/Author: 2026-07-18 / Codex

## Outcomes & Retrospective

The settings window now has five navigation entries with About last. About displays the application identity and localized version, and preserves update, website, repository, third-party notices, data-folder, and program-folder actions. Both tray Settings and tray About use `ShowAppSettingsWindow`; an existing settings window is reactivated and navigated instead of duplicated. The standalone About XAML and code-behind were removed.

Release validation completed with zero build warnings and errors and, after the follow-up refinements, 782 of 782 tests passing. About actions use contextual Fluent icons without diagonal arrow text. Settings is now a normal non-topmost window by default; a localized pin toggle in the footer enables topmost only for the current window instance. Reopening an existing settings window still restores and activates it through `ShowAppSettingsWindow`. `installer/Build-Installer.ps1` produces `artifacts/installer/AiteBar-Setup.exe`. An interactive launch was intentionally not performed because an installed AiteBar instance was already running; stopping a user-owned process was unnecessary for source, XAML compilation, contract-test, publish, and installer validation.

## Context and Orientation

`AiteBar/AppSettingsWindow.xaml` defines the resizable, always-on-top settings window. A `ListBox` in its left column contains navigation items, while named `Border` elements inside `SettingsScrollViewer` form the corresponding content sections. `AiteBar/AppSettingsWindow.xaml.cs` owns navigation synchronization, staged settings, and button handlers. `AiteBar/AppSettingsSectionNavigationHelper.cs` contains calculation-only logic for choosing and scrolling to a section. `AiteBar/MainWindow.xaml.cs` owns the single live `AppSettingsWindow` reference. `AiteBar/MainWindow.TrayMenuHandler.cs` creates tray commands. The standalone `AiteBar/AboutWindow.xaml` and `.xaml.cs` currently display the version and open update, web, repository, notices, data folder, and executable folder targets.

The term “section identifier” means a small enum value such as `General` or `About`, used instead of an unexplained numeric list index. The settings window remains one scrollable form; selecting an item scrolls to the matching section.

## Plan of Work

Extend `AppSettingsSectionNavigationHelper.cs` with an `AppSettingsSection` enum in navigation order. Add an About list item with an information glyph and an `AboutSettingsSection` at the end of `AppSettingsWindow.xaml`. Use the existing localized About resource strings and the settings window’s modern typography and command-button styles. Preserve every action from the standalone dialog.

In `AppSettingsWindow.xaml.cs`, accept an optional initial section, expose `NavigateToSection(AppSettingsSection)`, update the section array, set localized version text, and migrate safe target-opening handlers. Navigation requested before `Loaded` must be remembered and applied after layout is available.

Change `MainWindow.ShowAppSettingsWindow` to accept the typed section. If the window already exists, activate it and navigate; otherwise construct it with the desired initial section. Change both tray Settings and tray About commands to call that API. Delete the obsolete About XAML and code-behind, update source-contract tests that referenced it, and document that About is part of settings.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Edit the files described above using focused patches. Then run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    .\installer\Build-Installer.ps1

Expected evidence is a successful build with zero errors, the complete test suite passing, and `artifacts\installer\AiteBar-Setup.exe` receiving a new timestamp.

Actual evidence:

    Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.
    Пройдено 781, не пройдено 0, всего 781.
    Installer created in D:\01_Codebdbd\01_projects\aitebar\artifacts\installer

## Validation and Acceptance

The left settings navigation must have five items, with About last and carrying an information icon. Selecting it must scroll to the About section. Opening About from the tray must show or reactivate the single settings window and select About. Opening Settings from the tray must show or reactivate the same window at General. The About section must display the localized version and preserve update check, website, repository, third-party notices, application-data folder, and program-folder actions. There must be no compiled or referenced `AboutWindow` class after migration.

Automated source-contract tests must verify navigation ordering, the absence of direct `new AboutWindow`, and routing through `ShowAppSettingsWindow(AppSettingsSection.About)`. Release build and all tests must pass. Because this changes release UI, the installer must be rebuilt.

## Idempotence and Recovery

The edits and verification commands are repeatable. Existing user changes in the dirty worktree must be preserved. If WPF intermediate files under `obj` are locked in the sandbox, rerun build and test with approved elevated execution rather than deleting user files. If the migration fails midway, keep the old About files until every handler compiles in `AppSettingsWindow`; only then delete them.

## Artifacts and Notes

The starting behavior is:

    Tray About -> new AboutWindow().ShowDialog()
    Tray Settings -> new AppSettingsWindow(this).ShowDialog()
    Main settings button -> reusable ShowAppSettingsWindow()

The target behavior is:

    Tray About -> ShowAppSettingsWindow(AppSettingsSection.About)
    Tray Settings -> ShowAppSettingsWindow(AppSettingsSection.General)
    Existing window -> NavigateToSection(requestedSection), Show(), Activate()

## Interfaces and Dependencies

Define `public enum AppSettingsSection` with `General`, `Contexts`, `Hotkeys`, `QuickTools`, and `About` in display order. `AppSettingsWindow` must provide `public void NavigateToSection(AppSettingsSection section)`. `MainWindow` must provide `public Task ShowAppSettingsWindow(AppSettingsSection section = AppSettingsSection.General)`. Continue using `UpdateCheckUi`, `PathHelper`, `LocalizationService`, `DarkDialog`, and `ProcessStartInfo` already used by the standalone About implementation; add no external dependency.

Revision note (2026-07-17): Created the plan after repository inspection to make the About migration reproducible and to capture the existing duplicate tray-window path.

Revision note (2026-07-17): Recorded the localization-test discovery and the public enum decision after the first implementation and validation pass.

Revision note (2026-07-17): Marked all milestones complete and recorded final build, test, installer, and manual-launch status.

Revision note (2026-07-17): Recorded the follow-up icon correction and screenshot-compatible topmost policy, plus the 782-test validation result.

Revision note (2026-07-18): Superseded all automatic topmost enforcement with the explicit session pin model and recorded its build/test validation.
