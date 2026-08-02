# Restore panel cues and make mouse-hover activation optional

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

This release fix restores information and accessibility cues on the main AiteBar panel and gives the user control over automatic mouse activation. After the change, hovering the numbered context badge shows the active context name, keyboard navigation draws a visible blue outline around the focused panel button, and a new settings switch immediately before “Show panel position indicator” can disable only edge-hover activation. The global show-panel hotkey, tray command, and position indicator must continue to open the panel when hover activation is disabled.

## Progress

- [x] (2026-08-02 03:15Z) Traced the context badge, button templates, keyboard input mode, activation timer, settings clone/load/save path, localization resources, and release tests.
- [x] (2026-08-02 03:28Z) Added the localized context tooltip and visible keyboard-only focus geometry with regression contracts.
- [x] (2026-08-02 03:34Z) Added and persisted the mouse-hover activation switch in all four languages and gated only the activation timer.
- [x] (2026-08-02 03:47Z) Passed 103 focused tests, a zero-warning Release build, 1071 non-WPF plus 73 isolated WPF tests, rebuilt the 1.13.0 installer, verified its checksum, and smoke-started the published executable.

## Surprises & Discoveries

- Observation: The keyboard focus trigger still sets a blue `BorderBrush`, but its `FocusChrome` border has no nonzero `BorderThickness`.
  Evidence: Both button templates in `AiteBar/MainWindow.xaml` contain the keyboard-only multi-trigger and set `#3ABEFF`, but the focus border defaults to zero thickness, making the cue invisible.

- Observation: Mouse-hover activation is centralized in one dispatcher timer and does not share the explicit hotkey or tray paths.
  Evidence: `EnsureStartupInfrastructure` calls `ActivationDwellTracker.Update` and `ShowDock()` only for the edge activation zone. Gating that branch preserves `TogglePanelFromKeyboard`, tray `ShowDock()`, and indicator-initiated opening.

## Decision Log

- Decision: Store `ShowPanelOnMouseHover` as a non-nullable Boolean with default `true`.
  Rationale: Existing settings files omit the property and JSON deserialization will retain the initializer, preserving current behavior. A simple Boolean also avoids the nullable compatibility handling required by the older indicator setting.
  Date/Author: 2026-08-02 / Codex

- Decision: Show the tooltip as a localized “Context {number}: {name}”.
  Rationale: The number alone is already visible; the useful missing information is the active context name, while the localized prefix makes the tooltip unambiguous.
  Date/Author: 2026-08-02 / Codex

- Decision: Gate only the timer-driven activation zone and reset its dwell tracker while disabled.
  Rationale: The user asked to disable appearing on mouse hover, not to disable intentional opening. Resetting prevents a partially accumulated hover delay from opening the panel immediately after re-enabling the option.
  Date/Author: 2026-08-02 / Codex

## Outcomes & Retrospective

All three requested behaviors are implemented. The context badge has an immediate localized tooltip containing its number and name. Both panel button templates have a one-pixel transparent focus border that turns blue only when the existing keyboard cue flag is active. `ShowPanelOnMouseHover` defaults to true for old settings, appears immediately before the position-indicator switch, persists through clone/load/save, and gates only timer-driven edge activation.

The release candidate builds with zero warnings and errors. Focused tests passed 103/103. The release-equivalent split passed 1071 non-WPF tests and 73 isolated WPF tests, 1144 total. The rebuilt unsigned installer is 79,456,114 bytes and has SHA-256 `FABECE024C254FB7F6BB9CB9BA601385C2DB7856D9FEFAD7C3767D0AC41A9CD8`, matching `SHA256SUMS.txt`. The published executable remained alive for the eight-second smoke interval.

## Context and Orientation

`AiteBar/MainWindow.xaml` defines both panel button templates and the numbered `ContextIndicator`. `AiteBar/MainWindow.KeyboardNavigationHandler.cs` turns on the attached `KeyboardFocusVisualService.ShowKeyboardFocusCue` flag when navigation begins. `AiteBar/MainWindow.xaml.cs` refreshes the badge and contains `EnsureStartupInfrastructure`, whose timer detects mouse dwell in the activation zone.

Application settings are represented by `AppSettings` in `AiteBar/Models.cs`. `AiteBar/AppSettingsService.cs` deep-clones them so every new property must be copied there. `AiteBar/AppSettingsWindow.xaml` presents the switches, while `LoadSettings`, `BtnSave_Click`, and `RefreshAutomationNames` in `AiteBar/AppSettingsWindow.xaml.cs` load, save, and expose them to accessibility tools. Text resources live in `AiteBar/Resources/Strings.resx` plus `.ru`, `.uk`, and `.de` variants.

## Plan of Work

Give `ContextIndicator` immediate tooltip behavior and set its tooltip during `UpdateContextIndicator`, where both the enabled ordinal and active context object are already available. Apply the same edge-aware tooltip placement used by buttons.

In each main-panel button template, give `FocusChrome` a transparent one-pixel border so the existing keyboard-only trigger can change its brush. Keep the cue conditional on keyboard mode so ordinary mouse clicks do not leave a focus ring.

Add `ShowPanelOnMouseHover` to the settings model and clone. Insert `ChkShowPanelOnMouseHover` as the first switch in the existing general behavior card, immediately before `ChkShowTaskbarPositionIndicator`; load and save it and add its automation name. In the activation timer, return from the hidden-panel hover branch after resetting the dwell tracker whenever the option is false.

Add source/layout/settings tests that prove the tooltip assignment, nonzero focus border, switch ordering, clone persistence, and timer gate. Update localization completeness through the existing resource tests.

## Concrete Steps

Run from `D:\01_Codebdbd\01_projects\aitebar`:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~AppSettingsServiceTests|FullyQualifiedName~AppSettingsLayoutContractTests|FullyQualifiedName~RuntimeLocalizationWindowSourceTests|FullyQualifiedName~CommandButtonStyleTests"
    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build
    .\installer\Build-Installer.ps1 -Configuration Release

If the combined WPF host hangs, use the release workflow strategy: exclude WPF collection classes from one run and execute each excluded class in its own `dotnet test` process.

## Validation and Acceptance

With the panel visible, move the pointer over the numbered badge and observe “Context N: Name” in the current UI language. Open the panel by its hotkey, press Tab or an arrow key, and observe a one-pixel blue rounded outline around exactly the focused button as focus moves.

In application settings, observe “Show panel on mouse hover” immediately before “Show panel position indicator”. Turn it off and save. Moving the pointer into the configured edge activation zone for longer than the delay must not open the panel; the global hotkey, tray Open command, and position indicator must still open it. Restarting AiteBar must preserve the choice. Turning it back on must restore the existing dwell behavior.

## Idempotence and Recovery

All edits are additive or narrow template corrections and can be reapplied safely. Settings compatibility is preserved by the default-true property initializer. Build, tests, and installer generation are repeatable. Do not reset or discard the existing dirty release worktree.

## Artifacts and Notes

The pre-fix focus template proves the regression:

    <Border x:Name="FocusChrome" Background="Transparent" CornerRadius="4"/>
    ...
    <Setter TargetName="FocusChrome" Property="BorderBrush" Value="#3ABEFF"/>

Because no border thickness is assigned, the brush has no visible geometry.

## Interfaces and Dependencies

No new dependency is required. `AppSettings` must expose `public bool ShowPanelOnMouseHover { get; set; } = true;`. The settings window must expose a named `CheckBox` called `ChkShowPanelOnMouseHover`. Localization must provide `AppSettingsWindow_ShowPanelOnMouseHover`, `AppSettingsWindow_ShowPanelOnMouseHoverHint`, and `Main_ContextIndicatorTooltipFormat` in English, Russian, Ukrainian, and German.

Plan revision note (2026-08-02 03:15Z): Created after tracing all three regressions and resolving backward compatibility and activation-scope decisions.

Plan revision note (2026-08-02 03:47Z): Recorded completed implementation, backward-compatibility evidence, full test counts, rebuilt artifact details, checksum, and smoke result.
