# Add the distraction-free Zen Editor utility

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this change, AiteBar users can enable and launch a new built-in utility named Zen Editor from the primary panel. Its panel icon is the Fluent glyph U+E367. The utility immediately opens the last internal plain-text document in a borderless full-screen editor on the last used monitor. During ordinary writing the screen contains only the theme background, a centered text column, the text, selection, and caret. Documents are saved automatically and locally, can be switched or deleted through a minimal document picker, and can be exported as UTF-8 TXT files.

The five prescribed themes and bundled fonts work without internet access or global font installation. The utility never sends document text to telemetry, AI, analytics, a server, or any network endpoint. A user can verify the feature by enabling Zen Editor in AiteBar settings, opening it from the panel, typing text, closing and reopening it, switching themes, creating and switching documents, and exporting a TXT copy.

## Progress

- [x] (2026-07-29 03:47Z) Read the complete attached 26-section functional specification and the repository agent rules.
- [x] (2026-07-29 03:47Z) Traced the existing utility registration, panel-button, settings, localization, window-lifecycle, persistence, test, publish, and installer paths.
- [x] (2026-07-29 03:47Z) Chose an atomic file-based internal store with checksums and snapshots instead of adding a database package.
- [x] (2026-07-29 03:52Z) Implemented the document model, title and export helpers, fixed theme catalog, atomic store, checksum recovery, and focused unit tests including a two-million-character round trip.
- [x] (2026-07-29 03:59Z) Implemented the full-screen editor window, plain-text editing behavior, per-document 500-operation Undo/Redo, auto-save, context menu, document picker, export, and failure overlay.
- [x] (2026-07-29 04:04Z) Integrated Zen Editor into the utility registry, U+E367 panel catalog entry, settings, EN/RU/UK/DE localization, documentation, publish resources, and installer notices.
- [x] (2026-07-29 04:01Z) Bundled and verified the five prescribed fonts and their OFL license texts; focused glyph tests cover English, Russian, and Ukrainian-specific letters.
- [x] (2026-07-29 04:09Z) Built Release with zero warnings and errors, passed all 1,000 tests, built the installer, verified the five published OFL files, and retained automated all-four-edge panel coverage. A visual process smoke of the new binary was intentionally not run because the user's installed AiteBar process was active and the global single-instance mutex would redirect or interfere with it.
- [x] (2026-07-29 06:07Z) Corrected the editor-local TextBox template, initialized the complete context menu before the first right click, removed the slide-out header and minimize action entirely, and restricted editor exit to Escape or Alt+F4 while retaining forced save-on-close. A runtime WPF regression confirms 14 menu entries, 10 commands, and five themes before the first opening. Focused tests pass 27/27, the fallback full suite passes 1,004/1,004, and the rebuilt installer SHA-256 is `A3D554E3C399B19BD0FE29E8BB66993E6D538A1837C62CF17CCD63FA33D7B8FC`.
- [x] (2026-07-29 06:24Z) Replaced theme-derived context-menu colors and unadorned items with AiteBar's shared `DarkContextMenu`, `DarkMenuItem`, `ContextMenuIconTextStyle`, fixed icon column, Fluent glyphs, and standard shortcut labels. The active theme uses the application accent glyph. Runtime menu tests and the full 1,004-test suite pass. The rebuilt installer SHA-256 is `E41D0A5696AE0E2081D1FF7577387C0CACA582A1E53D0DE8C8F95FC5C06CDA19`.
- [x] (2026-07-29 06:52Z) Fixed the remaining vertical-grid mismatch: programmatically created separators now explicitly use the shared named `DarkMenuSeparator`, so all four separators are height 0 and collapsed and all ten visible rows remain on the standard 38-DIP grid. The 27 focused tests and the remaining 1,003 tests pass in separate STA runs; combining every WPF test in one host intermittently deadlocked during dispatcher shutdown. The rebuilt installer SHA-256 is `0597BA63964A907A4CB522FCA205B29CC901F44B99DE7E2D0C1849CEC9F685F8`.
- [x] (2026-07-29 07:09Z) Replaced the visually low Fluent substitutes for Undo, Redo, Cut, Paste, and Select All with the exact glyph/font pairs already used by Quick Note and the shared `TextEditingContextMenu`: Segoe UI arrows, Material cut/paste/select-all, and the existing Fluent copy glyph. Focused runtime assertions verify the font of every edit command; 27 focused and 1,003 remaining tests pass. The rebuilt installer SHA-256 is `5ED3D0EB1C8BFB2F8BB120C2659963F54CFDA52937E073B808481246FCBFB582`.
- [x] (2026-07-29 07:27Z) Removed the final architectural source of menu drift by introducing `AppContextMenuFactory` and routing MainWindow panel menus, tray, the taskbar indicator, and Zen Editor through the same menu/item/separator construction code. Contract tests require every location to use that factory; 27 focused and 1,003 remaining tests pass. The rebuilt installer SHA-256 is `C51ED833BDB39FE15831F4817FC8D59E5A3A6E8DD7B73186BCE4281C9D097471`.
- [x] (2026-07-29 07:40Z) Fixed the actual Zen-only vertical text offset: the editor's inherited `TextBlock.LineHeight` of roughly 30 DIP leaked through the context menu placement target. `AppContextMenuFactory` now explicitly resets menu line height to automatic and line stacking to `MaxHeight`. A runtime regression sets the editor line height to 30 and verifies that the menu remains isolated; 27 focused and 1,003 remaining tests pass. The rebuilt installer SHA-256 is `AC97607EBA23061108BB67C417B30D78211212C60E5DFAC80CAFF52818A3052F`.
- [x] (2026-07-29 08:03Z) Added typographic paragraph spacing without modifying stored text. `ZenParagraphEditor` maps each hard newline to a WPF paragraph, keeps a plain-text API for persistence and commands, uses line height `1.5em` and paragraph spacing `0.75em`, and computes Undo/Redo changes in plain-text coordinates. Runtime WPF tests verify round-trip text, trailing empty paragraphs, caret, selection, and an actual 45-DIP baseline step at 20-DIP type. Focused tests pass 32/32, the two layout/lifecycle tests pass 2/2, and the remaining suite passes 1,007/1,007. The rebuilt installer SHA-256 is `2C801A9AEFD246F5EF19F7B2883D642AF90486F469EC22F6DBF389336F060EEF`.
- [x] (2026-07-29 12:14Z) Addressed the pre-release review findings while intentionally retaining WPF rich-text keyboard commands and the current Copy/Cut behavior at the user's request. Ordinary insertion, one-character deletion, Enter, and caret mapping avoid whole-document string reads; complex replacements retain a correctness fallback. Checksum and JSON preparation run on a worker thread. The atomic index now carries backward-compatible document summaries, fullscreen suppression states are independent, version/changelog/installer metadata are synchronized at 1.12.0, focused Zen tests pass 34/34, the full suite passes 1,011/1,011, and the rebuilt installer SHA-256 is `21CEEBF33980482B1980EF0BCB85B8D9061702C8EEBED8C54C58B2A04F7101A3`.
- [x] (2026-07-29 12:54Z) Persisted the formatting that the user deliberately keeps available through WPF commands. Bold, italic, and underline are stored as plain-text coordinate ranges, included in the checksum, restored on load, and omitted from TXT. Existing legacy checksums remain valid. TXT export inserts exactly one blank line between paragraphs. The focused Zen suite passes 41/41 and Release builds with zero warnings/errors. The combined full WPF host and repository `vstest` fallback both reproduced the known dispatcher-shutdown deadlock without reporting a failed test; the immediately preceding pre-formatting full suite passed 1,011/1,011. The rebuilt 1.12.0 installer SHA-256 is `8CBEBD93C9A21BB4B151907597C933B4522A482EC5567AA215A1585804631320`.
- [x] (2026-07-29 11:48Z) Completed the final review follow-up. Undo/Redo now restores text and bold/italic/underline ranges, style capture visits each inline once, destructive edits snapshot the exact prior in-memory state, and whitespace-only TXT paragraphs normalize correctly. Release builds with zero warnings/errors, focused Zen tests pass 47/47, the complete suite passes 1,024/1,024, and the rebuilt 1.12.0 installer SHA-256 is `3E182E34EA33626D98719E8A3A818E3387753871DA63BCAC29DA7F2B791572E3`.
- [x] (2026-07-29 12:12Z) Added cyclic keyboard theme navigation: `Shift+Up` selects the previous theme and `Shift+Down` selects the next, wrapping at both ends. The related Text Processing comparison toggle now changes between localized Show/Hide labels. Release builds cleanly, 155 focused tests and all 1,028 tests pass, and the rebuilt 1.12.0 installer SHA-256 is `E2E9F40323DBF4512825E6DE13BE18EA47A46B318BAE366DA75A77113BB4185F`.

## Surprises & Discoveries

- Observation: Utility metadata currently exists in two places.
  Evidence: `AiteBar/UtilityRegistry.cs` requires each `IUtility` to expose its ID, glyph, and color, while `AiteBar/UtilityButtonCatalog.cs` repeats the same panel metadata. Zen Editor must keep `ZenEditor`, U+E367, and its color synchronized in both places.

- Observation: Reflection registration removes the need for a hard-coded startup registration call, but panel execution is still hard-coded.
  Evidence: `AiteBar/App.xaml.cs` calls `UtilityRegistry.RegisterAllFromAssembly`, while `AiteBar/MainWindow.xaml.cs` has a switch in `ExecuteUnifiedButtonActionAsync`.

- Observation: WPF application resources are automatically copied into publish output, and the installer recursively includes the whole publish directory.
  Evidence: `AiteBar/AiteBar.csproj` declares existing font resources and `installer/AiteBar.iss` recursively copies `artifacts/publish/win-x64`.

- Observation: A plain WPF `TextBox` exposes a native Undo stack, but that stack is cleared when another document is assigned.
  Evidence: The fixed contract requires Undo/Redo to survive document switches during one process run, so `ZenEditorUndoHistory` records compact offset/remove/add operations per document instead of retaining hundreds of full multi-megabyte strings.

- Observation: All five official font files include the required Ukrainian characters as well as Russian and English glyphs.
  Evidence: `ZenEditorIntegrationTests.BundledFonts_ContainRequiredRussianUkrainianAndEnglishGlyphs` passed for `AaЯяІіЇїЄєҐґ`.

- Observation: The first installed visual smoke exposed that AiteBar's application-wide implicit `TextBox` style leaked into the Zen Editor.
  Evidence: The user screenshot showed a 32-DIP dark rounded form instead of a full-height transparent writing surface. `App.xaml` bases every implicit `TextBox` on `BaseTextBoxStyle`, which sets the fixed form height and form-control template.

- Observation: Fullscreen detection intentionally skips all windows from AiteBar's own process.
  Evidence: `TaskbarPositionIndicatorService.IsFullscreenAppRunning` skips the current process, so the blue position indicator remained above Zen Editor until explicit utility suppression was added.

- Observation: Assigning a new `ContextMenu` only from `ContextMenuOpening` is too late when the editor-local TextBox style has no pre-existing menu.
  Evidence: The first right click produced an empty menu even though `BuildContextMenu` contained all commands; initializing it in the constructor makes the populated menu available before WPF begins opening it.

- Observation: Applying the current document theme to the context menu violates the application's menu contract.
  Evidence: Paper and Ivory supplied near-white menu backgrounds while the shared menu-item template retained light foreground values, and iconless items bypassed the standard 34-DIP icon column, producing both low contrast and visibly incorrect alignment.

- Observation: Programmatically created separators did not reliably acquire the application's implicit collapsed separator style inside the detached context-menu popup.
  Evidence: The installed screenshot showed four visible system separator lines with their own height, making group-to-group row spacing differ from the standard 38-DIP item grid. Explicitly applying the shared named separator resource removes that extra geometry.

- Observation: Equal control bounds do not guarantee equal perceived vertical alignment when a different icon font or glyph variant is used.
  Evidence: The supplementary-plane Fluent Select All glyph occupied the correct 24-DIP icon box but its ink sat visibly below the text. The application's established text-editing menu instead uses Material glyph U+E162 for Select All and corresponding Material glyphs for Cut and Paste.

- Observation: `TextBlock.LineHeight` set on the Zen Editor text surface can flow into its detached context-menu popup through WPF's placement-target inheritance context.
  Evidence: Zen Editor sets line height to `theme.FontSize * 1.5` (about 30 DIP), while ordinary menu rows are 38 DIP. The screenshot showed header text formatted against that tall inherited line box and visually pinned upward; other utility invocation points do not set this inherited value.

- Observation: A timed snapshot created immediately after the explicit pre-edit snapshot could otherwise become the newest backup while still containing the older on-disk state.
  Evidence: `SaveNowAsync` independently creates a five-minute snapshot. Advancing `_lastSnapshotUtc` immediately after the explicit in-memory snapshot prevents a second stale snapshot from winning recovery ordering.

- Observation: WPF `TextBox` supports one line height but has no paragraph-spacing property.
  Evidence: Adding blank lines would mutate the user's plain text, while increasing line height would also spread wrapped lines inside a paragraph. A plain-text adapter over `RichTextBox`/`FlowDocument` is required to give hard-newline paragraphs their own visual margin.

- Observation: The paragraph adapter's initial implementation made every edit serialize the whole `FlowDocument`, and caret geometry converted plain-text offsets through repeated prefix materialization.
  Evidence: `ZenParagraphEditor.OnTextChanged` called `new TextRange(...).Text` for the complete document, while `GetTextPointer` performed a binary search whose comparison serialized progressively large prefixes. The release contract includes documents of at least two million characters.

- Observation: An `async` storage API does not by itself move synchronous preparation off the caller thread.
  Evidence: `ComputeChecksum` and `JsonSerializer.SerializeToUtf8Bytes` execute before the first incomplete file-write await, so a save invoked from the WPF dispatcher performs CPU and allocation-heavy work on the UI thread.

- Observation: The document picker only needs ID, title, modified time, and current status, but the initial store implementation deserialized and checksummed every complete document to build that list.
  Evidence: `ZenEditorStore.ListAsync` called `LoadWithRecoveryCoreAsync` for every document ID, including multi-megabyte text bodies.

## Decision Log

- Decision: Implement Zen Editor as `ZenEditorUtility : UtilityBase<ZenEditorWindow>` and keep it independent of Quick Note and Text Processing.
  Rationale: Quick Note is a single Markdown-capable note and Text Processing is an AI transformation tool. Reusing either window would violate the plain-text-only, no-formatting, no-AI, full-screen contract and risk regressions in existing utilities.
  Date/Author: 2026-07-29 / Codex

- Decision: Store documents in `%APPDATA%\Codebdbd\Aite Bar\ZenEditor` using an atomic JSON index, atomic per-document JSON records, and a bounded `Backups` directory.
  Rationale: A temporary file followed by atomic replacement provides transaction-like single-record durability without a new database dependency. Per-document records avoid rewriting every document when one large document changes, while SHA-256 checksums allow corruption detection. Backups allow automatic recovery.
  Date/Author: 2026-07-29 / Codex

- Decision: Use a WPF `TextBox` configured for plain text rather than `RichTextBox`.
  Rationale: `TextBox` naturally strips rich formatting, scales to large plain-text values more predictably, exposes native Undo/Redo and accessibility support, and preserves physical lines and spaces without Markdown or FlowDocument conversion.
  Date/Author: 2026-07-29 / Codex

- Decision: Keep the editor window as one process-local instance through `UtilityBase<TWindow>` and restore it to full screen when relaunched.
  Rationale: AiteBar itself already enforces a single application instance. The utility base prevents a second Zen Editor window inside that process and provides the intended repeat-launch activation behavior.
  Date/Author: 2026-07-29 / Codex

- Decision: Do not add a configurable global Zen Editor hotkey unless the user explicitly requests one.
  Rationale: The fixed specification defines editor-local shortcuts but does not define an AiteBar global launch hotkey. The panel button is required and sufficient for initial integration.
  Date/Author: 2026-07-29 / Codex

- Decision: Use a custom compact per-document Undo/Redo history with a capacity of 500 operations while leaving WPF editing behavior intact.
  Rationale: It meets the cross-document session requirement without the prohibitive memory cost of storing 500 complete copies of documents that may contain two million characters.
  Date/Author: 2026-07-29 / Codex

- Decision: Treat removal of at least 1,000 characters, or a remove-and-insert replacement, as a destructive edit that requires an immediate protective snapshot.
  Rationale: The specification requires a snapshot before clearing a large selection or replacing content but does not define “large.” One thousand characters protects meaningful blocks without producing a backup for routine word deletion; replacement always snapshots because paste-over-selection can destroy arbitrary content.
  Date/Author: 2026-07-29 / Codex

- Decision: Give the Zen Editor text surface a dedicated borderless template containing only a transparent `PART_ContentHost` scroll viewer.
  Rationale: Merely setting colors is insufficient while the application-wide form template still imposes a fixed height, dark chrome, and rounded frame. A dedicated template guarantees one continuous window background with no text-block background, border, or corner radius.
  Date/Author: 2026-07-29 / Codex

- Decision: Add explicit fullscreen-utility suppression to `TaskbarPositionIndicatorService`.
  Rationale: Same-process fullscreen windows are deliberately excluded from generic fullscreen detection, so a lifecycle signal is required to hide the AiteBar indicator immediately while Zen Editor is normal and restore it when minimized or closed.
  Date/Author: 2026-07-29 / Codex

- Decision: Remove the editor header, minimize action, and context-menu Exit command; Escape and Alt+F4 are the only editor exits.
  Rationale: The user explicitly rejected a slide-out header and requested an uninterrupted single-surface editor with exactly those two exit gestures. Escape is also handled while the context menu owns keyboard focus.
  Date/Author: 2026-07-29 / Codex

- Decision: Keep the Zen Editor context menu visually independent of the document theme and use the same shared dark menu resources and Fluent glyph treatment as the rest of AiteBar.
  Rationale: Application chrome must remain legible and consistent in every editor theme. Reusing the shared styles also guarantees the same text baseline, icon column, disabled state, submenu arrow, hover treatment, and shortcut alignment as other utilities.
  Date/Author: 2026-07-29 / Codex

- Decision: Name the application's collapsed separator style `DarkMenuSeparator`, keep it as the implicit separator base, and reference it explicitly from programmatic menus.
  Rationale: Detached WPF popups can miss implicit resource lookup for programmatically created separators. A named shared resource preserves one program-wide definition while making its use deterministic.
  Date/Author: 2026-07-29 / Codex

- Decision: Reuse the exact edit-command glyph and font combinations from `TextEditingContextMenu` and Quick Note rather than choosing semantically equivalent Fluent icons.
  Rationale: The existing combinations are the program's visually calibrated contract. Semantic equivalence is insufficient when font ascent, descent, and glyph ink bounds differ.
  Date/Author: 2026-07-29 / Codex

- Decision: Centralize programmatic context-menu construction in `AppContextMenuFactory` and make MainWindow, tray, taskbar indicator, and Zen Editor consume it.
  Rationale: Sharing resource keys while duplicating object construction still allows local visual drift. A single factory makes item padding, icon container, font selection, colors, enabled state, gesture text, and separators structurally identical.
  Date/Author: 2026-07-29 / Codex

- Decision: Reset `TextBlock.LineHeight` and `TextBlock.LineStackingStrategy` on every factory-created context menu.
  Rationale: Application chrome must not inherit document typography from its placement target. Explicit defaults make the same menu template render identically whether opened from a normal control or from a large-line-height editor.
  Date/Author: 2026-07-29 / Codex

- Decision: Render Zen Editor through `ZenParagraphEditor`, treating every hard newline as a visual paragraph with `0.75em` bottom spacing while exposing and persisting only normalized plain text.
  Rationale: At 20-DIP type with 1.5 line height, 15 DIP produces a restrained 45-DIP paragraph step. The visual document structure allows spacing without injecting blank lines or formatting into the stored/exported text.
  Date/Author: 2026-07-29 / Codex

- Decision: Keep WPF rich-text keyboard commands and the existing `RichTextBox.Copy`/`Cut` behavior unchanged.
  Rationale: The pre-release review identified these as a plain-text contract deviation, but the user explicitly requested that finding 3 remain unchanged. The performance and release corrections must not silently alter that behavior.
  Date/Author: 2026-07-29 / Codex

- Decision: Optimize the existing paragraph adapter rather than removing paragraph spacing or returning to a plain `TextBox`.
  Rationale: Paragraph spacing is an explicit accepted user-visible requirement. The adapter will maintain its cached plain string incrementally for ordinary insertions and single-character deletions, expose exact changes to Undo/Redo, and map positions by traversing runs and paragraphs without allocating prefix strings. Complex replacements retain a correctness-first full-text fallback.
  Date/Author: 2026-07-29 / Codex

- Decision: Store lightweight document summaries in `ZenEditorStoreIndex` and rebuild them once for older indexes.
  Rationale: The picker must not load every document body. Keeping summaries in the already atomic index makes normal listing proportional to document count and tiny metadata, while a backward-compatible rebuild preserves existing user documents.
  Date/Author: 2026-07-29 / Codex

- Decision: Publish the new built-in utility as version 1.12.0.
  Rationale: Zen Editor is a backward-compatible new capability, which is a minor SemVer increment from 1.11.x. Project, assembly, installer-derived metadata, and bilingual changelog entries must agree.
  Date/Author: 2026-07-29 / Codex

- Decision: Persist only the formatting commands exposed by the retained editor behavior—bold, italic, and underline—as plain-text coordinate ranges.
  Rationale: The user reported that Ctrl+B survives only until reopen and expects retained formatting to persist. Coordinate ranges keep the canonical text and TXT export plain, avoid storing theme colors or font sizes, remain backward compatible with existing JSON documents, and allow checksums to protect both text and styling.
  Date/Author: 2026-07-29 / Codex

- Decision: Store both the before-style and after-style coordinate ranges in every Zen Editor history operation, including operations with identical before/after text.
  Rationale: WPF formatting commands raise a change without changing plain text, and rebuilding a FlowDocument during text Undo removes all inline formatting unless the target style state is reapplied explicitly.
  Date/Author: 2026-07-29 / Codex

- Decision: Expose a dedicated `SaveSnapshotAsync(ZenEditorDocument)` store operation instead of overloading normal save semantics.
  Rationale: The normal `createSnapshot` option intentionally snapshots the existing durable record. A destructive edit needs a different guarantee: preserve the exact in-memory state immediately before the edit.
  Date/Author: 2026-07-29 / Codex

## Outcomes & Retrospective

Zen Editor is implemented end to end as a first-class AiteBar utility. The U+E367
button participates in the existing visibility and reorder systems; the editor,
document picker, atomic storage, recovery, export, five embedded themes,
per-document Undo/Redo, localization, documentation, notices, publish, and
installer paths are complete.

Release validation completed with zero compiler warnings and zero errors.
After the installed visual-smoke correction, focused Zen Editor tests passed
24/24 and the final complete suite passed 1,001/1,001.
`installer/Build-Installer.ps1` produced the corrected
`artifacts/installer/AiteBar-Setup.exe` (79,432,971 bytes) plus
`SHA256SUMS.txt`. The verified installer SHA-256 is
`60EF1A38191E45F0279DC0471A9375EC3DD857589DD275712E45F74AEC023B23`.
Five OFL texts were verified in the publish directory.

The pre-release hardening pass completed on version 1.12.0. A real STA/WPF
regression edits a two-million-character document and proves that ordinary
insertion and deletion do not trigger a whole-document read; the same test
checks Enter, paragraph count, and plain-text caret coordinates. A storage
regression proves that a fresh store can list a two-million-character document
from persisted index metadata without reading its deliberately corrupted body.
Checksum and JSON payload preparation now yield to a worker thread before
performing full-document CPU work. Release build completed with zero warnings
and errors, and the complete test suite passed 1,011/1,011. The 1.12.0 installer
is 79,424,083 bytes, reports ProductVersion 1.12.0, includes five published OFL
files, and has SHA-256
`21CEEBF33980482B1980EF0BCB85B8D9061702C8EEBED8C54C58B2A04F7101A3`,
matching `SHA256SUMS.txt`.

Formatting persistence and paragraph-separated TXT export were completed after
that hardening pass. Focused STA tests apply bold, italic, and underline to a
selection, capture the coordinate range, restore it into a fresh editor, and
verify both formatting and unchanged plain text. Store tests round-trip those
ranges and verify that legacy pre-formatting checksums still load without false
recovery. Export tests cover adjacent paragraphs, existing blank lines,
multiple blank lines, leading/trailing breaks, and single-line/empty documents.
The 41 focused Zen tests pass. The final combined WPF testhost and the prescribed
`dotnet vstest` fallback each reproduced the repository's known nondeterministic
dispatcher-shutdown hang and were terminated after their bounded timeouts; no
test failure was emitted. Release compilation remains clean. The final 1.12.0
installer is 79,430,387 bytes and has SHA-256
`8CBEBD93C9A21BB4B151907597C933B4522A482EC5567AA215A1585804631320`,
matching `SHA256SUMS.txt`.

The final review follow-up closed the remaining release blockers. Formatting-only changes and ordinary text changes now round-trip through the custom history without losing bold, italic, or underline ranges. Style persistence no longer has quadratic paragraph rescans, destructive replacements preserve the exact pre-edit text and styles, and TXT export treats whitespace-only lines as blank paragraphs. The final automated evidence is a zero-warning Release build, 47 passing focused Zen tests, 1,024 passing tests in the complete suite, and a rebuilt version 1.12.0 installer whose SHA-256 matches `SHA256SUMS.txt`.

The only unperformed validation is an interactive visual launch of this new
binary. An installed AiteBar instance was already running as PID 36424 from
`C:\Users\ostee\AppData\Local\Programs\Aite Bar\AiteBar.exe`; AiteBar's global
mutex prevents a parallel isolated application launch, and terminating the
user's running instance was deliberately avoided. Source-level WPF contracts,
font glyph loading, panel layout on all four edges, Release compilation, and
the full automated suite provide non-interactive coverage.

## Context and Orientation

AiteBar is a .NET 10 WPF desktop application. `AiteBar/UtilityRegistry.cs` defines `IUtility`, the reflection-discovered `[Utility]` attribute, and `UtilityBase<TWindow>`, which owns one window instance and handles activation and errors. `AiteBar/App.xaml.cs` registers all marked utility classes at startup. `AiteBar/ActionService.cs` launches a registered utility by stable string ID. `AiteBar/MainWindow.xaml.cs` maps panel button IDs to actions. `AiteBar/UtilityButtonCatalog.cs` defines every panel utility button and its setting-backed visibility. `AiteBar/UnifiedButtonService.cs` turns the visible catalog definitions into ordered panel buttons.

`AiteBar/Models.cs` contains `AppSettings`. `AiteBar/AppSettingsService.cs` clones, loads, normalizes, and saves that model. `AiteBar/AppSettingsWindow.xaml` and its code-behind expose utility visibility switches. Localization resides in `AiteBar/Resources/Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx`. A `DarkWindow` automatically refreshes localized WPF bindings when the application language changes.

Zen Editor’s internal store is not an exported file collection. A document record is a JSON object containing a stable GUID ID, plain text, creation and modification timestamps in UTC, caret and selection positions, scroll offset, deleted flag, checksum, and a flag recording whether it has ever contained text. The store index records the active document ID, selected theme ID, last monitor device name, and last export directory. “Atomic” means the service writes complete JSON to a temporary sibling file, flushes it, then replaces or moves it into the destination so the destination is never intentionally left half-written.

The editor must not pass document text to `TelemetryService`. Exceptions may be logged and telemetry may record only a generic operation identifier if existing global policy requires it, but no title, text, path, checksum, selection, or other user content may be attached.

## Plan of Work

First, add testable non-UI types. `AiteBar/ZenEditorModels.cs` will define document metadata, the store index, load results, and theme definitions. `AiteBar/ZenEditorTextHelper.cs` will normalize display titles, produce safe TXT filenames, normalize exported line endings to CRLF, and clamp caret and selection positions. `AiteBar/ZenEditorThemeCatalog.cs` will expose exactly five immutable definitions with the fixed names, colors, font sizes, and text-column widths. `AiteBar/ZenEditorStore.cs` will serialize records asynchronously, compute checksums over canonical text payloads, atomically replace files, maintain snapshots, soft-delete documents, recover corrupted records from the newest valid snapshot, and create a single initial empty document when necessary. Unit tests will use isolated temporary directories.

Second, add `AiteBar/ZenEditorWindow.xaml` and code-behind. The window will be borderless, non-resizable, taskbar-covering, per-monitor full screen, shown in the taskbar, and restored to the last monitor. A central `ZenParagraphEditor` will expose only plain text while rendering hard-newline paragraphs with fixed typography, use the selected bundled font and fixed width, disable spellcheck, keep left alignment, insert four spaces for Tab, paste plain text, and provide editor-local commands. The editor has no header or minimize action; Escape and Alt+F4 close it.

The window will debounce saves for 300 ms after text changes and copy non-empty selections after 150 ms once keyboard selection settles or immediately after mouse selection completes. Losing activation, switching or creating documents, export, close, and Windows session ending will force a save. Close will remain cancellable if the final save fails. A minimal error overlay will expose Retry, Export TXT, and Dismiss without claiming success.

Third, add `AiteBar/ZenEditorDocumentPicker.xaml` and code-behind as a modal overlay-like owned window. It will show newest documents first in `dd.MM.yyyy HH:mm:ss  Title` format, mark the current document, support mouse, Enter, arrows, Home, End, Delete, Escape, and prefix navigation without a visible search field. Deletion requires confirmation and creates a backup first.

Fourth, add `AiteBar/ZenEditorUtility.cs`, use stable ID `ZenEditor`, panel glyph `\uE367`, a muted professional blue compatible with the existing UI contract, and restore an existing window instead of opening another. Add `ShowPresetZenEditor` to `AppSettings`, clone it in `AppSettingsService`, add the matching `UtilityButtonCatalog` definition and settings switch, and add the `MainWindow` dispatch case. Add English, Russian, Ukrainian, and German strings for every visible label and error.

Fifth, place one unmodified Regular font resource for Literata, Source Serif 4, Noto Sans, IBM Plex Sans, and Inter under `AiteBar/Resources/ZenEditor/Fonts`. Place each upstream OFL text under `AiteBar/Resources/ZenEditor/Licenses`, declare the font files as WPF resources, and include the license texts as content copied to publish. Update `THIRD_PARTY_NOTICES.md` and `THIRD_PARTY_NOTICES.txt` with font names, upstream project URLs, and OFL-1.1 attribution. Verify Cyrillic characters and Ukrainian `ІіЇїЄєҐґ` render from each embedded family rather than a fallback.

Finally, update `README.md`, `docs/functions.md`, and the relevant architecture or user-manual section. Extend catalog, settings, persistence, theme, title, export, recovery, and visual-contract tests. Build and test Release. Because the feature changes panel settings, manually verify panel show/hide and access on Top, Bottom, Left, and Right, then exercise the Zen Editor acceptance scenarios.

For the pre-release hardening pass, update `AiteBar/ZenParagraphEditor.cs` so common edits update the cached plain string from local WPF changes and position conversion traverses document runs without creating full prefix strings. Update `AiteBar/ZenEditorWindow.xaml.cs` to consume those exact changes and to save a cloned UI snapshot on a worker thread. Update `AiteBar/ZenEditorModels.cs` and `AiteBar/ZenEditorStore.cs` with index-backed summaries and a one-time compatibility rebuild. Update `AiteBar/TaskbarPositionIndicatorService.cs` so utility suppression never assigns the independently detected external-fullscreen flag. Add focused correctness and performance-contract tests, then synchronize version 1.12.0 and `CHANGELOG.md`.

For the final review follow-up, extend `ZenEditorUndoHistory` so every operation stores style ranges before and after the edit, including formatting-only changes whose plain text is unchanged. Undo and Redo must restore both parts of that state. Change `ZenParagraphEditor.CaptureTextStyles` to traverse the FlowDocument once with a running plain-text offset. Add a store operation that snapshots the supplied in-memory document, and invoke it with the pre-edit text and styles before saving a destructive replacement. During TXT export, treat whitespace-only physical lines as blank paragraphs.

## Concrete Steps

All commands run from `D:\01_Codebdbd\01_projects\aitebar`.

Inspect changes while implementing:

    git status --short
    git diff --check

Run focused tests after the storage and helper milestone:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~ZenEditor"

Build the complete solution:

    dotnet build .\AiteBar.sln -c Release

Run all tests:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If WPF temporary generated files make `dotnet test` fail, use the repository fallback after a successful build:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

The expected result is a successful Release build with zero errors and a test run with zero failed tests. Exact test counts will be recorded after implementation rather than predicted.

If publish resources or installer inputs change, build the installer:

    .\installer\Build-Installer.ps1

Then verify that exactly one current installer exists in `artifacts\installer` and that the publish directory contains the Zen Editor font and license resources.

## Validation and Acceptance

Enable Zen Editor under AiteBar’s built-in quick tools. The primary panel must show a button rendered from Fluent glyph U+E367, and the button must participate in the existing drag reorder and detach context menu. Clicking it must hide the panel and show one full-screen Zen Editor window on the active or last-used monitor. Clicking the panel button again must activate the existing editor, never create a second one.

On first use, the Paper theme must show an empty document with focus and caret ready for input. Type Russian, Ukrainian, and English text including multiple spaces, blank lines, tabs, and at least two million generated characters. The displayed content must remain plain text, editing must remain responsive, and pasted rich content must lose formatting but preserve textual content. Closing and reopening must restore the last document, theme, caret, selection, scroll position, and monitor.

Wait at least 300 ms after editing, switch away, and reopen. The latest text must be present without a success notification. Simulate a corrupt current record in a temporary-store unit test; the newest valid backup must load and the load result must indicate that recovery actually occurred. A valid newer record must never be replaced by an older backup.

Move the pointer anywhere within the editor: no header or window controls may appear. Escape and Alt+F4 must each close the editor after the final forced save.

Right-click and use Shift+F10. The context menu must use the exact specified order, contain no icons or explanatory text, disable unavailable edit commands, show exactly five themes, and mark the active theme. Ctrl+Alt+1 through Ctrl+Alt+5 must switch to the fixed theme definitions and persist the choice.

Create documents, confirm that a never-used empty document does not multiply, open the picker, navigate entirely by keyboard, prefix-jump by typing, delete only after confirmation, and verify fallback to the next newest document or a new empty document. Export a TXT copy and verify UTF-8 encoding, CRLF line endings, sanitized title-derived filename, remembered folder, and no linkage between internal and exported content.

Select text with mouse and keyboard. The final non-empty selection must be copied automatically without a toast, while transient drag selections must not be copied repeatedly. Ctrl+C must still work. Clipboard images without text must not enable paste.

For AiteBar regression coverage, verify panel show, hide, positioning, contexts, hotkeys, tray access, and all four panel edges. Verify existing Quick Note, Clipboard Manager, and Text Processing still open and preserve their established behavior.

## Idempotence and Recovery

All store initialization and normalization operations are safe to repeat. Atomic writes use uniquely named temporary files in the destination directory and remove them after success or failure. A failed save keeps the prior valid destination and retains the editor content in memory. The editor will not close silently after a failed forced save.

Tests create GUID-named directories beneath the system temporary directory and remove only those exact directories. Font resources are committed under a dedicated repository folder and never installed into Windows. If a font download is interrupted, delete only that incomplete resource file and repeat the verified download; do not substitute a similarly named system font.

The feature is additive. If compilation fails partway through implementation, use the Progress section to locate the incomplete milestone and complete the missing references; do not reset unrelated user work or delete the existing AiteBar settings directory.

## Artifacts and Notes

The user-provided fixed requirements are stored outside the repository in the Codex attachment `pasted-text.txt`. This plan embeds the implementation-relevant behavior so future work does not depend on that attachment remaining available.

Expected storage shape:

    %APPDATA%\Codebdbd\Aite Bar\ZenEditor\
      index.json
      Documents\
        <document-guid>.json
      Backups\
        <document-guid>\
          <utc-timestamp>-<unique-suffix>.json

No document text appears in logs, telemetry tags, application settings, filenames inside the internal store, or the AiteBar panel.

## Interfaces and Dependencies

`AiteBar/ZenEditorUtility.cs` must define:

    [Utility]
    public sealed class ZenEditorUtility : UtilityBase<ZenEditorWindow>

`AiteBar/ZenEditorModels.cs` must define immutable or serialization-friendly types representing `ZenEditorDocument`, `ZenEditorStoreIndex`, `ZenEditorLoadResult`, and `ZenEditorTheme`.

`AiteBar/ZenEditorStore.cs` must expose asynchronous operations equivalent to:

    Task<ZenEditorLoadResult> InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ZenEditorDocumentSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<ZenEditorDocument> LoadAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(ZenEditorDocument document, bool createSnapshot, CancellationToken cancellationToken = default);
    Task<ZenEditorDocument> CreateAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveIndexAsync(ZenEditorStoreIndex index, CancellationToken cancellationToken = default);

The exact shape may be refined during implementation, but storage mutation must remain asynchronous, atomic, checksum-validated, locally testable through a constructor accepting a root directory, and free of UI dependencies.

`AiteBar/ZenEditorThemeCatalog.cs` must expose exactly five stable IDs: `paper`, `ivory`, `mist`, `graphite`, and `night`. It must normalize unknown IDs to `paper`.

No new runtime NuGet package is required. Use .NET `System.Text.Json`, `System.Security.Cryptography`, asynchronous file APIs, WPF, and existing project helpers. Use `Microsoft.Win32.SaveFileDialog` for TXT export and Windows Forms screen information or existing monitor helpers for monitor selection.

Plan revision note: 2026-07-29, updated after completing the core, UI, integration, font, localization, and documentation milestones; final validation remains.

Plan revision note: 2026-07-29, finalized after clean Release build, 1,000 passing tests, installer creation, publish-resource verification, and documentation of the safe visual-smoke limitation.

Plan revision note: 2026-07-29, corrected after the first installed screenshot exposed global form-style leakage and the same-process indicator overlay. Added a dedicated transparent editor template, explicit indicator suppression, regression contracts, 1,001 passing tests, and rebuilt the installer.

Plan revision note: 2026-07-29, reopened for pre-release hardening after code review found whole-document edit work, UI-thread save preparation, full-body picker enumeration, release metadata drift, and coupled fullscreen flags. Rich-text command behavior is explicitly excluded from this pass at the user's request.

Plan revision note: 2026-07-29, completed the pre-release hardening pass with allocation-safe common edits, background save preparation, index-backed summaries with migration, independent fullscreen flags, version 1.12.0 release metadata, 1,011 passing tests, and a rebuilt verified installer.

Plan revision note: 2026-07-29, reopened after the user confirmed that retained Ctrl+B behavior must also survive document reload. The storage contract now includes backward-compatible bold, italic, and underline ranges while TXT export remains plain text.

Plan revision note: 2026-07-29, completed formatting persistence and paragraph-separated TXT export with backward-compatible checksums, focused WPF/store/export coverage, synchronized documentation, clean Release compilation, and a rebuilt verified installer.

Plan revision note: 2026-07-29, reopened after final review identified formatting-destructive Undo/Redo, quadratic style capture, stale protective snapshots, and whitespace-only paragraph duplication. The plan now requires stateful history, a single-pass style walk, explicit in-memory snapshots, focused regressions, and a fresh complete release-gate run.

Plan revision note: 2026-07-29, completed the final review follow-up with all four defects fixed, 47/47 focused tests, 1,024/1,024 complete tests, a clean Release build, and rebuilt installer hash `3E182E34EA33626D98719E8A3A818E3387753871DA63BCAC29DA7F2B791572E3`.

Plan revision note: 2026-07-29, added cyclic `Shift+Up`/`Shift+Down` theme navigation and recorded the accompanying localized Text Processing comparison-toggle correction, full 1,028-test pass, and rebuilt installer hash `E2E9F40323DBF4512825E6DE13BE18EA47A46B318BAE366DA75A77113BB4185F`.
