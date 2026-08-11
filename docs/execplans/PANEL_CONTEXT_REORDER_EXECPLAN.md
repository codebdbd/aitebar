# Reorder panel contexts in settings

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document follows `PLANS.md` in the repository root. It is self-contained so a contributor can continue the work from this file and the current working tree alone.

## Purpose / Big Picture

AiteBar has ten panel contexts. A context is a stable panel identity such as `context-3`, and custom buttons point to that identity through `CustomElement.ContextId`. Users can rename contexts in `AppSettingsWindow`, and now they should also be able to change their sequence in the same list. After this change, users can drag panel rows up or down in the panel names settings section. Saving settings persists the new `settings.Contexts` order. Buttons remain attached to their original context ids, so moving a panel changes where it appears in the sequence without losing its buttons.

The user-visible behavior is: open program settings, go to panel names, drag a row to another position, save, and observe that panel menus, next/previous switching, direct context hotkeys, and the active panel indicator follow the new row order.

## Progress

- [x] (2026-08-11 00:00Z) Read `ContextStateHelper` and found `NormalizeContexts` currently rebuilds contexts by fixed id order.
- [x] (2026-08-11 00:00Z) Found `AppSettingsWindow.BuildContextRows` dynamically builds the panel name rows.
- [x] (2026-08-11 00:00Z) Found direct context hotkeys currently resolve `Alt+N` to `context-N` instead of the Nth ordered context.
- [x] (2026-08-11 00:00Z) Make `NormalizeContexts` preserve existing valid context order while still filling missing default contexts.
- [x] (2026-08-11 00:00Z) Add drag-and-drop row reorder in `AppSettingsWindow`.
- [x] (2026-08-11 00:00Z) Replace the drag handle glyph with Material `drag_indicator` so the handle renders as a reorder affordance.
- [x] (2026-08-11 00:00Z) Save context rows back in the displayed order without changing button `ContextId` values.
- [x] (2026-08-11 00:00Z) Update direct context hotkeys and active panel indicator to use display/order index.
- [x] (2026-08-11 00:00Z) Add focused tests for reorder behavior and direct-index lookup.
- [x] (2026-08-11 00:00Z) Run Release build and tests, recording unrelated failures.

## Surprises & Discoveries

- Observation: `ContextStateHelper.GetContextNumber(context.Id)` is used both as identity-derived number and as a display number.
  Evidence: `AppSettingsWindow` badge text and `MainWindow` active indicator call `GetContextNumber` directly.
- Observation: Reordering cannot work if normalization always loops `context-0` through `context-9`.
  Evidence: `NormalizeContexts` currently creates default ids in numeric order and ignores the input list order.

## Decision Log

- Decision: Treat `settings.Contexts` list order as the display and hotkey order.
  Rationale: This keeps `ContextId` stable for button ownership while allowing users to move panels.
  Date/Author: 2026-08-11 / Codex.
- Decision: Keep the first row always enabled rather than always forcing `context-0` enabled by identity after reorder.
  Rationale: The UI contract says the first panel is always enabled. After reorder, "first" is the visible first row.
  Date/Author: 2026-08-11 / Codex.
- Decision: Implement drag-and-drop with a glyph handle and row-level drop handling, not up/down buttons.
  Rationale: The user asked for a guru implementation in the existing panel names section; direct manipulation is clearer for ten rows.
  Date/Author: 2026-08-11 / Codex.
- Decision: Use the Material Icons `drag_indicator` glyph for the row handle.
  Rationale: The previous Fluent codepoint rendered as a heart-like glyph in the shipped font, which made the affordance confusing.
  Date/Author: 2026-08-11 / Codex.

## Outcomes & Retrospective

Panel context reorder is implemented in the settings panel-name list. A Material `drag_indicator` handle on each row starts a row drag; dropping on another row rebuilds the list in the new order. Saving persists `settings.Contexts` in that order while leaving `settings.Elements` untouched, so buttons stay attached to the same stable `ContextId`. The active panel indicator and direct numbered hotkeys now use list position.

Validation succeeded for Release build, `ContextStateHelperTests`, and the focused `ContextReorder` tests. The full test suite still has the known unrelated failure in `PromptBuilderIntegrationTests.Generator_ShowsAutomaticOptionBeforeModelsFinishLoading`.

Release follow-up (2026-08-11): the platform-dependent `LF`/`CRLF` assertion in that unrelated test was normalized. The final 1.15.10 validation passes all 1,322 tests, and the installer reports ProductVersion 1.15.10.

## Context and Orientation

`AiteBar/ContextStateHelper.cs` normalizes context metadata and handles enabled-context navigation. It must preserve list order for reorder to persist. It also resolves enabled context index and direct context access.

`AiteBar/AppSettingsWindow.xaml.cs` dynamically builds panel rows in `BuildContextRows`. The rows already contain a badge, name text box, enabled switch, and clear button. This work adds a drag handle and keeps an ordered in-memory row list.

`AiteBar/MainWindow.xaml.cs` uses context order for next/previous switching, but direct context hotkeys currently map the number to `context-N`. This must change to select the Nth context in `settings.Contexts`.

## Plan of Work

First, change `ContextStateHelper.NormalizeContexts` so it starts with valid unique contexts in input order, appends any missing default context ids, and then normalizes names and colors by current list index. This preserves reorder while keeping exactly ten contexts. Add a helper `GetContextDisplayNumber(contexts, contextId)` that returns the current list index for UI labels.

Second, update `AppSettingsWindow` row state to include the row's current index, drag handle button, and context id. The row grid gets a handle column before the badge. The handle uses a compact glyph, allows drag start on mouse move, and each row accepts drop. Dropping one context row on another moves the source row before the target row and rebuilds the rows while preserving typed names and enabled states.

Third, update save logic to assign `settings.Contexts` from `_contextRows` order. For each row, preserve the same `ContextId`, icon glyph, and context metadata, compute the default/custom name using the row index, and force row index `0` enabled.

Fourth, update direct context hotkeys and active indicator labels to use ordered contexts, not `ContextId` numbers.

Fifth, add tests in `ContextStateHelperTests` and `AppSettingsServiceTests` to prove reorder persists, buttons keep their context ids, and direct lookup by ordered index returns the moved context.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Edit:

    AiteBar/ContextStateHelper.cs
    AiteBar/AppSettingsWindow.xaml.cs
    AiteBar/MainWindow.xaml.cs
    AiteBar/SettingsWindow.xaml.cs
    AiteBar.Tests/ContextStateHelperTests.cs
    AiteBar.Tests/AppSettingsServiceTests.cs

Validate:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter ContextReorder
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

## Validation and Acceptance

The feature is accepted when dragging a panel row in settings changes the persisted order after Save; custom buttons remain in their original contexts; the first displayed row is always enabled; direct hotkeys choose the Nth displayed panel; and the active indicator shows the displayed index, not the old id suffix.

If the full suite fails because of the existing `PromptBuilderIntegrationTests.Generator_ShowsAutomaticOptionBeforeModelsFinishLoading` exact substring check, record it as unrelated.

## Idempotence and Recovery

Reordering is only committed by Save. Closing settings with Cancel keeps the old order. The operation does not alter `settings.Elements`, so button ownership survives repeated reorder operations.

## Artifacts and Notes

Implementation notes:

    `ContextStateHelper.NormalizeContexts` preserves user order only when the source contains all 10 valid contexts. Partial legacy settings still normalize in fixed `context-0..9` order for compatibility.
    `AppSettingsWindow` uses the Material `drag_indicator` glyph in each context row and saves contexts through `BuildReorderedContexts`.
    `MainWindow` direct context activation now resolves the Nth displayed context instead of constructing `context-N`.
    `SettingsWindow` context dropdown labels use the displayed row index.

Validation transcripts:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter ContextStateHelperTests
    Пройдено: 22, не пройдено: 0, всего: 22

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter ContextReorder
    Пройдено: 4, не пройдено: 0, всего: 4

    dotnet build .\AiteBar.sln -c Release
    Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Не пройдено: 1, пройдено: 1316, всего: 1317
    Failing test: AiteBar.Tests.PromptBuilderIntegrationTests.Generator_ShowsAutomaticOptionBeforeModelsFinishLoading

## Interfaces and Dependencies

`ContextStateHelper` should expose:

    public static PanelContext? GetContextAt(IReadOnlyList<PanelContext> contexts, int index)
    public static int GetContextDisplayNumber(IReadOnlyList<PanelContext> contexts, string? contextId)

`AppSettingsWindow` should expose an internal static helper for tests:

    internal static List<PanelContext> BuildReorderedContexts(AppSettings settings, IReadOnlyList<AppSettingsContextRowState> rows)

Revision note, 2026-08-11 / Codex: Initial plan created before implementing context reorder to preserve stable context ids while making list order user-controlled.

Revision note, 2026-08-11 / Codex: Updated implementation notes after replacing the incorrect Fluent drag glyph with Material `drag_indicator`.
