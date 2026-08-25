# Rebuild Quick Note as a Complete Visual Editor

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan follows `PLANS.md` from the repository root. It replaces the conflicting Quick Note implementations with one complete utility and must be maintained while work proceeds.

## Purpose / Big Picture

Quick Note must be a dependable visual note editor rather than a collection of overlapping experiments. A person can type and paste text, use the toolbar and context menus for all supported formatting, insert links, headings, lists and code blocks, copy code with a familiar glyph, undo and redo, pin the window, switch color themes, resize the window, and reopen the current note without the UI or document structure breaking.

The rebuilt utility deliberately does not import or preserve notes written by earlier implementations. It starts with a clean document and uses one new persisted document format. This removes compatibility branches that previously flattened or destroyed code blocks.

## Progress

- [x] (2026-08-23 01:30+03:00) Confirmed the previous worktree mixed a 2,233-line window implementation, an incomplete split implementation, incompatible tests, RTF persistence and multiple code-copy approaches.
- [x] (2026-08-23 01:35+03:00) Confirmed the architecture against current WPF guidance: one RichTextBox/FlowDocument, routed editing commands, and a narrow persistence adapter at the service boundary.
- [x] (2026-08-23 01:40+03:00) Recorded the explicit product decision that old note data does not need migration or preservation.
- [x] (2026-08-23 13:35+03:00) Downloaded JetBrains Mono 2.304 from the official release, verified the archive SHA-256, embedded Regular and Bold, and distributed OFL-1.1.
- [x] (2026-08-23 14:05+03:00) Replaced the conflicting Quick Note source set with one coherent code-behind and one XAML surface while retaining the command and glyph vocabulary.
- [x] (2026-08-23 14:10+03:00) Implemented native code Sections with a dedicated header Paragraph and copy Hyperlink glyph; removed the overlay and embedded-control approaches.
- [x] (2026-08-23 14:12+03:00) Tested the internal XamlPackage option and then superseded it because the note file must be a normal user-facing RTF document.
- [x] (2026-08-23 00:30Z) Superseded the XamlPackage persistence decision with user-facing atomic RTF persistence at `QuickNote.rtf`.
- [x] (2026-08-23 14:22+03:00) Rebuilt focused tests for formatting, persistence, lifecycle, themes, geometry and code-copy behavior; all 93 Quick Note tests pass.
- [x] (2026-08-23 14:30+03:00) Built Release serially, passed all 1,344 tests, smoke-launched the published application, and generated `AiteBar-Setup-1.15.14.exe` with matching SHA-256.

## Surprises & Discoveries

- Observation: the worktree contained five untracked partial files plus a shortened main window file, while the tracked full window defined the same handlers.
  Evidence: the compiler reported 26 duplicate-member errors when both implementations were present.
- Observation: RTF does not retain the code Section contract reliably enough for this product; previous attempts tried tags, font inference, embedded controls and an external overlay.
  Evidence: screenshots showed code blocks flattening after reopen and copy controls either becoming text or overlapping the first code line.
- Observation: RTF is the required user-facing file format, but it cannot store live WPF document controls such as the copy button.
  Evidence: `AiteBar/QuickNoteRtfAdapter.cs` exports code blocks as readable fenced RTF sections and restores them to visual code sections on load.
- Observation: moving Paragraphs out of a WPF List can leave hidden structural positions in text offsets.
  Evidence: offset-only selection restoration truncated the final two visible characters; restoration now verifies and extends the actual selected text.
- Observation: parallel solution build intermittently returns exit code 1 with zero compiler errors because the WPF graph is reached both directly and through the test project.
  Evidence: both projects build independently and `dotnet build .\AiteBar.sln -c Release -m:1` succeeds with zero warnings and errors.

## Decision Log

- Decision: use one editable RichTextBox with a native FlowDocument.
  Rationale: headings, paragraphs, lists, hyperlinks and Section code blocks then share one caret, selection, clipboard and undo stack.
  Date/Author: 2026-08-23 / Codex
- Decision: persist with `DataFormats.Rtf` to `QuickNote.rtf` and adapt visual code blocks to readable fenced code sections during save/load.
  Rationale: `QuickNote.rtf` is a normal user-facing note file and can be opened through external editors; the app restores fenced sections into the richer Telegram-style visual code block on load.
  Date/Author: 2026-08-23 / Codex
- Decision: represent a code block as a tagged Section whose first Paragraph is a dedicated header containing a native Hyperlink copy glyph and whose remaining Paragraphs contain code.
  Rationale: a real document row cannot overlap code, survives package persistence, and avoids nested controls and visual-coordinate overlays.
  Date/Author: 2026-08-23 / Codex
- Decision: use WPF editing commands for standard actions and narrow custom commands for product-specific transformations.
  Rationale: command routing keeps toolbar, context menu and keyboard behavior consistent and automatically exposes command availability.
  Date/Author: 2026-08-23 / Codex
- Decision: embed JetBrains Mono from the official JetBrains distribution and include OFL-1.1 attribution.
  Rationale: code typography must be deterministic on every installation and legally redistributable.
  Date/Author: 2026-08-23 / Codex
- Decision: themes change only the note background and the shared foreground color. Every dark theme uses one light foreground; every light theme uses black for text and all glyphs. Code blocks use one fixed background and one fixed code foreground in every theme.
  Rationale: this is the explicit theme contract from the user and prevents controls or code styling from drifting independently between themes.
  Date/Author: 2026-08-23 / Codex

## Outcomes & Retrospective

Quick Note now uses one editable FlowDocument, structural code blocks, deterministic bundled code typography and user-facing RTF persistence at `QuickNote.rtf`. Code copy is a glyph in its own document header row and cannot overlap the first code line. Theme tests enforce one shared light foreground for dark themes, black foreground for light themes, and invariant code colors.

Validation completed with 93 focused Quick Note tests and all 1,344 repository tests passing. The serial Release solution build completed with zero warnings and errors. The published application remained alive during a six-second process smoke test. The unsigned installer is `artifacts/installer/AiteBar-Setup-1.15.14.exe`, SHA-256 `048F9141AA6EF6DD41E6B3A293EBE27D298B8D176C0BABB6C326CA6863ECB6A2`; signing was skipped because no certificate was supplied.

## Context and Orientation

`AiteBar/QuickNoteWindow.xaml` is the only visual surface. Its toolbar and menus must expose undo, redo, cut, copy, paste, select all, font size, headings, bold, italic, underline, strikethrough, bullet and numbered lists, link insertion, code block insertion, clear formatting, themes, pinning, opening the saved file, clearing the note and closing the window. Existing localized labels in `AiteBar/Resources/Strings*.resx` remain the source for user-visible text.

`AiteBar/QuickNoteWindow.xaml.cs` owns lifecycle and UI commands. Competing untracked partial implementations were removed so every handler has one definition.

`AiteBar/QuickNoteDocumentFormatting.cs` owns document-element construction and inspection. A FlowDocument is WPF's editable document tree. A Section is a group of Paragraph blocks. The code header is a normal Paragraph in the Section, tagged so copying can exclude it.

`AiteBar/QuickNoteService.cs` owns the single note file and atomic save. Atomic save means writing a complete temporary file before replacing the target, so an interrupted write does not truncate the current note.

`AiteBar.Tests/QuickNote*Tests.cs` verifies non-visual logic and WPF interaction on an STA thread, which is the Windows UI thread model required by WPF controls.

## Plan of Work

First establish a clean source set: keep the public utility entry points used by `QuickNoteUtility`, then replace the Quick Note window, service, contracts, formatting helper and focused tests as one coherent unit. Unrelated dirty files remain untouched.

Build the XAML around one RichTextBox. Standard operations use `ApplicationCommands` and `EditingCommands`; custom controls preserve the existing Fluent and Segoe MDL2 glyphs. The heading selector and overflow menu use real menus/submenus. Every button has a localized tooltip and automation name. The window remains pinnable, remembers geometry, clamps to the active monitor, autosaves after a short debounce, and forces a final save before close.

Build code sections with a native header row. The header contains a right-aligned copy glyph represented by Hyperlink, not Button. The copy handler finds the containing Section and joins only code paragraphs. The header remains separated from the first code line by layout, so overlap is structurally impossible.

Change persistence to atomic RTF at `QuickNote.rtf`. Visual code blocks are saved as readable fenced code sections in the RTF file and restored to visual code sections when Quick Note loads. Loading a missing or invalid RTF file produces a clean editable paragraph. Existing `QuickNote.aite-note` can be read once as a legacy migration source if the new RTF file does not exist.

Download the official JetBrains Mono desktop font archive or source release, retain `OFL.txt`, embed the regular and bold fonts needed by the editor, and reference them through WPF pack font syntax. Do not install the font globally.

Replace focused tests so they match only the new architecture. Tests must cover every formatting command, code header/copy exclusion, RTF close/reopen round-trip, invalid-file recovery, atomic save, pin/close behavior, geometry clamping, black theme foregrounds and JetBrains Mono resource presence.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Download and inspect the official JetBrains Mono distribution, then add font resources and license under `AiteBar/Resources/Fonts/JetBrainsMono`.

Edit the Quick Note source and tests using focused patches. After each milestone run:

    dotnet build .\AiteBar\AiteBar.csproj -c Release --no-restore /p:NuGetAudit=false
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~QuickNote"

At final validation run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If the documented WPF temporary-file failure occurs, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

Then run:

    .\installer\Build-Installer.ps1

## Validation and Acceptance

Open Quick Note and verify that every toolbar glyph, context-menu item and submenu invokes the same semantic action as its keyboard shortcut. Type ordinary text; apply all inline styles; create headings and both list types; insert and open an HTTP link; undo and redo each operation.

Insert one-line and multiline code blocks. The copy glyph must occupy its own header row at the upper right and never overlap text at any window width. Clicking it copies code only. Type, Enter, Backspace, Delete, undo and redo inside code using the same editor caret.

Close and reopen Quick Note. The new document must preserve supported structure and formatting through `QuickNote.rtf`. Corrupting the RTF file must open an empty usable document and report a load failure without crashing.

Switch every theme. Only the window background and shared foreground change. All dark themes use the same light text/glyph color; all light themes use black text and glyphs. Code background, code foreground and embedded JetBrains Mono typography remain identical in every theme without a machine-wide font installation.

The Release build and complete test project must pass. The final installer must exist in `artifacts/installer`, have the project version and a matching SHA-256 entry.

## Idempotence and Recovery

Source replacement is limited to Quick Note files and their focused tests. Existing user note files are never deleted by build or tests. Test services always use temporary paths. Re-running builds, tests and installer generation is safe.

If an edit fails halfway, restore compilation by completing the coherent Quick Note file set rather than mixing old and new implementations. Do not restore unrelated dirty files.

## Artifacts and Notes

The prior visible failures were: copy actions serialized as note text, code Sections flattened by RTF, and overlay buttons positioned from the first glyph and therefore overlapping long code lines. The new architecture removes all three mechanisms rather than adjusting offsets.

## Interfaces and Dependencies

`QuickNoteUtility` continues to construct `QuickNoteService` and `QuickNoteWindow`. `QuickNoteWindow` retains `ShowSimple(AppSettings)` and `SaveNowAsync()` behavior used by tests and utility lifecycle.

`IQuickNotePersistence` continues to expose load, save, external-change detection and open-file actions so the window remains testable. `QuickNoteService` uses `QuickNote.rtf` as the default note file and uses `DataFormats.Rtf` for the live file; `DataFormats.XamlPackage` is used only to read a legacy `.aite-note` file during one-time migration.

`QuickNoteDocumentFormatting` must expose code construction, code text extraction, code/header recognition, heading sizing, link validation and list-marker mapping without depending on a Window instance.

JetBrains Mono files are application resources declared in `AiteBar/AiteBar.csproj`; no new NuGet package or runtime dependency is required.

Revision note (2026-08-23): replaced the previous completed-but-conflicted plan after the user explicitly requested a full Quick Note rebuild, no old-note preservation, best-practice WPF architecture and embedded JetBrains Mono.

Revision note (2026-08-23): tightened the theme contract so themes affect only background and shared foreground, while code styling is invariant.
