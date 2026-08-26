# Add Interactive Checklists to Quick Note

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. It follows `PLANS.md` in the repository root.

## Purpose / Big Picture

Quick Note is a fast, lightweight single-note scratchpad in AiteBar. Users frequently use it for daily task management and to-do items. Currently, Quick Note supports bulleted and numbered lists, but lacks interactive checklists with toggleable states and strikethrough styling.

After this work, a user can create interactive checklists (`[ ]` / `[x]`) using a new toolbar button, pressing `Ctrl+Shift+C`, or typing `[ ] ` or `[x] ` at the start of a line. Clicking a checkbox toggles its state with immediate strikethrough and muted foreground text styling. Pressing `Enter` automatically creates a new checklist item on the next line or exits list mode if empty. All checklist items are saved seamlessly in `.aite-note` and exported/imported cleanly in RTF and Markdown formats.

## Progress

- [x] (2026-08-26 15:58Z) Approved proposal and created implementation plan.
- [x] (2026-08-26 16:00Z) Implement task tags and contract definitions in `QuickNoteContracts.cs`.
- [x] (2026-08-26 16:02Z) Implement task item element creation, formatting, strikethrough, and removal in `QuickNoteDocumentFormatting.cs`.
- [x] (2026-08-26 16:03Z) Update `QuickNoteRtfAdapter.cs` and `QuickNoteService.cs` to serialize and restore interactive task items.
- [x] (2026-08-26 16:04Z) Add toolbar button and localization strings in `QuickNoteWindow.xaml`, `Strings.resx`, `Strings.ru.resx`, `Strings.de.resx`, and `Strings.uk.resx`.
- [x] (2026-08-26 16:05Z) Implement keybindings (`Ctrl+Shift+C`, `Ctrl+Shift+L`, `Ctrl+Shift+9`, `Enter`, `Back`), mouse click handlers, and typing auto-conversion in `QuickNoteWindow.Editor.cs` and `QuickNoteWindow.xaml.cs`.
- [x] (2026-08-26 16:05Z) Add comprehensive unit tests in `AiteBar.Tests/QuickNoteTaskItemTests.cs` and update `QuickNoteFormattingControlsTests.cs`.
- [x] (2026-08-26 16:06Z) Run full test suite (1389 passed, 0 failed) and verify build in Release mode.

## Surprises & Discoveries

- Observation: When restoring task items from markdown-style plain text paragraphs into a `FlowDocument`, mutating paragraph inlines invalidates `document.Blocks` enumerators.
  Evidence: Adding `.ToList()` to `document.Blocks.ToList()` in `RestoreTaskItems` resolved `InvalidOperationException` during document block traversal.
- Observation: Default WPF `Run` text decoration collections may be empty rather than null.
  Evidence: Unit tests asserting strikethrough state check for collection containing `TextDecorationLocation.Strikethrough` rather than null reference.

## Decision Log

- Decision: Use `InlineUIContainer` with an accessible `CheckBox` at the start of the `Paragraph` as the primary interactive element.
  Rationale: WPF `RichTextBox` supports inline elements cleanly. Using `Focusable = false` on the checkbox ensures clicking it toggles the task without stealing focus or closing unpinned overlay windows.
  Date/Author: 2026-08-26 / Codex.
- Decision: Save and export task items as markdown `[ ] ` and `[x] ` in text/RTF exports.
  Rationale: Maintains 100% compatibility with external text editors, clipboard copy-paste, and markdown tools without leaking internal UI metadata.
  Date/Author: 2026-08-26 / Codex.
- Decision: Add both toolbar button, context menu item, and shortcuts (`Ctrl+Shift+C`, `Ctrl+Shift+L`, `Ctrl+Shift+9`), as well as typing auto-conversion when typing `[ ] ` or `[x] ` at the beginning of a line.
  Rationale: Gives both mouse and keyboard-heavy users zero-friction checklist capture.
  Date/Author: 2026-08-26 / Codex.

## Outcomes & Retrospective

Interactive checklists are fully implemented and verified in Quick Note:
- Users can create task items via toolbar button (`BtnTaskList`), context menu, shortcut (`Ctrl+Shift+C` / `Ctrl+Shift+L` / `Ctrl+Shift+9`), or typing `[ ] ` / `[x] ` at line start.
- Toggling a task item immediately strikes through and mutes the text or restores normal text styling upon unchecking.
- Pressing `Enter` continues list creation; pressing `Enter` on an empty task item or `Backspace` at line start removes the checkbox cleanly.
- Task items round-trip through `.aite-note` (XamlPackage) and export to RTF / plain text as readable `[ ] ` and `[x] ` markdown items.
- All 1389 unit tests pass in Release configuration with 0 compilation warnings.
