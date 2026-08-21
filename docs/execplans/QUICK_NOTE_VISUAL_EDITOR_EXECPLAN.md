# Replace Quick Note With a Single Visual Editor

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan follows `PLANS.md` from the repository root. It replaces the current hybrid Quick Note editor with one visual editing surface and must be maintained while work proceeds.

## Purpose / Big Picture

Quick Note must behave like a reliable visual note editor. A person must be able to click anywhere, place the caret, type, select, delete, format, clear the note, undo, redo, and reopen the same visually formatted note. Code is a styled part of the same document, not a nested editor or a Markdown source view.

The observable result is a Quick Note window whose formatting toolbar changes the appearance of the active document and whose code blocks accept normal RichTextBox input. Existing notes must remain readable through a one-time migration path.

## Progress

- [x] (2026-08-21 14:00Z) Identified the architecture failure: code blocks are nested TextBox controls inside a RichTextBox, producing separate focus, selection, command and save paths.
- [x] (2026-08-21 14:00Z) Created this ExecPlan and recorded that the working tree contains incomplete Quick Note changes which must be assessed rather than assumed correct.
- [x] (2026-08-21 14:20Z) Replaced Markdown persistence with a single RTF visual-document format; there is no legacy import or second on-disk format.
- [x] (2026-08-21 14:20Z) Removed nested code-block controls and made code blocks native styled FlowDocument sections.
- [x] (2026-08-21 15:25Z) Removed the obsolete Markdown parser/serializer and nested BlockUIContainer compatibility branch. Added visual-document focused tests for native editable code sections, headings and safe links.
- [x] (2026-08-21 15:25Z) Built a clean source snapshot and passed the focused Quick Note suite: 135 tests passed.
- [x] (2026-08-21 15:45Z) Closed release-review findings: RTF-reloaded code sections receive the active visual code style, an external file lock becomes a safe conflict path, and list-marker removal preserves caret mapping. Focused tests pass and the Release executable starts successfully.
- [ ] Manually verify caret entry, Enter, Backspace/Delete, Ctrl+Z/Y, clear and reopen in the running WPF application.
- [ ] Build Release, run the full test suite, manually verify Quick Note, and assemble a new installer.

## Surprises & Discoveries

- Observation: the current code block is a BlockUIContainer containing a Border, Grid and TextBox, while ordinary text belongs to the parent RichTextBox FlowDocument.
  Evidence: `AiteBar/QuickNoteMarkdown.cs` creates `CodeBlockContainer`; the copy button reads that child TextBox directly.
- Observation: the current code insertion command returns focus to the parent RichTextBox after creating the child control.
  Evidence: `AiteBar/QuickNoteWindow.xaml.cs`, `BtnCode_Click` calls `TxtNote.Focus()` after insertion.
- Observation: the test output directory is intermittently held by an external process. The main application can be built after clearing its obj folder, while the test project may require an isolated verification copy.
  Evidence: `CS2012` access denied for `AiteBar.Tests/obj/Release/.../AiteBar.Tests.dll`.

## Decision Log

- Decision: Quick Note will use one visual FlowDocument editor and will not expose Markdown as a user-facing editing mode.
  Rationale: the requested product is a readable, visual quick note; markup syntax and nested editors create user-visible failure modes without providing needed value.
  Date/Author: 2026-08-21 / Codex
- Decision: code blocks will be styled Section and Paragraph blocks in the same FlowDocument.
  Rationale: native document blocks share the RichTextBox caret, selection, deletion, clipboard and undo/redo implementation.
  Date/Author: 2026-08-21 / Codex
- Decision: Quick Note uses RTF exclusively and deliberately does not migrate old Markdown notes.
  Rationale: the user explicitly rejected backward-compatibility paths; retaining a second file format would recreate the hybrid architecture being removed.
  Date/Author: 2026-08-21 / Codex

## Outcomes & Retrospective

The Quick Note implementation uses one RTF-backed visual FlowDocument and no longer contains a Markdown persistence or nested-editor path. The focused suite passes and the Release executable has completed a startup smoke test. Final release status still requires the running-window manual scenarios and the complete test suite.

## Context and Orientation

`AiteBar/QuickNoteWindow.xaml` contains the Quick Note window and its one RichTextBox named `TxtNote`. `AiteBar/QuickNoteWindow.xaml.cs` handles toolbar commands, autosave, undo/redo and theme application. `AiteBar/QuickNoteMarkdown.cs` currently converts between Markdown and FlowDocument and contains the defective nested code editor. `AiteBar/QuickNoteService.cs` owns the persisted note and conflict-copy behavior. `AiteBar.Tests/QuickNote*Tests.cs` contains focused tests.

A FlowDocument is WPF's in-memory document tree. A Section is a native group of document blocks; a Paragraph is a native editable text block. Both are edited by the same RichTextBox. A nested TextBox is a separate input control and must not be embedded in this editor.

## Plan of Work

Quick Note persists a single RTF payload that stores its FlowDocument without reading UI controls. Old Markdown files are not imported and do not participate in the runtime path.

Replace code-block construction with a tagged Section containing Paragraph children. Apply the code background, border, typography and padding through native FlowDocument properties. Update toolbar code insertion to place the RichTextBox caret inside the new Section. Update clear, theme and serialization paths so every action addresses the same FlowDocument.

Add tests that edit code content through document blocks, serialize and reload it, clear the whole document, and validate legacy-note migration. Then run the Release build and full tests. Because WPF temporary outputs can be externally locked, use the project safe build command after stopping build servers; if the test output remains externally locked, run the same source snapshot in a clean temporary directory and record the evidence.

## Concrete Steps

From the repository root `D:\01_Codebdbd\01_projects\aitebar`, inspect:

    rg -n "QuickNoteService|Load.*Note|Save.*Note|QuickNoteMarkdown" AiteBar AiteBar.Tests -g "*.cs"

After implementation, run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    .\installer\Build-Installer.ps1

## Validation and Acceptance

Open Quick Note and create a heading, a list, ordinary text and a code block. Click in the middle of code and insert text; use Enter, Backspace, Delete, Ctrl+Z and Ctrl+Y. Clear the document through the menu, then reopen the note. The note must save visual formatting and no formatting command may target a nested or hidden editor.

Focused automated tests must prove native code-block document editing and RTF persistence. The full test project must pass before release. The installer must appear in `artifacts/installer`.

## Idempotence and Recovery

A failed save must keep the active in-memory document and preserve the existing conflict-copy behavior. Generated `bin`, `obj` and `artifacts` directories may be deleted and regenerated; source notes must never be removed by build scripts.

## Artifacts and Notes

The current code-block failure is reproducible without special input: insert a multiline or empty code block, then click within it. The nested TextBox event routing prevents the parent document editor from providing one reliable caret and command model.

## Interfaces and Dependencies

The final implementation must retain `QuickNoteWindow` as the single UI surface and `QuickNoteService` as the persistence boundary. No browser editor, Markdown editor, nested TextBox, BlockUIContainer or UI-tree traversal may be used for code-block editing or persistence.

Revision note (2026-08-21): created after user redirected the task from Markdown compatibility to a visual quick-note product.
