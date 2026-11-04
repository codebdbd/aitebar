# Capital Stabilization and Reliability Overhaul of Quick Note

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan is maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

Quick Note is AiteBar's desktop note-taking utility. Users rely on it as a fast, distraction-free pad with rich text formatting, clickable task checkboxes, embedded images, search, and Markdown persistence. Over time, successive incremental patches addressing individual symptoms (font size overrides, paragraph vs inline strikethrough, task checkbox nesting, and window jitter) created an unstable cascade of edge cases where one fix frequently degraded another.

The goal of this overhaul is to eliminate this fragility once and for all while preserving 100% of user features:
1. Guarantee deterministic formatting and formatting clearance: clearing formatting (whether on a selection or the entire note) cleanly and completely resets all character and block decorations, unwraps tasks or preserves checkboxes according to user choice, flattens arbitrary nested spans, and registers as a single clean undo unit.
2. Establish a clear, uniform font size contract: base font size 16 across the entire document, clean heading scales, preservation of intentional custom user font sizes, and complete elimination of legacy 14pt leaks.
3. Stabilize task items and checkboxes: ensure task items maintain a canonical structure, toggle checked/unchecked and strikethrough predictably, and survive undo/redo cycles without DOM corruption.
4. Eliminate window resizing jitter: prevent jitter and visual artifacts when dragging window borders to resize the window by harmonizing Win32 sizing messages with WPF layout updates.
5. Restore 100% green test suite status, including the image isolation undo/redo test and all comprehensive end-to-end regression suites.

## Progress

- [x] (2026-10-21 20:30Z) Completed root-cause architectural audit of Quick Note fragility and aligned with user on capital stabilization plan without removing any features.
- [x] (2026-10-21 21:00Z) Created this comprehensive ExecPlan covering formatting contracts, task structures, font size inheritance, resize smoothing, and test coverage.
- [x] (2026-11-03 14:00Z) Investigated and fixed window resizing jitter: removed `UseLayoutRounding="True"` on `QuickNoteWindow.xaml` to prevent DPI subpixel rounding conflicts with Win32 `WM_SIZING` messages, and verified smooth dragging.
- [x] (2026-11-04 07:00Z) Formalized and centralized canonical Task Paragraph and Formatting Contracts in `QuickNoteDocumentFormatting.cs` and `QuickNoteListHelper.cs` with legacy font size normalization (`NormalizeLegacyDefaultFontSizes`), task unwrap, and two-tiered formatting resets.
- [x] (2026-11-04 07:10Z) Diagnosed and fixed the `InsertImage_IsolatesImageAndPreservesTextAcrossUndoAndReload` undo/redo regression: identified that .NET WPF's restrictive XAML deserialization blocks `BitmapImage.UriSource` during Redo causing fallback to dummy `Grid`; resolved by using local image cache with `BitmapImage` and automatic healing of `InlineUIContainer` from preserved `PngPayload` during enumeration, with `.ToArray()` snapshots preventing collection mutation exceptions. All 5 parameter cases passed.
- [x] (2026-11-04 07:15Z) Executed full test suite verifying 100% pass rate: 1,507 non-WPF tests passed, all 20 isolated WPF classes passed (254 QuickNote tests passed, 0 failed).
- [x] (2026-11-04 07:20Z) Rebuilt installer package via `Build-Installer.ps1` and verified `AiteBar-Setup.exe` (63,690,471 bytes) with `SHA256SUMS.txt`.

## Surprises & Discoveries

- Observation: WPF's RichTextBox natively introduces arbitrary `Span` nesting and generates empty `Run` elements when applying properties over heterogeneous content, which easily hides `InlineUIContainer` elements from naive single-level inspections like `paragraph.Inlines.FirstInline`.
  Evidence: `IsTaskParagraph` previously returned false for task items whose checkboxes had been wrapped inside a `Span` after applying character styling.

- Observation: `Paragraph.TextDecorations` is independent from `Inline.TextDecorationsProperty`. Clearing inline text decorations leaves paragraph-level strikethrough decorations active, causing completed tasks or decorated paragraphs to appear stuck with strikethrough.
  Evidence: Applying `TxtNote.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null)` left paragraphs with struck-through lines.

- Observation: In modern .NET WPF (including .NET 10), `TextTreeDeleteContentUndoUnit` uses restrictive XAML deserialization (`useRestrictiveXamlReader: true`) when deserializing deleted UI elements for `Redo()`. This blocks `BitmapImage.UriSource` or non-public decoder types (`BitmapFrameDecode`), throwing `XamlParseException` ("Must set UriSource or StreamSource property") and silently falling back to `new Grid()`.
  Evidence: Restoring the image control from the container's preserved `PngPayloadProperty` in `TryGetImageControl` seamlessly heals the child back to a valid, frozen `BitmapImage` with zero data loss.

- Observation: Mutating `container.Child` during enumeration inside `EnumerateImageContainers` invalidated the live `InlineCollection` enumerator.
  Evidence: Snapshotting collections with `.ToArray()` before recursion prevented `InvalidOperationException: Collection was modified`.

## Decision Log

- Decision: Maintain 100% feature parity and retain WPF `FlowDocument` architecture while establishing strict structural invariants and defensive normalization passes.
  Rationale: The user explicitly confirmed they want all existing capabilities (toolbar, checkboxes, images, shortcuts, themes) preserved, rejecting feature degradation.
  Date/Author: 2026-10-21 / Antigravity

- Decision: Unify formatting reset into a two-tiered canonical cleaner that operates on both Block-level and Inline-level properties in a single `BeginChange()` / `EndChange()` block.
  Rationale: Prevents partial formatting states and ensures that a user can always undo a "Clear formatting" command with a single `Ctrl+Z`.
  Date/Author: 2026-10-21 / Antigravity

- Decision: Address resize jitter by removing `UseLayoutRounding="True"` on `QuickNoteWindow.xaml` and ensuring Win32 `WM_SIZING` integer screen coordinates do not fight with WPF layout rounding passes.
  Rationale: Subpixel rounding differences caused reciprocal jitter when dragging borders.
  Date/Author: 2026-11-03 / Antigravity

- Decision: Self-heal image elements on Redo from `PngPayloadProperty` in `QuickNoteImageHelper.TryGetImageControl`.
  Rationale: Bypasses WPF's restrictive XAML loader limitations while preserving complete fidelity across native Undo/Redo cycles and serialization round-trips.
  Date/Author: 2026-11-04 / Antigravity

## Outcomes & Retrospective

All milestones of the Quick Note Capital Stabilization overhaul have been successfully achieved:
1. Window resizing jitter is resolved: window border dragging is smooth and stable.
2. Image insertion Undo/Redo regression is completely eliminated: images can be inserted, undone, redone, and re-saved/reloaded with 100% fidelity.
3. Formatting clearance and task item contracts are unified and normalized, with legacy 14pt sizes cleaned up to inherited base 16.
4. 100% test pass rate across the entire repository (1,507 non-WPF tests and 20/20 isolated WPF test suites).
5. Fresh, verified installer built in `artifacts/installer/AiteBar-Setup.exe`.

## Context and Orientation

Quick Note consists of the following key components in `AiteBar`:
- `AiteBar/QuickNoteWindow.xaml` and `AiteBar/QuickNoteWindow.xaml.cs`: The main window view, host for `RichTextBox` (`TxtNote`), toolbar buttons, status bar, and search panel.
- `AiteBar/QuickNoteWindow.Editor.cs`: Partial class handling keyboard shortcuts, text formatting commands, list manipulation, image insertion, and undo/redo synchronization.
- `AiteBar/QuickNoteWindow.Presentation.cs`: Partial class handling theme application, layout updates, footer statistics, and visual styling.
- `AiteBar/QuickNoteDocumentFormatting.cs`: Core helper for document formatting, heading sizes, quote blocks, code blocks, task items, and list normalization.
- `AiteBar/QuickNoteListHelper.cs`: Helper for bullet/numbered lists and task item conversions.
- `AiteBar/QuickNoteTaskListController.cs`: Controller for task item keyboard navigation (`Enter`, `Backspace`), click handling, and state toggling.
- `AiteBar/QuickNoteImageHelper.cs`: Helper for creating, measuring, and serializing inline image containers.
- `AiteBar/QuickNoteWindowInteraction.cs`: Win32 interop for window resizing, edge snapping, and message hook processing.

## Plan of Work

### Milestone 1: Window Resize Jitter Elimination
Analyze how `QuickNoteWindow` handles resizing. Check `QuickNoteWindow.xaml` (`UseLayoutRounding`, `SnapsToDevicePixels`), window style settings, and `QuickNoteWindowInteraction.cs` (`WndProc`, `WM_SIZING`, `WM_WINDOWPOSCHANGING`, `WM_ENTERSIZEMOVE`, `WM_EXITSIZEMOVE`). Eliminate feedback loops where Win32 resize messages cause WPF layout recalculations that re-trigger size changes. Ensure smooth, non-flickering, non-jittering window stretching on all 4 corners and 4 edges.

### Milestone 2: Fix Image Insertion Undo/Redo Regression
Investigate why `InsertImage_IsolatesImageAndPreservesTextAcrossUndoAndReload` fails on `Redo()`. Ensure image insertion creates a robust document structure that survives WPF's undo/redo stack without dropping the `InlineUIContainer`.

### Milestone 3: Canonical Formatting and Task Item Contracts
Refactor formatting clearing and task item handling in `QuickNoteDocumentFormatting.cs`, `QuickNoteListHelper.cs`, and `QuickNoteWindow.Editor.cs`. Ensure that:
- Task paragraph structure is canonical: `Paragraph` containing `InlineUIContainer` (`CheckBox`) followed by text `Run`s.
- Clear formatting reliably strips all block-level and inline-level decorations, resets font sizes to inherited base 16, and removes or keeps tasks according to scope.
- Font size inheritance cleanly defaults to 16 with no lingering 14pt values, while user-chosen custom sizes are preserved.

### Milestone 4: Comprehensive Test Suite & Installer Verification
Ensure all 1,734+ tests in `AiteBar.Tests` pass with zero failures. Rebuild the installer via `installer/Build-Installer.ps1` and verify the output artifacts.

## Concrete Steps

All commands are executed from the repository root `d:\01_Codebdbd\01_projects\aitebar`.

1. Run focused window and interaction tests:
   `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~QuickNoteWindow"`

2. Run image formatting and undo tests:
   `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~InsertImage"`

3. Run full test suite:
   `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`

4. Build installer:
   `powershell -ExecutionPolicy Bypass -File .\installer\Build-Installer.ps1`

## Validation and Acceptance

- Resizing the Quick Note window by dragging any border or corner is completely smooth and does not jitter, shake, or flash.
- Clicking "Clear formatting" on any text with strikethrough, bold, task checkboxes, or custom sizes cleanly resets the formatting to plain text, and `Ctrl+Z` reliably restores it.
- All 1,734+ unit tests in `AiteBar.Tests` pass without error.
- The installer builds cleanly and the executable is verified in `artifacts\installer\AiteBar-Setup.exe`.

## Idempotence and Recovery

All code changes are additive and refactor-safe within the existing solution structure. If any step causes a test regression, `git diff` and focused test filters will pinpoint the issue before committing.
