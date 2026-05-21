# Stabilize QuickNote editor

This ExecPlan is a living document. It follows PLANS.md in the repository root.

## Purpose / Big Picture

QuickNote must behave like a small reliable scratchpad: typing must not turn all text bold, toolbar actions must not move the caret unpredictably, links must remain clickable with Ctrl, lists must toggle cleanly, and saved markdown must reopen into the same visible formatting. The implementation will stop mixing unrelated plain-text rewrites with rich editor state by moving markdown parsing and serialization into a testable helper and making toolbar operations preserve selection and focus.

## Progress

- [x] (2026-05-17 23:20 Europe/Kiev) Reviewed the current QuickNote code and identified the main failure mode: the window mutates a WPF FlowDocument directly while also rebuilding the entire document as plain text for lists and cleanup.
- [x] (2026-05-18 00:05 Europe/Kiev) Replaced the markdown parser/serializer with `AiteBar/QuickNoteMarkdown.cs`, including escaping for literal markdown characters.
- [x] (2026-05-18 00:05 Europe/Kiev) Updated list and clear-formatting commands to use deterministic line transformations and restored editor focus after toolbar actions.
- [x] (2026-05-18 00:05 Europe/Kiev) Fixed URL matching so highlighted ranges and opened URLs use the same trimming behavior.
- [x] (2026-05-18 00:05 Europe/Kiev) Added unit tests for markdown escaping, markdown load/save, list toggles, marker cleanup, and URL normalization.
- [x] (2026-05-18 00:05 Europe/Kiev) Ran `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-restore`; result: 62 passed.
- [x] (2026-05-18 00:06 Europe/Kiev) Ran `dotnet build .\AiteBar.sln -c Release`; result: build succeeded with 0 warnings and 0 errors.
- [x] (2026-05-18 13:13 Europe/Kiev) Changed list marker editing to apply small `TextRange` operations instead of rebuilding the entire document, preserving existing inline formatting.
- [x] (2026-05-18 13:13 Europe/Kiev) Rebuilt `artifacts\installer\AiteBar-Setup.exe`; timestamp: 2026-05-18 13:13:48.

## Surprises & Discoveries

- Observation: QuickNote has no test coverage in AiteBar.Tests.
  Evidence: `rg -n "QuickNote" .\AiteBar.Tests .\AiteBar` found only app code and resource references.

- Observation: WPF text offsets must account for line breaks through `TextRange`, not only text runs.
  Evidence: the old `GetTextPointerAtOffset` walked only `TextPointerContext.Text`, so offsets after `LineBreak` elements could point to the wrong place.

- Observation: Rebuilding the entire FlowDocument for a list command is too destructive.
  Evidence: the implementation now applies only marker insert/remove operations through `TextRange`, so formatted text outside the marker itself is not recreated as plain text.

## Decision Log

- Decision: Keep markdown as the storage format.
  Rationale: The file is already named QuickNote.md and the user explicitly wanted markdown-like behavior where formatted text is shown visually but stored with markdown markers.
  Date/Author: 2026-05-17 / Codex.

- Decision: Use a small purpose-built markdown helper instead of introducing a new dependency.
  Rationale: The module only needs inline bold, italic, code, underline as visual-only, links, and simple line-list prefixes. A dependency would be heavier than the current scope and harder to integrate into WPF FlowDocument.
  Date/Author: 2026-05-17 / Codex.

- Decision: Keep list markers as markdown-style plain text prefixes instead of WPF `List` blocks.
  Rationale: The note is stored as `QuickNote.md`; using WPF list blocks would require a larger serializer and would make the saved file less predictable. Deterministic line transforms are enough for this scratchpad.
  Date/Author: 2026-05-18 / Codex.

## Outcomes & Retrospective

Completed the stabilization pass. QuickNote now has a dedicated markdown helper, deterministic list transformations, safer URL handling, better save failure behavior, and unit tests. The remaining gap is manual visual QA in the running WPF app, especially toolbar feel and color theme taste, which cannot be proven by unit tests alone.

## Context and Orientation

The feature lives in `AiteBar/QuickNoteWindow.xaml`, `AiteBar/QuickNoteWindow.xaml.cs`, `AiteBar/QuickNoteService.cs`, and `AiteBar/QuickNoteTheme.cs`. `QuickNoteWindow` owns the WPF RichTextBox and UI commands. `QuickNoteService` loads and saves `QuickNote.md` under the application data folder. `ActionService.StartQuickNoteAsync` opens the window from the main panel.

The current bug class comes from converting the whole RichTextBox to plain text for list commands and cleanup. That destroys inline formatting and changes text pointers, which makes the caret appear to jump.

## Plan of Work

Create a `QuickNoteMarkdown` helper in `AiteBar/QuickNoteMarkdown.cs` that can load markdown into a FlowDocument and serialize a FlowDocument back to markdown. The helper will escape literal markdown markers and will only emit markdown markers for explicit bold, italic, and code spans. It will ignore link coloring so link highlighting never pollutes saved markdown.

Update `QuickNoteService` to delegate parsing and serialization to that helper.

Update `QuickNoteWindow` to preserve selection around toolbar button clicks, list toggles, cleanup, and link highlighting. List operations will still operate on line text, but the selection/caret will be restored deterministically and tests will cover the pure line transformation.

Add tests in `AiteBar.Tests/QuickNoteMarkdownTests.cs`.

## Concrete Steps

Run from `D:\01_Codebdbd\01_projects\aitebar`:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` hits WPF temporary file issues, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net8.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

Acceptance is: typed text stays normal by default; bold applies only to the selected text or future typed text when intentionally enabled; list buttons add and remove prefixes for selected/current lines; clear formatting removes inline formatting and list markers; URLs are blue and open only with Ctrl+click; saving and reopening preserves supported formatting without adding unexpected markers.

Unit tests must prove markdown round-trip, escaping, URL trimming, and list toggle behavior.

## Idempotence and Recovery

All edits are source edits and tests. Re-running build and tests is safe. The plan does not require deleting user data or resetting the repository.

## Artifacts and Notes

Validation transcript:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-restore
    Passed! Failed: 0, Passed: 62, Skipped: 0, Total: 62

    dotnet build .\AiteBar.sln -c Release
    Build succeeded. Warnings: 0, Errors: 0

    .\installer\Build-Installer.ps1
    Installer created in D:\01_Codebdbd\01_projects\aitebar\artifacts\installer

## Interfaces and Dependencies

`AiteBar/QuickNoteMarkdown.cs` will expose static methods:

    public static void LoadMarkdown(FlowDocument document, string markdown)
    public static string ToMarkdown(FlowDocument document)
    public static QuickNoteTextEdit ToggleListMarkers(string text, int selectionStart, int selectionEnd, bool numbered)
    public static QuickNoteTextEdit ClearLineMarkers(string text, int selectionStart, int selectionEnd)
    public static IEnumerable<Match> MatchUrls(string text)
    public static string NormalizeUrlForOpen(string matchedUrl)
