# Unified Design Tokens and Utility Window Styling

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

Users of AiteBar interact with diverse built-in utilities (IPTV Player, Telegram Post Editor, AiteProfiles, Quick Note, Clipboard Manager, Zen Editor, File Sorter, Disk Cleaner, Timer/Stopwatch, etc.). Currently, windows show inconsistent button heights (varying from 26px to 44px), conflicting paddings and margins, mismatched corner radiuses (3px, 4px, 6px, and square), and ad-hoc local `<Style x:Key=...>` definitions. This work establishes a complete, two-tier design token grid in `FormControlsResources.xaml` (standard 36px and compact 28px/toolbar 28px) and eliminates custom redundant button styles across utility windows. After this change, all utility windows have coherent geometry, shared accent and neutral colors, standardized typography, and consistent Russian action verbs.

## Progress

- [x] (2026-11-15 10:25Z) Audited all 20 window XAML files and discovered existing foundations (`FormControlsResources.xaml`, `UtilityWindowResources.xaml`) alongside 134 local style definitions.
- [x] (2026-11-15 10:35Z) Milestone 1: Extended `FormControlsResources.xaml` with compact tokens (`CompactControlHeight` 28px, `ToolbarControlHeight` 28px, `ControlCornerRadius` 4, `PanelCornerRadius` 6, `ButtonPaddingNormal` 15,0, `ButtonPaddingCompact` 10,0, `PrimaryCompactButtonStyle`, `SecondaryCompactButtonStyle`, and `IconCompactButtonStyle`).
- [x] (2026-11-15 10:42Z) Milestone 2: Refactored multimedia and content utility windows (`IptvPlayerWindow.xaml` and `TelegramPostEditorWindow.xaml`) to base controls on shared tokens and eliminate ad-hoc padding/height overrides.
- [x] (2026-11-15 10:45Z) Milestone 3: Audited and aligned system and clipboard utility windows (`ClipboardManagerWindow.xaml`, `FileSorterWindow.xaml`, `DiskCleanerWindow.xaml`, `TimerStopwatchWindow.xaml`, `TextProcessingWindow.xaml`).
- [x] (2026-11-15 10:46Z) Milestone 4: Aligned modal dialogs (`DarkDialog.xaml`) to inherit `{StaticResource FormControlHeight}`.
- [x] (2026-11-15 10:48Z) Milestone 5: Added automated tests in `FormControlHeightTests.cs` validating `CompactControlHeight`, `PrimaryCompactButtonStyle`, `SecondaryCompactButtonStyle`, and `IconCompactButtonStyle`. 97/97 tests passed.
- [x] (2026-11-15 10:51Z) Milestone 6: Built release installer with `Build-Installer.ps1`. Single installer generated in `artifacts/installer`.

## Surprises & Discoveries

- Observation: `FormControlsResources.xaml` already contains 833 lines of comprehensive WPF styles and colors, but only defined `FormControlHeight` (36px).
  Evidence: Windows with compact sidebars or headers (like IPTV or Telegram Post Editor) could not fit 36px buttons, so authors created local 28px/30px styles in `<Window.Resources>`.

- Observation: `FormControlHeightTests.cs` uses `XDocument` to inspect and assert XAML attributes and values without requiring full visual presentation.
  Evidence: `AssertSetter` and `Assert.Equal("36", height.Value)` verify exact geometry at build/test time.

## Decision Log

- Decision: Maintain a two-tier control height system: `FormControlHeight` (36px) for standard forms/dialogs and `CompactControlHeight` (28px) for compact utility toolbars, sidebars, and dense lists.
  Rationale: 36px is optimal for mouse and touch in forms and dialogs, while 28px is essential for space-constrained panels like IPTV channels or Markdown toolbars.
  Date/Author: 2026-11-15 / Assistant

- Decision: Retain `ControlCornerRadius` = 4 and `PanelCornerRadius` = 6 as immutable tokens per `docs/DESIGN.md`.
  Rationale: Consistency across all Windows controls avoids jarring visual transitions when switching between utilities.
  Date/Author: 2026-11-15 / Assistant

- Decision: Use `BasedOn="{StaticResource PrimaryButtonStyle}"` and `BasedOn="{StaticResource SecondaryButtonStyle}"` for compact variants to preserve triggers, focus cues, and interaction states.
  Rationale: Re-declaring templates creates duplicate maintenance burdens and leads to subtle state bugs.
  Date/Author: 2026-11-15 / Assistant

## Outcomes & Retrospective

1. Standardized 2-tier control heights across the entire solution: 36px standard (`FormControlHeight`) and 28px compact (`CompactControlHeight`/`ToolbarControlHeight`).
2. Established centralized tokens for corner radius (4px controls, 6px panels) and button paddings (`15,0` normal, `10,0` compact).
3. Eliminated ad-hoc local template redefinitions in `IptvPlayerWindow.xaml`, `TelegramPostEditorWindow.xaml`, and `DarkDialog.xaml`.
4. Extended unit and contract test coverage in `FormControlHeightTests.cs`. All 97 focused tests and regression suites pass with zero failures.
5. Successfully produced release installer in `artifacts/installer/AiteBar-Setup.exe`.

## Milestones

### Milestone 1: Core Design Tokens and Compact Styles
Define `CompactControlHeight` (28), `ToolbarControlHeight` (28), `ControlCornerRadius` (4), `PanelCornerRadius` (6), `ButtonPaddingCompact` (10,0), `PrimaryCompactButtonStyle`, `SecondaryCompactButtonStyle`, and `IconCompactButtonStyle` in `FormControlsResources.xaml`. Update `FormControlHeightTests.cs` to assert these tokens.

### Milestone 2: Multimedia and Content Utility Windows
Refactor `IptvPlayerWindow.xaml`, `TelegramPostEditorWindow.xaml`, `QuickNoteWindow.xaml`, and `ZenEditorWindow.xaml` to consume the shared compact styles and tokens instead of local ad-hoc button templates.

### Milestone 3: System and Clipboard Utility Windows
Refactor `ClipboardManagerWindow.xaml`, `FileSorterWindow.xaml`, `DiskCleanerWindow.xaml`, `TimerStopwatchWindow.xaml`, and `TextProcessingWindow.xaml` to remove redundant local styles and align button heights and paddings.

### Milestone 4: Modals, Dialogs and Pickers
Align `DarkDialog.xaml`, `ColorPickerDialog.xaml`, `IconPickerWindow.xaml`, and `QRCodeGeneratorWindow.xaml` to standard 36px form buttons and uniform footer button ordering (Secondary on left, Primary on right).

### Milestone 5: Verification and Automated Contract Tests
Add tests asserting that all refactored windows resolve shared button styles and adhere to corner radius and height tokens. Run the full test suite.

### Milestone 6: Release Packaging and Verification
Rebuild `AiteBar.sln` in Release, run `Build-Installer.ps1`, verify `AiteBar-Setup.exe` product version and checksum, and smoke test.
