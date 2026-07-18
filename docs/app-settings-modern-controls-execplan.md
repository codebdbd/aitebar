# Modernize every Program Settings control without changing saved behavior

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept current while work proceeds. Maintain it according to `PLANS.md` at the repository root.

## Purpose / Big Picture

Program Settings has a fixed left navigation region and one scrolling page, while the window itself can be resized for comfortable use. The settings surface uses a modern Fluent-style presentation: every section has a title and description, each complex choice is presented in a distinct card, related binary settings are rows with switch-shaped controls, language is a ComboBox, numeric choices use discrete sliders with visible current values, contexts are a clean editable list, hotkeys are single capture fields, and built-in utilities are labeled switch rows.

The saved `AppSettings` shape, defaults, Save/Cancel boundary, immediate language behavior, context rules, hotkey validation, and utility catalog remain unchanged. The result is observable by opening Program Settings and exercising every control before saving and after reopening.

## Progress

- [x] (2026-07-15 12:20Z) Confirmed the current inventory: four sections, eight General choices, eight generated context rows, nine hotkeys, sixteen utility visibility settings, one clipboard privacy setting, and the existing footer commands.
- [x] (2026-07-15 12:20Z) Selected control semantics and regression boundaries before editing production XAML.
- [x] (2026-07-15 12:42Z) Added pure conversion/capture helpers and 22 passing contract tests before removing old controls.
- [x] (2026-07-15 13:02Z) Added reusable card, setting-row, selection-card, switch, slider, and hotkey-field visuals.
- [x] (2026-07-15 13:02Z) Recomposed General and Contexts while preserving load/save and localization behavior.
- [x] (2026-07-15 13:05Z) Replaced the nine modifier-plus-key editors with tested hotkey capture controls; runtime capture and clear passed.
- [x] (2026-07-15 13:05Z) Recomposed Quick Tools and clipboard privacy as switch rows.
- [x] (2026-07-15 13:18Z) Release build and full suite pass with zero warnings/errors and 730/730 tests; installer rebuild and final published startup-log smoke also pass.
- [x] (2026-07-17 00:00Z) Made Program Settings resizable from an 800-by-480 minimum while retaining the 900-by-560 initial size and fixed sidebar/footer composition.
- [x] (2026-07-17 00:00Z) Reworked the three discrete sliders with a thicker track, four step markers, a larger accent thumb, click-to-position behavior, and hover, drag, keyboard-focus, and disabled states; focused layout/settings tests pass 24/24.
- [x] (2026-07-17 00:00Z) Final Release build completed with zero warnings and errors; the complete suite passed 771/771.
- [x] (2026-07-17 00:00Z) Added Fluent icons for General, Contexts, Hotkeys, and Quick Tools through the shared left-navigation item template, retaining localized content, tooltips, and automation names.
- [x] (2026-07-17 00:00Z) Navigation-icon focused tests passed 25/25; final Release build completed with zero warnings and errors, and the complete suite passed 772/772.
- [x] (2026-07-17 04:47 local) Rebuilt the win-x64 publish and installer; `AiteBar-Setup.exe` is 77,495,232 bytes and its recorded SHA-256 matches the generated file.
- [x] (2026-07-17 00:00Z) Reviewed the user's runtime screenshot and identified undersized 180-unit navigation, cramped 900-by-560 initial geometry, 12-unit section gaps, and a slider track/thumb coordinate mismatch.
- [x] (2026-07-17 00:00Z) Increased the initial/minimum window geometry, navigation width, content insets, card padding, and section spacing; aligned the slider active progress and step markers to the 26-unit thumb centers.
- [x] (2026-07-17 00:00Z) Applied the user's explicit 700-unit maximum to the right content column and adjusted the initial window width to 1000 units to avoid unused horizontal space.
- [x] (2026-07-17 00:00Z) Moved Clipboard Manager history persistence from the Quick Tools visibility section into the grouped General application-behavior card, preserving its privacy hint and save semantics.
- [x] (2026-07-17 00:00Z) Removed decorative perimeter borders from the navigation panel, settings cards, and selection tiles to match Windows Settings' filled-surface hierarchy; retained dividers, input-control borders, and keyboard focus cues.
- [x] (2026-07-17 00:00Z) Replaced the font-dependent `×` clear affordance with a fixed 32-unit Fluent Dismiss button and hid it for unassigned shortcuts.
- [x] (2026-07-17 00:00Z) Added persisted, validated, registered, executable settings hotkeys for Icon Converter and Clipboard Manager so all seven built-in utility windows are covered; updated technical/user/function documentation.
- [x] (2026-07-17 06:18 local) Clear-button and utility-hotkey follow-up passed focused tests 155/155, a zero-warning/error Release build, the full 774/774 suite, and final publish/installer packaging.
- [x] (2026-07-17 05:51 local) Persistence relocation and borderless-surface update passed focused tests 16/16, a zero-warning/error Release build, the full 773/773 suite, and final publish/installer packaging.
- [x] (2026-07-17 05:00 local) Screenshot-driven geometry correction passed focused tests 25/25, a zero-warning/error Release build, the complete 772/772 suite, and final publish/installer packaging.
- [x] (2026-07-18 00:00 local) Superseded the earlier compact geometry by explicit user request: initial width is 1200 DIP, navigation was first expanded and then screenshot-corrected to 255 DIP, the gap is 24 DIP, and centered right content grows responsively to a 1000-DIP maximum; Release and all 782 tests pass.
- [x] (2026-07-18 00:00 local) Matched the Windows Settings navigation measurements from the user's screenshot: removed the extra 10-DIP navigation padding so the surface starts at the window's 16-DIP margin, and reduced navigation rows from 44 to 36 DIP.
- [x] (2026-07-18 00:00 local) Replaced repetitive Quick Tools captions with dedicated action-oriented titles and descriptions in all four languages; rows now sort alphabetically using the active UI culture.
- [x] (2026-07-18 00:00 local) Moved the repeated hotkey capture instruction to a single line above the hotkey card and reduced each row to its unique command title and capture field; Release and all 783 tests pass.

## Surprises & Discoveries

- Observation: language is the only setting applied and persisted immediately; ordinary controls are staged until Save.
  Evidence: `SegLanguage_Click` saves through `AppSettingsService`, while `BtnSave_Click` writes the remaining values.

- Observation: WPF has no native Fluent `ToggleSwitch` control in this project.
  Evidence: the application targets WPF without a WinUI dependency. Reusing `CheckBox` with a switch template preserves the existing nullable `IsChecked`, keyboard, automation, and save code without adding a package.

- Observation: activation zone and delay each expose four non-uniform values.
  Evidence: zone values are 10, 30, 50, and 100 percent; delay values are 100, 200, 300, and 500 milliseconds. The Slider therefore represents indices zero through three and a pure mapper converts between indices and persisted values.

- Observation: the existing hotkey editor stores a known catalog token, not arbitrary key text.
  Evidence: `HotkeyKeyCatalog.GlobalHotkeyKeys` contains Space, bracket OEM keys, letters, digits, numpad keys, arithmetic keys, and F1 through F12. Capture must accept only this catalog and preserve `HotkeyBinding` exactly.

- Observation: the first full suite exposed four valuable compatibility contracts after the focused set passed.
  Evidence: context rows must continue referencing `FormControlHeight`; integration tests require the Icon Converter and Clipboard Manager checkbox `Content` attributes; numeric unit labels must be localized. The four contracts were preserved, their focused rerun passed 4/4, and the subsequent full run passed 730/730.

- Observation: the WPF intermediate output can be locked even when the source edit is valid.
  Evidence: the first focused run failed with access denied for `AiteBar/obj/Release/net10.0-windows/win-x64/App.g.cs`; the same command outside the sandbox completed with 24/24 tests passing.

## Decision Log

- Decision: retain 900 by 560 as the initial size but allow native resizing down to 800 by 480; keep the sidebar and footer fixed within the window and the right content area scrollable.
  Rationale: the original fixed-size decision was appropriate for the first modernization phase, but the user subsequently requested resize flexibility. The minimum preserves the four selection cards and navigation without squeezing the content into an unusable layout.
  Date/Author: 2026-07-17 / Codex

- Decision: use a styled WPF `CheckBox` for every staged binary setting, even though it looks like a switch.
  Rationale: true Fluent switches imply immediate application. The user explicitly prioritized regression prevention, so visual modernization must not silently change the Save/Cancel contract. Automation continues to expose a checkable control.
  Date/Author: 2026-07-15 / Codex

- Decision: replace five language radio buttons with one `ComboBox` and preserve the existing immediate localization handler through `SelectionChanged` guarded by `_isLoadingSettings`.
  Rationale: language is a secondary compact choice and a ComboBox matches the requested design.
  Date/Author: 2026-07-15 / Codex

- Decision: keep panel edge and size as mutually exclusive RadioButtons but restyle them as visual selection cards.
  Rationale: RadioButton semantics give correct accessibility and arrow-key behavior while the template provides the modern card appearance.
  Date/Author: 2026-07-15 / Codex

- Decision: implement zone and delay as index-based sliders with visible localized units and a pure mapper.
  Rationale: a standard numeric Slider cannot snap to non-uniform persisted values. Index mapping keeps all four legacy values exact and testable.
  Date/Author: 2026-07-15 / Codex

- Decision: keep index mapping for all three discrete sliders while replacing only their local visual template and pointer behavior.
  Rationale: a six-unit track, four visible stops, a larger thumb, and interaction states improve appearance and usability without changing persisted values or Save/Cancel semantics.
  Date/Author: 2026-07-17 / Codex

- Decision: supply each navigation glyph through `ListBoxItem.Tag` and render it in the shared item template with the bundled Fluent icon font.
  Rationale: localized text remains the item's accessible `Content`, while one template controls alignment and selected, hover, and keyboard-focus colors for every icon without code-behind or a new dependency.
  Date/Author: 2026-07-17 / Codex

- Decision: use 1000 by 680 initially, 960 by 600 minimum, a 220-unit navigation column, a 20-unit column gap, a right content column capped at 700 units, a 14-unit right content inset, and 32-unit major-section gaps.
  Rationale: the user's runtime screenshot demonstrates that the previous 900-by-560/180-unit layout truncates Russian navigation labels and leaves cards visually cramped. These dimensions still fit a 1024-by-768 work area at minimum while giving localized content predictable room.
  Date/Author: 2026-07-17 / Codex

- Decision: supersede the 2026-07-17 width values with a 1200-DIP initial width, 255-DIP navigation, 24-DIP gap, and 1000-DIP right-content cap while retaining the 960-by-600 minimum; navigation rows are 36 DIP and the navigation panel adds no padding beyond the root's 16 DIP.
  Rationale: the user's measured screenshot showed that 280 DIP and the compounded 16+10 DIP left inset were oversized. The corrected values match the requested Windows 11 proportions while `MaxWidth="1000"` keeps maximized content readable and centered instead of stretching across the full monitor.
  Date/Author: 2026-07-18 / Codex

- Decision: give Quick Tools settings-specific localized title/description pairs and sort the existing WPF rows at runtime with `LocalizationService.ResolvedCulture`.
  Rationale: descriptions should explain the action instead of repeating the title, and a fixed XAML order cannot remain alphabetical after switching among English, German, Ukrainian, and Russian. Reordering the existing named rows preserves every checkbox binding and Save/Cancel behavior.
  Date/Author: 2026-07-18 / Codex

- Decision: render `HotkeyCapture_RowHint` once above the hotkey card instead of inside all eleven rows.
  Rationale: the instruction applies to the whole editor and its repetition added visual noise without adding row-specific information.
  Date/Author: 2026-07-18 / Codex

- Decision: render slider progress independently from the WPF Track repeat buttons and align background, progress, and ticks to the centers of a 26-unit thumb.
  Rationale: the native decrease-repeat region ends at the thumb edge, which created the visible gap in the screenshot. A ProgressBar-backed active layer reaches the thumb center at every discrete value, and matching 13-unit insets prevent the thumb halo from being clipped at either endpoint.
  Date/Author: 2026-07-17 / Codex

- Decision: place `ClipboardManagerPersistHistory` with the General behavior switches rather than directly after the Clipboard Manager visibility switch.
  Rationale: utility visibility answers whether the panel button is shown, while persistence controls an application-wide privacy/storage policy. Grouping it with other global behavior switches makes the distinction clearer and avoids implying that persistence is a subordinate visual option enabled by the utility toggle.
  Date/Author: 2026-07-17 / Codex

- Decision: use borderless filled surfaces for structural containers and reserve outlines for interactive input chrome and keyboard focus.
  Rationale: the user's Windows Settings reference uses background tone, spacing, corner radius, and row dividers to communicate grouping. Decorative borders around the sidebar and every card create visual noise and an outdated nested-box appearance.
  Date/Author: 2026-07-17 / Codex

- Decision: show a bundled Fluent Dismiss icon only when a hotkey binding is assigned.
  Rationale: an always-visible text multiplication sign was visually inconsistent, clipped under some font metrics, and offered a meaningless clear action for `Not assigned`. A fixed icon button provides deterministic geometry and clear hover/pressed feedback.
  Date/Author: 2026-07-17 / Codex

- Decision: add missing hotkeys for Icon Converter and Clipboard Manager, not for every quick-action button.
  Rationale: these are the only built-in utility windows missing from the existing utility-hotkey set. Search, Explorer, Show Desktop, and similar quick actions are system commands rather than AiteBar utility windows and remain outside this settings group.
  Date/Author: 2026-07-17 / Codex

- Decision: add a reusable `HotkeyCaptureBox` that captures modifiers plus one catalog key, clears on Delete/Backspace, and exposes a cloned `HotkeyBinding`.
  Rationale: one chord field is substantially clearer than five separate controls. Catalog restriction, validation on Save, focus cues, and pure key normalization prevent behavioral regressions.
  Date/Author: 2026-07-15 / Codex

## Outcomes & Retrospective

The original control-modernization implementation and its subsequent UX follow-ups are complete. Program Settings opens at 1200 by 680, can be resized to a minimum of 960 by 600, keeps a 255-unit navigation column with a 24-unit gap, starts navigation surfaces at the root's 16-unit left margin, uses 36-unit navigation rows, and caps the centered right content column at 1000 units. Structural containers use borderless filled surfaces and dividers like Windows Settings; input chrome and keyboard focus cues remain. Clipboard history persistence sits with the General behavior switches. Quick Tools uses dedicated action-oriented titles and descriptions and remains alphabetic after runtime language changes. The hotkey capture instruction appears once above compact command rows. Slider progress, ticks, and the 26-unit thumb share endpoint geometry. Hotkey clear actions use a fixed Fluent Dismiss icon, appear only for assigned bindings, and include deterministic hover/pressed states. All seven built-in utility windows now expose persisted global hotkeys, including Icon Converter and Clipboard Manager. The saved model, immediate language behavior, and staged Save/Cancel boundary remain intact. The latest Release build completed with zero warnings and errors, and the complete suite passed 783/783. Earlier installer sizes and hashes below remain historical evidence for their respective milestones.

The pure helper set passed 22/22 before the old editors were removed; the expanded focused settings/hotkey/localization set passed 92/92 after replacement. Runtime UI Automation verified all four sections, a 900-by-560 window, the new controls, `Ctrl + Shift + F12` capture, Delete-to-clear, and Tab escape from the field. The first full suite exposed four compatibility contracts, all were preserved, and the final suite passed 730/730 with a zero-warning Release build. The rebuilt installer is 77,491,666 bytes with SHA-256 `37AAFAB9907529BD842F63BD93CA80B845EBC007E3A90A4BB4305419B7CC07C5`. Its signing step was skipped because no certificate was supplied, matching existing release behavior. The published process remained alive for seven seconds and did not change `error.log`.

## Context and Orientation

`AiteBar/AppSettingsWindow.xaml` contains the four anchored sections and all statically declared controls. `AiteBar/AppSettingsWindow.xaml.cs` loads settings, rebuilds localized lists, dynamically creates context rows, validates hotkeys, and writes settings on Save. `AiteBar/SettingsWindowResources.xaml` contains shared form resources used by other windows and must not be globally changed for this screen-specific redesign. New styles should therefore live in the local resource dictionary of `AppSettingsWindow.xaml` unless they are reusable code-only controls.

The General section stores UI culture, panel edge, monitor choice, panel size percentage, activation zone percentage, activation delay, taskbar indicator visibility, secondary-monitor behavior, and update checks. Contexts consist of eight name TextBoxes and enabled states; context zero is always enabled. Hotkeys consist of nine `HotkeyBinding` values. Quick Tools maps sixteen CheckBoxes through `UtilityButtonCatalog`, plus clipboard history persistence. Save and Cancel remain outside the scrolling region.

A “selection card” is a RadioButton whose visual template is a bordered rectangular preview rather than a circular radio glyph. A “switch row” is a Grid with title and descriptor at the left and a switch-styled CheckBox at the right. A “capture field” is a focusable control that displays a chord such as `Ctrl + Alt + P`, then replaces that value when the user focuses it and presses another supported chord.

## Plan of Work

First add `AppSettingsDiscreteChoiceHelper` with exact value arrays and conversion methods from persisted value to nearest valid index and from index to a valid value. Add tests for every valid choice, invalid indices, and nearest-value behavior. Add `HotkeyCaptureHelper` for catalog key normalization and display text, with tests for letters, digits, OEM keys, numpad keys, missing keys, and modifier ordering. These helpers must have no dependency on a live WPF window.

Add `HotkeyCaptureBox` as a small WPF control. It owns a `Binding` dependency property or explicit get/set methods that clone `HotkeyBinding`; it must never mutate the model object passed into it. On focus it displays an instructional focus state. On a supported non-modifier key it captures the current Ctrl, Shift, Alt, and Win modifiers and the normalized catalog token. Delete or Backspace clears to `None`. Unsupported keys leave the previous value intact. Its template uses the same 36-unit form height, #2D2D2D field background, four-unit corner radius, accent focus border, a chord label, and a small clear affordance.

In `AppSettingsWindow.xaml`, keep local brushes and styles for section headers, setting cards, grouped list cards, row separators, selection-card RadioButtons, switch CheckBoxes, value chips, and sliders. Use the established AiteBar accent and dark palette with subtle borders; avoid excessive gradients, glow, or animation. The window opens at 900 by 560 and supports native resizing no smaller than 800 by 480; scrolling remains available for short window heights.

Rebuild General as a section header followed by separate cards. Language is one title/description row with `CmbLanguage`. Edge and panel size each use four visual RadioButton cards. Zone and delay use index sliders, endpoint labels, and a value chip updated by `ValueChanged`. The three booleans share one grouped card with dividers and switch-styled existing CheckBoxes. Existing names for edge, size, and booleans remain so their current logic is minimally disturbed.

Rebuild Contexts as a section header plus one grouped list card. `BuildContextRows` creates a taller row with colored badge, a title/name field region, and a switch-styled CheckBox. Context zero presents a disabled ON switch and its always-enabled descriptor. Preserve `_contextRows`, draft capture, localization rebuilding, name normalization, and context colors.

Rebuild Hotkeys as a section header plus one grouped list card. Replace the 36 modifier ToggleButtons and nine ComboBoxes with nine named `HotkeyCaptureBox` controls. Adapt `LoadSettings`, localization refresh, and `BtnSave_Click` to read and write those controls. Keep `ValidateHotkeyBindings` unchanged so missing modifiers, duplicate chords, reserved combinations, and registration failures retain their current behavior.

Rebuild Quick Tools as a section header and grouped switch rows. Keep every existing CheckBox name because `GetUtilityVisibilityBindings` is already the centralized mapping. Put clipboard history persistence in a separate privacy card with its current warning descriptor. No visibility setting may be added, removed, reordered in the service mapping, or changed to immediate application.

Extend `AppSettingsLayoutContractTests` for the new control inventory and structure: one language ComboBox, three discrete Sliders, eight visual choice cards, nine hotkey capture controls, twenty switch-styled static CheckBoxes, direct section anchors, one ScrollViewer, a fixed footer, resize attributes, and the slider template's step and interaction visuals. Source contracts must prove the old hotkey split controls and key-list population are gone only after the replacement tests pass.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`. Run focused tests after each milestone:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~AppSettings|FullyQualifiedName~HotkeyCapture|FullyQualifiedName~HotkeyValidation|FullyQualifiedName~RuntimeLocalizationWindowSourceTests"

After all XAML and code-behind work, run sequentially:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build
    .\installer\Build-Installer.ps1

If WPF generated files are inaccessible inside the workspace sandbox, rerun the same command outside it with approval. Do not delete unrelated `obj` files or stop unrelated processes.

## Validation and Acceptance

Open Program Settings and verify each of the four sidebar sections. General must show a working language ComboBox, four edge cards, four size cards, two discrete sliders whose chips show only legacy values, and three switch rows. Changing language must immediately relocalize all headings and preserve drafts. Cancel must restore the original language and discard every other staged change. Save must persist all controls and reopen with the same values.

Contexts must display all eight badges, names, and switches without clipping; panel one remains enabled and cannot be disabled. Hotkey capture must accept every catalog family, display modifiers in a stable order, clear on Delete, reject unsupported keys, preserve duplicate/missing-modifier validation, and save/reopen correctly. Quick Tools must contain all sixteen existing visibility rows plus a separate clipboard privacy row.

At runtime, the sidebar must remain stationary, the right side must be the only scrolling content surface, and Save/Cancel must remain visible. The window must open at 900 by 560, resize smoothly in both directions, stop shrinking at 800 by 480, and expose the native resize cursor at its border. The final published process must remain alive for at least six seconds without changing `%APPDATA%\Codebdbd\Aite Bar\error.log`.

## Idempotence and Recovery

All edits are source-only and safe to repeat. Keep the current dirty worktree and never use `git reset`, `git checkout --`, or broad file replacement. Apply the redesign one section at a time. If a replacement fails focused tests, retain or restore only that section's old controls with `apply_patch`; do not weaken inventory tests. Stop only an AiteBar process whose executable path belongs to this workspace before publish or installer operations.

## Artifacts and Notes

The modern content hierarchy is:

    section header and descriptor
      complex choice card
        title and descriptor
        appropriate control
      related settings list card
        setting row / divider / setting row

The target is inspired by the supplied composition screenshot, not pixel-for-pixel duplication. AiteBar's existing dark palette, 36-unit inputs, 44-unit footer buttons, four-unit input radius, and accessibility behavior remain authoritative.

## Interfaces and Dependencies

No NuGet dependency is added. Add internal pure helpers `AppSettingsDiscreteChoiceHelper` and `HotkeyCaptureHelper`. Add a WPF `HotkeyCaptureBox` in namespace `AiteBar`. It must expose a safe way to assign and retrieve a `HotkeyBinding`, update its display when localization changes, and capture `PreviewKeyDown` without allowing unsupported catalog tokens.

Stable new XAML names are `CmbLanguage`, `SliderActivationZone`, `TxtActivationZoneValue`, `SliderActivationDelay`, `TxtActivationDelayValue`, and nine `HotkeyCaptureBox` names corresponding to the existing nine hotkey commands. Existing edge, size, boolean, context-host, section-anchor, navigation, and footer names remain stable.

Plan revision note (2026-07-15 12:20Z): created after the user clarified that the reference image defines the inner card/control design as well as the page composition. This plan preserves the validated sidebar implementation but supersedes the earlier decision to keep legacy inner controls unchanged.

Plan revision note (2026-07-15 13:18Z): recorded completed implementation, focused and full test evidence, runtime hotkey/UI evidence, compatibility fixes, installer metadata, and the clean published startup smoke.

Plan revision note (2026-07-17 00:00Z): incorporated the user's resize and slider-design follow-up, superseding the earlier fixed-window decision while preserving the initial size and existing settings behavior; recorded the implementation and focused 24/24 test evidence.

Plan revision note (2026-07-17 00:00Z): recorded final zero-warning Release build and complete 771/771 test evidence for the follow-up.

Plan revision note (2026-07-17 00:00Z): added the left-navigation icon follow-up, its accessibility-preserving template decision, and its pending validation step.

Plan revision note (2026-07-17 00:00Z): recorded completion of the navigation-icon follow-up with focused 25/25 tests, a zero-warning Release build, and complete 772/772 test evidence.

Plan revision note (2026-07-17 04:47 local): recorded the final publish/installer rebuild, artifact size, signing status, and matching SHA-256 evidence.

Plan revision note (2026-07-17 00:00Z): incorporated the screenshot-driven geometry correction, documented the root causes and layout decisions, and added its pending validation milestone.

Plan revision note (2026-07-17 05:00 local): recorded completion of the geometry correction, the explicit 700-unit content cap, focused/full/build validation, and the final installer metadata.

Plan revision note (2026-07-17 05:51 local): recorded completion of the borderless Windows-style surfaces, Clipboard persistence relocation, 773/773 validation, and final installer metadata.

Plan revision note (2026-07-17 06:18 local): recorded completion of the Fluent clear action and complete utility-window hotkey coverage, documentation updates, 774/774 validation, and final installer metadata.
