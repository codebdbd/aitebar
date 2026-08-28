# Stabilize Quick Note formatting rendering

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan follows `PLANS.md` from the repository root.

## Purpose / Big Picture

Quick Note must show the same formatting before and after reopening a note or changing its theme. The Copy glyph in a code header must keep the code-header color, checked tasks must keep their semantic formatting without destroying user formatting, and code, links, quotes, and task items must be rendered by one document-specific styling path. A user can verify this by creating a code block in a light theme, reopening the note, changing themes, and seeing the Copy glyph, code body, links, and checked tasks retain their intended roles.

## Progress

- [x] (2026-08-27 17:05Z) Read `PLANS.md`, inspected the current Quick Note formatter, renderer, persistence adapter, themes, and focused tests.
- [x] (2026-08-27 17:05Z) Recorded existing in-progress changes that add recursive user-strikethrough state and remove task-only strike decorations from RTF export.
- [x] (2026-08-27 17:14Z) Excluded all `TxtNote` descendants from the window chrome text-color pass, so document runtime visuals are styled only by document code.
- [x] (2026-08-27 17:14Z) Added theme-derived code-header background rendering and one `ApplyCodeHeaderTheme` routine shared by code-block creation and presentation re-rendering.
- [x] (2026-08-27 17:21Z) Moved base inline rendering from `QuickNoteWindow.Presentation` into `QuickNoteDocumentFormatting`; checklist formatting now applies only its checked-state overlay after the shared renderer.
- [x] (2026-08-27 17:14Z) Preserved the existing recursive user-strikethrough and RTF task export work; additionally removed the generated paragraph-level task strike from RTF task export.
- [x] (2026-08-27 17:14Z) Added code-header role regression tests and ran focused, Quick Note, and non-Quick Note test partitions.
- [x] (2026-08-27 18:24Z) Replaced the remaining editor and RTF inline cloners with one document contract, routed quote rendering through the common inline renderer, and added physical `.aite-note` round-trip coverage.

## Surprises & Discoveries

- Observation: `ApplyTheme` already skips `Button` descendants of `TxtNote`, but it applies `theme.Text` to every `TextBlock` in the whole window.
  Evidence: `AiteBar/QuickNoteWindow.Presentation.cs` loops through `FindVisualChildren<TextBlock>(this)` before document styles are applied. WPF materializes a button's glyph content as a text element, so the global pass can override the Copy glyph after document load.

- Observation: the current worktree already adds `IsTaskStrikethrough` and recursive user-strikethrough marking, and the RTF adapter strips known task decorations when exporting a task.
  Evidence: `AiteBar/QuickNoteDocumentFormatting.cs` exposes both attached properties and `MarkUserStrikethroughRecursive`; `AiteBar/QuickNoteRtfAdapter.cs` calls `CloneTaskInlineForExport`.

- Observation: the task export clone also inherited `Paragraph.TextDecorations`, which carries the checked-task decoration independently of inline decorations.
  Evidence: the task branch creates its shell through `CreateParagraphShell(taskParagraph)`. Clearing the export shell decoration lets the `[x]` marker recreate only the task decoration on restore.

- Observation: the solution-level Release build exits with a failure summary but zero warnings and zero errors after building the application, while the test project Release build succeeds.
  Evidence: `dotnet build .\\AiteBar.sln -c Release --no-restore` printed `AiteBar -> ...AiteBar.dll` followed by `Build FAILED` with `0 Warning(s)` and `0 Error(s)`; `dotnet build .\\AiteBar.Tests\\AiteBar.Tests.csproj -c Release --no-restore` succeeded.

- Observation: the editor and RTF adapter both hand-copy WPF `Inline` properties, but their property sets diverged.
  Evidence: `QuickNoteRtfAdapter.CloneInlineOrPlainText` now copies typeface and background for `Hyperlink`, while `QuickNoteWindow.Editor.CreateHyperlinkFragment` only copied URI, tag, foreground, and decorations.

- Observation: the default user note is `.aite-note`, but existing service integration tests instantiate `QuickNoteService` with `QuickNote.rtf`.
  Evidence: `QuickNoteService` selects `QuickNote.aite-note` when no path is supplied; the `QuickNoteServiceTests` fixture uses `QuickNote.rtf`.

## Decision Log

- Decision: make the smallest compatible architectural change rather than replace `FlowDocument` with a separate editor model.
  Rationale: Quick Note is an existing WPF rich-text editor with persistence, undo/redo, images, Markdown-compatible formatting, and focused tests. A full model replacement would be a separate feature-sized migration. The immediate defect class is removed by enforcing one renderer boundary within the existing document.
  Date/Author: 2026-08-27 / Codex

- Decision: code-header colors belong to `QuickNoteTheme`, not global constants or a window-wide visual-tree mutation.
  Rationale: code body colors already vary per theme. Header background and glyph colors must travel with the same theme to prevent light-theme headers using dark-theme constants.
  Date/Author: 2026-08-27 / Codex

- Decision: derive the code-header background from the existing code surface role rather than expand every persisted theme record with another literal color.
  Rationale: each current theme already owns `CodeBackground` and `CodeText`. A deterministic derived header role keeps theme data compact while guaranteeing that the header changes with its code surface.
  Date/Author: 2026-08-27 / Codex

- Decision: introduce `QuickNoteDocumentContract` as the sole owner of Inline cloning and recursive theme rendering.
  Rationale: an Inline is a WPF rich-text element such as a run, link, bold span, or inline code span. Both selection edits and persistence duplicate such elements. One contract prevents property sets and visual rules from silently diverging between lifecycle paths.
  Date/Author: 2026-08-27 / Codex

- Decision: keep code blocks native in `.aite-note`; use the portable projection only for task controls and images.
  Rationale: flattening code blocks to fence paragraphs replaced their editor-native structure on every autosave and was the direct source of the runtime regression. XamlPackage can retain the code block, while task controls and image payloads still require reversible persistence markers.
  Date/Author: 2026-08-27 / Codex

## Outcomes & Retrospective

The window chrome no longer overwrites embedded document visuals. Code-header background, label, and Copy glyph are derived from the active code theme through one shared formatter, so creation, reload, and theme change use the same path. Existing task semantic fixes now preserve nested user strike and distinguish user-authored paragraph strike from generated task strike during RTF export.

Base inline rendering for plain text, links, inline code, and code-copy links is now owned by `QuickNoteDocumentFormatting.ApplyInlineTheme`. The window supplies render brushes but does not reimplement inline rules; checklist rendering first invokes that shared path and then applies only the checked-task muted color and strike overlay.

The final document contract is `QuickNoteDocumentContract`. It owns all Inline clone shells and recursive theme traversal. The editor and RTF adapter no longer carry divergent property-copy lists. RTF saves a portable projection; `.aite-note` retains native code-block structure while projecting only task controls and images, then restores those portable elements during loading.

The portable projection now serializes task paragraphs before image processing, so a task with an embedded image retains its task marker without moving a runtime checkbox out of the live document. The shared inline renderer merges the required link underline with existing user decorations instead of replacing them.

Task checkbox events now resolve their owning paragraph through an attached document reference. The old full-document scan remains only as a compatibility fallback for a checkbox that has not yet been connected, so ordinary task clicks do not scale linearly with note length.

Validation completed on 2026-08-27: `dotnet build .\\AiteBar.Tests\\AiteBar.Tests.csproj -c Release --no-restore` succeeded with zero warnings and errors; focused formatting tests passed 25/25; Quick Note tests passed 167/167; non-Quick Note tests passed 1281/1281. A full combined test command exceeded the terminal's 30-second response limit, so the two disjoint filters provide the complete result.

The required solution-level build was also attempted. It produced the repository's existing anomalous failure summary with zero diagnostics after compiling `AiteBar`; the successful test-project build and the two complete test partitions are the usable validation evidence for this change.

## Context and Orientation

`AiteBar/QuickNoteWindow.Presentation.cs` contains `ApplyTheme`, which styles fixed WPF controls in the Quick Note window and calls `ApplyDocumentStyles` for the rich-text `FlowDocument`. A `FlowDocument` is WPF's editable structured text tree. Its blocks include paragraphs and code sections; its inline elements include runs, links, and spans.

`AiteBar/QuickNoteDocumentFormatting.cs` creates and identifies document structures such as code headers, quotes, dividers, and task checkboxes. `AiteBar/QuickNoteTheme.cs` defines the named color roles for each theme. `AiteBar/QuickNoteRtfAdapter.cs` converts an editable document to and from RTF while preserving Quick Note task markers.

The defect occurs because window-wide visual traversal and the document renderer both write foreground colors to document-owned visual elements. The code-header Copy glyph is a runtime `Button` embedded in the document, so it must be styled by document code only.

## Plan of Work

First, change `ApplyTheme` so its fixed-control text pass skips every element below `TxtNote`. Keep document styling in `ApplyDocumentStyles`; this prevents the window chrome from mutating generated controls inside the editor.

Second, add code-header color roles to `QuickNoteTheme` and a single public formatting routine in `QuickNoteDocumentFormatting` that applies the active theme to a code header container. `CreateCodeBlockElement` will construct a header through this routine, and `QuickNoteWindow.Presentation.cs` will call the same routine after load or a theme change. The header label and Copy glyph will therefore receive the same theme-derived colors in both lifecycles.

Third, preserve the existing attached-property task-strikethrough work. Add tests proving nested user-strikethrough survives task conversion and that a checked task exported and restored through the RTF adapter removes only task-generated strike after being unchecked. If an RTF limitation remains, encode the semantic state in the adapter rather than inferring it from brushes.

## Concrete Steps

Run all commands from `D:\01_Codebdbd\01_projects\aitebar`.

1. Edit `AiteBar/QuickNoteTheme.cs`, `AiteBar/QuickNoteDocumentFormatting.cs`, and `AiteBar/QuickNoteWindow.Presentation.cs` as described in Plan of Work. Do not change unrelated dirty worktree files.

2. Add focused tests in `AiteBar.Tests/QuickNoteThemeTests.cs` and `AiteBar.Tests/QuickNoteWindowFormattingTests.cs`. Test the color roles directly in STA where WPF controls are created.

3. Build the test project:

       dotnet build .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

   Expect `Build succeeded` with zero errors.

4. Run focused Quick Note tests:

       dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --filter "QuickNote"

   Expect all selected tests to pass. If WPF test discovery temporarily fails due generated files, run:

       dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

5. Run the full test project after focused tests pass:

       dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build

## Validation and Acceptance

Acceptance requires a light theme and dark theme to render code-header label and Copy glyph from code-header theme roles, not from window body text. Reopening a saved note and changing themes must not turn the glyph into the regular light-theme body color. A nested struck-through run must retain its user strike after task conversion and uncheck. A checked task saved as RTF and restored must regain task styling and lose only task-generated strike after uncheck.

## Idempotence and Recovery

All edits are source-only and can be built and tested repeatedly. The plan does not modify user notes. If a focused test fails, retain the test and adjust only the renderer or adapter responsible for the failing behavior; do not suppress the assertion or revert unrelated dirty worktree changes.

## Artifacts and Notes

Expected final evidence is a successful `dotnet build` for `AiteBar.Tests` and passing focused and full test runs. The final diff should be limited to Quick Note formatting, themes, the RTF adapter, focused tests, and this plan.

## Interfaces and Dependencies

`QuickNoteThemeCatalog` must derive a code-header surface from the existing `CodeBackground` role, while the header label and glyph use `CodeText`. `QuickNoteDocumentFormatting` must expose a routine with the equivalent behavior of:

    ApplyCodeHeaderTheme(BlockUIContainer header, QuickNoteTheme theme)

The routine must update the header grid, its label, and the Copy button without relying on traversal from `QuickNoteWindow`. `QuickNoteWindow.Presentation.cs` is the only caller that supplies the current window theme after loading or changing themes.

Revision 2026-08-27 17:05Z: Created the plan after identifying competing window and document styling paths; recorded the in-progress task-strikethrough and RTF changes already present in the worktree.

Revision 2026-08-27 17:14Z: Completed the renderer-boundary and code-header changes, added regression tests, and recorded split full-suite validation because the terminal limits a single command response to 30 seconds.

Revision 2026-08-27 17:21Z: Consolidated base inline rendering so checklist formatting no longer duplicates link and inline-code color rules.

Revision 2026-08-27 17:24Z: Recorded the solution-level build anomaly observed during final validation.

Revision 2026-08-27 17:31Z: Follow-up review found incomplete Hyperlink cloning and unconditional removal of paragraph-level strike from task RTF export. Added semantic tracking for user paragraph strike, completed hyperlink formatting clone, and added focused regression coverage.

Revision 2026-08-27 18:14Z: Physical package testing showed that XamlPackage drops task controls. Persist both formats from the portable marker projection so task restoration no longer depends on runtime WPF controls surviving serialization.

Revision 2026-08-27 18:24Z: Completed the document-contract migration and verified the physical package round-trip. `dotnet build .\\AiteBar.Tests\\AiteBar.Tests.csproj -c Release --no-restore` succeeded; Quick Note tests passed 171/171 and non-Quick Note tests passed 1281/1281.

Revision 2026-08-27 18:33Z: Fixed task-plus-image projection ordering and preservation of user hyperlink decorations. Focused tests passed 2/2 and the Quick Note suite passed 173/173. Repeated non-Quick Note test attempts exceeded the terminal's 30-second response limit after test start; the preceding full non-Quick Note baseline was 1281/1281.

Revision 2026-08-27 18:45Z: Replaced task-checkbox paragraph scans with an attached owner reference and retained a safe fallback for unconnected legacy controls. The task-focused tests passed 7/7; the Quick Note suite passed 173/173.

Revision 2026-08-27 19:10Z: Production regression review found that the `.aite-note` save path flattened native code blocks to fence paragraphs on every autosave. The save contract now retains native code blocks and projects only task controls and images; the global RichTextBox mouse interception was removed so ordinary clicks place the caret. Targeted regression coverage passed 14/14 and the complete Quick Note suite passed 173/173.
