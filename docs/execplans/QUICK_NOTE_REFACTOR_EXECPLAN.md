# Refactor and Solidify Quick Note Architecture (Architecture & Formatting Stabilization)

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

The goal of this plan is to eliminate technical debt, formatting bugs, and architectural antipatterns in Quick Note (`QuickNoteUtility`), transforming it into a robust, clean, and reliable edge-window note editor.

After this refactoring:
1. Quick Note maintains 100% fidelity across saves, reloads, and window reopens: checkboxes no longer get permanently stuck with strikethrough after saving/reloading; font hierarchies (headers H1-H3 vs body) are preserved accurately without line-height clipping; and quote/list blocks do not degrade.
2. The user experience is predictable: the toolbar reflects the current selection context (reactive `ToggleButton` states for bold, italic, underline, strikethrough, headings, and lists); pasting external rich text strips foreign typography while preserving structural formatting and matching current theme colors; and keyboard formatting commands operate deterministically without caret jumps or corrupting undo stacks.
3. The codebase is decoupled into maintainable, testable layers: UI shell/chrome (`QuickNoteWindow`), editor view-model and command pipeline (`IQuickNoteEditorCommand`, `QuickNoteEditorViewModel`), document styling/sanitization engine (`QuickNoteStyleEngine`), and persistent storage (`QuickNoteDocumentCodec`).

## Progress

- [x] (2026-10-01 08:24Z) Milestone 1: Fix critical typography & formatting rendering bugs (LineHeight clipping to NaN, Paragraph vs Run heading consistency, task strikethrough persistence).
- [x] (2026-10-01 08:28Z) Milestone 2: Implement sanitized clipboard ingestion (strip external fonts/colors/margins while retaining semantic tags in QuickNoteClipboardSanitizer).
- [x] (2026-10-01 08:31Z) Milestone 3: Introduce reactive toolbar & command pipeline (FormatToggleButtonStyle, ToggleButton bindings, UpdateToolbarFormattingState on selection changes).
- [x] (2026-10-03 05:40Z) Milestone 3.1: Complete remaining practical formatting fixes:
  - Proportional A+/A- font scaling (`ApplyFontSizeDeltaToSelection`) preserving relative size delta across headings and body runs without collapsing.
  - Toolbar active highlighting for Bullet, Numbered, and Task lists via `ToggleButton` bindings and context detection.
  - Quote block RTF round-trip serialization via private fence tokens (`\uE000AiteBar:quote:v1:start/end\uE001`), `RestoreQuoteBlocksFromFences`, and `NormalizeQuoteBlocks`.
- [x] (2026-10-03 06:05Z) Milestone 4: Decompose Editor God View:
  - Extracted `QuickNoteListHelper` for list discovery, item unwrapping, and range intersection.
  - Extracted `QuickNoteHyperlinkHelper` for link insertion, fragmenting, inline range cloning, and unwrap operations.
  - Extended `QuickNoteTaskListController` with `HandleEnterKey` and `HandleBackspaceKey` to centralize all task item keyboard and checkbox operations.
  - Reduced `QuickNoteWindow.Editor.cs` by over 500 lines of complex FlowDocument AST manipulations into testable helper classes with dedicated unit test suites (`QuickNoteListHelperTests`, `QuickNoteHyperlinkHelperTests`).
- [x] (2026-10-03 06:07Z) Milestone 5: Regression testing, backward-compatibility validation, and full release verification:
  - Full test suite passes: 1,664 passed, 0 failed.
  - Release build clean: 0 warnings, 0 errors.
  - Full Inno Setup installer package built cleanly via `.\installer\Build-Installer.ps1`, producing `artifacts\installer\AiteBar-Setup-1.15.25.exe` and `SHA256SUMS.txt`.

## Surprises & Discoveries

- Observation: Setting `DependencyProperty.UnsetValue` on a `TextRange.ApplyPropertyValue` call throws ArgumentException for NamedObject.
  Evidence: `TextRange.ApplyPropertyValue` requires concrete property values; local value overrides on inlines must instead be cleared via `inline.ClearValue(TextElement.FontSizeProperty)`.
- Observation: DataObject in WPF code-behind was ambiguous with Windows Forms DataObject when System.Windows.Forms is referenced.
  Evidence: Explicitly qualification as `System.Windows.DataObject` is required.
- Observation: FontStyle was ambiguous with System.Drawing.FontStyle in Editor.cs.
  Evidence: Disambiguated as `System.Windows.FontStyle`.
- Observation: Proportional font resizing must distinguish paragraph-level heading sizes from inline run-level overrides.
  Evidence: When selection covers both an H1 paragraph (24px) and body paragraph (13px), scaling each element's own local size preserves the heading/body delta without collapsing both to an identical flat size.
- Observation: WPF RTF serializer flattens custom Section borders and backgrounds.
  Evidence: Quote blocks serialized as RTF lost their left border and background on reload; introducing fence tokens `QuoteFenceStart`/`End` matching `CodeFenceStart`/`End` achieves 100% fidelity round-trip in RTF files.
- Observation: In WPF FlowDocument, TextRange on a ListItem automatically prefixes the item marker ("•\t") into the returned text.
  Evidence: Unit tests reading plain text from a ListItem must inspect the inner Paragraph or Run directly.

## Decision Log

- Decision: Retain WPF `FlowDocument` / `RichTextBox` as the rendering surface rather than switching entirely to plain-text `TextBox` or introducing third-party AvalonEdit.
  Rationale: Quick Note's core product contract in `AiteBar` relies on inline rich elements (checkboxes as clickable UI elements, clickable links, inline images, and mixed formatting). Preserving `FlowDocument` honors existing UI/UX contracts and file formats while fixing the internal modeling flaws.
  Date/Author: 2026-10-01 / Antigravity

- Decision: Separate structural styling (`Paragraph.FontSize`, `Paragraph.Margin`) from inline formatting (`Run.FontWeight`, `Run.FontStyle`, `Run.TextDecorations`).
  Rationale: Current code set font size on `Paragraph` for markdown triggers (`# `) but on `Run` for `Ctrl+1..3`, leading to colliding inheritance rules and breaking normal text reset (`Ctrl+0`). Headings must consistently be paragraph-level attributes.
  Date/Author: 2026-10-01 / Antigravity

- Decision: Replace static `LineHeight="20"` in `QuickNoteWindow.xaml` with dynamic line spacing (`LineHeight="NaN"` / proportional line spacing).
  Rationale: Hardcoded 20px line height causes text clipping on glyph ascenders and descenders when headings are 24px or 32px.
  Date/Author: 2026-10-01 / Antigravity

- Decision: Use private Unicode area tokens (`\uE000...`) for Quote RTF serialization.
  Rationale: Mirrors proven code-fence strategy, avoids collision with user text (`> `), and preserves full quote block geometry and themes across RTF file reloads.
  Date/Author: 2026-10-03 / Antigravity

- Decision: Extract FlowDocument AST manipulation into focused helpers (`QuickNoteListHelper`, `QuickNoteHyperlinkHelper`, `QuickNoteTaskListController`) rather than rewriting window chrome into custom user controls.
  Rationale: Complies with AGENTS.md rules to maintain existing lightweight architecture and prevent window lifecycle/pin/DPI regressions.
  Date/Author: 2026-10-03 / Antigravity

## Outcomes & Retrospective

All milestones of the Quick Note refactoring plan have been successfully implemented and verified:
1. **Visual & Rendering Quality**: Headings now have proper vertical line-height spacing without clipping; Heading 1..3 and reset to body consistently operate at the paragraph level; task checkbox strikethrough survives RTF reload without losing user formatting; quote blocks round-trip through RTF with intact left border and theme colors; proportional A+/A- font scaling maintains hierarchical deltas between headings and body text.
2. **Robust Ingestion**: Clipboard paste is sanitized of hostile/alien font families, fixed font sizes, and external web margins, preserving semantic bold/italic/underline/strikethrough and clean plain text.
3. **Reactive UI**: Toolbar formatting buttons (Bold, Italic, Strikethrough, Underline, Bullet, Numbered, TaskList) now provide live visual feedback reflecting the current caret and selection context.
4. **Clean Architecture**: Over 500 lines of complex FlowDocument tree manipulations (list unwrapping, hyperlink fragmentation, task keyboard enter/backspace handling) have been decoupled from `QuickNoteWindow.Editor.cs` into focused, unit-tested helper and controller classes.
5. **Quality Assurance**: 1,664 automated tests pass with 0 failures; the release build compiles with 0 warnings and 0 errors; the full self-contained installer builds cleanly.

## Context and Orientation

Quick Note is located in `AiteBar`:
- `AiteBar/QuickNoteWindow.xaml` and `AiteBar/QuickNoteWindow.xaml.cs`: Window chrome, pin state, edge clamping, event routing.
- `AiteBar/QuickNoteWindow.Editor.cs`: Editing commands, keyboard input hooks, markdown auto-conversions, font scaling (~1,800 lines).
- `AiteBar/QuickNoteWindow.Presentation.cs`: Themes, zoom, link hover tracking, visual chrome (~700 lines).
- `AiteBar/QuickNoteDocumentFormatting.cs`: Regex parsing, task lists, formatting heuristics, strike-through decorations (~814 lines).
- `AiteBar/QuickNoteDocumentCodec.cs` & `AiteBar/QuickNoteRtfAdapter.cs`: Package serialization, RTF import/export.
- `AiteBar/QuickNoteFileStore.cs` & `AiteBar/QuickNoteService.cs`: Disk persistence, hashing, conflict copies.

Tests reside in:
- `AiteBar.Tests/QuickNote*Tests.cs` (e.g. `QuickNoteDocumentCodecTests.cs`, `QuickNoteServiceTests.cs`, etc.).

## Plan of Work

### Milestone 1: Fix Critical Typography & Formatting Bugs
1. **Fix LineHeight Clipping:**
   - In `AiteBar/QuickNoteWindow.xaml`, remove or update `<Setter Property="LineHeight" Value="20"/>` in `FlowDocument` style to permit font-proportional line height (`LineHeight="NaN"` or calculate line height based on font size).
2. **Standardize Heading Application:**
   - In `AiteBar/QuickNoteWindow.Editor.cs`, unify `ApplyHeading` logic. `Ctrl+1`, `Ctrl+2`, `Ctrl+3`, and `# ` markdown expansions must all set block-level properties (`Paragraph.FontSize`, `Paragraph.Margin`) and clear inline font-size overrides on child `Run`s within that paragraph.
   - Ensure `ApplyHeading(0)` / `Ctrl+0` resets the paragraph to default body size (`13.0`) and default body margin cleanly.
3. **Fix Task Checkbox Strikethrough Round-Trip Bug:**
   - In `AiteBar/QuickNoteDocumentFormatting.cs` and `AiteBar/QuickNoteRtfAdapter.cs`, ensure that completion strikethrough is treated as a visual derivation of the task checkbox state (`task.IsChecked == true`) rather than a permanent user-defined inline decoration.
   - When a note is deserialized, if a task paragraph has a checked box, its strikethrough must be toggled off if the user unchecks the box, without being blocked by `IsUserStrikethroughProperty`.

### Milestone 2: Sanitized Clipboard Ingestion
1. **Implement `ClipboardSanitizer`:**
   - Create `AiteBar/QuickNoteClipboardSanitizer.cs`.
   - In `TxtNote_Pasting` (`AiteBar/QuickNoteWindow.xaml.cs`):
     - If clipboard contains an image, retain existing image insertion.
     - If clipboard contains HTML or RTF, parse and sanitize: strip font-family, hardcoded font sizes, text foreground/background colors, and external margins/paddings. Retain only structural tags (`<b>`, `<i>`, `<u>`, `<s>`, `<a>`, `<code>`, `<li>`, `<p>`).
     - Insert clean inlines/blocks formatted with the current theme foreground and typography.
     - If plain text, insert as standard text without rich styling.

### Milestone 3: Reactive Toolbar & Command Pipeline
1. **Model Selection State:**
   - Create `QuickNoteSelectionState`:
     - Properties: `IsBold`, `IsItalic`, `IsUnderline`, `IsStrikethrough`, `HeadingLevel` (0..3), `IsBulletList`, `IsNumberedList`, `IsTaskList`, `CanUndo`, `CanRedo`.
2. **Update Toolbar XAML:**
   - In `AiteBar/QuickNoteWindow.xaml`, convert formatting `Button`s to `ToggleButton`s (or bind `IsChecked` where appropriate) using standard WPF commands or bound view-model properties.
3. **Command Execution:**
   - Centralize formatting mutations into cohesive command handlers that track atomic undo batches.

### Milestone 4: Code-Behind Decoupling
1. **Separate Window Chrome from Editor Surface:**
   - Retain window lifecycle, edge snapping, DPI handling, and monitor clamping in `QuickNoteWindow.xaml.cs`.
   - Move document editing, input handling, and toolbar state logic to a dedicated controller / view-model (`QuickNoteEditorController`).
   - Clean up `QuickNoteWindow.Editor.cs` and `QuickNoteWindow.Presentation.cs` by delegating to this controller.

### Milestone 5: Verification & Regression Testing
1. Add focused unit tests covering:
   - Line height calculation for H1/H2/H3.
   - Task item unchecking after deserialization.
   - Clipboard sanitization with dirty HTML/RTF input.
   - Font size hierarchy preservation during selection formatting.
2. Build Release and execute tests via `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`.
3. Verify panel on all 4 edges, pin toggle, and note persistence.

## Concrete Steps

1. Run build and existing tests to confirm clean baseline:
   ```powershell
   dotnet build .\AiteBar.sln -c Release
   dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
   ```
2. Implement Milestone 1 changes in `QuickNoteWindow.xaml`, `QuickNoteWindow.Editor.cs`, and `QuickNoteDocumentFormatting.cs`.
3. Add unit tests for Milestone 1 in `AiteBar.Tests/QuickNoteDocumentFormattingTests.cs`.
4. Implement Milestone 2 (`QuickNoteClipboardSanitizer.cs` and tests).
5. Implement Milestone 3 (Toolbar bindings and commands).
6. Implement Milestone 4 (Decoupling code-behind).
7. Final validation run:
   ```powershell
   dotnet build .\AiteBar.sln -c Release
   dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
   ```

## Validation and Acceptance

- **LineHeight & Typography:** Setting a line to H1 (24px) or H2 (18px) visually renders all glyphs without top/bottom cropping.
- **Task List Persistence:** Creating a task `[ ] Item`, checking it (becomes strikethrough), saving, closing, reopening, and unchecking it removes the strikethrough completely.
- **Paste Sanitization:** Pasting colored/styled text from a browser renders in the active Quick Note theme's font and color, preserving bold/italic/links but discarding external fonts and colors.
- **Test Suite:** All existing tests and newly added unit tests pass with zero failures.

## Idempotence and Recovery

- All file changes are source-code refactorings tracked in Git.
- Storage format maintains backward compatibility with existing `.aite-note` (XamlPackage) and `.rtf` files created by older versions of AiteBar.
- In case of failure, changes can be rolled back cleanly via git without data loss.
