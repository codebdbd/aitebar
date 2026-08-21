# Harden Text Processing and Prompt Builder

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. It follows `PLANS.md` from the repository root.

## Purpose / Big Picture

Text Processing and Prompt Builder send a user's text to an AI provider and show the answer while it streams. After this change, closing either window during generation preserves the user's original input rather than a partial answer. Draft persistence becomes an explicit opt-in, sensitive drafts can be removed from settings backups, clearing a prompt requires confirmation, and Text Processing retains the model-discovery cache while the utility exists.

## Progress

- [x] (2026-08-21 13:56+03:00) Audited both utilities and ran their existing focused test set: 234 passed.
- [x] (2026-08-21 14:02+03:00) Implemented safe close persistence, private draft defaults, backup cleanup, clear confirmation, and shared model cache.
- [x] (2026-08-21 14:03+03:00) Added focused regression tests; 282 focused tests passed.
- [x] (2026-08-21 14:04+03:00) Release build completed with 0 warnings and 0 errors; full suite passed 1351/1351.
- [x] (2026-08-21 14:03+03:00) Rebuilt the current installer and verified its SHA-256 manifest.
- [x] (2026-08-21 14:29+03:00) Removed Code, Text, and Analytics tabs; legacy modes migrate to Images and legacy Icons to Graphics.
- [x] (2026-08-21 14:29+03:00) Fixed hidden painting-artist leakage, XML/HTML response corruption, context budgeting, and generated named-style references.
- [x] (2026-08-21 14:29+03:00) Creative-only focused tests passed 239/239; full Release suite passed 1354/1354; installer rebuilt.
- [x] (2026-08-21 14:43+03:00) Added Grok Imagine as the default visual target for new settings without altering existing saved target selections.
- [x] (2026-08-21 14:43+03:00) Removed generated aspect-ratio, resolution, dimensions, output-format, and command-flag directives from all visual and video prompt paths; added regression coverage for Grok Imagine.
- [x] (2026-08-21 14:43+03:00) Focused Prompt Builder tests passed 61/61; Release build completed with 0 warnings and 0 errors; full Release suite passed 1359/1359.
- [x] (2026-08-21 15:18+03:00) Made `TextProcessingService.BuildRequest` reject input above the shared 50,000-character limit and added a regression test.
- [x] (2026-08-21 15:18+03:00) Added deterministic Text Processing golden contracts for Markdown/code preservation, multilingual HTML typography, and reasoning-output cleanup.
- [x] (2026-08-21 15:18+03:00) Focused Text Processing tests passed 78/78; Release build completed with 0 warnings and 0 errors; full Release suite passed 1366/1366.

## Surprises & Discoveries

- Observation: both windows replace the editor content with a streaming preview before the response completes, but save the editor before cancelling on close.
  Evidence: `Window_Closing` calls `SaveEditorText` before `_processingCts.Cancel` in both window classes.
- Observation: settings backups retain every serialized field, including saved drafts.
  Evidence: `AppSettingsService.WriteSettingsWithBackupAsync` copies the old settings file before replacement.
- Observation: selecting a painting artist and then switching to a non-artist painting section leaves the artist stored and injected despite being hidden.
  Evidence: `CmbPaintingSection_SelectionChanged` resets only `_paintingStyle`, while `BuildRequest` always replaces `{paintingArtist}`.

## Decision Log

- Decision: make persisted text an opt-in setting for both utilities.
  Rationale: prompt and correction input may contain private material; preserving it by default is not a safe expectation for a desktop quick utility.
  Date/Author: 2026-08-21 / Codex.
- Decision: remove backup files after a successful settings save when draft persistence is disabled.
  Rationale: otherwise the old plaintext remains recoverable in backup files. Deleting backups only after a successful save preserves recovery if the save fails.
  Date/Author: 2026-08-21 / Codex.
- Decision: keep the numeric values of the remaining creative Prompt Builder categories and migrate removed values to Images.
  Rationale: older JSON settings and per-category drafts use integer keys. Reusing values would make an old draft appear under an unrelated creative category.
  Date/Author: 2026-08-21 / Codex.
- Decision: remove output-level named studio, franchise, and artist references while retaining the creative visual qualities as generic descriptors.
  Rationale: model support for named-style prompting varies; generic descriptors provide stable results across providers.
  Date/Author: 2026-08-21 / Codex.
- Decision: make Grok Imagine the default visual target for newly created settings, while preserving existing stored target selections.
  Rationale: this exposes the requested primary workflow without silently overriding a user's established prompt destination.
  Date/Author: 2026-08-21 / Codex.
- Decision: keep output geometry and delivery parameters out of generated prompts.
  Rationale: aspect ratio, resolution, dimensions, and output format belong to the target UI or API and duplicate instructions reduce prompt clarity and can conflict with structured settings.
  Date/Author: 2026-08-21 / Codex.
- Decision: enforce the text-size boundary in both the UI state and the request-building service.
  Rationale: callers outside the WPF window must receive the same bounded-memory and context-safety guarantee as interactive users.
  Date/Author: 2026-08-21 / Codex.

## Outcomes & Retrospective

The audit blockers are resolved. Closing a streaming operation now persists its captured source input. Text Processing and Prompt Builder drafts are opt-in, disabling either setting clears its data and deletes retained settings backups after the new settings file is safely written. Prompt Builder now confirms destructive clear, and Text Processing retains an `AiGateway` for cache reuse.

Focused regression tests passed 282/282. `dotnet build .\\AiteBar.sln -c Release` completed with 0 warnings and 0 errors. `dotnet test .\\AiteBar.Tests\\AiteBar.Tests.csproj -c Release` passed 1351/1351. Manual verification on real Windows remains useful for keyboard-only interaction, screen-reader announcements, and close-during-stream behavior against a real provider.

The current installer is `artifacts/installer/AiteBar-Setup-1.15.14.exe`; its SHA-256 is `1CFDC9961A459A72B892D66D98412353B942BD77590407BD4E406E6EB58C5ADB`.

The refreshed installer SHA-256 is `3110A5098EB5B93B95B480660FEA2E8E7BB8FC519C8E6DA4BDCE4A63A14BAC20`.

Grok Imagine is now a localized visual target and default for new settings. All image, painting, graphics, animation, thematic-image, and video prompt instructions explicitly leave aspect ratio, resolution, dimensions, output format, and command flags to the target interface. Focused tests passed 61/61, the Release build had 0 warnings and 0 errors, and the full suite passed 1359/1359. The refreshed installer SHA-256 is `859C13DD52F9005C97220D051D170FA4AA08FCB21EE3C9A4EE4F2A731DE298BE`.

Text Processing now applies the same 50,000-character boundary at its public request-building API as in its WPF UI. Golden contracts protect Markdown/code, multilingual HTML typography, and reasoning cleanup without using a network or API key. Focused tests passed 78/78, the Release build had 0 warnings and 0 errors, and the full suite passed 1366/1366.

## Context and Orientation

`AiteBar/TextProcessingWindow.xaml.cs` and `AiteBar/PromptBuilderWindow.xaml.cs` own the WPF windows. They stream AI output into `TxtEditor`, persist window state through `AppSettingsService`, and cancel work through `CancellationTokenSource` when closing. `AiteBar/Models.cs` defines persisted settings. `AiteBar/AppSettingsService.cs` serializes those settings to JSON and rotates five backup files. `AiteBar/AppSettingsWindow.xaml` and its code-behind expose global user settings. `AiteBar/TextProcessingUtility.cs` and `AiteBar/PromptBuilderUtility.cs` create windows and can retain an `AiGateway`, the component that caches model discovery.

## Plan of Work

Add a boolean persistence choice for Text Processing and change the existing Prompt Builder draft choice to default false. Update cloning and the settings UI so users can opt in and can turn either choice off. When either choice is turned off, clear the associated in-memory settings fields before saving. After the settings save succeeds, delete old backup files so earlier plaintext drafts are not retained.

Change each window's close handler to cancel requests first and save its known source text while streaming. The normal editor value is still saved when idle. Add a confirmation dialog before Prompt Builder clears its editor.

Make `TextProcessingUtility` retain one `AiGateway`, matching Prompt Builder, so model metadata remains cached across newly opened windows.

Add unit and integration-style tests for defaults, backup cleanup, safe close persistence structure, clear confirmation, and retained gateway construction. Run the focused tests, then the full Release build and test suite.

## Concrete Steps

From `D:\01_Codebdbd\01_projects\aitebar`, edit the named files with the changes described above. Run:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~TextProcessing|FullyQualifiedName~PromptBuilder|FullyQualifiedName~AppSettingsService"
    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

The expected result is zero failed tests and a successful Release build.

## Validation and Acceptance

Open either utility, enter text, begin generation, and close it before streaming completes. On reopening, the original input must be present, not a partial response. In app settings, both draft switches must be off by default. Disabling either switch and saving must remove its saved data and settings backups. Prompt Builder must ask for confirmation before clearing non-empty text. Reopening Text Processing repeatedly must reuse model metadata through its retained gateway.

## Idempotence and Recovery

The setting cleanup is idempotent: clearing already-empty draft fields and missing backup files is safe. If settings saving fails, backup deletion must not run; the user can retry Save. Tests use temporary directories and clean them up.

## Artifacts and Notes

Before implementation, the focused suite completed with:

    Passed: 234, Failed: 0

After implementation:

    Focused tests: Passed: 282, Failed: 0
    Release build: 0 warnings, 0 errors
    Full tests: Passed: 1351, Failed: 0
    Installer SHA-256: 1CFDC9961A459A72B892D66D98412353B942BD77590407BD4E406E6EB58C5ADB

## Interfaces and Dependencies

`AppSettings` will expose `SaveTextProcessingDraft` alongside `SavePromptBuilderDrafts`, both defaulting to false. `AppSettingsService` will expose a safe backup-cleanup method for the settings window to call only after `SaveAppSettings` succeeds. No new third-party dependencies are required.

Change note: created and completed on 2026-08-21 after the release audit to track fixes for streaming-close integrity, private drafts, destructive clear, and model-cache reuse.
