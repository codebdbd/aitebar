# Interactive Quick Note Code Blocks

This ExecPlan is a living document. It follows `PLANS.md` from the repository root. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be updated while implementing the work.

## Purpose / Big Picture

Quick Note code blocks must look and behave like compact code components rather than dark paragraphs. A user can insert a block, identify its language, copy its contents, collapse long code, and see line numbers when the block is code rather than plain text. Closing and reopening the note must not lose its text or turn it into a list.

## Progress

- [x] (2026-08-24 00:25Z) Inspected current visual block, insertion path, and package/RTF restoration path.
- [ ] Add a persisted code-block metadata model and fence serialization that is safe for repeated package saves.
- [ ] Add header language selection, copy action, collapse action, and code-only line numbers.
- [ ] Add focused persistence and visual-structure tests.
- [ ] Build, test, and manually verify opening, copying, language selection, collapse, and repeat reload.

## Surprises & Discoveries

- Observation: `XamlPackage` drops `Section.Tag` and converts the header `BlockUIContainer` to paragraphs.
  Evidence: the existing regression test observed a loaded Section with `Tag=<null>` and three paragraphs.
- Observation: the current persisted discriminator is the JetBrains Mono `FontFamily`, not a reliable semantic record.
  Evidence: `QuickNoteRtfAdapter.NormalizeCodeBlocks` recognizes package-loaded sections using their code font.

## Decision Log

- Decision: Store code-block metadata in private fence paragraphs during persistence rather than depending on WPF `Tag` serialization.
  Rationale: fence paragraphs survive both RTF and XamlPackage and allow restoring text, language, and collapsed state deterministically.
  Date/Author: 2026-08-24 / Codex.
- Decision: Show line numbers only after a language other than `txt` is selected.
  Rationale: a plain textual fragment should remain visually simple, matching the approved reference.
  Date/Author: 2026-08-24 / Codex.

## Outcomes & Retrospective

Implementation is in progress. This section will record the verified behavior and any remaining limitations after tests and manual checks.

## Context and Orientation

`AiteBar/QuickNoteWindow.xaml.cs` owns editor commands. `BtnCode_Click` replaces the selection with a `Section`. `AiteBar/QuickNoteDocumentFormatting.cs` creates the visual Section, including its header and copy button. `AiteBar/QuickNoteRtfAdapter.cs` converts a code Section to private fence paragraphs for RTF and restores those paragraphs on load. `AiteBar/QuickNoteService.cs` owns `.aite-note` package persistence.

A code fence is a pair of private paragraphs that ordinary users never see. Between them are one paragraph per code line. It is used because WPF serializes visual controls inconsistently, while ordinary paragraph text is stable.

## Plan of Work

Extend the fence start marker to contain a versioned, encoded language identifier and collapsed flag. Update the restore path to parse both the legacy marker and the extended marker, then pass the recovered metadata to the code-block factory. Add a metadata holder that is recoverable after load, and render the header from that holder.

In `QuickNoteDocumentFormatting.cs`, create code body rows with a fixed-width line-number column only when the language is not `txt`. The header has a language menu, existing copy button, and an expand/collapse button. Collapse hides the body and changes the chevron. Header actions update metadata, mark the note dirty, and preserve focus.

Use a package export document that converts only code sections to fences while preserving all non-code blocks and embedded images through XamlPackage cloning. On load, restore fences before applying themes. Add tests for text, language, collapse state, code lines, image survival, and two consecutive package save/load operations.

## Concrete Steps

Run from `D:\01_Codebdbd\01_projects\aitebar`:

    dotnet build .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-restore
    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll --TestCaseFilter:"FullyQualifiedName~QuickNote"

The expected result is a successful Release build and all selected Quick Note tests passing.

## Validation and Acceptance

Insert a code block from selected text. The header initially says `txt`, copying copies exactly the visible text, and no line numbers are shown. Select a programming language from the header: a line-number column appears. Collapse the block: the body hides and the chevron changes. Close and reopen the note twice: language, collapsed state, code text, and normal text retain their form; images remain visible.

## Idempotence and Recovery

Build and tests are safe to repeat. Persistence writes atomically through the existing `QuickNoteService`; failed writes retain the previous note. Legacy `v2` and Markdown-style code fences remain readable and default to `txt` and expanded state.

## Artifacts and Notes

The focused regression tests will be in `AiteBar.Tests/QuickNoteImageHelperTests.cs` and a new code-block formatting test file if isolation improves clarity.

## Interfaces and Dependencies

No external library is required. The implementation uses WPF `Section`, `Grid`, `TextBlock`, `Button`, and `ContextMenu`, and existing `QuickNoteTheme`, `QuickNoteService`, and `QuickNoteRtfAdapter` types. The final code must preserve the existing `CreateCodeBlockElement(string, QuickNoteTheme)` call site by providing compatible defaults or updating every caller.

Plan revision 2026-08-24: created after discovering that the visual WPF code-block structure is not a persistence format.
