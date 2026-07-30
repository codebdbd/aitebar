# Complete Zen Editor keyboard, search, recovery, and reliability behavior

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root. It builds on the completed implementation recorded in `ZEN_EDITOR_EXECPLAN.md`, but repeats all context needed for this improvement so that a contributor can continue from this file alone.

## Purpose / Big Picture

After this change, Zen Editor remains visually empty during ordinary writing but behaves like a dependable text editor when the user asks for commands. Standard `Shift+Up` and `Shift+Down` line-selection works again; themes cycle with `Ctrl+Alt+Up` and `Ctrl+Alt+Down`. `Ctrl+F` opens a temporary accessible search strip, Enter and Shift+Enter move between matches, and Escape closes the strip before a later Escape closes the editor. The editor no longer remains above unrelated applications after Alt+Tab, and storage failures from document, theme, export, and recovery commands are caught and shown through the existing save-error surface rather than escaping an `async void` event.

Formatting remains hidden during ordinary writing but becomes discoverable in the shared context menu through Bold, Italic, and Underline commands. The document command is renamed from the ambiguous “Open document” to “Documents”. A “Recently deleted” command lets the user restore and reopen a soft-deleted internal document. Save-error UI receives keyboard focus and an assertive accessibility announcement. These changes must preserve local-only storage, formatting persistence, plain-text TXT export, atomic writes, five fixed themes, the headerless full-screen surface, and the user-requested Escape/Alt+F4 exit behavior when no temporary search surface is open.

## Progress

- [x] (2026-07-30 08:55Z) Read `PLANS.md`, the completed `ZEN_EDITOR_EXECPLAN.md`, current Zen Editor source, tests, localization, and user documentation.
- [x] (2026-07-30 08:55Z) Recorded the usability and reliability design decisions in this new self-contained plan.
- [ ] Implement testable shortcut, search, and guarded-command helpers.
- [ ] Correct full-screen z-order so active Zen Editor covers the taskbar but deactivated Zen Editor does not cover Alt+Tab targets.
- [ ] Add the temporary Ctrl+F search strip and formatting context-menu commands.
- [ ] Add recently-deleted listing, restoration, and picker behavior.
- [ ] Add accessible save-error focus and localization in English, Russian, Ukrainian, and German.
- [ ] Update `docs/USER_MANUAL.md`, `docs/functions.md`, `CHANGELOG.md`, and focused tests.
- [ ] Build Release, run the non-WPF suite and every isolated WPF class, and record exact evidence.
- [ ] Rebuild the 1.12.1 installer and record its version, size, signature state, and SHA-256.

## Surprises & Discoveries

- Observation: The requested `Shift+Up` and `Shift+Down` theme cycling consumes WPF’s standard line-selection gestures.
  Evidence: `ZenEditorWindow.Window_PreviewKeyDown` handles those keys before the `RichTextBox` receives them.

- Observation: The full-screen window is native-topmost even after deactivation.
  Evidence: `ZenEditorWindow.xaml` sets `Topmost="True"` and `ApplyFullScreenBounds` passes `HWND_TOPMOST` to `SetWindowPos`, while `Window_Deactivated` only saves.

- Observation: Save-on-edit catches failures, but several menu and keyboard operations run through `async void` without a common exception boundary.
  Evidence: theme changes, new-document creation, document-picker operations, and async context-menu handlers await storage methods that can throw.

- Observation: Soft deletion and pre-delete snapshots already preserve enough data for user-facing restoration.
  Evidence: `ZenEditorStore.DeleteAsync` sets `IsDeleted`, retains the document JSON, and snapshots before saving the deleted state; only listing and restore operations are missing.

## Decision Log

- Decision: Move theme cycling to `Ctrl+Alt+Up` and `Ctrl+Alt+Down` while retaining direct `Ctrl+Alt+1` through `Ctrl+Alt+5`.
  Rationale: Standard Shift+arrow selection is more fundamental than theme cycling and must reach the editor unchanged.
  Date/Author: 2026-07-30 / Codex

- Decision: Keep full-screen sizing and taskbar coverage, but promote the editor to the native topmost band only while it is active and demote it to the normal band on deactivation.
  Rationale: A normal active full-screen window still covers its screen, while demotion allows Alt+Tab targets to appear. The borderless full-screen geometry remains unchanged.
  Date/Author: 2026-07-30 / Codex

- Decision: Use a reusable guarded async-command helper rather than duplicating try/catch blocks in every event handler.
  Rationale: WPF `async void` handlers otherwise turn storage exceptions into dispatcher-level failures. A small helper is independently testable and routes every failure to the existing overlay without logging document text.
  Date/Author: 2026-07-30 / Codex

- Decision: Implement literal, case-insensitive, wrapping search without Replace, regular expressions, history, or a permanent panel.
  Rationale: Literal Ctrl+F solves navigation in long documents while keeping the writing surface minimal and predictable. Search selection must not trigger the editor’s automatic copy-on-selection behavior.
  Date/Author: 2026-07-30 / Codex

- Decision: Add formatting as one context-menu submenu using WPF’s existing editing commands.
  Rationale: Formatting is already persisted and documented. Exposing the existing Bold, Italic, and Underline commands improves discoverability without adding a toolbar or new document format.
  Date/Author: 2026-07-30 / Codex

- Decision: Restore a deleted document by clearing its soft-delete flag, updating its modified timestamp, making it active, and immediately opening it.
  Rationale: This makes recovery observable and useful while preserving the existing internal storage model. No permanent-delete command is introduced in this milestone.
  Date/Author: 2026-07-30 / Codex

## Outcomes & Retrospective

Implementation is in progress. This section will record completed behavior, exact test evidence, remaining limitations, and installer details after validation.

## Context and Orientation

AiteBar is a .NET 10 WPF desktop application. Zen Editor is one built-in utility and is implemented by `AiteBar/ZenEditorUtility.cs`, which keeps one process-local `ZenEditorWindow`. The full-screen UI lives in `AiteBar/ZenEditorWindow.xaml` and `AiteBar/ZenEditorWindow.xaml.cs`. `ZenParagraphEditor` in `AiteBar/ZenParagraphEditor.cs` adapts WPF `RichTextBox` to a cached plain-text model while retaining Bold, Italic, and Underline ranges. `AiteBar/ZenEditorStore.cs` maintains atomic JSON records under `%APPDATA%\Codebdbd\Aite Bar\ZenEditor`, SHA-256 validation, soft deletion, and bounded backups. `AiteBar/ZenEditorDocumentPicker.xaml` and its code-behind provide the internal document list.

“Topmost band” means the Windows z-order group that stays above ordinary windows. `SetWindowPos` can move the Zen Editor into `HWND_TOPMOST` while active and `HWND_NOTOPMOST` when inactive without moving or resizing it. “Guarded async command” means an awaited operation whose exception is caught, logged without document content, and displayed through the editor’s existing failure overlay. “Soft deleted” means the document record remains on disk with `IsDeleted=true` but is excluded from the normal document list.

Localization strings are in `AiteBar/Resources/Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx`. User documentation is in `docs/USER_MANUAL.md` and `docs/functions.md`. Focused tests use `AiteBar.Tests/ZenEditor*.cs`. WPF tests belong to the `WpfTestCollection` and must run in isolated hosts because this repository has previously observed nondeterministic Dispatcher shutdown hangs when every WPF class shares one test process. The class list in `.github/workflows/build-test.yml` and `.github/workflows/release.yml` must stay synchronized when a new WPF test class is introduced.

The working version is 1.12.1. A timer hotfix was committed separately as `ee9de95` before this plan so Zen Editor changes do not mix with that completed work.

## Plan of Work

First, add small testable helpers. `AiteBar/ZenEditorShortcutResolver.cs` will map a WPF key and modifier set to semantic actions so tests can prove that Shift+arrow is unhandled and Ctrl+Alt+arrow cycles themes. `AiteBar/ZenEditorSearchHelper.cs` will return the next or previous literal case-insensitive match with wraparound and no allocation proportional to the number of matches. `AiteBar/ZenEditorAsyncCommandGuard.cs` will await a supplied task and invoke a failure callback once if it throws.

Second, update `ZenEditorWindow.xaml` and code-behind. Remove persistent `Topmost="True"`, add `HWND_NOTOPMOST` and no-move/no-size flags, promote on activation, demote on deactivation, and keep `ApplyFullScreenBounds` responsible for active full-screen geometry. Route keyboard handling through the shortcut resolver. Add a collapsed search strip using shared AiteBar controls and accessible names. Ctrl+F opens it, selected text may seed the query, Enter/F3 finds forward, Shift+Enter/Shift+F3 finds backward, and Escape hides search before closing the editor. Programmatic match selection suppresses automatic clipboard copy.

Third, add a Formatting submenu to `BuildContextMenu`. Each command executes WPF `EditingCommands.ToggleBold`, `ToggleItalic`, or `ToggleUnderline`, displays its keyboard shortcut, and reflects the current selection state when the menu opens. Add a Recently Deleted command that opens the document picker in restore mode. Change the display value of `ZenEditor_OpenDocument` to “Documents…” in every language without changing its stable resource key.

Fourth, extend `ZenEditorStore` with `ListDeletedAsync` and `RestoreAsync`. Deleted summaries are sorted newest first. Restore validates the retained record or backup, clears `IsDeleted`, updates `ModifiedUtc`, writes atomically, updates the index, and returns a clone. Extend `ZenEditorDocumentPicker` with an explicit normal/open mode and deleted/restore mode. In restore mode, Enter or double-click chooses a document for restoration and the title and accessibility labels state that purpose.

Fifth, route keyboard, context-menu, picker, export, and theme operations through the guarded helper. Existing `SaveNowAsync` continues returning `false` for save failures and must not duplicate overlays. Update `ShowSaveError` to mark the message as an assertive live region and focus the Retry button after layout so keyboard and screen-reader users immediately reach the recovery action.

Finally, update localization, documentation, changelog, and tests. Add non-UI tests for shortcut resolution, search wraparound, guard behavior, deleted listing/restoration, and fault handling. Extend runtime WPF tests for the collapsed search strip, search navigation without clipboard auto-copy, formatting menu entries, save-error focus, and picker modes. Update source contracts for topmost behavior and new menu counts. Run the release gates and build the installer.

## Concrete Steps

All commands run from `D:\01_Codebdbd\01_projects\aitebar`.

After each milestone:

    git diff --check
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~ZenEditor" --no-restore

Build the full solution:

    dotnet build .\AiteBar.sln -c Release

Run non-WPF tests by excluding the WPF class list used in `.github/workflows/build-test.yml`, then run each WPF class in a separate host:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --filter <non-WPF-filter>
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~<WPF-class>"

Build and verify the installer:

    .\installer\Build-Installer.ps1
    Get-FileHash .\artifacts\installer\AiteBar-Setup.exe -Algorithm SHA256
    (Get-Item .\artifacts\installer\AiteBar-Setup.exe).VersionInfo.ProductVersion
    Get-AuthenticodeSignature .\artifacts\installer\AiteBar-Setup.exe

Expected outcomes are zero build warnings and errors, zero failed tests, ProductVersion 1.12.1, one installer artifact, a checksum matching `SHA256SUMS.txt`, and an explicit signature state.

## Validation and Acceptance

Open Zen Editor and type multiple lines. Holding Shift while pressing Up or Down must extend the selection exactly as a normal WPF editor does; the theme must not change. Pressing Ctrl+Alt+Up or Ctrl+Alt+Down must cycle through all five themes with wraparound. Ctrl+Alt+1 through Ctrl+Alt+5 must still select exact themes.

Press Ctrl+F. A small temporary search strip must appear without changing the text-column width. Type a query present more than once. Enter must select the next match, Shift+Enter the previous match, and both directions must wrap. Search must be literal and case-insensitive. A missing query displays a localized no-results state. Selecting a match through search must not replace the Windows clipboard. Escape first closes search and returns focus to the editor; with search closed, Escape closes the editor after a forced save.

Right-click. The shared AiteBar context menu must retain its standard geometry and glyph alignment. A Formatting submenu must expose Bold, Italic, and Underline with Ctrl+B, Ctrl+I, and Ctrl+U. Applying them, saving, closing, and reopening must preserve ranges; TXT export remains plain text. The normal document command must read “Documents…” rather than implying an external file dialog.

Delete a document through the normal picker, open Recently Deleted, choose that document with Enter or double-click, and verify that it reopens with text, formatting, and metadata intact. It must disappear from Recently Deleted and return to the normal newest-first list.

With the editor active, it must cover the taskbar on its monitor. Alt+Tab to another application; the other application must be visible above the inactive editor. Alt+Tab back; Zen Editor must restore full-screen active z-order and editor focus.

Inject a throwing async operation through unit tests and verify the guard calls the failure callback once and does not leak the exception. Show the save-error overlay in a WPF test and verify that its message is an assertive live region and Retry receives keyboard focus.

## Idempotence and Recovery

All changes are additive or modify stable keyboard routing. Store restore is safe to retry: restoring an already active document returns it without creating duplicates. Search never changes document text. Z-order changes use no-move/no-size flags and can be applied repeatedly.

Tests use GUID-named temporary directories and may delete only those exact directories. No test touches the user’s `%APPDATA%` Zen Editor store or clipboard unless it first preserves and restores the exact clipboard value; prefer state assertions that do not mutate the real clipboard. If a WPF test host hangs during shutdown, terminate only that exact test process, record the class, and rerun the class in an isolated host. Do not reset or discard unrelated working-tree changes.

If installer creation fails after publish, rerun `installer/Build-Installer.ps1`; it safely recreates the workspace `artifacts/publish/win-x64` output and the single installer artifact. The user’s installed application and running process must not be stopped without explicit need.

## Artifacts and Notes

The normal writing surface must remain one solid theme background with only text, selection, and caret. Search and error UI are explicit temporary surfaces and therefore may use shared AiteBar chrome. No word count, goals, AI, Markdown, permanent toolbar, formatting toolbar, configurable fonts, or configurable colors are introduced.

The context menu continues to use `AppContextMenuFactory`, so its styling remains identical to other AiteBar utilities. The document text, search query, title, paths, formatting ranges, and deleted-document content must not appear in telemetry or logs.

## Interfaces and Dependencies

`AiteBar/ZenEditorShortcutResolver.cs` must expose an internal semantic action enum and a pure resolver similar to:

    internal static ZenEditorShortcutAction Resolve(Key key, ModifierKeys modifiers);

`AiteBar/ZenEditorSearchHelper.cs` must expose a pure method similar to:

    internal static int Find(string text, string query, int startIndex, bool forward);

It returns `-1` when no match exists and wraps once in either direction.

`AiteBar/ZenEditorAsyncCommandGuard.cs` must expose:

    internal static Task ExecuteAsync(Func<Task> action, Action<Exception> onError);

`ZenEditorStore` must additionally expose:

    Task<IReadOnlyList<ZenEditorDocumentSummary>> ListDeletedAsync(
        string untitled,
        CancellationToken cancellationToken = default);

    Task<ZenEditorDocument> RestoreAsync(
        Guid id,
        CancellationToken cancellationToken = default);

No new NuGet dependency is needed. Use existing .NET, WPF, store, localization, menu factory, and test infrastructure.

Plan revision note: 2026-07-30, created after a product and technical review identified keyboard-selection conflict, permanent topmost behavior, unguarded async UI operations, absent long-document search, hidden formatting commands, ambiguous internal-document wording, inaccessible soft-delete recovery, and save-error accessibility gaps.
