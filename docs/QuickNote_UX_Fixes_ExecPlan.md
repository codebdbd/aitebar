# Quick Note UX Fixes

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan follows `PLANS.md` from the repository root. Keep this document self-contained whenever it is revised: a future contributor should be able to continue the work by reading this file and the current working tree only.

## Purpose / Big Picture

Quick Note is a small always-on-top WPF utility window for a single local Markdown note. The user asked to fix the user-facing issues identified as items 2, 3, 5, 6, 7, and 9 from the prior review, and also to fix Undo and Redo. After this change, a user should be able to keep the note open while switching apps, have its size and location remembered, use formatting that survives save and reload, understand and open conflict copies, open links more naturally, get clear feedback when link highlighting is limited for long notes, and use Ctrl+Z/Ctrl+Y reliably.

## Progress

- [x] (2026-06-11 06:05Z) Reviewed `PLANS.md`, current Quick Note files, settings service, Markdown tests, and service tests.
- [x] (2026-06-11 06:05Z) Created this ExecPlan with the requested scope and validation path.
- [x] (2026-06-11 06:35Z) Added settings fields for Quick Note pinned state and remembered bounds, plus layout clamping helper tests.
- [x] (2026-06-11 06:45Z) Updated `QuickNoteWindow` UI and behavior for pinning, geometry persistence, conflicts, normal link click, long-note feedback, and Undo/Redo buttons/menu items.
- [x] (2026-06-11 06:50Z) Extended Markdown persistence so underline round-trips through `<u>...</u>`.
- [x] (2026-06-11 06:55Z) Added focused unit tests for geometry clamping, underline Markdown round-trip, and conflict-copy opening.
- [x] (2026-06-11 07:05Z) Ran `dotnet build .\AiteBar.sln -c Release --disable-build-servers`; build passed with 0 warnings and 0 errors.
- [x] (2026-06-11 07:06Z) Ran `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --no-restore`; 410 tests passed, 0 failed, 0 skipped.
- [x] (2026-06-11 07:07Z) Attempted safe smoke launch; skipped because an existing user AiteBar process was already running and was not terminated.

## Surprises & Discoveries

- Observation: Quick Note currently closes on WPF `Deactivated`, which means switching to another app hides the note unless a transient menu or popup is open.
  Evidence: `AiteBar/QuickNoteWindow.xaml.cs` has `Window_Deactivated` calling `CloseSlidingAsync()` when `!IsActive`.
- Observation: The window can be resized and dragged, but `AppSettings` only stores `QuickNoteThemeId`, not Quick Note bounds.
  Evidence: `AiteBar/Models.cs` contains `QuickNoteThemeId` and no Quick Note width, height, left, top, or pinned setting.
- Observation: Underline is exposed in the toolbar but current Markdown serialization only handles bold, italic, and code.
  Evidence: `AiteBar/QuickNoteMarkdown.cs` `AppendStyledText` emits `**`, `*`, and backticks only.
- Observation: Standard `dotnet build` initially failed with Access denied in generated `obj` files. Disabling build servers produced real compiler diagnostics and then a clean build after the test import was fixed.
  Evidence: `dotnet build .\AiteBar.sln -c Release --disable-build-servers` completed successfully after adding `using System.Windows;` to `AiteBar.Tests/QuickNoteMarkdownTests.cs`.

## Decision Log

- Decision: Keep Quick Note as a single-note utility and improve the existing window rather than adding multiple notes, search, or a note list.
  Rationale: The user explicitly asked to fix specific UX deficiencies, not to expand the product scope beyond the current quick-capture utility.
  Date/Author: 2026-06-11 / Codex
- Decision: Add an explicit pin toggle controlling focus-loss closing instead of removing auto-close entirely.
  Rationale: Some users expect a quick popover that disappears, while the reported issue is the inability to keep it open while working in other windows. A pin makes both behaviors discoverable and reversible.
  Date/Author: 2026-06-11 / Codex
- Decision: Make link opening work by normal click while preserving editing behavior with selection and caret placement as much as WPF allows.
  Rationale: The reported issue was discoverability. Normal click on an already-highlighted URL is the behavior most users expect; Ctrl+click remains supported.
  Date/Author: 2026-06-11 / Codex
- Decision: Serialize underline as `<u>...</u>` in the Markdown file.
  Rationale: This keeps the file human-readable, is valid Markdown-compatible inline HTML, and avoids inventing a custom marker that would collide with existing `**`, `*`, and backtick parsing.
  Date/Author: 2026-06-11 / Codex
- Decision: Use `--disable-build-servers` for validation in this environment.
  Rationale: The normal build path hit stale MSBuild/compiler-server file locks in `obj`; disabling build servers removed that environmental noise and validated the actual code.
  Date/Author: 2026-06-11 / Codex

## Outcomes & Retrospective

Implemented the requested Quick Note fixes while keeping the utility as a single-note lightweight window. The window now has a pin toggle, remembered bounds, visible Undo/Redo controls, normal-click URL opening, long-note link-highlight status, conflict-copy opening, and underline Markdown round-trip. Automated validation passed: Release build completed with 0 warnings and 0 errors, and the test suite reported 410 passed tests. A safe runtime smoke launch was not performed because an existing user AiteBar process was already running; the process was left untouched.

## Context and Orientation

The repository is a .NET 8 Windows WPF application. The main app project is `AiteBar`, and tests are in `AiteBar.Tests`. Quick Note is registered in `AiteBar/App.xaml.cs` through `UtilityRegistry.Register(new QuickNoteUtility())`. The utility implementation is split across `AiteBar/QuickNoteUtility.cs`, `AiteBar/QuickNoteWindow.xaml`, `AiteBar/QuickNoteWindow.xaml.cs`, `AiteBar/QuickNoteService.cs`, `AiteBar/QuickNoteMarkdown.cs`, `AiteBar/QuickNoteDocumentHelper.cs`, `AiteBar/QuickNoteLayoutHelper.cs`, and `AiteBar/QuickNoteTheme.cs`.

`AppSettings` in `AiteBar/Models.cs` is serialized by `AiteBar/AppSettingsService.cs` into JSON. It already stores `QuickNoteThemeId`, but not window bounds or whether the note should stay open when focus leaves the window. `QuickNoteService` manages the single Markdown file named `QuickNote.md` in the app data directory. `QuickNoteMarkdown` converts between WPF `FlowDocument` content and Markdown text.

## Plan of Work

First, extend `AppSettings` with Quick Note fields for pinned state and remembered bounds. The bounds should be nullable or validated defaults so older settings files load safely. The window should save width, height, left, and top after resize, move, and normal close; it should clamp restored bounds to the selected monitor's working area to avoid opening off-screen.

Second, update `QuickNoteWindow.xaml` and `QuickNoteWindow.xaml.cs`. Add Undo and Redo toolbar buttons and menu items with Ctrl+Z and Ctrl+Y labels, wire them to WPF editing commands, and keep the editor focused after toolbar actions. Add a pin toggle in the header. When pinned, losing focus must not close the note. When unpinned, the current focus-loss close behavior remains. Add clear status text when inline link highlighting is disabled for a long note. Add an "Open conflict copy" path after conflict save, either as status plus a menu item or as a status action, so the user can find the generated file.

Third, update `QuickNoteMarkdown` so formatting exposed by the toolbar survives save and reload. Underline should be serialized in a local, deterministic Markdown-compatible marker and parsed back into underline. The chosen marker must be documented in tests. Existing escaping behavior must continue to protect literal Markdown characters.

Fourth, update tests in `AiteBar.Tests`. Add focused tests for underline round-trip, conflict copy tracking/open path if a new service method is added, URL open normalization if behavior changes, and settings/default helper behavior where possible. Avoid trying to UI-automate WPF interactions in unit tests unless the project already has a stable pattern.

Finally, validate with the required Release build and tests. If `dotnet test` hits the known WPF/MSBuild temporary-file issue, run the documented `dotnet vstest` fallback against the Release test DLL.

## Concrete Steps

Run commands from `D:\01_Codebdbd\01_projects\aitebar`.

Inspect current state:

    git status --short
    rg -n "QuickNote" AiteBar AiteBar.Tests

After implementation, run:

    dotnet build .\AiteBar.sln -c Release --disable-build-servers
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --no-restore

Observed validation transcript:

    dotnet build .\AiteBar.sln -c Release --disable-build-servers
    Сборка успешно завершена.
        Предупреждений: 0
        Ошибок: 0

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --no-restore
    Пройден!   : не пройдено     0, пройдено   410, пропущено     0, всего   410

If the test command fails only because of WPF/MSBuild temporary generated files such as `wpftmp`, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net8.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

The feature is accepted when the code proves the following behavior. Opening Quick Note, pinning it, and switching to another application keeps the note visible; unpinning restores auto-close-on-focus-loss. Moving and resizing Quick Note, closing it, and reopening it restores the user's size and location within the monitor working area. Underline, bold, italic, code, and list formatting that the toolbar exposes persist through save and reload. When an external edit conflict occurs, the generated conflict copy can be identified and opened from the app rather than only appearing as a vague status. Clicking a URL opens it without requiring hidden knowledge of Ctrl+click, while Ctrl+click still works. Long notes that exceed the inline link-highlighting limit show a clear status instead of silently disabling highlighting. Ctrl+Z and Ctrl+Y work in the editor and through visible Undo/Redo actions.

Automated acceptance requires Release build and tests to pass. Manual acceptance is required for the WPF focus, pin, geometry, and link-click interactions because the repository currently has unit tests rather than UI automation for Quick Note.

## Idempotence and Recovery

The edits are additive and safe to repeat. If settings files from older versions omit new fields, C# property defaults must provide safe behavior. If a saved window position is off-screen because monitors changed, restoration must clamp the window into the current working area rather than failing or opening invisibly. If a conflict copy cannot be opened, the app should log the exception and show the existing open-failed status without data loss.

## Artifacts and Notes

Initial evidence from current code:

    AiteBar/QuickNoteWindow.xaml.cs: Window_Deactivated closes the note on focus loss.
    AiteBar/QuickNoteWindow.xaml: Width="580" Height="430" with no settings-backed restore.
    AiteBar/Models.cs: QuickNoteThemeId exists, but no Quick Note bounds or pin fields.
    AiteBar/QuickNoteMarkdown.cs: Markdown serialization handles bold, italic, and code only.

## Interfaces and Dependencies

Use only existing .NET and WPF dependencies. Do not add external packages. New settings should be properties on `AppSettings` in `AiteBar/Models.cs`. If a helper is needed for bounds clamping, prefer adding it to `AiteBar/QuickNoteLayoutHelper.cs` with unit tests in `AiteBar.Tests/QuickNoteLayoutHelperTests.cs`. If conflict opening needs a new method, add it to `QuickNoteService` and keep process launch behind the existing `IQuickNoteProcessStartDispatcher` for testability.

Revision note 2026-06-11: Initial plan created to guide the requested Quick Note UX fixes and keep the work restartable.

Revision note 2026-06-11: Updated after implementation and validation to record the completed changes, build-server workaround, and test evidence.
