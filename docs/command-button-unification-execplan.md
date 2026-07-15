# Unify primary and secondary command buttons

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

A user should see the same command-button geometry and interaction behavior in Timer/Stopwatch, File Sorter, Icon Converter, QR Generator, the button editor, and application settings. Primary actions such as Start, Sort, Save ICO, and Save retain the blue accent. Secondary actions such as Reset, Choose Image, export/copy QR actions, and Cancel retain a neutral surface. The result is observable by opening those windows and comparing their button height, typography, padding, rounded border, focus indication, hover, pressed, and disabled states.

## Progress

- [x] (2026-07-15 08:00Z) Read `PLANS.md`, inventoried the agreed buttons, and inspected their current resource inheritance and layouts.
- [x] (2026-07-15 08:06Z) Defined the shared command geometry and removed the Timer and Icon Converter shadow styles.
- [x] (2026-07-15 08:06Z) Migrated the agreed buttons without changing handlers, enablement, or default/cancel behavior.
- [x] (2026-07-15 08:08Z) Added structural coverage for geometry, inheritance, semantic colors, exact target mapping, and absence of local shadow styles.
- [x] (2026-07-15 08:12Z) Focused structural and Icon Converter WPF layout coverage passed 18/18 after correcting semantic state inheritance.
- [x] (2026-07-15 08:17Z) Completed Release build, 661-test full suite, installer rebuild and checksum verification, publish restart, and clean log smoke.
- [x] (2026-07-15 08:22Z) Reopened visual acceptance after the disabled Save button exposed an accent-border and contrast defect; removed the persistent primary border and added an explicit darker, non-accented disabled state.
- [x] (2026-07-15 08:55Z) Completed the correction pass: computed WPF color test passed with 17/17 focused tests, full suite passed 662/662, installer checksum matched, publish restarted, and the log remained unchanged.

## Surprises & Discoveries

- Observation: Timer currently defines the desired geometry locally instead of in the shared utility dictionary.
  Evidence: `TimerStopwatchWindow.xaml` sets height 44, minimum width 150, horizontal padding 18, and font size 14 in its local `CommandButtonStyle`.

- Observation: `UtilityWindowResources.xaml` already exposes `CommandButtonStyle` and `PrimaryCommandButtonStyle`, but they are only aliases and do not contain the Timer geometry.
  Evidence: both styles currently inherit button colors without adding geometry.

- Observation: Icon Converter shadows the same style keys with a smaller local height of 34.
  Evidence: its window resources define local command and primary command styles before the two footer actions use them.

- Observation: the QR Generator is 920 device-independent pixels wide and distributes four export actions across equal star columns.
  Evidence: four minimum widths of 150 plus three eight-pixel gaps fit within the content area, so the Timer minimum width does not require a narrower exception.

- Observation: deriving the primary command style from the secondary style also inherits the secondary hover and pressed triggers.
  Evidence: WPF style triggers participate in `BasedOn` inheritance; the old Timer chain therefore allowed neutral interaction colors to override the normal accent. A neutral internal geometry base cannot solve this, so the base now inherits the primary template and geometry while each public semantic style supplies its own surface states.

- Observation: overriding the primary `BorderBrush` with the accent color leaves a blue outline after the inherited disabled trigger replaces only background and foreground.
  Evidence: the Add Button screenshot showed disabled Save with a blue border and a fill too close to active Cancel. The shared primary style now inherits the base transparent border, while keyboard focus remains controlled by the existing focus-cue trigger.

## Decision Log

- Decision: use exactly two public semantic styles, `CommandButtonStyle` and `PrimaryCommandButtonStyle`.
  Rationale: all agreed buttons share one geometry and interaction contract; the primary style changes only semantic colors.
  Date/Author: 2026-07-15 / Codex

- Decision: introduce the internal `CommandButtonBaseStyle` and derive both public styles from it rather than deriving primary from secondary.
  Rationale: geometry stays defined once, primary keeps the established accent interaction states, and secondary defines its own neutral interaction states without cross-contamination.
  Date/Author: 2026-07-15 / Codex

- Decision: do not use a permanent accent border on primary commands and define their disabled state explicitly as background `#242426`, foreground `#77777B`, transparent border, and arrow cursor.
  Rationale: active primary emphasis comes from its accent fill; disabled controls must lose the accent and remain clearly distinguishable from an enabled neutral command. Keyboard focus continues to use the separate focus outline in the shared template.
  Date/Author: 2026-07-15 / Codex

- Decision: preserve the Timer geometry as the source of truth: height 44, minimum width 150, padding 18 horizontally, and font size 14.
  Rationale: the user explicitly selected Start and Reset as the visual reference, and the target layouts have enough room or size to content.
  Date/Author: 2026-07-15 / Codex

- Decision: keep QR export and copy actions secondary.
  Rationale: the four actions are peers; marking one as primary would invent a priority that the workflow does not have.
  Date/Author: 2026-07-15 / Codex

- Decision: leave MainWindow, toolbar, icon-only, preset, and unrelated dialog buttons outside this migration.
  Rationale: the user selected a bounded first group, and those other buttons have different interaction or layout contracts.
  Date/Author: 2026-07-15 / Codex

## Outcomes & Retrospective

The agreed command buttons now consume two shared public styles with the Timer reference geometry. A private base defines height 44, minimum width 150, horizontal padding 18, and font size 14 once. Primary actions inherit accent pointer states from `PrimaryButtonStyle`; secondary actions add neutral pointer states without contaminating primary behavior. Timer and Icon Converter no longer shadow the shared keys.

The initial focused structural and WPF layout set passed 18/18. After the screenshot correction, a real computed-style WPF test proved that enabled secondary uses background `#383838`, disabled primary uses `#242426`, foregrounds differ, and the disabled border is transparent; that focused set passed 17/17. The Release build completed with zero warnings and errors, and the final full suite passed 662/662. Installer version 1.11.1 was rebuilt with SHA-256 `9C8F78DAE631D7F95A76F0D8531CDBB160746C5742E5101CBEAE3D091BD1CABB`, matching `SHA256SUMS.txt`. The updated publish is running as PID 20616. The production log retained its previous timestamp and size, so startup added no exception. A final human visual pass through the six affected windows remains useful for subjective spacing acceptance, but automated geometry, computed visual state, resource, localization layout, build, artifact, and startup gates are complete.

## Context and Orientation

`AiteBar/FormControlsResources.xaml` contains the common primary and secondary button templates, colors, focus outline, and pointer states. `AiteBar/UtilityWindowResources.xaml` is loaded globally from `AiteBar/App.xaml` and provides semantic aliases for utility command buttons. `AiteBar/TimerStopwatchWindow.xaml` currently adds the selected geometry locally. `AiteBar/FileSorterWindow.xaml`, `AiteBar/IconConverterWindow.xaml`, `AiteBar/QRCodeGeneratorWindow.xaml`, `AiteBar/SettingsWindow.xaml`, and `AiteBar/AppSettingsWindow.xaml` contain the agreed target buttons.

A device-independent pixel is WPF's layout unit. At 100 percent Windows scaling it corresponds to one physical pixel; at higher scaling WPF enlarges it automatically. A primary action is the single emphasized action that advances or commits the workflow. A secondary action is neutral and does not receive the accent background.

## Plan of Work

Add the Timer geometry to `CommandButtonStyle` in `AiteBar/UtilityWindowResources.xaml`. Make `PrimaryCommandButtonStyle` derive directly from that geometry and override foreground, background, and border with the accent resources. Keep the older utility aliases for compatibility, but ensure the public styles no longer depend on a geometry-free primary alias.

Remove the local `CommandButtonStyle` and `PrimaryCommandButtonStyle` from Timer and Icon Converter so both consume the global definitions. Change File Sorter's Sort button, Icon Converter's Choose and Save buttons, all four QR footer buttons, the Cancel and Save buttons in SettingsWindow, and the Cancel and Save buttons in AppSettingsWindow to the shared styles. Do not change their click handlers, enablement, default/cancel behavior, margins, or workflow logic.

Add a focused test that loads the shared dictionary as XML and asserts the geometry, inheritance, and primary accent overrides. The same test should parse each target XAML file and assert that every agreed button references the correct shared style. Extend existing WPF layout coverage where available so the larger buttons remain within the fixed window roots.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Run focused validation:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~CommandButtonStyleTests|FullyQualifiedName~IconConverterWindowLayoutTests"

Then run repository validation:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build
    .\installer\Build-Installer.ps1

If WPF generated files fail with access denied inside the filesystem sandbox, repeat build and test outside it. Before installer publish, stop only the running AiteBar process whose executable path is under `artifacts\publish\win-x64`, then restart that publish afterward.

## Validation and Acceptance

The focused style test must prove the shared secondary style has height 44, minimum width 150, padding 18,0, and font size 14, and that the primary style derives from it. It must prove the exact target style mapping. The Icon Converter WPF layout test must continue to show the Choose Image and Save ICO actions within the window root in Russian and English at minimum size.

The full Release build must complete without warnings or errors and every test must pass. Manual acceptance is to open each agreed window and confirm the button geometry matches Timer Start and Reset, primary buttons are accented, secondary buttons are neutral, localized labels are not clipped, keyboard focus is visible, and disabled QR export actions remain visibly disabled.

## Idempotence and Recovery

The migration is source-only and repeatable. Existing style aliases remain during this phase, so unrelated windows keep working. Do not reset the dirty worktree or remove user settings. If a fixed window clips after migration, first adjust its local layout container rather than weakening the shared style contract.

## Artifacts and Notes

Initial reference geometry from `TimerStopwatchWindow.xaml`:

    Height = 44
    MinWidth = 150
    Padding = 18,0
    FontSize = 14

Final build, test, installer, and smoke transcripts will be recorded here.

## Interfaces and Dependencies

No package, C# interface, or public API is added. The stable resource keys at completion are `CommandButtonStyle` for neutral commands and `PrimaryCommandButtonStyle` for accented commands. Both remain WPF `Style` resources targeting `Button`.

Plan revision note (2026-07-15 08:00Z): created the self-contained plan after inspecting the reference Timer styles, all agreed target buttons, and the QR/footer layout capacity.

Plan revision note (2026-07-15 08:08Z): recorded the completed shared-style migration and regression coverage before focused validation.

Plan revision note (2026-07-15 08:10Z): corrected semantic state inheritance after focused tests exposed the need to separate primary and secondary trigger chains.

Plan revision note (2026-07-15 08:12Z): recorded 18/18 focused structural and WPF layout tests and moved to full Release validation.

Plan revision note (2026-07-15 08:17Z): recorded complete Release, full-suite, installer checksum, publish restart, and post-start log evidence.

Plan revision note (2026-07-15 08:22Z): reopened the plan from user screenshot acceptance and corrected the persistent accent border and weak disabled contrast.

Plan revision note (2026-07-15 08:55Z): recorded the computed WPF visual-state regression test, final 662-test suite, rebuilt installer checksum, publish restart, and clean log evidence.
