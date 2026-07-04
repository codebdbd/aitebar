# Quick Note visual list formatting

This ExecPlan is a living document. It follows `PLANS.md` in the repository root.

## Purpose / Big Picture

Quick Note list formatting currently inserts literal Markdown markers such as `- ` and `1. ` into the editor. That makes selection unstable because those markers become normal editable text and are included in later range calculations. After this change, Quick Note should use WPF `List` and `ListItem` blocks for visual list formatting in the editor, while still saving and loading Markdown list markers in `QuickNote.md`.

The user-visible result is that selecting several lines and choosing the bullet or numbered list menu should show real visual list bullets or numbers like Windows Notepad, without visible `- ` or `1. ` text in the editor and without selection jumping to unrelated rows.

## Progress

- [x] (2026-07-03 14:02Z) Confirmed the current list path uses text replacement through `QuickNoteMarkdown.GetToggleListMarkerRangeEdit` and `QuickNoteWindow.ApplyRangeEdit`.
- [x] (2026-07-03 14:06Z) Confirmed WPF's built-in list command requires block-level paragraphs or list items; one paragraph with `LineBreak` cannot behave like multiple visual list items.
- [x] (2026-07-03 14:22Z) Updated `QuickNoteMarkdown.LoadMarkdown` and `ToMarkdown` to round-trip Markdown list lines as visual `List` blocks.
- [x] (2026-07-03 14:25Z) Updated `QuickNoteWindow` list command to use WPF list editing commands instead of Markdown text replacement.
- [x] (2026-07-03 14:30Z) Added focused tests for visual list load/save and block-aware text pointer offset mapping.
- [x] (2026-07-03 14:48Z) Updated clear formatting to unwrap selected visual `List` blocks back to normal paragraphs.
- [x] (2026-07-03 14:50Z) Ran Release build and focused Quick Note tests.
- [x] (2026-07-03 15:18Z) Fixed review findings: heading semantic clearing, selected list item unwrapping, indented/nested list Markdown serialization, and debounced footer stats updates.
- [x] (2026-07-03 15:22Z) Ran focused Quick Note tests and Release build after performance fix.
- [x] (2026-07-03 16:05Z) Optimized formatting dropdown hot paths by preserving WPF `TextRange` selection directly and caching footer document stats.
- [x] (2026-07-03 16:31Z) Fixed follow-up review findings: clear formatting no longer reuses stale offsets after list changes, document theming recurses into visual lists, link hover detection avoids whole-document scans, and save snapshot creation is explicit.
- [x] (2026-07-03 20:49Z) Completed pre-release review follow-up: link hover matching now scans the current paragraph instead of a single run, `CHANGELOG.md` documents the unreleased Quick Note changes, and the installer was rebuilt.
- [x] (2026-07-04 09:18Z) Moved initial Quick Note document load before `Show()` and deferred document inline restyling so the first visible frame already contains note text.
- [x] (2026-07-04 09:42Z) Fixed clear-formatting selection preservation for toolbar clicks and made selected list-item detection resilient to selection boundary positions.

## Surprises & Discoveries

- Observation: Quick Note stores loaded Markdown as one `Paragraph` with `LineBreak` nodes.
  Evidence: `QuickNoteMarkdown.LoadMarkdown` creates one paragraph and appends `LineBreak` for each Markdown line.

- Observation: WPF `EditingCommands.ToggleBullets` applied to one paragraph with line breaks creates one list item, not one item per visual line.
  Evidence: local STA probe showed a selected `Paragraph` containing `one`, `LineBreak`, `two` became one `System.Windows.Documents.List` block.

- Observation: WPF `EditingCommands.ToggleBullets` applied to separate paragraph blocks creates a list with one `ListItem` per selected paragraph.
  Evidence: local STA probe showed two selected `Paragraph` blocks became one `System.Windows.Documents.List` with `ListItems.Count == 2`.

- Observation: The old text pointer offset helper was tied to one-paragraph documents and returned an invalid negative offset at document start after switching to insertion-position scanning.
  Evidence: `QuickNoteDocumentHelperTests.GetTextPointerAtOffset_ClampsNegativeAndPastEndOffsets` failed with actual offset `-1`; explicit start/end clamps fixed it.

- Observation: Clear formatting still removed only literal Markdown list markers after the editor switched to visual WPF lists.
  Evidence: user reported that pressing the clear formatting button did not remove lists; `QuickNoteWindow.ClearSelectedFormatting` only called `QuickNoteMarkdown.GetClearLineMarkerRangeEdit`.

- Observation: Formatting and selection changes caused micro-stalls because footer statistics were recalculated synchronously on every `TextChanged` and `SelectionChanged`.
  Evidence: user reported microfreezes during text formatting; `QuickNoteWindow.TxtNote_SelectionChanged` and `TxtNote_TextChanged` called `UpdatePlaceholderAndStats`, which reads text from `FlowDocument` via `TextRange`.

- Observation: Opening the list/heading dropdown did an expensive selection offset round-trip even before any formatting was applied.
  Evidence: `QuickNoteWindow.FormatCombo_DropDownOpened` called `GetSelectionOffsets`, then the selection was restored through `SelectEditorRange`, which maps numeric offsets back through `QuickNoteDocumentHelper.GetTextPointerAtOffset`.

- Observation: Clear formatting still reused numeric selection offsets after changing the `FlowDocument` block tree.
  Evidence: `QuickNoteWindow.ClearSelectedFormatting` called `RemoveSelectedListFormatting(selectionStart, selectionEnd)`, then called `SelectEditorRange(selectionStart, ...)` and marker cleanup against the mutated document.

- Observation: Theme refresh skipped inline styles inside visual list items.
  Evidence: `QuickNoteWindow.ApplyDocumentStyles` only inspected top-level `Paragraph` blocks and ignored `System.Windows.Documents.List` / `ListItem` children.

- Observation: Link hover detection still scanned the whole note on mouse move when the link was plain auto-detected text rather than a `Hyperlink` inline.
  Evidence: `QuickNoteWindow.FindLinkAtMouse` called `GetTextOffset(pointer)`, `GetEditorText()`, then `QuickNoteMarkdown.MatchLinks(text)` for every hover check.

- Observation: Matching only the current `Run` fixed the hot path but could miss auto-detected links split by inline formatting.
  Evidence: a URL can be represented by multiple `Run` or `Span` inlines when the user applies formatting to part of the line.

- Observation: Quick Note could show an empty editor first and populate text shortly after.
  Evidence: `QuickNoteWindow.Window_Loaded` awaited `_noteService.LoadAsync(TxtNote.Document)`, so WPF could render the window while file reading and Markdown document construction were still pending.

- Observation: Clear formatting could remove only the last selected list item.
  Evidence: the clear-formatting toolbar button did not preserve the editor's live selection before click handling, unlike the heading/list dropdowns; selection could collapse to the last item before `RemoveSelectedListFormatting` ran.

## Decision Log

- Decision: Use real WPF `List` blocks for Markdown list lines and for toolbar list commands.
  Rationale: This is the only path that makes bullets/numbers visual markers rather than editable Markdown text, matching Windows Notepad behavior.
  Date/Author: 2026-07-03 / Codex

- Decision: Keep Markdown text marker helper methods in `QuickNoteMarkdown` for existing tests and possible non-UI use, but stop using them for toolbar list formatting.
  Rationale: Removing them would broaden the change and invalidate focused tests that still document pure text helper behavior.
  Date/Author: 2026-07-03 / Codex

- Decision: Clear formatting unwraps selected top-level WPF `List` blocks into their contained paragraph blocks before resetting inline formatting.
  Rationale: This removes visual bullets/numbers without relying on toggle commands that could accidentally create a list when the current selection is plain text.
  Date/Author: 2026-07-03 / Codex

- Decision: Footer statistics updates are debounced with a short `DispatcherTimer`.
  Rationale: Footer counts are useful but not part of the formatting command itself; delaying them avoids blocking toolbar clicks and selection movement on full-document `TextRange` extraction.
  Date/Author: 2026-07-03 / Codex

- Decision: Preserve toolbar dropdown selections as WPF `TextRange` positions instead of numeric offsets.
  Rationale: WPF already has stable text pointers for the live `FlowDocument`; avoiding offset conversion removes full-document `TextRange` scans from the list formatting path.
  Date/Author: 2026-07-03 / Codex

- Decision: Clear formatting now removes legacy literal markers before list unwrapping and unwraps visual lists using the current `TextRange`.
  Rationale: This avoids applying old offsets to a document whose block structure has already changed.
  Date/Author: 2026-07-03 / Codex

- Decision: Plain-text link hover matching is limited to the current `Run`.
  Rationale: Mouse move is a hot path; matching one inline run avoids repeated full-document text extraction and regex scans.
  Date/Author: 2026-07-03 / Codex

- Decision: Plain-text link hover matching is limited to the current paragraph instead of the current run.
  Rationale: Paragraph-local matching keeps the mouse-move path bounded while preserving links that cross inline formatting boundaries.
  Date/Author: 2026-07-03 / Codex

- Decision: Load the local note synchronously before `Show()` for the initial window display, then run document inline style refresh at background dispatcher priority.
  Rationale: Quick Note is a local single-note utility; displaying already-loaded content in the first frame is preferable to showing a blank editor while async load resumes. Deferring style refresh keeps link/code color work off the first visible frame.
  Date/Author: 2026-07-04 / Codex

- Decision: Preserve the editor selection on clear-formatting toolbar `PreviewMouseDown` and restore it before applying the command.
  Rationale: Formatting commands must operate on the user's original selected text, not on the caret position left by toolbar mouse handling.
  Date/Author: 2026-07-04 / Codex

## Outcomes & Retrospective

Implemented visual list load/save, visual toolbar list commands, selected-item clear-formatting removal for visual lists with toolbar selection preservation, heading semantic clearing, indented/nested list Markdown serialization, debounced footer stats updates, direct `TextRange` selection preservation for dropdown commands, cached footer document stats, recursive styling for list contents, paragraph-local link hover detection, initial document load before first window display, and explicit Markdown snapshot creation before file writes. Focused Quick Note Markdown and document offset tests passed after adding coverage for `FlowDocument.List` blocks and paragraph boundaries. Release build and installer build passed.

## Context and Orientation

The relevant files are:

- `AiteBar/QuickNoteMarkdown.cs`: parses Markdown into a WPF `FlowDocument` and serializes a `FlowDocument` back to Markdown.
- `AiteBar/QuickNoteWindow.xaml.cs`: handles toolbar commands, including list menu selection.
- `AiteBar.Tests/QuickNoteMarkdownTests.cs`: focused tests for non-UI Markdown behavior.

In WPF, a `FlowDocument` is a document tree. A `Paragraph` contains inline content such as text runs and links. A `List` contains `ListItem` blocks; the bullet or number is visual chrome and is not ordinary text typed into the note.

## Plan of Work

First, update `QuickNoteMarkdown.LoadMarkdown` so each Markdown line becomes a block-level structure. Plain lines and headings become `Paragraph` blocks. Consecutive `- ` or `* ` lines become one bullet `List`. Consecutive `1. ` style lines become one numbered `List`. Each list item contains a `Paragraph` with inline Markdown parsed the same way as a normal line.

Second, update `QuickNoteMarkdown.ToMarkdown` so it serializes `Paragraph` blocks and `List` blocks. Bullet lists should save as `- text`; numbered lists should save as `1. text`, `2. text`, and so on. Existing inline formatting, links, headings, underline, strikethrough, and code should continue to serialize through the existing inline helpers.

Third, update `QuickNoteWindow.CmbList_SelectionChanged` so it restores the user's selection and executes WPF's list commands (`EditingCommands.ToggleBullets` or `EditingCommands.ToggleNumbering`) instead of calling `PrefixSelectedLines`. Keep save scheduling and statistics updates.

Finally, add focused tests proving Markdown list lines render as `List` blocks and save back to Markdown. Run Release build, focused Quick Note tests, and the fallback full `vstest` if `dotnet test` is unstable.

## Validation and Acceptance

Automated validation:

- From `D:\01_Codebdbd\01_projects\aitebar`, run `dotnet build .\AiteBar.sln -c Release -m:1` and expect 0 errors.
- Run `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter QuickNoteMarkdownTests --no-build -m:1` and expect all Quick Note Markdown tests to pass.
- Run the full test suite or fallback `dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll`.

Manual acceptance:

- Open Quick Note, type three separate lines, select them, choose bullet list, and observe real bullets without visible `- ` text.
- Repeat with numbered list and observe visual numbering.
- Select a visual list and click clear formatting; bullets or numbers should disappear and the item text should remain as normal paragraphs.
- Save and reopen the note; the visual list should return from Markdown markers in `QuickNote.md`.

## Idempotence and Recovery

The changes are ordinary source edits. Re-running load/save tests is safe. If the full `dotnet test` hangs in unrelated tests, use the documented project fallback `dotnet vstest` against the built test DLL.

## Artifacts and Notes

- `dotnet build .\AiteBar.sln -c Release -m:1` passed with 0 warnings and 0 errors when run outside the sandbox because WPF MarkupCompile cannot update `obj` cache files inside the restricted sandbox.
- `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "QuickNoteMarkdownTests|QuickNoteDocumentHelperTests" -m:1` passed: 35 total, 35 passed.
- `dotnet build .\AiteBar.sln -c Release -m:1` passed again after debouncing footer stats updates.
- `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "QuickNoteMarkdownTests|QuickNoteDocumentHelperTests" -m:1` passed after dropdown/selection optimization: 35 total, 35 passed.
- `dotnet build .\AiteBar.sln -c Release -m:1` passed after dropdown/selection optimization.
- `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "QuickNoteMarkdownTests|QuickNoteDocumentHelperTests" -m:1` initially hit the known sandbox WPF `obj` access issue, then passed outside the sandbox: 35 total, 35 passed.
- `dotnet build .\AiteBar.sln -c Release -m:1` initially hit the known sandbox WPF `obj` access issue, then passed outside the sandbox with 0 warnings and 0 errors.
- `dotnet build .\AiteBar.sln -c Release -m:1` passed after the pre-release hover fix with 0 warnings and 0 errors.
- Full `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release -m:1` and fallback `dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll` timed out in this environment after 3 minutes.
- `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "QuickNoteMarkdownTests|QuickNoteDocumentHelperTests" -m:1` passed after the pre-release hover fix: 35 total, 35 passed.
- `.\installer\Build-Installer.ps1` passed and rebuilt `artifacts\installer\AiteBar-Setup.exe`.
- `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "QuickNoteMarkdownTests|QuickNoteDocumentHelperTests" -m:1` passed after the startup-load fix: 35 total, 35 passed.
- `dotnet build .\AiteBar.sln -c Release -m:1` passed after the startup-load fix with 0 warnings and 0 errors.
- `.\installer\Build-Installer.ps1` passed after the startup-load fix and rebuilt `artifacts\installer\AiteBar-Setup.exe`.
- `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "QuickNoteMarkdownTests|QuickNoteDocumentHelperTests" -m:1` passed after the multi-line clear-formatting fix: 35 total, 35 passed.
- `dotnet build .\AiteBar.sln -c Release -m:1` passed after the multi-line clear-formatting fix with 0 warnings and 0 errors.
