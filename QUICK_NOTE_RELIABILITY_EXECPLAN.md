# Make Quick Note formatting, persistence, and window behavior reliable

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan is maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this work, Quick Note must preserve every combination of formatting exposed by its toolbar across save and reload, must not overwrite an unchanged note merely because the window closes, and must protect local and external edits with atomic writes and unique conflict copies. The unpinned window must remain open while its confirmation dialog is active, saved positions must reopen on the correct monitor, the toolbar must fit at every supported window width, and clearing formatting from part of a hyperlink must not remove the link from unselected text.

The result is observable by using combinations such as bold plus italic or underline plus strikethrough, closing and reopening Quick Note, and seeing the same formatting without literal Markdown markers. It is also demonstrable through focused unit and WPF tests, a successful Release build, the full test suite, and a rebuilt installer.

## Progress

- [x] (2026-07-28) Audited the complete Quick Note implementation and reproduced nested-format loss and conflict-copy filename collisions.
- [x] (2026-07-28) Created this self-contained ExecPlan with acceptance criteria for all confirmed findings.
- [x] (2026-07-28) Implemented recursive inline Markdown parsing and round-trip coverage for nested toolbar formats.
- [x] (2026-07-28) Prevented unchanged close from rewriting the note and changed normal note writes to temporary-file replacement.
- [x] (2026-07-28) Added a missing-file baseline and collision-proof conflict-copy creation.
- [x] (2026-07-28) Protected modal clear confirmation and settings writes from unhandled asynchronous failures.
- [x] (2026-07-28) Preserved unselected hyperlink fragments during partial clear formatting.
- [x] (2026-07-28) Reopened saved geometry on the monitor containing the saved bounds, including negative coordinates.
- [x] (2026-07-28) Moved secondary formatting commands into a compact overflow menu so the toolbar fits supported widths.
- [x] (2026-07-28) Completed focused tests (91/91), Release build, full suite (927/927), and installer build.
- [x] (2026-07-28) Verified `AiteBar-Setup.exe` (77,593,769 bytes) and matching SHA-256 manifest.
- [x] (2026-07-28) Performed a final code review of the complete Quick Note boundary and fixed five additional persistence/round-trip/command-state issues.
- [x] (2026-07-28) Expanded focused coverage to 99 passing tests and completed a zero-warning Release build plus 935/935 full-suite tests.
- [x] (2026-07-28) Rebuilt the final installer after review and verified its SHA-256 manifest.
- [x] (2026-07-28) Triaged the attached external review against the actual click/save paths and applied the confirmed security and lifecycle hardening.
- [x] (2026-07-28) Bounded forced-save semaphore waiting, tightened `mailto:`/`tel:` validation, switched link regexes to the non-backtracking engine, hid absolute conflict paths, and removed duplicate hyperlink URL parsing.
- [x] (2026-07-28) Expanded focused coverage to 107 passing tests; the mixed development solution passes 943/943 with a zero-warning Release build.
- [x] (2026-07-28) Applied the safe cleanup from the second factual review: centralized tag/font contracts, reused one newline normalizer, and replaced resize string fallback with validated enum parsing.
- [x] (2026-07-28) Expanded focused coverage to 129 passing tests; the mixed development solution passes 965/965 with a zero-warning Release build.

## Surprises & Discoveries

- Observation: The serializer can emit nested Markdown that the parser cannot reconstruct.
  Evidence: Reflection against the built assembly produced `***both*** -> **\*both**\*`, `<u>~~under-strike~~</u> -> <u>\~\~under-strike\~\~</u>`, and `[**bold link**](https://example.com) -> [\*\*bold link\*\*](https://example.com)`.

- Observation: Conflict-copy names have one-second precision and are overwritten.
  Evidence: Two immediate calls returned the same `QuickNote.conflict-20260727-103942.md` path; only the second content remained and the directory contained one conflict file.

- Observation: The left toolbar requests about 471 pixels, while the default window provides about 420 pixels after chrome and right-side commands, and the minimum window provides about 300 pixels.
  Evidence: The fixed widths and margins in `AiteBar/QuickNoteWindow.xaml` exceed the Grid column budget at both `Width="580"` and `MinWidth="460"`.

- Observation: The first focused run exposed a second nested-format bug in the serializer: inherited styles were reopened around every child run.
  Evidence: `**bold `code`**` initially serialized as `**bold ****`code`**`; moving Markdown delimiters to the owning inline container fixed both this case and triple-emphasis with code.

- Observation: A failed test compilation left `testhost` PID 31852 holding the Release test assemblies.
  Evidence: The process path pointed into this repository's `AiteBar.Tests\bin\Release` directory. Terminating only that PID released the files; subsequent focused and full runs completed normally.

- Observation: Resetting the heading combo to its body-text item prevented choosing body text again after applying a heading.
  Evidence: WPF does not raise `SelectionChanged` when the already-selected body item is chosen. Both formatting combos now return to command state (`SelectedIndex="-1"`) after every action.

- Observation: Inline code nested with strikethrough was serialized with code as the outer delimiter, causing the strike markers to reload as literal code.
  Evidence: The canonical order is now `~~`code`~~`; focused round-trip tests cover code+strike, underline+code+strike, and a formatted link.

- Observation: Partial clear-formatting rebuilt linked prefix/suffix text from only the first `Run`, flattening nested bold, italic, or strike structure.
  Evidence: Range cloning now preserves the complete inline tree for unselected linked fragments, while the selected fragment serializes without formatting or link syntax.

- Observation: External edits could evade timestamp/length comparison if content length and timestamp were unchanged, and the latest conflict-copy path was forgotten after an application restart.
  Evidence: The service now includes SHA-256 content identity in its baseline and discovers the newest conflict copy during note load; focused tests reproduce both cases.

- Observation: Link insertion trimmed selected leading and trailing whitespace.
  Evidence: Link display text is now preserved exactly while URL normalization continues to trim the URL field.

- Observation: The attached review correctly identified unbounded forced-save waiting and weak defense-in-depth validation for raw email/phone links, but its command-injection path for Markdown `file:`/`javascript:` links was not reachable.
  Evidence: Explicit Markdown hyperlinks are classified as URL links and already pass through the HTTP/HTTPS-only click validator before `Process.Start`. The shared validator is now stricter for every link type and has regression cases for rejected `file:`, `javascript:`, shell metacharacters, and traversal-like phone payloads.

- Observation: `ConfigureAwait(false)` is not appropriate in `QuickNoteWindow.SaveNowAsync`.
  Evidence: The continuation updates WPF controls and reads the UI-owned `FlowDocument`; retaining the dispatcher context is intentional. Reliability is instead provided by a bounded ten-second forced wait that leaves the window open on timeout.

- Observation: The resize handler treated every unknown XAML `Tag` as `BottomRight`.
  Evidence: The default switch arm returned `WMSZ_BOTTOMRIGHT`; the handler now accepts only the eight values in `QuickNoteResizeEdge` and ignores invalid tags. Tests cover all eight valid names plus empty, case-mismatched, unknown, and null values.

- Observation: The second review's remaining God-object, synchronous-first-load, and persistence-adapter findings are architectural tradeoffs rather than demonstrated user defects.
  Evidence: The adapter is the test seam used by WPF close/formatting tests, and loading before the first paint avoids presenting an empty note. Both changes would require separate UX/performance acceptance work.

## Decision Log

- Decision: Treat all eight confirmed audit findings plus direct non-atomic note writes as one reliability change.
  Rationale: They interact through the same save/reload and window lifecycle. Fixing them together allows end-to-end tests that prove user data and selection behavior survive the complete workflow.
  Date/Author: 2026-07-28 / Codex

- Decision: Preserve the existing lightweight single-note architecture and Markdown file format.
  Rationale: The request is corrective. It does not authorize a new editor engine, database, or persistent history model.
  Date/Author: 2026-07-28 / Codex

- Decision: Prefer small pure helpers for parsing, file naming, geometry selection, and toolbar layout contracts.
  Rationale: These behaviors can be tested without relying exclusively on fragile interactive WPF automation.
  Date/Author: 2026-07-28 / Codex

- Decision: Keep the four most common inline commands visible and move undo, redo, underline, code, and clear formatting into a localized overflow menu.
  Rationale: This preserves access to every command while fitting both the default and minimum window widths without increasing the lightweight window size.
  Date/Author: 2026-07-28 / Codex

- Decision: Compare external note content by hash in addition to existence, size, and timestamp.
  Rationale: Avoiding a rare false negative is worth one sequential read before a pending save because Quick Note is a single local text file and data preservation is the priority.
  Date/Author: 2026-07-28 / Codex

- Decision: Apply targeted hardening from the external review without undertaking the proposed God-object rewrite.
  Rationale: The safety and lifecycle improvements are independently testable. Splitting the WPF window into several services would be a separate high-risk refactor and is not required to correct the reported behavior.
  Date/Author: 2026-07-28 / Codex

## Outcomes & Retrospective

Implementation is complete and the final review validation is green. Quick Note now retains nested formatting, avoids redundant close writes, writes atomically, detects file creation and same-metadata content edits after its baseline, creates and rediscovers unique conflict copies, safely handles settings failures and the clear dialog, preserves unselected rich hyperlink fragments, restores the correct monitor, and keeps every formatting command repeatable and reachable in the compact toolbar.

Focused Quick Note tests pass 129/129. The Release solution build completes with zero warnings and zero errors, and the complete mixed-development suite passes 965/965. `installer/Build-Installer.ps1` rebuilt publish output and `artifacts/installer/AiteBar-Setup.exe`; code signing was skipped because no signing certificate was supplied. The final local installer is 77,606,670 bytes and its SHA-256 is `4E040E9388E4AF8E935FDA6AA857867B9386F56FFBAB62CD8406DEAA578A2CCF`, matching `SHA256SUMS.txt`.

A separate launch smoke test was not performed because an installed AiteBar instance (PID 27144) was already running and the application uses a single-instance workflow. That user process was deliberately left untouched; compilation, publish, installer generation, focused WPF tests, and the full suite provide the final automated evidence.

## Context and Orientation

`AiteBar/QuickNoteWindow.xaml` defines the lightweight always-on-top note window, its formatting toolbar, editor, theme popup, footer, and resize grips. `AiteBar/QuickNoteWindow.xaml.cs` owns lifecycle, focus behavior, autosave, formatting commands, link handling, theme application, geometry persistence, and status updates.

`AiteBar/QuickNoteMarkdown.cs` converts between the WPF `FlowDocument` used by `RichTextBox` and the persisted `QuickNote.md` text. A round-trip means converting Markdown to a `FlowDocument` and then back to Markdown without losing the user-visible formatting or content.

`AiteBar/QuickNoteService.cs` reads and writes the Markdown file, detects edits made by another process, creates conflict copies, and opens note files through the Windows shell. `AiteBar/QuickNotePersistence.cs` is the small interface used to isolate that service in window tests.

`AiteBar/QuickNoteDocumentHelper.cs` contains text-offset and selection helpers. `AiteBar/QuickNoteLayoutHelper.cs` contains monitor-independent geometry math. `AiteBar/QuickNoteTheme.cs` defines themes, while `AiteBar/QuickNoteLinkDialog.xaml(.cs)` provides the modal link editor.

Focused tests live in `AiteBar.Tests/QuickNoteMarkdownTests.cs`, `QuickNoteServiceTests.cs`, `QuickNoteDocumentHelperTests.cs`, `QuickNoteLayoutHelperTests.cs`, `QuickNoteWindowCloseTests.cs`, `QuickNoteWindowFormattingTests.cs`, and `QuickNoteFormattingControlsTests.cs`.

The working tree already contains unrelated Text Processing changes and two unrelated ExecPlans. They belong to the user and must remain untouched. The Quick Note changes made immediately before this plan restore empty formatting combobox entries and make clear-formatting document edits atomic; this plan builds on those changes.

## Plan of Work

First, update `QuickNoteMarkdown` so inline containers recursively parse their content rather than creating a plain `Run`. The parser must preserve combinations of bold, italic, code, underline, strikethrough, and hyperlink content that the serializer can emit. Add explicit round-trip tests for nested combinations and formatted links.

Second, update `QuickNoteService` with a real baseline state that distinguishes “not loaded yet” from “loaded and missing.” Use a temporary file in the note directory followed by an atomic replace or move so an interrupted save never truncates the existing note. Generate conflict-copy names with sub-second uniqueness and non-overwriting creation. Tests must cover external creation after a missing baseline, two immediate conflicts, and preservation of the original file when a write fails where practical.

Third, change window close semantics so a forced close waits for pending/in-flight saves but does not serialize an unchanged document. Wrap settings saves used by pin, theme, and geometry handlers so exceptions are logged rather than escaping `async void` dispatcher callbacks. Set the modal-dialog guard around the clear confirmation just as the link dialog already does.

Fourth, make partial hyperlink clearing split the hyperlink at the selected boundaries: text outside the selection retains its URL, while only selected text becomes plain and has formatting reset. Keep whole-link clearing behavior when the entire link is selected. Add WPF tests for a middle substring and full-link selection.

Fifth, select the work area from saved coordinates before showing the window. Add a layout helper accepting available monitor rectangles and a saved point or bounds, with deterministic tests representing primary, secondary, removed, and negative-coordinate monitors.

Sixth, make the header responsive without increasing the minimum or default window size. Keep primary formatting commands visible and place overflow commands in a compact menu or use adaptive visibility according to existing visual conventions. Add a XAML contract test that calculates or asserts the responsive structure at the minimum width.

Finally, run the focused Quick Note tests, `dotnet build .\AiteBar.sln -c Release`, and `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`. If the documented WPF host issue occurs, run `dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll`. Rebuild the installer with `.\installer\Build-Installer.ps1` and verify `artifacts\installer\AiteBar-Setup.exe` plus its SHA-256 file.

## Concrete Steps

Run all commands from `D:\01_Codebdbd\01_projects\aitebar`.

Inspect the scoped diff before editing:

    git diff -- AiteBar/QuickNote* AiteBar.Tests/QuickNote*

Run focused tests during implementation:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~QuickNote"

Run final validation:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

Use the documented fallback if required:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

Build and inspect the installer:

    .\installer\Build-Installer.ps1
    Get-Item .\artifacts\installer\AiteBar-Setup.exe
    Get-FileHash .\artifacts\installer\AiteBar-Setup.exe -Algorithm SHA256

## Validation and Acceptance

The change is accepted when all of the following observable behaviors are true.

Applying any combination of toolbar formats, saving, closing, and reopening yields the same visible combination without literal Markdown syntax. A Markdown-formatted link retains both its URL and nested bold/italic/code/strike styling.

Closing an unchanged note performs no content write. Creating `QuickNote.md` externally after the window opened with no file produces a conflict copy instead of overwriting the external content. Two conflict copies created immediately have different paths and both contents survive. Normal note saves use a temporary file and leave either the old complete file or the new complete file.

With Quick Note unpinned, opening the clear confirmation leaves the note and dialog open until the user confirms or cancels. A settings write failure is logged and does not terminate the UI dispatcher.

Clearing formatting from the middle of a hyperlink keeps the prefix and suffix linked. Clearing the entire hyperlink removes the link. Selected text and caret remain stable.

Saved coordinates on a secondary or negative-coordinate monitor reopen on that monitor. If that monitor is gone, bounds clamp to a remaining work area.

At widths 580 and 460, toolbar controls do not overlap pin, menu, or close buttons and all commands remain reachable.

Focused tests, Release build, the full suite or approved fallback, and installer build all complete successfully.

## Idempotence and Recovery

All edits are source changes and tests; rerunning builds and tests is safe. Atomic note writes must create uniquely named temporary files in the destination directory and delete abandoned temporary files in a `finally` block. Conflict-copy creation must never overwrite an existing copy.

Do not reset or discard unrelated working-tree changes. If a WPF test leaves a process running, terminate only the test process identified by that run. Installer generation may overwrite files only under the repository’s documented `artifacts/publish/win-x64` and `artifacts/installer` directories.

## Artifacts and Notes

Initial audit evidence:

    Nested formatting:
      INPUT=***both***
      ROUNDTRIP=**\*both**\*

    Conflict collision:
      SAME_PATH=True
      FINAL_CONTENT=second local version
      CONFLICT_FILE_COUNT=1

    Toolbar budget:
      LeftToolbarDesired=471
      DefaultLeftColumnBudget~=420
      MinWindowLeftColumnBudget~=300

All 47 Quick Note localization keys exist and are non-empty in `Strings.resx`, `Strings.de.resx`, `Strings.uk.resx`, and `Strings.ru.resx`.

## Interfaces and Dependencies

Keep `IQuickNotePersistence` as the window boundary. It may gain a method only if a window-level behavior cannot be expressed safely through the current methods.

`QuickNoteMarkdown.LoadMarkdown(FlowDocument, string)` and `QuickNoteMarkdown.ToMarkdown(FlowDocument)` remain the public conversion entry points. New parser helpers should stay internal or private and use existing WPF inline types.

`QuickNoteService.NotePath`, `ReadMarkdown`, `ReadMarkdownAsync`, `Load`, `LoadAsync`, `SaveAsync`, `SaveConflictCopyAsync`, `HasExternalChanges`, `OpenInEditor`, and `OpenConflictCopy` must remain source-compatible.

Use only .NET and existing WPF APIs. Do not add a Markdown package or a persistence dependency for this corrective change.

Revision note (2026-07-28): Initial plan created from the completed Quick Note audit so implementation can proceed without relying on chat history.

Revision note (2026-07-28): Updated after implementation and automated verification; documented the serializer finding, targeted stale-testhost recovery, toolbar decision, and passing test counts.

Revision note (2026-07-28): Closed the plan after successful installer generation and checksum verification.

Revision note (2026-07-28): Reopened for the requested final code review; recorded five additional findings, their fixes, and expanded passing test evidence.

Revision note (2026-07-28): Closed the final review after rebuilding the installer and verifying its manifest; documented why the already-running installed instance was not disturbed for a second smoke launch.

Revision note (2026-07-28): Reopened to triage the attached third-party review; recorded which findings were reachable, applied targeted hardening, and added security/lifecycle regression tests.

Revision note (2026-07-28): Applied the low-risk contract cleanup from the factual re-review and documented the architectural items intentionally left outside this corrective release.
