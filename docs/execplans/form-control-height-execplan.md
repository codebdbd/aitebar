# Standardize form controls at 36 device-independent pixels

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

All ordinary one-line inputs and selection controls in AiteBar should align to the same 36-unit row. Users should no longer see a 30-pixel search field next to 32-pixel combos, 32-pixel hotkey modifiers, or mismatched selection buttons. The change covers form TextBox and ComboBox controls, parameter selectors, hotkey modifier buttons, Quick Note formatting ComboBoxes, and the Browse, rotation-profile, library, and custom-icon selection buttons. Large command buttons remain 44 units and multiline text remains 60 or content-sized.

## Progress

- [x] (2026-07-15 09:07Z) Re-read `PLANS.md`, inventoried 30 XAML TextBoxes, 22 XAML ComboBoxes, one dynamically created context TextBox path, and all selected button-like controls.
- [x] (2026-07-15 09:10Z) Added `FormControlHeight=36` and migrated base TextBox/ComboBox styles plus specialized overrides.
- [x] (2026-07-15 09:10Z) Migrated parameter selectors, hotkey modifiers, Timer presets, Quick Note formatting selectors, and four Settings form-selection buttons.
- [x] (2026-07-15 09:12Z) Added structural and computed WPF regression tests; focused validation passed 35/35 before the final Quick Note selector addition.
- [x] (2026-07-15 09:14Z) Release solution build passed with zero warnings/errors and the full suite passed 677/677 before the final Quick Note selector addition.
- [x] (2026-07-15 09:21Z) Completed final Release build, full suite, installer rebuild, publish restart, and startup log smoke.
- [x] (2026-07-15 10:58Z) Applied the visual follow-up: compact 4-unit TextBox padding and dedicated `#2D2D2D` input/search background; after user correction, Icon Converter/QR panel backgrounds use `#192A41`.

## Surprises & Discoveries

- Observation: the global one-line TextBox and ComboBox styles are both 32 units high.
  Evidence: `BaseTextBoxStyle` and `BaseComboBoxStyle` in `AiteBar/FormControlsResources.xaml` each set `Height` to 32.

- Observation: local compact styles override the global height in QR Generator and Icon Picker.
  Evidence: `CompactTextBoxStyle` and `CompactComboStyle` set 32, while `SearchTextBoxStyle` sets 30.

- Observation: two QR fields are intentionally multiline.
  Evidence: `TxtEmailBody` and `TxtSmsMessage` set `AcceptsReturn="True"` and local `Height="60"`; a local dependency property value takes precedence over the shared style and must remain unchanged.

- Observation: the two Quick Note ComboBoxes use a custom icon-oriented template, but they still select formatting parameters.
  Evidence: `CmbHeading` and `CmbList` use `FormatComboStyle`; the final audit classified them as in-scope and aligned their containing row to 36.

- Observation: Timer mode buttons and Icon Picker tabs are already 36 units high, while Timer presets are 34 and Icon Converter option radios are 28.
  Evidence: their local styles explicitly declare those values.

- Observation: a `System.Double` resource can set `FrameworkElement.Height`, but cannot directly set `RowDefinition.Height`, whose type is `GridLength`.
  Evidence: the first full suite caught a `XamlParseException` in both Quick Note window-construction tests; keeping the layout row as literal `Height="36"` fixed it, and the repeated focused and full suites passed.

- Observation: parallel WPF build and test invocations contend over generated `obj` files and can leave reusable MSBuild workers behind.
  Evidence: the initial parallel validation produced access-denied errors for `App.g.cs`; sequential execution with `-nr:false` passed.

## Decision Log

- Decision: define a single `FormControlHeight` resource with value 36 in `FormControlsResources.xaml` and reference it from shared and specialized form styles.
  Rationale: one source of truth prevents TextBox, ComboBox, and selection controls from drifting independently.
  Date/Author: 2026-07-15 / Codex

- Decision: apply 36 to every one-line TextBox and form ComboBox, including search fields, timer input, color inputs, and dynamically created context-name fields.
  Rationale: these controls have the same form-row role even when their contents differ.
  Date/Author: 2026-07-15 / Codex

- Decision: preserve 60 for the two multiline QR fields, but migrate the two Quick Note toolbar ComboBoxes and their row to 36.
  Rationale: multiline editors need vertical space; the Quick Note controls are still parameter-selection ComboBoxes and the user's program-wide ComboBox requirement takes precedence over their prior compact geometry.
  Date/Author: 2026-07-15 / Codex

- Decision: apply 36 to segmented parameter controls, Icon Converter option radios, Timer presets, hotkey modifier toggles, and the four form-selection buttons.
  Rationale: each is a button-like value selector aligned with the same form inputs; Timer mode and Icon Picker tab controls already satisfy the contract and need no visual change.
  Date/Author: 2026-07-15 / Codex

- Decision: keep 44-unit primary/secondary command buttons and MainWindow controls outside this plan.
  Rationale: command prominence and panel layout are separate established contracts.
  Date/Author: 2026-07-15 / Codex

## Outcomes & Retrospective

The application now exposes a single `FormControlHeight` resource with value 36. Base one-line TextBoxes and ComboBoxes, specialized QR and search controls, App Settings segmented selectors and hotkey modifiers, Icon Converter options, Timer modes and presets, Quick Note formatting ComboBoxes, and the four Settings selection buttons all use that contract. The two intentional QR multiline inputs remain 60. Command buttons remain on their separate 44-unit contract and window chrome remains compact.

`FormControlHeightTests` protects the shared token, specialized style references, exact Settings button mapping, multiline exceptions, and resolved WPF runtime heights. Final validation passed a Release solution build with zero warnings/errors, 18/18 focused tests, and 678/678 full tests. The installer and checksum were regenerated, the new publish started successfully as PID 22164, and `error.log` remained unchanged at 409679 bytes after the startup smoke.

The visual follow-up separated field backgrounds from the existing secondary-button background instead of changing the shared button brush. `BaseTextBoxStyle` now uses `FormInputBackground=#2D2D2D`, keeps that exact fill on hover while indicating hover through the border, and uses `Padding=4,0`; Icon Picker search inherits the same style. After the user's color correction, Icon Converter `PanelBorderStyle` and QR `PanelBorderStyle`/`InspectorSectionStyle` use `#192A41`.

## Context and Orientation

WPF uses device-independent pixels for layout. At 100 percent Windows scaling one unit corresponds to one physical pixel, and WPF scales it at higher DPI. `AiteBar/FormControlsResources.xaml` is loaded globally through `AiteBar/App.xaml`; it owns the implicit TextBox and ComboBox styles. Local styles in QR Generator, Icon Picker, App Settings, Icon Converter, and Timer override selected properties. A local property such as `Height="60"` on an individual element wins over a style setter.

The inventory contains 30 TextBoxes in XAML: 28 one-line controls and two QR multiline controls. `AppSettingsWindow` also creates one TextBox per context at runtime, and those controls receive the implicit global style. The 22 ComboBoxes comprise 20 form selectors plus two Quick Note toolbar selectors, all now within the 36-unit contract. App Settings contains 21 segmented parameter RadioButtons and 36 hotkey modifier ToggleButtons. Icon Converter has four option RadioButtons. Timer has eleven preset buttons and two mode RadioButtons, with the modes already at the target height.

## Plan of Work

Add a `System.Double` resource named `FormControlHeight` with value 36 near the form-control brushes in `AiteBar/FormControlsResources.xaml`. Use it in `BaseTextBoxStyle` and `BaseComboBoxStyle`. Add `FormSelectionButtonStyle` based on `SecondaryButtonStyle`, setting height to the token.

Remove or replace conflicting local heights: QR compact TextBox and ComboBox styles, Icon Picker search, File Sorter location ComboBox, App Settings hotkey modifiers, Settings rotation-profile button, Settings Browse/library/custom-icon buttons, general-settings segmented RadioButtons, Icon Converter option RadioButtons, and Timer presets. Existing mode tabs already at 36 may reference the token when doing so does not change their behavior. Do not alter click handlers, bindings, margins, widths, enablement, or field validation.

Add a focused test that parses the shared dictionary and asserts the height token and style references. It must cover all specialized one-line TextBox and ComboBox styles and preserve only the approved multiline exceptions. It must assert the exact form-selection button mapping and the selected button-like style heights. Add a computed WPF test proving ordinary TextBox and ComboBox instances resolve to 36. Run existing Settings, Icon Converter, and other window layout tests to catch clipping.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Run focused validation:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~FormControlHeightTests|FullyQualifiedName~IconConverterWindowLayoutTests|FullyQualifiedName~SettingsWindow"

Then run repository validation:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build
    .\installer\Build-Installer.ps1

If WPF-generated files are locked in the sandbox, repeat the build or tests outside it. Before installer publish, stop only the AiteBar process whose executable path is under `artifacts\publish\win-x64`, then restart that publish afterward.

## Validation and Acceptance

Computed WPF tests must observe resolved `Height` 36 for the base TextBox, base ComboBox, and form-selection button. Structural coverage must prove hotkey and parameter-selection styles, including Quick Note formatting selectors, reference the shared height and the two QR multiline fields remain 60. Existing window layout tests must pass at minimum size and in supported localized cultures.

Manual acceptance is to open Add Button, Program Settings general/hotkeys, QR Generator, Icon Picker, File Sorter, Icon Converter, Timer, Quick Note, and the small input dialogs. All one-line fields and value-selection buttons should align to 36 without clipped labels. Command buttons should remain 44 and the two QR multiline fields should remain 60.

## Idempotence and Recovery

The edits are source-only and safe to repeat. Do not reset the dirty worktree or delete settings. If a fixed-height window clips, adjust its local row spacing or window layout after confirming the 36-unit contract; do not silently reintroduce a smaller control height. If publish files are locked, identify and stop only the running artifact process before retrying the installer.

## Artifacts and Notes

Initial divergent values are 30 for Icon Picker search, 32 for base inputs, QR compact inputs, File Sorter ComboBox, hotkey modifiers, and the rotation-profile selector, 34 for Timer presets, and 28 for Icon Converter option radios.

Final installer: `artifacts/installer/AiteBar-Setup.exe`, 77,489,214 bytes, SHA-256 `2A7A17393F206D961BB4415F9EDE748559A780C6E2D777EF2C078836E56F3E10`. Code signing was skipped because no PFX certificate was supplied. Publish startup PID: 22164. The existing `%APPDATA%\Codebdbd\Aite Bar\error.log` stayed at 409679 bytes with timestamp 2026-07-15 09:15:34 local during the six-second startup check.

Latest visual-follow-up installer supersedes the preceding artifact: 77,477,001 bytes, SHA-256 `F5B66BF41D752860D4F6C7787BA9ADAF06D912806942166A8106C5A32A02E622`. Code signing remains skipped because no PFX certificate was supplied. Final publish startup PID: 21416; `error.log` remained 409679 bytes.

User color correction supersedes that artifact: the Icon Converter panel and both QR panel styles now use `#192A41`. Focused tests passed 22/22 and the full suite passed 682/682. Latest installer size is 77,492,740 bytes with SHA-256 `D804EF98F98F3597EB20547603546859C64F1CF7D902297A51D986431477ED51`; publish PID 21596 started successfully and `error.log` remained 409679 bytes.

Placeholder alignment follow-up adds the shared `FormInputPlaceholderMargin=5,0,0,0`, matching the caret position produced by the 4-unit TextBox padding plus the 1-unit inner template border. QR input, Clipboard search, Rotation Profile search, and all three Settings placeholders use it. Focused tests passed 28/28 and the full suite passed 688/688. Latest installer size is 77,488,187 bytes with SHA-256 `5A9D5E4C0584566B0748598FD4193E162240104C770ED61E5136E54FC5D38DAC`; publish PID 18812 started successfully and `error.log` remained unchanged.

Clipping follow-up corrects parent geometry that still assumed the former 32–34-unit controls. Dynamic context rows now resolve `FormControlHeight` instead of forcing 34, and both timer preset layout rows are 88 units so two 36-unit buttons with 4-unit margins fit exactly. Focused tests passed 65/65 and the full suite passed 690/690. Latest installer size is 77,485,910 bytes with SHA-256 `4D4F97E8B5783DFF38930F765D10B44E7BF0A87E7944B02E57EB22EADBA67952`; publish PID 24120 started successfully and `error.log` remained unchanged.

## Interfaces and Dependencies

No package or C# API is added. The stable shared resource is `FormControlHeight`, a `System.Double` equal to 36. `FormSelectionButtonStyle` is a WPF `Style` targeting `Button` and based on `SecondaryButtonStyle`. Existing `BaseTextBoxStyle` and `BaseComboBoxStyle` retain their keys and templates.

Plan revision note (2026-07-15 09:07Z): created the self-contained plan after a complete input, combo, selector, hotkey, and exception inventory.

Plan revision note (2026-07-15 09:15Z): recorded implementation and validation; expanded the contract to the two Quick Note formatting ComboBoxes after the final all-control audit.

Plan revision note (2026-07-15 09:21Z): recorded final build, 678-test suite, installer checksum, publish restart, and unchanged startup error log; marked the plan complete.

Plan revision note (2026-07-15 10:58Z): recorded the requested color/padding follow-up, its 682-test validation, regenerated installer, and clean startup smoke.
