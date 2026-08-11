# Add per-panel context clearing from settings

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document follows `PLANS.md` in the repository root. It is self-contained so a contributor can continue the work from this file and the current working tree alone.

## Purpose / Big Picture

AiteBar users can organize buttons into multiple panel contexts. A panel context is one named panel slot, such as "Panel 0" or a user-renamed "Work" panel, and every custom button stores the `ContextId` of the panel it belongs to. Users currently can rename and enable panels in the application settings, but they cannot clear the buttons from one panel in a direct, controlled way.

After this change, the context name list in `AppSettingsWindow` includes a compact glyph button using `U+F34D` on each context row. Clicking it asks for confirmation, immediately removes custom buttons whose `ContextId` matches that row, saves settings, refreshes the panel, and leaves all other contexts, context names, context colors, context enabled states, hotkeys, and built-in quick tool settings intact.

## Progress

- [x] (2026-08-11 00:00Z) Located context row construction and save flow in `AiteBar/AppSettingsWindow.xaml.cs`.
- [x] (2026-08-11 00:00Z) Confirmed confirmation dialogs use `new DarkDialog(message, isConfirm: true)`.
- [x] (2026-08-11 00:00Z) Add a row-level clear glyph button to the dynamically built context rows.
- [x] (2026-08-11 00:00Z) Initially tracked confirmed pending context clears until Save.
- [x] (2026-08-11 00:00Z) Change clearing to apply immediately after confirmation, because the pending behavior looked like a broken button.
- [x] (2026-08-11 00:00Z) Add localized tooltip/confirmation/empty-state text.
- [x] (2026-08-11 00:00Z) Replace the generic command-button style with a compact icon-only style for the row clear button.
- [x] (2026-08-11 00:00Z) Add focused tests for clearing only a selected context and preserving other contexts.
- [x] (2026-08-11 00:00Z) Run Release build and tests, recording any unrelated failures.

## Surprises & Discoveries

- Observation: `AppSettingsWindow` dynamically builds the context rows in code rather than XAML.
  Evidence: `BuildContextRows` creates a `Grid`, badge, `TextBox`, and `CheckBox` for each `PanelContext`.
- Observation: The current row state tuple only stores the enabled checkbox, name textbox, and badge border.
  Evidence: `_contextRows` is declared as `List<(CheckBox EnabledCheckBox, TextBox NameTextBox, Border BadgeBorder)>`.
- Observation: Pending clear was a bad fit for the requested glyph button.
  Evidence: The button only disabled itself and updated tooltip state until Save, so the visible panel did not change after confirming.

## Decision Log

- Decision: Make clear a pending settings action that applies on Save instead of immediately mutating settings on click.
  Rationale: The context naming UI is a settings form with Save and Cancel. A pending action respects the existing form contract: Cancel closes without committing changes.
  Date/Author: 2026-08-11 / Codex.
- Decision: Revise clear to apply immediately after the confirmation dialog.
  Rationale: The user expectation for a destructive row button is that confirmation is the commit point. Deferring to Save made the control appear inert.
  Date/Author: 2026-08-11 / Codex.
- Decision: Place one glyph button on each context row rather than one global button for the active panel.
  Rationale: The user asked to put the action where panel names are edited. A row-level button makes the target context visible and avoids ambiguity.
  Date/Author: 2026-08-11 / Codex.

## Outcomes & Retrospective

Panel context clearing is implemented as an immediate confirmed action. Each context row now has a compact `U+F34D` glyph button. Confirming the action removes only custom buttons whose `ContextId` matches the confirmed row, saves settings, refreshes the main panel, and updates the row's clear-button state. Context names, enabled states, colors, and other context metadata are preserved.

Validation succeeded for the Release build and the focused panel-context tests. The full test suite still has one unrelated failure in `PromptBuilderIntegrationTests.Generator_ShowsAutomaticOptionBeforeModelsFinishLoading`, which checks for an exact source substring unrelated to panel clearing.

Release follow-up (2026-08-11): final testing exposed that `UpdateSettings` deliberately preserves the service-owned element collection, so the first implementation cleared only the settings-window copy. Clearing now uses `AppSettingsService.RemoveElementsForContexts`, persists the actual service state, restores it on save failure, and is covered by a regression test. The final 1.15.10 validation passes all 1,322 tests.

## Context and Orientation

`AiteBar/AppSettingsWindow.xaml.cs` owns the main settings window. `BuildContextRows` creates the "panel names" section from `PanelContext` values. Each row currently has a small numeric badge, a text box for the name, and a switch that enables or disables the panel. `BtnSave_Click` copies these row values back into `settings.Contexts` inside `UpdateSettings`.

Custom buttons live in `settings.Elements`. Each `CustomElement` has a `ContextId`. Clearing one panel means replacing `settings.Elements` with all elements except those whose `ContextId` matches the chosen row. This must not alter `settings.Contexts`.

## Plan of Work

First, replace the `_contextRows` tuple with a small record that stores the row context id, enabled checkbox, name textbox, badge border, and clear button.

Second, update `BuildContextRows` so each row has an extra auto-width column containing a compact glyph button. The button content is the requested glyph `\uF34D`, uses `FontHelper.FluentKey`, has a tooltip from localization, and stores the context id in `Tag`. It is disabled if that context has no custom buttons. Clicking the button opens a confirm `DarkDialog`. If confirmed, the settings service removes elements for that context, saves settings, refreshes the panel, and row tooltips/state are refreshed.

Third, keep `BtnSave_Click` responsible for non-destructive form state such as panel names/order/enabled flags. Add a small internal helper method to make clear filtering testable without constructing WPF windows.

Fourth, add localized strings in `AiteBar/Resources/Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx` for the tooltip, confirmation, pending state, and empty state.

Fifth, add focused tests that call the helper and prove only the chosen context is removed and context metadata remains intact.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Edit:

    AiteBar/AppSettingsWindow.xaml.cs
    AiteBar/Resources/Strings.resx
    AiteBar/Resources/Strings.ru.resx
    AiteBar/Resources/Strings.uk.resx
    AiteBar/Resources/Strings.de.resx
    AiteBar.Tests/AppSettingsServiceTests.cs or a focused AppSettingsWindow test file

Validate:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter PanelContextClear
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

## Validation and Acceptance

The feature is accepted when a user can open settings, go to the panel names section, click the `U+F34D` glyph button for one context, confirm, and immediately observe that only that context's user buttons are gone. Other contexts' buttons remain. The target context's name, enabled state, color, and glyph remain unchanged.

The focused test should pass and prove that filtering by `ContextId` preserves unrelated elements. The Release build should pass. If the full suite fails because of the existing `PromptBuilderIntegrationTests.Generator_ShowsAutomaticOptionBeforeModelsFinishLoading` exact substring check, record it as unrelated.

## Idempotence and Recovery

The UI action is confirmed before it mutates settings. After confirmation it saves through the existing settings path and refreshes the panel. Re-clicking a cleared row does nothing because the clear button becomes disabled once the context is empty.

## Artifacts and Notes

Implementation notes:

    `BuildContextRows` now creates a clear-button column with a compact glyph button whose content is `\uF34D`.
    Clicking the button confirms, clears the context through `UpdateSettings`, saves, refreshes the panel, and refreshes row tooltips.
    `BtnSave_Click` no longer owns the destructive clear action.
    `ClearElementsForContexts` removes only matching `settings.Elements` and returns the removed count for tests.

Validation transcripts:

    dotnet build .\AiteBar.sln -c Release
    Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter PanelContextClear
    Пройдено: 2, не пройдено: 0, всего: 2

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Не пройдено: 1, пройдено: 1316, всего: 1317
    Failing test: AiteBar.Tests.PromptBuilderIntegrationTests.Generator_ShowsAutomaticOptionBeforeModelsFinishLoading
    Failure reason: Assert.Contains could not find "CmbModels.ItemsSource = _models;\n        " in the source string.

## Interfaces and Dependencies

`AppSettingsWindow` should expose an internal static helper with this shape:

    internal static int ClearElementsForContexts(AppSettings settings, IReadOnlySet<string> contextIds)

It returns the number of removed elements and mutates only `settings.Elements`.

Revision note, 2026-08-11 / Codex: Initial plan created before implementation to keep the panel clearing behavior scoped, testable, and consistent with the settings Save/Cancel flow.

Revision note, 2026-08-11 / Codex: Updated progress and implementation notes after adding UI, pending clear state, localized strings, and focused tests.

Revision note, 2026-08-11 / Codex: Added validation results and retrospective after Release build, focused tests, and full test run.

Revision note, 2026-08-11 / Codex: Changed clear from pending-until-Save to immediate confirmed save/refresh, and added a compact icon-only button style.
