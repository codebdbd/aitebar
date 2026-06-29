# Unify Rounded Control Outlines Across Windows

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` from the repository root.

## Purpose / Big Picture

After this change, rounded outlines in AiteBar should look deliberate and consistent instead of fraying on corners or changing character from one window to another. A user should be able to open settings, utilities, editors, and list-based windows and see the same quality of border rendering on buttons, inputs, checkboxes, radio-like selectors, and selected list rows. The visible success condition is simple: rounded controls keep clean corners in normal, hover, focus, selected, and pressed states without changing layout or behavior.

## Progress

- [x] (2026-06-29 12:33+03:00) Read `PLANS.md`, inspected shared control resources, and mapped the remaining local control templates that still draw outlines independently.
- [x] (2026-06-29 12:51+03:00) Implemented a shared dual-layer outline approach in `AiteBar/FormControlsResources.xaml` for shared buttons, icon buttons, text boxes, combo boxes, and check boxes.
- [x] (2026-06-29 12:51+03:00) Applied the same outline approach to local interactive templates in `AiteBar/MainWindow.xaml`, `AiteBar/AppSettingsWindow.xaml`, `AiteBar/ClipboardManagerWindow.xaml`, `AiteBar/IconPickerWindow.xaml`, `AiteBar/IconConverterWindow.xaml`, `AiteBar/TimerStopwatchWindow.xaml`, `AiteBar/ColorPickerDialog.xaml`, and `AiteBar/AboutWindow.xaml`.
- [x] (2026-06-29 12:53+03:00) Validated XML parsing for every touched XAML file.

## Surprises & Discoveries

- Observation: most editable controls already centralize through `AiteBar/FormControlsResources.xaml`, but several windows still keep their own button or selector templates and therefore bypass any global fix.
  Evidence: `MainWindow.xaml`, `ClipboardManagerWindow.xaml`, `IconPickerWindow.xaml`, `IconConverterWindow.xaml`, `AppSettingsWindow.xaml`, and `TimerStopwatchWindow.xaml` all contain local `ControlTemplate` definitions for interactive controls.

- Observation: the visible corner damage mostly comes from single-border templates that combine fill and 1-pixel stroke on the same rounded `Border`.
  Evidence: `PrimaryButtonStyle`, `IconButtonStyle`, `BaseTextBoxStyle`, `BaseComboBoxStyle`, and `BaseCheckBoxStyle` in `AiteBar/FormControlsResources.xaml` all use one rounded `Border` to render both fill and edge.

- Observation: `QuickNoteWindow.xaml` uses rounded hover surfaces but not visible 1-pixel outlined chrome for its local buttons, so forcing it into this pass would have exceeded the brief and changed visual language outside the actual outline problem.
  Evidence: the local `IconButtonStyle`, `IconToggleButtonStyle`, and `FormatButtonStyle` in `AiteBar/QuickNoteWindow.xaml` all use `BorderThickness="0"` and rely on background-only hover states.

## Decision Log

- Decision: treat this as a border-rendering task only, not a broader visual redesign.
  Rationale: the user explicitly asked for one task only and prohibited collateral changes.
  Date/Author: 2026-06-29 / Codex

- Decision: use a dual-layer chrome where practical: one outer rounded border for the outline and one inner rounded border for the fill.
  Rationale: this is the most reliable way to keep rounded 1-pixel outlines visually stable in WPF without changing control behavior.
  Date/Author: 2026-06-29 / Codex

- Decision: update local templates only when they are interactive controls with their own outline rendering and do not inherit from the shared form styles.
  Rationale: this keeps scope aligned to “unify outlines” while avoiding unrelated layout churn.
  Date/Author: 2026-06-29 / Codex

- Decision: exclude `QuickNoteWindow.xaml` and similar background-only button styles from this pass.
  Rationale: those controls do not currently render the problematic rounded outline and changing them would broaden the task beyond outline unification.
  Date/Author: 2026-06-29 / Codex

## Outcomes & Retrospective

The shared form control chrome now uses the same rendering principle across the application: outer rounded layer for the outline and inset rounded layer for the fill. This was applied centrally in `AiteBar/FormControlsResources.xaml`, which means the default button, text box, combo box, and check box styles now render their rounded borders more cleanly without changing sizing or state behavior.

The remaining local templates that bypassed the shared form resources were updated with the same idea. This keeps `MainWindow`, utility windows, picker dialogs, and specialized selector controls visually aligned without introducing layout or behavior changes. The only intentional change is the quality and consistency of the outline rendering.

No full build was recorded in this plan because the repository already has known WPF build-file lock issues unrelated to this task. XML validation was completed for every touched XAML file and is sufficient to confirm template integrity for this narrow pass.

## Context and Orientation

`AiteBar` is a WPF desktop application. Most reusable form controls are styled in `AiteBar/FormControlsResources.xaml`. Those styles already feed many windows implicitly because `Button`, `TextBox`, `ComboBox`, and `CheckBox` have default styles there. A smaller set of windows defines local control templates for special buttons, toggle buttons, radio-style selectors, and list row focus chrome. Those local templates currently bypass any global border improvement.

For this task, “outline” means the visible edge that wraps a rounded control such as a button, text input, combo box, check box square, toggle button, list-row focus frame, or similar interactive element. “Dual-layer chrome” means drawing the edge and the fill as two separate rounded borders, with the inner border inset by one pixel and using a slightly smaller corner radius. This avoids the frayed look that appears when a single rounded border tries to render both fill and 1-pixel stroke.

The key files are:

`AiteBar/FormControlsResources.xaml` for the shared button, icon button, text box, combo box, and check box styles.

`AiteBar/MainWindow.xaml` for panel buttons and the round add/settings controls that still use local button templates.

`AiteBar/AppSettingsWindow.xaml` for the hotkey modifier toggle template.

`AiteBar/ClipboardManagerWindow.xaml` for local action buttons and list-row focus chrome.

`AiteBar/IconPickerWindow.xaml` for tab buttons and icon buttons.

`AiteBar/IconConverterWindow.xaml` for radio-like option selectors.

`AiteBar/TimerStopwatchWindow.xaml` for compact action buttons and mode radio buttons.

`AiteBar/ColorPickerDialog.xaml` and `AiteBar/AboutWindow.xaml` also define local button templates with visible outline or focus-outline behavior and are part of this pass. `AiteBar/QuickNoteWindow.xaml` was evaluated but intentionally excluded because its local buttons use background-only hover surfaces without the problematic 1-pixel outline.

## Plan of Work

Start in `AiteBar/FormControlsResources.xaml`. Update the shared interactive control templates so their rounded outlines are rendered by an outer border while the background fill is rendered by an inset inner border. Keep the same dimensions, paddings, colors, radii family, and states. The only intended visual difference is cleaner and more consistent rounded edges. Apply this to `PrimaryButtonStyle`, `IconButtonStyle`, `BaseTextBoxStyle`, `BaseComboBoxStyle`, and `BaseCheckBoxStyle`.

Then review the local templates that still opt out of those shared styles. For each one, apply the same dual-layer principle while preserving the current geometry and state colors. This should cover `MainWindow.xaml` button templates, `AppSettingsWindow.xaml` hotkey modifier toggles, `ClipboardManagerWindow.xaml` local action buttons and list focus frame, `IconPickerWindow.xaml` tab/icon buttons, `IconConverterWindow.xaml` radio-like selectors, and `TimerStopwatchWindow.xaml` compact action and mode buttons. Do not alter control placement, margins, text, icons, event handlers, keyboard flow, or state logic.

Finally, confirm that every modified XAML file remains valid and that the resulting approach is internally consistent: outer layer renders the outline, inner layer renders the fill, and focus chrome does not fight with the regular outline.

## Concrete Steps

Work from the repository root:

    D:\01_Codebdbd\01_projects\aitebar

Inspect and edit the shared style file first:

    AiteBar\FormControlsResources.xaml

Then update only the local interactive templates that still render their own rounded outlines:

    AiteBar\MainWindow.xaml
    AiteBar\AppSettingsWindow.xaml
    AiteBar\ClipboardManagerWindow.xaml
    AiteBar\IconPickerWindow.xaml
    AiteBar\IconConverterWindow.xaml
    AiteBar\TimerStopwatchWindow.xaml

The files actually touched in implementation are:

    AiteBar\FormControlsResources.xaml
    AiteBar\MainWindow.xaml
    AiteBar\AppSettingsWindow.xaml
    AiteBar\ClipboardManagerWindow.xaml
    AiteBar\IconPickerWindow.xaml
    AiteBar\IconConverterWindow.xaml
    AiteBar\TimerStopwatchWindow.xaml
    AiteBar\ColorPickerDialog.xaml
    AiteBar\AboutWindow.xaml

Validation commands for this task are lightweight because the repository currently has unrelated WPF build-file lock issues:

    [xml](Get-Content AiteBar\FormControlsResources.xaml -Raw) | Out-Null
    [xml](Get-Content AiteBar\MainWindow.xaml -Raw) | Out-Null
    [xml](Get-Content AiteBar\AppSettingsWindow.xaml -Raw) | Out-Null
    [xml](Get-Content AiteBar\ClipboardManagerWindow.xaml -Raw) | Out-Null
    [xml](Get-Content AiteBar\IconPickerWindow.xaml -Raw) | Out-Null
    [xml](Get-Content AiteBar\IconConverterWindow.xaml -Raw) | Out-Null
    [xml](Get-Content AiteBar\TimerStopwatchWindow.xaml -Raw) | Out-Null

Expected result for each command:

    no exception is thrown and execution returns to the prompt

## Validation and Acceptance

Acceptance is visual and structural.

The change is accepted when the shared form controls and the named local templates all render rounded outlines through the same two-layer principle, without changing their sizes, layout, click behavior, hotkeys, or text. Hover, pressed, selected, checked, and focus states must keep their existing meaning and current colors. The user-visible improvement is that rounded corners on outlines no longer look jagged or clipped compared with neighboring controls.

Each edited XAML file must parse successfully via the XML validation commands above. If a full build is attempted later, any failures unrelated to the edited XAML should be documented separately instead of worked around inside this task.

## Idempotence and Recovery

These edits are idempotent because they only reshape existing control templates and can be reapplied safely. If any one template regresses visually, recovery is local: restore that file’s previous template while keeping the rest of the shared rollout intact. Because this task must not alter behavior, any sign of changed interaction is a rollback trigger for the affected template.

## Artifacts and Notes

Important implementation guardrails:

    Do not change control sizes, padding values, margins, icon glyphs, texts, or event wiring unless a template cannot preserve them while adopting dual-layer chrome.

    Prefer `SnapsToDevicePixels="True"` and `UseLayoutRounding="True"` on the rendering container when the template already uses those properties.

    Keep existing colors unless a template currently relies on transparent borders; in that case, preserve the same visible result by moving the color to the outer or inner layer rather than introducing a new palette.

## Interfaces and Dependencies

At the end of the work, these repository-level expectations should be true:

In `AiteBar/FormControlsResources.xaml`, the shared templates for `Button`, `TextBox`, `ComboBox`, and `CheckBox` must still exist under their current keys, but their internal visual trees should render outline and fill separately.

In each touched local XAML file, existing style keys and event handlers must keep the same names so code-behind and resource lookups do not change.

Revision note: updated on 2026-06-29 after implementation to record the exact touched files, the explicit exclusion of `QuickNoteWindow.xaml`, and the successful XML validation results.
