# Decompose the Quick Note window code-behind

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. It is maintained under `PLANS.md` in the repository root.

## Purpose / Big Picture

Quick Note must remain a fast, single-window note editor while its implementation becomes easier to change safely. After this work, opening, editing, formatting, importing, exporting, pinning, saving, and closing a note behaves exactly as before, but the code behind the WPF window is grouped by responsibility instead of being concentrated in one large source file. This is observable by running the Quick Note tests: the same user scenarios pass after methods have moved into focused partial-class files.

## Progress

- [x] (2026-08-24 00:00Z) Inspected `QuickNoteWindow.xaml.cs`, its XAML event bindings, persistence services, and Quick Note tests.
- [x] (2026-08-24 00:00Z) Ran the focused `QuickNote` suite; 116 tests passed.
- [x] (2026-08-24 00:00Z) Moved editor commands, formatting, links, images, lists, and document edits to `AiteBar/QuickNoteWindow.Editor.cs`.
- [x] (2026-08-24 00:00Z) Moved Markdown/text import and export commands to `AiteBar/QuickNoteWindow.Exchange.cs`.
- [x] (2026-08-24 00:00Z) Moved theme styling, hit testing, clipboard actions, and geometry persistence to `AiteBar/QuickNoteWindow.Presentation.cs`.
- [x] (2026-08-24 00:00Z) Rebuilt the test project and ran the focused Quick Note suite with `vstest`; 116 tests passed.
- [ ] Run the complete test suite and manually verify the Quick Note window lifecycle (the full `vstest` run did not produce a result within the desktop tool's 30-second command window).

## Surprises & Discoveries

- Observation: `QuickNoteWindow.xaml.cs` has 2,442 physical lines and owns lifecycle, autosave, editor commands, visual styling, Markdown exchange, clipboard interactions, and geometry persistence.
  Evidence: the class begins at `AiteBar/QuickNoteWindow.xaml.cs:24`; source inspection found these method groups in one partial class.
- Observation: XAML binds event handlers by method name, so moving private handlers to another partial file preserves the binding and does not require XAML changes.
  Evidence: all files compile into the same `QuickNoteWindow` type.

## Decision Log

- Decision: use WPF partial classes rather than introduce new service abstractions in this refactor.
  Rationale: the goal is to reduce file size without changing UI behavior or expanding the dependency graph. The moved methods continue accessing the existing private fields and named XAML controls through the same compiled class.
  Date/Author: 2026-08-24 / Codex
- Decision: keep construction, window lifetime, keyboard routing, autosave coordination, and status state in `QuickNoteWindow.xaml.cs`.
  Rationale: these methods coordinate the other partial files and form the stable lifecycle boundary.
  Date/Author: 2026-08-24 / Codex

## Outcomes & Retrospective

The window code-behind is now divided into four files. `AiteBar/QuickNoteWindow.xaml.cs` is reduced from 2,754 to 716 lines and retains construction, lifecycle, autosave, loading, keyboard routing, status state, and disposal. `AiteBar/QuickNoteWindow.Editor.cs` contains 1,066 lines of document-editing commands, `AiteBar/QuickNoteWindow.Exchange.cs` contains 128 lines of import/export commands, and `AiteBar/QuickNoteWindow.Presentation.cs` contains 610 lines of themes, hit testing, clipboard, and geometry behavior.

No method body, XAML handler name, data format, or persistence API changed. The focused test suite passed after the move. The standalone Release build was blocked by the existing unavailable NuGet vulnerability feed (`NU1900`) even with `--no-restore`; the focused test command did compile both projects before running its tests.

## Context and Orientation

`AiteBar/QuickNoteWindow.xaml` defines the Quick Note WPF controls and names its event-handler methods. `AiteBar/QuickNoteWindow.xaml.cs` is the current code-behind class. A WPF partial class is a C# class split over several files; the compiler joins all parts into one type, so private fields and XAML-generated controls remain available after a method moves.

`AiteBar/QuickNoteService.cs` stores the note atomically and tracks external file edits. `AiteBar/QuickNoteDocumentFormatting.cs`, `QuickNoteMarkdownExchange.cs`, and `QuickNoteImageHelper.cs` contain reusable document helpers. `AiteBar.Tests/QuickNote*.cs` contains the focused tests for persistence, formatting, closing, import/export, images, layout, and themes.

This task must not change the note file format, XAML names, event-handler names, keyboard shortcuts, or user-visible strings. It is a source-organization refactor only.

## Plan of Work

Create `AiteBar/QuickNoteWindow.Formatting.cs` as a `partial class QuickNoteWindow`. Move the toolbar commands and their helper methods that edit the `FlowDocument`: bold, italic, underline, strike-through, headings, lists, clear formatting, code-block insertion, link insertion, image insertion, selection-offset helpers, and undo/redo. Keep each method body unchanged except for required `using` directives.

Create `AiteBar/QuickNoteWindow.Exchange.cs` for Markdown/text import and export commands, including their error handling. These methods own file dialogs but keep calling the current `QuickNoteMarkdownExchange` and `SaveNowAsync` methods on the same window.

Create `AiteBar/QuickNoteWindow.Presentation.cs` for theme application, document style traversal, window geometry, URL/code-copy hit testing, and editor statistics. These are visual or window-presentation concerns and retain existing helper calls.

Leave constructors, loading/closing, text and keyboard event coordination, timer scheduling, `SaveNowAsync`, status state, and disposal in `AiteBar/QuickNoteWindow.xaml.cs`. Remove every moved method from the original file so each method has exactly one definition.

Do not alter `QuickNoteWindow.xaml`, `QuickNoteService.cs`, persistence formats, or test expectations unless compilation proves a moved method had an accidental dependency. Add no behavioral tests solely for a move; preserve and run the existing focused tests, then the full suite.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

1. Move methods into the three partial-class files described above, preserving access modifiers, method names, and bodies.
2. Run:

       dotnet build .\AiteBar.sln -c Release

   Expect both `AiteBar` and `AiteBar.Tests` to build with no compiler errors.

3. Run:

       dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

   Expect all tests to pass. If `dotnet test` encounters WPF temporary-file failures, run:

       dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

4. Start the application and manually verify: show Quick Note, type text, format text, insert a link and image, export and import a Markdown file, toggle pin, move and resize the window, and close it. Reopen Quick Note and verify text, formatting, geometry, and pin behavior remain intact.

## Validation and Acceptance

Acceptance is behavior-preserving. The application builds in Release; the complete test suite passes; the existing Quick Note focused suite passes; and manual use confirms that an unpinned note closes after focus loss while a pinned note remains open, edits save and reappear after reopening, formatting and code blocks remain editable, and Markdown exchange still works.

## Idempotence and Recovery

Moving a method is repeatable as long as it is removed from its original file before being added to its destination. If a move causes a compiler error, return that method to `QuickNoteWindow.xaml.cs` and move it only with its helper dependencies. No data migration or destructive filesystem operation is involved.

## Artifacts and Notes

Focused test evidence before the move:

    Passed: 116, failed: 0, skipped: 0

The test command emitted `NU1900` warnings because the NuGet vulnerability feed was unavailable. They did not prevent compilation or test execution.

## Interfaces and Dependencies

All new files declare the existing namespace `AiteBar` and the existing type:

    public partial class QuickNoteWindow

They use only existing WPF and project types. `QuickNoteWindow.xaml` continues to bind its existing event names to private methods on this compiled partial class. `QuickNoteService`, `IQuickNotePersistence`, `QuickNoteMarkdownExchange`, `QuickNoteImageHelper`, `QuickNoteDocumentFormatting`, and `QuickNoteLayoutHelper` retain their current interfaces.

Revision note (2026-08-24): created before implementation to make the source-organization refactor reproducible and behavior-preserving.

Revision note (2026-08-24): recorded the completed partial-class split and focused-test evidence. The complete-suite and manual-lifecycle checks remain pending because the desktop command runner did not return a full-suite result in its available wait window.
