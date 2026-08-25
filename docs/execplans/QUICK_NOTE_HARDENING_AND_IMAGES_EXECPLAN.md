# Harden Quick Note persistence and add embedded images

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. It follows `PLANS.md` in the repository root.

## Purpose / Big Picture

Quick Note must remain a fast, safe single-note editor when Windows temporarily locks the clipboard and a user works with a long note. It must also accept a picture pasted from the clipboard, selected from a local file, or dropped into the editor. Pictures are stored inside `QuickNote.aite-note`, a portable WPF package, not as file paths.

After this work, a user can paste an image, choose one with a toolbar button, or drag an image file into the editor and see it inline. Saving and reopening the note retains the image. Oversized or unsupported images are rejected with a visible localized error, and failures to write to the Windows clipboard do not crash the window. Opening the note externally creates an RTF export and cannot corrupt the package source of truth.

## Progress

- [x] (2026-08-23 21:30Z) Read `PLANS.md`, Quick Note implementation files, tests, and the existing Quick Note contract.
- [x] (2026-08-23 21:30Z) Recorded the required scope: persistence race hardening, clipboard resilience, link-detection performance, accessibility, and embedded images from paste, file picker, and drag-and-drop.
- [x] (2026-08-23 22:20Z) Designed and proved an RTF-safe PNG marker representation that survives save/reload without external file references.
- [x] (2026-08-23 21:38Z) Proved that WPF native RTF serialization loses inline image controls on reload.
- [x] (2026-08-23 22:20Z) Implemented optimistic concurrency revalidation immediately before replacing the note file, preserving a conflict copy on divergence.
- [x] (2026-08-23 22:20Z) Centralized clipboard text writes and clipboard image reads behind a testable interface with retry and localized failure state.
- [x] (2026-08-23 22:20Z) Added image insertion, limits, drag/drop, paste interception, file picker, document styling, and RTF adaptation.
- [x] (2026-08-23 22:28Z) Added accessibility names and visible keyboard focus for Quick Note formatting controls.
- [x] (2026-08-23 22:28Z) Capped and cached link detection for long paragraphs; exposed the localized paused status when the cap applies.
- [x] (2026-08-23 22:28Z) Added focused image tests and ran the focused Quick Note suite.
- [x] (2026-08-23 22:45Z) Preserved inline formatting adjacent to exported image markers and cached normalized PNG payloads to avoid repeated encoding during limit checks and saves.
- [x] (2026-08-23 22:45Z) Added AutomationProperties coverage for pin and close controls, with source-level regression coverage.
- [x] (2026-08-23 23:25Z) Switched default persistence to `QuickNote.aite-note`, migrated legacy `QuickNote.rtf` on first load, and retained RTF as an explicit external-editor export.
- [x] (2026-08-24 00:25Z) Added explicit Markdown/TXT exchange: Markdown uses a sibling PNG assets directory, TXT exports plain text, and import makes a conflict-copy backup before replacement.

## Surprises & Discoveries

- Observation: Quick Note uses a normal RTF file and converts app-only code-block controls into textual fence markers before save.
  Evidence: `AiteBar/QuickNoteService.cs` calls `QuickNoteRtfAdapter.CreateExportDocument`, and `AiteBar/QuickNoteRtfAdapter.cs` reconstructs code sections on load.
- Observation: WPF `BlockUIContainer` controls are not directly portable as interactive RTF content.
  Evidence: the existing adapter deliberately removes the code header control during export and rebuilds it after RTF load.
- Observation: WPF native RTF serialization also loses `InlineUIContainer(Image)` content.
  Evidence: `QuickNoteServiceTests.LoadAsync_PreservesInlineImageInRtf` created a 1x1 `BitmapSource`, saved it through `QuickNoteService`, and failed because the loaded document had no image container.
- Observation: current clipboard writes are direct WPF calls although `IQuickNoteClipboard` exists.
  Evidence: `QuickNoteWindow.xaml.cs` calls `Clipboard.SetText` directly while `QuickNotePersistence.cs` declares the unused abstraction.
- Observation: WPF RTF loading normalizes some markup element types while retaining their formatting properties.
  Evidence: an exported `Bold` inline loads as `Span` with `TextElement.FontWeightProperty == FontWeights.Bold`; the focused image round-trip regression asserts the retained property rather than an implementation-specific CLR type.

## Decision Log

- Decision: Images will be embedded as compressed PNG bytes inside dedicated hidden RTF marker runs rather than stored as paths.
  Rationale: RTF support for arbitrary WPF image controls is not dependable across external editors. A base64 PNG marker is self-contained and can be restored by Quick Note while remaining one portable RTF file. The export path wraps only app-generated marker text in the RTF hidden-text control, and the load path reveals it in memory before WPF parses the document.
  Date/Author: 2026-08-23 / Codex.
- Decision: The image safety limit is 8 MiB encoded input, 16 megapixels decoded, 1,600 pixels on the longest edge, and 24 MiB total embedded PNG payload in one note.
  Rationale: Quick Note is a lightweight utility. The limits prevent decompression bombs, runaway memory use, and impractically large autosaves while allowing normal screenshots and photos.
  Date/Author: 2026-08-23 / Codex.
- Decision: The marker run uses 1pt transparent text rather than a raw RTF `\\v` group.
  Rationale: WPF owns RTF serialization and does not expose a stable hook for inserting control words. The marker is invisible in Quick Note and restored on load; external RTF editors are not expected to render embedded images.
  Date/Author: 2026-08-23 / Codex.
- Decision: Export splits a paragraph around an image marker and XAML-clones each non-image inline segment.
  Rationale: This preserves font properties, links, and paragraph formatting while retaining a marker form that WPF RTF can reload. The PNG payload is cached on the image container so validation and save do not repeatedly encode the same bitmap.
  Date/Author: 2026-08-23 / Codex.
- Decision: `QuickNote.aite-note` is the primary file and uses WPF `DataFormats.XamlPackage`; `QuickNote.rtf` is an export for external editors.
  Rationale: XamlPackage persists the WPF document and image resources reliably in one portable file. RTF cannot safely retain the editor's interactive image elements. The existing RTF file is read once and migrated so current users do not lose notes.
  Date/Author: 2026-08-23 / Codex.
- Decision: Markdown is the editable interchange format and uses a sibling `*-assets` directory for PNG files; TXT is plain-text-only.
  Rationale: Markdown remains readable and version-control friendly without embedding large base64 payloads. Relative asset paths permit a user to move the Markdown file and asset directory together, while importing embeds validated images back into the package.
  Date/Author: 2026-08-24 / Codex.
- Decision: The persistence service will compare the target file snapshot expected by the caller immediately before replace and reject replacement on mismatch.
  Rationale: This narrows the check-then-write race and routes the UI through its existing conflict-copy behavior instead of silently overwriting an external save.
  Date/Author: 2026-08-23 / Codex.
- Decision: Icon-only controls receive UI Automation names and visible keyboard focus states.
  Rationale: Tooltips are not an adequate accessible name or keyboard focus indication.
  Date/Author: 2026-08-23 / Codex.

## Outcomes & Retrospective

Implemented embedded image insertion from clipboard, file picker, and file drop. The primary `QuickNote.aite-note` package retains images natively; RTF uses markers only for the external export path. Image input is bounded to 8 MiB encoded data, 16 megapixels decoded, 1,600 pixels per side, and 24 MiB per note. Clipboard copying now fails safely, link scans are cached and capped for long paragraphs, and formatting adjacent to image markers is retained during RTF export/import. Markdown/TXT exchange is explicit in the context menu: Markdown writes validated PNG assets beside the file and imports them by safe relative paths, while TXT is plain text.

Validation: `dotnet build .\\AiteBar.Tests\\AiteBar.Tests.csproj -c Release --no-restore` completed successfully and focused `dotnet vstest ... --TestCaseFilter:"FullyQualifiedName~QuickNote"` passed 110/110 after Markdown/TXT exchange was added. The full `dotnet test` run spawned stuck WPF test-host child processes in this environment; they were stopped after they held the test DLL. A solution build also returns a nonzero status after offline NuGet warnings while reporting zero compiler errors. These environment issues require CI or a clean desktop session for final full-suite confirmation.

Known limitation: the final external-change check substantially narrows, but cannot make a filesystem replace a true compare-and-swap against a non-cooperating external editor. If a change is observed before replacement, Quick Note writes a conflict copy instead of overwriting the target. External RTF editors do not render Quick Note's private image markers as images.

## Context and Orientation

`AiteBar/QuickNoteWindow.xaml` declares the Quick Note visual tree and editor. `AiteBar/QuickNoteWindow.xaml.cs` currently owns editor commands, window lifetime, save orchestration, link detection, and theming. `AiteBar/QuickNoteService.cs` reads and writes `QuickNote.aite-note`, tracks a snapshot of the last known file, migrates a legacy RTF once, creates conflict copies, and produces RTF exports. `AiteBar/QuickNoteRtfAdapter.cs` converts app-only structures to RTF-safe paragraphs for the export path. `AiteBar/QuickNoteDocumentFormatting.cs` constructs code-block WPF elements. `AiteBar/QuickNotePersistence.cs` holds interfaces used by the window.

An embedded image is a PNG byte sequence held in an `InlineUIContainer` while the note is open. During RTF export, the adapter replaces it with a paragraph containing a private marker and base64 data. During load, the adapter validates and decodes the marker into an image element. The marker is an implementation detail and must never be shown as normal note text.

## Plan of Work

First, add small, testable helper types for file snapshots, clipboard retry, image validation, PNG conversion, image size fitting, and document image recognition. Keep byte limits and marker strings in one helper. Extend `IQuickNotePersistence` so save receives the expected target snapshot and can signal a conflict without overwriting the file. The window will perform conflict-copy handling for a pre-save or final save conflict.

Next, extend the RTF adapter to export code blocks and images. The image adapter must only accept its own marker format, must reject invalid base64 and over-limit data without failing an entire note load, and must retain unrecognized paragraphs as text. Add image controls using `InlineUIContainer` and cap display dimensions while retaining a frozen `BitmapSource` so the data can be read safely.

Then update the XAML and code-behind. Add an image toolbar button, an editor context-menu command, `AllowDrop`, image file drag handlers, and a paste command handler that prefers bitmap content then falls back to the default text paste behavior. All clipboard operations go through an injected retrying service. Add localized status values for copy/image failures and a paused link-detection status. Introduce a short-lived cache keyed by paragraph identity and document version; skip regex scans when a paragraph exceeds the chosen threshold.

Finally, apply automation names and focus triggers to every glyph control, split non-visual helpers from the window where practical, add focused tests, and validate build and user-visible scenarios.

## Concrete Steps

Run from `D:\01_Codebdbd\01_projects\aitebar` after each milestone:

    dotnet build .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-restore
    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll --TestCaseFilter:"FullyQualifiedName~QuickNote"
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-restore
    dotnet build .\AiteBar.sln -c Release --no-restore

If `dotnet test` fails due to a temporary WPF generated-file lock, use the VSTest command above after a successful test-project build. Do not delete `obj` or unrelated working-tree changes.

## Validation and Acceptance

Acceptance requires the following observable behavior. A note changed by another process between the initial save check and the final replacement produces a conflict copy and does not overwrite the external file. A locked clipboard produces a status error and leaves the application usable. A 100,000-character paragraph does not run full regex scanning on every mouse move and shows the paused link status.

For images, paste a screenshot, choose a PNG/JPEG file, and drop a PNG/JPEG file into the editor. Each operation inserts a visible inline image, saves it, closes the window, and restores it after reopening. Invalid formats, files over 8 MiB, decoded images over 16 megapixels, and notes whose embedded image payload exceeds 24 MiB are rejected without changing the existing document. Keyboard-only navigation displays focus and screen readers receive meaningful names for toolbar and code-copy commands.

Focused tests must include persistence race detection, clipboard failure/retry, image validation and encode/decode round trips, image RTF round trips, input routing, long-paragraph link cap, and AutomationProperties values. The full suite and Release build must complete without errors.

## Idempotence and Recovery

All code changes are additive or replacements of existing Quick Note paths. Re-running tests is safe. Failed image insertion must not mutate the document. A failed save leaves the temp file cleanup best-effort and leaves the in-memory note marked pending. Do not delete user RTF or conflict copies during migration; malformed app image markers are preserved as ordinary text instead of being discarded.

## Artifacts and Notes

Implementation evidence and final test counts will be recorded here after each completed milestone.

## Interfaces and Dependencies

No new NuGet package is required. Use WPF `BitmapSource`, `PngBitmapEncoder`, `BitmapDecoder`, `InlineUIContainer`, `OpenFileDialog`, `DataObject`, and `AutomationProperties`. At completion, `QuickNoteService` exposes a save operation that can report a target snapshot mismatch, and `IQuickNoteClipboard` exposes safe text and image clipboard reads/writes. Image helper APIs are internal and accept byte arrays or `BitmapSource`; they must not access the filesystem directly.

Plan revised on 2026-08-23 after the native RTF image round-trip test failed; the image marker is now explicitly hidden in external RTF viewers while remaining embedded and portable.
