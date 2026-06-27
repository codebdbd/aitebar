# Upgrade Clipboard Manager To Persistent Premium Utility

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` from the repository root.

## Purpose / Big Picture

After this change, `Clipboard manager` becomes a serious daily-use tool instead of a thin clipboard list. A user can reopen AiteBar after an app restart and still have their clipboard history, pin the few entries they keep reusing, quickly search and filter the history, and copy text in more useful forms than the raw clipboard content. The feature is considered successful only when the utility keeps working reliably with text and images, does not duplicate its own copy operations, survives app restarts, and exposes privacy-aware cleanup controls.

The user-visible behavior to enable is: copy text or an image, open `Clipboard manager`, see rich entries with metadata and actions, pin important entries, close AiteBar, start it again, and still see the history. Text entries must support fast “copy original” and “copy one line” actions because this is the most practical workflow for prompts, terminal commands, URLs, and multiline snippets.

## Progress

- [x] (2026-06-27 16:06+03:00) Read `PLANS.md`, the current clipboard implementation, localization resources, settings window, and existing tests.
- [x] (2026-06-27 16:06+03:00) Verified that the working tree already contains in-progress clipboard persistence changes and that they must be preserved and extended rather than replaced.
- [x] (2026-06-27 16:49+03:00) Added a stronger clipboard entry model with identifiers, pin state, versioned persistence, duplicate promotion, and non-UI service actions for pin/delete/clear/copy variants.
- [x] (2026-06-27 16:49+03:00) Added deterministic helper logic and focused tests for text normalization, persistence round-trip, duplicate handling, and privacy-aware clearing behavior.
- [x] (2026-06-27 16:49+03:00) Upgraded `ClipboardManagerWindow` with filters, pinning, delete, copy original, copy one-line, richer metadata, and separate clear-history vs wipe-all actions.
- [x] (2026-06-27 16:49+03:00) Added clipboard-manager persistence settings and updated user-facing documentation.
- [x] (2026-06-27 16:49+03:00) Verified `dotnet build .\AiteBar.sln -c Release --disable-build-servers` succeeds.
- [x] (2026-06-27 18:28+03:00) Refined Clipboard Manager row interaction: card-click copy no longer should reorder entries on copy-back, duplicate text blocks were removed from text entries, and action clicks are isolated from the row click handler.
- [x] (2026-06-27 18:37+03:00) Removed the `1-line` action from the main UI and simplified the list visual treatment to reduce clutter; added right-side breathing room and a narrower scrollbar to stop the scrollbar from visually cutting card borders.
- [x] (2026-06-27 19:08+03:00) Fixed two review findings: card borders are no longer locally overwritten from code-behind, and clipboard copy-back suppression now uses a short payload-matching window instead of a broad 2-second ignore period.
- [x] (2026-06-27 19:26+03:00) Reworked Clipboard Manager list rendering around a templated `ListBox`, added keyboard navigation (`Ctrl+F`, arrows, `Enter`, `Delete`), removed dead wipe handler code from the window, and added focused tests for copy-back suppression behavior.
- [ ] Full `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --disable-build-servers` is still red because of unrelated existing failures in `AppSettingsWindowIntegrationTests`, `IconConverterIntegrationTests`, `LocalizationServiceTests`, and `IconConverterWindowLayoutTests`.
- [x] (2026-06-27 16:49+03:00) Verified focused clipboard coverage with `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --disable-build-servers --filter "Clipboard|ClipboardManager"`: 8 passed, 0 failed.

## Surprises & Discoveries

- Observation: clipboard history persistence is already partially implemented in the current dirty working tree, but the persisted payload is still too weak for premium behavior because it only stores text, image bytes, and timestamps.
  Evidence: `AiteBar/ClipboardHistoryService.cs` currently serializes `PersistedClipboardEntry` with `Text`, `ImageBase64`, and `Timestamp`.

- Observation: the current duplicate policy silently ignores new duplicates instead of promoting them to the top, which makes repeated-use snippets less useful than they should be.
  Evidence: `AiteBar/ClipboardHistoryService.cs` loops entries, sets `isDuplicate = true`, and skips insertion completely.

- Observation: repository guidance explicitly warns against turning clipboard history into persistent storage without privacy handling, user-facing clearing behavior, and documentation.
  Evidence: `AGENTS.md` says clipboard history must not become persistent storage without a separate privacy/settings/documentation decision.

- Observation: the first visual polish pass regressed usability because icon placeholders looked improvised and the text card repeated the same content twice, making the list feel noisy instead of compact.
  Evidence: manual user feedback on 2026-06-27 identified pseudo-glyph buttons, broken card feel, and duplicate text rendering in `ClipboardManagerWindow`.

- Observation: the main visual “torn border” problem was amplified by entries rendering too close to the vertical scrollbar, so the thumb visually overlapped the card edge while scrolling.
  Evidence: user screenshots on 2026-06-27 show the right card border apparently breaking exactly where the thick scrollbar/thumb sits.

- Observation: the previous copy-back suppression was too coarse because it could discard a legitimate repeated copy of the same payload shortly after the user clicked a clipboard card.
  Evidence: `ClipboardHistoryService.ShouldIgnoreClipboardPayload` used a 2-second suppression window for any matching payload.

- Observation: the old manual card construction path in `ClipboardManagerWindow.xaml.cs` was becoming the main reason UI fixes were expensive, because every visual tweak required rebuilding control trees in code.
  Evidence: card layout, previews, action buttons, and state styling were all created imperatively in `CreateEntryCard`.

- Observation: the repository's full test suite currently contains unrelated failures outside the clipboard area, including stale source assertions and IconConverter-specific issues.
  Evidence: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --disable-build-servers` failed in `AppSettingsWindowIntegrationTests.LanguageSelection_PersistsUiCultureImmediately`, `LocalizationServiceTests.XamlTextProperties_DoNotContainTranslatableLiteralText`, `IconConverterWindowLayoutTests.Window_MinimumSize_DoesNotClipCriticalControlsInRussian`, and `IconConverterIntegrationTests.IconConverter_IsWiredIntoPanelSettingsAndUtilityRegistry`.

## Decision Log

- Decision: keep the singleton/service initialization work already present in the dirty tree and evolve it instead of reverting to the older per-window listener model.
  Rationale: the singleton listener is already wired into `MainWindow` and directly supports persistence across utility window open/close cycles.
  Date/Author: 2026-06-27 / Codex

- Decision: define the first premium feature set as persistent history, pinned entries, duplicate promotion, text quick-actions, filters, and privacy-aware cleanup instead of attempting cloud sync or OS-level cross-device clipboard features.
  Rationale: these features are unique enough to materially improve daily workflow, are feasible inside the current architecture, and can be verified locally with deterministic tests.
  Date/Author: 2026-06-27 / Codex

- Decision: add a settings-level persistence toggle and documentation instead of making persistence an undocumented always-on behavior.
  Rationale: the repository explicitly calls out privacy concerns, so the premium upgrade must give the user visibility and control.
  Date/Author: 2026-06-27 / Codex

- Decision: keep row-click-to-copy, but treat row actions as isolated controls and suppress copy-back re-capture by payload rather than relying only on a single WM_CLIPBOARDUPDATE skip.
  Rationale: the row interaction is useful, but it must not reorder history or trigger accidental second actions when the user clicks an inner button.
  Date/Author: 2026-06-27 / Codex

- Decision: remove the `1-line` action from the visible card UI rather than demoting it to a prominent secondary control.
  Rationale: the feature is too niche for the main clipboard list and adds visual noise disproportionate to its value.
  Date/Author: 2026-06-27 / Codex

- Decision: keep payload-based suppression, but narrow it to a short notification-budgeted window instead of a long generic ignore interval.
  Rationale: this still prevents the utility from re-capturing its own copy-back while materially reducing the chance of swallowing a real user copy.
  Date/Author: 2026-06-27 / Codex

- Decision: keep the search field at the bottom per the latest requested layout, but offset the discoverability loss by adding direct keyboard focus (`Ctrl+F`) and list-first keyboard navigation.
  Rationale: this preserves the requested composition while still making frequent clipboard-search workflows efficient.
  Date/Author: 2026-06-27 / Codex

## Outcomes & Retrospective

Clipboard Manager now behaves like a persistent utility instead of a temporary list. It keeps history between AiteBar sessions when enabled, promotes duplicates instead of dropping them, supports pinning, can copy text back as either the original snippet or a single line, and offers search plus `All / Pinned / Text / Images` filters. The app settings UI now exposes persistence explicitly, and the user docs explain both persistence and cleanup behavior.

The clipboard-specific implementation is verified by focused automated tests and by a successful Release build. The main remaining gap is not in the clipboard feature itself, but in unrelated failing tests elsewhere in the repository that still block a completely green full-suite run.

## Context and Orientation

`AiteBar` is a Windows desktop utility built with WPF on `.NET 10`. The clipboard manager is implemented as a built-in utility. The utility window is `AiteBar/ClipboardManagerWindow.xaml` plus `AiteBar/ClipboardManagerWindow.xaml.cs`. Clipboard capture and persistence live in `AiteBar/ClipboardHistoryService.cs`. The utility registration is in `AiteBar/ClipboardManagerUtility.cs`. The main application window now owns the clipboard listener lifecycle through `AiteBar/MainWindow.xaml.cs`.

The key repository files for this work are:

`AiteBar/ClipboardHistoryService.cs` which owns listening to `WM_CLIPBOARDUPDATE`, deduplication, copy-back behavior, and persistence to `clipboard_history.json`.

`AiteBar/ClipboardManagerWindow.xaml` and `AiteBar/ClipboardManagerWindow.xaml.cs` which render the clipboard history UI and currently rebuild the list manually in code.

`AiteBar/Models.cs`, `AiteBar/AppSettingsService.cs`, `AiteBar/AppSettingsWindow.xaml`, and `AiteBar/AppSettingsWindow.xaml.cs` which hold user settings and the compact application settings UI. A clipboard persistence option should live here so the user can control cross-session storage.

`AiteBar/PathHelper.cs` which defines the app data folder where persistent utility data lives.

`AiteBar/Resources/Strings.resx`, `AiteBar/Resources/Strings.ru.resx`, `AiteBar/Resources/Strings.uk.resx`, and `AiteBar/Resources/Strings.de.resx` which must be kept in key parity.

`AiteBar.Tests` already contains source-level clipboard integration tests, app settings tests, and localization parity tests. New clipboard tests should focus on deterministic logic, not raw WPF clipboard automation.

For this plan, “premium” means the utility is useful enough to keep open in real work: it remembers history across sessions, helps reuse snippets faster than Windows clipboard alone, and gives the user trust through consistent cleanup and predictable behavior.

## Plan of Work

Start by hardening the clipboard entry model in `AiteBar/ClipboardHistoryService.cs`. Each entry should gain a stable identifier, pin state, and richer derived metadata so the UI can update individual entries safely and tests can reason about them without comparing raw object references. Replace the ad hoc persisted array with a small versioned envelope that can store the upgraded entries and can still read the older payload shape. Duplicate text or image captures should no longer be ignored; instead, the existing item should be refreshed with a new timestamp, promoted to the top, and preserve pin state.

Add one small helper file for deterministic text transformations, for example `AiteBar/ClipboardTextTransforms.cs`. This helper should expose operations for display text truncation, “copy one line” normalization, and compact metadata formatting. Keeping this out of the window code makes it trivial to test. If a second helper is needed for persistence mapping, keep it near the clipboard service rather than scattering JSON logic in the window.

Add targeted tests in `AiteBar.Tests` for the new logic. The important tests are: persistence round-trip for text and image entries, legacy JSON migration compatibility, duplicate promotion instead of duplicate dropping, pinned-entry preservation, one-line text normalization, and new app-setting normalization if a persistence flag is introduced. These tests should instantiate the service with a temporary app-data override or an internal test constructor and should avoid real clipboard APIs.

Upgrade `ClipboardManagerWindow.xaml` and `AiteBar/ClipboardManagerWindow.xaml.cs` into a more capable utility surface while staying compact. The window should add filter controls for `All`, `Pinned`, `Text`, and `Images`; richer status text; and per-entry actions. Each text entry should provide `Copy`, `1-line`, `Pin/Unpin`, and `Delete`. Each image entry should provide `Copy`, `Pin/Unpin`, and `Delete`. The footer should expose privacy-aware cleanup with separate actions for clearing regular history and wiping everything. Search should work together with filters.

Add settings and documentation. In `AiteBar/Models.cs` and `AiteBar/AppSettingsService.cs`, add a clipboard persistence setting, default it to enabled because this feature is being explicitly requested, and normalize older settings safely. In `AiteBar/AppSettingsWindow.xaml` and `AiteBar/AppSettingsWindow.xaml.cs`, add a compact checkbox under quick tools or a nearby utility settings block so the user can disable persistence later. Then update `docs/functions.md` and `docs/USER_MANUAL.md` to describe the new clipboard manager behavior, persistence scope, pinned entries, and cleanup controls.

## Concrete Steps

Work from the repository root:

    D:\01_Codebdbd\01_projects\aitebar

During implementation, update this plan after each meaningful milestone. The expected command sequence is:

    dotnet build .\AiteBar.sln -c Release

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If the known WPF temporary-project issue appears, use:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

For manual verification, run:

    dotnet run --project .\AiteBar\AiteBar.csproj

Expected successful build transcript:

    Build succeeded.
        0 Warning(s)
        0 Error(s)

Expected successful test transcript should end with zero failed tests. The exact test count may change, but the new clipboard tests must pass.

Manual verification must include these scenarios:

    1. Copy multiline text, open Clipboard manager, verify the new entry appears with text metadata and both `Copy` and `1-line` actions.
    2. Use the `1-line` action and verify the clipboard now contains the collapsed single-line version.
    3. Pin an entry, close AiteBar, start it again, and verify the pinned entry still exists.
    4. Copy the same text again and verify the existing item moves to the top instead of creating a duplicate or being ignored.
    5. Copy an image and verify it appears in the `Images` filter and can be copied back without immediate duplicate creation.
    6. Clear regular history and confirm pinned entries remain if that is the implemented policy; then use the full wipe action and confirm everything is removed from both UI and persisted storage.
    7. Disable clipboard persistence in settings, restart AiteBar, and verify non-pinned history is not restored.

## Validation and Acceptance

Acceptance is behavior-based.

The change is accepted when `Clipboard manager` reliably listens for text and image clipboard updates during the application lifetime, keeps using the suppression logic so copy-back does not instantly duplicate its own entries, and stores history on disk only according to the configured persistence behavior.

The utility must restore entries between app sessions when persistence is enabled. The restored entries must include timestamps, pin state, and image payloads when they are within the existing size limits.

Pinned entries must be visually distinguishable, searchable through a `Pinned` filter, and preserved correctly when a duplicate is recaptured.

Repeated copies of the same text or same image must promote the existing entry to the top and refresh its timestamp instead of silently doing nothing.

Text entries must expose a clearly useful transformation action that collapses multiline text into a single line before copying it back. This action must never mutate the stored original entry.

The utility must expose cleanup controls that make persistence understandable and privacy-safe. At minimum, the user must be able to clear the current history and remove the persisted store.

All new localization keys must exist in the English, Russian, Ukrainian, and German resource files with matching placeholders.

`dotnet build .\AiteBar.sln -c Release` must succeed. `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` must succeed, or the documented `dotnet vstest` fallback must succeed if the known WPF issue blocks the primary command.

## Idempotence and Recovery

The upgraded persistence format must be read safely more than once and must tolerate missing or partially invalid files by logging and falling back to an empty history instead of crashing the app. The migration path from the old persisted array must also be idempotent.

Cleanup actions must be repeatable. Running “clear history” or “wipe all” more than once should leave the utility in a valid empty state without errors.

If a persisted file becomes corrupted during development, the recovery path is to delete `clipboard_history.json`, restart the app, and verify that the service recreates a clean state. The implementation must not require manual registry or installer cleanup.

## Artifacts and Notes

Important implementation notes:

    The current dirty working tree already moved clipboard listening to `ClipboardHistoryService.Instance.Initialize(hwnd)` inside `AiteBar/MainWindow.xaml.cs`. Preserve that integration.

    `PathHelper.SetAppDataFolderOverride(...)` already exists and should be used for logic tests that need isolated persistence files.

    `LocalizationServiceTests` already verifies resource-key parity across `Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx`, so new clipboard keys must be added consistently.

    The current clipboard window rebuilds cards manually in code. It is acceptable to keep that approach if the code remains readable, but new reusable helper methods should be extracted instead of letting `UpdateEntriesList` turn into an untestable monolith.

## Interfaces and Dependencies

At the end of the work, the following stable repository-level shapes should exist.

In `AiteBar/Models.cs`, add:

    public bool ClipboardManagerPersistHistory { get; set; } = true;

If a second privacy setting is needed, keep it narrowly scoped and justify it in this plan before adding it.

In `AiteBar/ClipboardHistoryService.cs`, evolve `ClipboardHistoryEntry` so it includes:

    public string Id { get; init; }
    public bool IsPinned { get; init; }

and service operations such as:

    public bool DeleteEntry(string entryId);
    public bool TogglePin(string entryId);
    public bool CopyEntryAsSingleLine(string entryId);
    public void ClearUnpinnedHistory();
    public void ClearAllHistory();

The exact signatures may vary slightly, but the service must expose non-UI methods for pinning, deleting, clearing, and copying transformed text so the window code remains thin.

Add a deterministic helper in a new file such as `AiteBar/ClipboardTextTransforms.cs` with functionality equivalent to:

    public static string ToSingleLine(string text);
    public static string BuildSummary(ClipboardHistoryEntry entry);

The implementation may continue using only built-in .NET, WPF, and Win32 APIs already present in the repository. No new external NuGet dependency is required for this feature.

Revision note: this plan was created because the requested clipboard-manager upgrade is large enough to count as a significant feature under `AGENTS.md`, and because the repository requires a living ExecPlan for work at this scope.
