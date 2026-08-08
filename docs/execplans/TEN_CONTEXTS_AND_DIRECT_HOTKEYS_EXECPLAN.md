# Add ten contexts and direct context hotkeys

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. It follows `PLANS.md` in the repository root.

## Purpose / Big Picture

AiteBar will expose ten numbered panels, `0` through `9`. Existing user content in panels `1` through `8` must remain in those same panels after an update; panels `0` and `9` are added without moving buttons. Each panel receives a bright, distinct badge color. Pressing `Alt+0` through `Alt+9` selects the matching enabled panel and reveals AiteBar, so the common operation is a two-key shortcut.

## Progress

- [x] (2026-08-08 08:45Z) Inspected current context normalization, menu rendering, and global hotkey registration.
- [ ] Add stable context-number helpers and migrate the fixed context set from `1..8` to `0..9`.
- [ ] Replace the context palette and update settings/indicator numbering to use context numbers rather than list positions.
- [ ] Register and execute fixed `Alt+0..9` context activation commands; migrate the conflicting old default show shortcut.
- [ ] Update focused tests, build, run tests, and manually exercise top, bottom, left, and right panel positions.

## Surprises & Discoveries

- Observation: The current default show-panel shortcut is `Alt+4`.
  Evidence: `AiteBar/Models.cs` initializes `GlobalHotkeyAlt = true` and `GlobalHotkeyKey = "D4"`.
- Observation: Existing panel identities are `context-1` through `context-8`, while the visual indicator is currently derived from the enabled-list index.
  Evidence: `ContextStateHelper.FixedContextCount` is `8`; `MainWindow.UpdateContextIndicator` sets `activeIndex + 1`.

## Decision Log

- Decision: Preserve `context-1` through `context-8` and add `context-0` at the beginning and `context-9` at the end.
  Rationale: Existing buttons store their context identifier. Keeping identifiers stable prevents an update from silently moving user buttons to a different panel.
  Date/Author: 2026-08-08 / Codex
- Decision: Use fixed global `Alt+0..9` bindings for direct panel selection.
  Rationale: The feature is intended for frequent use; Ctrl-number controls browser and IDE tabs, while Win-number controls the taskbar. Alt-number is two keys and maps naturally to panel labels.
  Date/Author: 2026-08-08 / Codex
- Decision: Remove the standalone show-panel hotkey command.
  Rationale: `Alt+0..9` already shows the panel while selecting its target. Keeping a separately configurable command duplicates the same result and creates avoidable hotkey conflicts.
  Date/Author: 2026-08-08 / Codex

## Outcomes & Retrospective

Work in progress. This section will record the final migration behavior, validation evidence, and remaining limitations.

## Context and Orientation

`AiteBar/ContextStateHelper.cs` normalizes the fixed list of panel records. A panel record has a stable `Id`, display name, enabled state, icon, and color. `AiteBar/AppSettingsService.cs` runs normalization when settings are loaded and saved. `AiteBar/MainWindow.xaml.cs` displays the active panel, creates the context menu, and refreshes the panel contents.

`AiteBar/HotkeyService.cs` converts `HotkeyBinding` values into Win32 global hotkey registrations. `MainWindow` receives those registrations and executes matching `HotkeyCommand` values. Settings controls in `AiteBar/AppSettingsWindow.xaml` and `AiteBar/AppSettingsWindow.xaml.cs` show the context rows and configurable existing hotkeys.

## Plan of Work

First, change context normalization to create records in numeric order `0..9`. It must recognize legacy `context-1..8` data and preserve names, icons, enabled states, and button assignments. Add a helper that returns a panel number from a context id so UI labels and hotkey actions never infer a number from a filtered enabled-list index.

Next, replace the palette with ten saturated colors that remain readable with white text on the dark UI. Settings badges and the active indicator will consume the helper palette as they already do. Update row logic so panel `0`, not a list index assumption, is treated as the required primary panel only if that is the final behavior chosen by migration tests.

Then, remove the old standalone show-panel binding from the model, settings UI, and `HotkeyService`. Extend `HotkeyService` with ten stable command ids and definitions bound to `Alt+D0` through `Alt+D9`. `MainWindow` will map the command to a context id, activate it if enabled, save the selection, and reveal the panel.

Focused unit tests will assert ten contexts, legacy preservation, palette assignment, direct-binding registration, and command mapping. Existing application settings UI tests will be updated only where their contract explicitly expects eight rows.

## Concrete Steps

Run these commands from `D:\01_Codebdbd\01_projects\aitebar` after the edits:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If the WPF test host blocks or hangs, stop only the test process tree started by the command and run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

Start `AiteBar\bin\Release\net10.0-windows\win-x64\AiteBar.exe`. Enable panels 0 and 9 in settings and confirm `Alt+0` and `Alt+9` select them. Verify that disabled panels are not activated. Drag the panel to Top, Bottom, Left, and Right and repeat a direct shortcut check.

## Validation and Acceptance

The user can see exactly ten settings rows labelled `0` through `9`, with ten distinct color badges. A legacy configuration retains its buttons in panels `1` through `8` and gains empty panels `0` and `9`. `Alt+N` activates enabled panel `N` and opens the panel. No standalone show-panel hotkey appears in settings or is registered. Build succeeds and all tests pass.

## Idempotence and Recovery

Normalization is idempotent: once it creates `context-0..9`, repeated application produces the same ordered records. It never changes a button's stored context id. If build output is locked, close only the AiteBar process started for validation and rebuild. Existing settings remain recoverable because the normalization only adds records and rewrites default palette values.

## Artifacts and Notes

Before this work the direct default show-panel binding was `Alt+4`, which collides with direct selection of panel 4. The implementation must explicitly handle that migration rather than registering two commands for the same chord.

## Interfaces and Dependencies

`ContextStateHelper` will provide a public/static context-number lookup that accepts `context-0..9` and a direct-id lookup for a number. `HotkeyService` will expose commands for the ten direct activations and retain the existing `CreateDefinitions`, `RegisterAll`, and `TryGetCommand` interfaces. No external package is required; global registration continues to use the existing Win32 `RegisterHotKey` interop.

Plan created 2026-08-08 because the request expands a fixed eight-context UI and global hotkey system into a ten-context, migration-sensitive feature. Updated 2026-08-08: removed the standalone show-panel shortcut entirely because direct context selection already performs that action.
