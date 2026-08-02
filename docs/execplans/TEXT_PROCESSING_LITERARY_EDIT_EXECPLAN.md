# Add a Literary Editing mode to Text Processing

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

After this change, a user can open AiteBar's Text Processing utility and choose a fourth tab, `Литературная редакция`. That mode improves clarity, fluency, rhythm, and style while preserving the input language, meaning, facts, names, intended tone, and paragraph structure. It returns only the edited text and remains separate from proofreading, typography, and technical cleanup. Proofread remains the selected tab whenever a new window opens.

## Progress

- [x] (2026-08-02 01:00Z) Inspected the mode enum, WPF tabs, mode-selection code, prompt builder, response validator, localizations, tests, and current documentation.
- [x] (2026-08-02 01:14Z) Added the Literary Editing enum value, professional English prompt, mode-specific generation temperature, and a response-preservation threshold suitable for controlled rewriting.
- [x] (2026-08-02 01:14Z) Added the fourth localized WPF tab and synchronized runtime selection, descriptions, accessibility, and layout tests.
- [x] (2026-08-02 01:14Z) Updated current user, function, and architecture documentation to describe four independent modes.
- [x] (2026-08-02 01:14Z) Passed 143 focused and 1128 full tests, built a zero-warning Release, rebuilt the installer, and recorded artifact evidence.

## Surprises & Discoveries

- Observation: The current response validator uses one 35 percent distinct-word-overlap threshold for every mode.
  Evidence: `TextProcessingService.ViolatesContentPreservation` has no mode input, and `TextProcessingWindow.ProcessAsync` calls it for Proofread, Typography, and Cleanup alike. A legitimate literary rewrite can preserve meaning while changing more wording than a technical correction, so the new mode needs a lower threshold while retaining the dominant-script translation guard.

- Observation: Existing numeric enum values are implicitly persisted in settings even though a new window now always starts on Proofread.
  Evidence: `AppSettings.TextProcessingLastMode` remains in the settings model and historical data may contain values 0 through 2. The new mode must therefore be appended as value 3 rather than inserted between existing values.

## Decision Log

- Decision: Add `TextProcessingMode.LiteraryEdit = 3` and keep Proofread as the unconditional startup selection.
  Rationale: Appending preserves existing serialized numeric meanings, and the established startup contract requires error checking to remain first.
  Date/Author: 2026-08-02 / Codex

- Decision: Literary Editing may rewrite awkward wording and remove unintentional repetition, but may not translate, invent facts, change names, alter narrative perspective, or reorganize paragraphs.
  Rationale: This creates a useful editorial mode without turning the utility into a generative writer or making its output unpredictable.
  Date/Author: 2026-08-02 / Codex

- Decision: Keep dominant-script rejection for every mode and use a 15 percent distinct-word-overlap floor for Literary Editing instead of the 35 percent technical-mode floor.
  Rationale: Script changes reliably catch Russian-to-English and similar translations. A lower lexical floor allows controlled same-language rewriting while still rejecting unrelated or same-script translated output with almost no shared vocabulary.
  Date/Author: 2026-08-02 / Codex

## Outcomes & Retrospective

The fourth `Литературная редакция` tab is complete and localized in English, Russian, Ukrainian, and German. It uses an independent professional English prompt, temperature 0.4, and a 15 percent lexical-overlap safety floor while retaining dominant-script translation rejection. Existing modes keep their numeric values, prompts, temperatures, and 35 percent technical-edit floor; Proofread remains the startup tab. Current documentation describes the controlled stylistic scope. Release builds without warnings or errors, all 1128 tests pass, and the installer was rebuilt.

## Context and Orientation

`AiteBar/TextProcessingMode.cs` defines the preset mode values. `AiteBar/TextProcessingService.cs` builds the provider-facing system prompt, sets generation parameters, protects technical fragments, cleans responses, and checks whether output improperly changes language or content. `AiteBar/TextProcessingWindow.xaml` defines the WPF tab row, while `AiteBar/TextProcessingWindow.xaml.cs` maps tab tags to enum values, enables tabs, displays localized descriptions, sends requests, and validates completed output.

Localized user-facing strings live in `AiteBar/Resources/Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx`. Current behavior is documented in `docs/USER_MANUAL.md`, `docs/functions.md`, and `docs/architecture.md`. Automated coverage lives primarily in `AiteBar.Tests/TextProcessingServiceTests.cs`, `TextProcessingWindowLayoutTests.cs`, and the source-contract tests whose names begin with `TextProcessing`.

A system prompt is the instruction sent to the AI provider separately from the user's text. A preservation threshold is the minimum fraction of distinct words that the input and output must share after protected technical fragments are removed. It is a final safety check, not a request to the model.

## Plan of Work

Append `LiteraryEdit` to `TextProcessingMode`. Add a concise professional English prompt to `TextProcessingService.GetSystemPrompt`, use the existing shared language/content and protected-token contracts, and select a controlled temperature of 0.4. Expose a small mode-to-overlap-threshold helper so the window can use 0.15 only for Literary Editing and retain 0.35 for the existing technical modes.

Add a fourth `TabItem` named `ModeLiteraryEdit` to `TextProcessingWindow.xaml`. Extend the tag switch, enabled-state updates, selection synchronization, and localized description switch in code-behind. Add all required resource keys in the four resource files. Preserve the current fixed window geometry and the existing Proofread startup selection.

Update tests to enumerate all four modes, assert the new English prompt and temperature, assert enum value stability, verify the fourth real WPF tab loads and fits, and prove the literary threshold is lower without disabling dominant-script translation rejection. Update current documentation from three technical modes to four independent modes and explain that Literary Editing intentionally permits controlled wording improvements.

## Concrete Steps

Run all commands from `D:\01_Codebdbd\01_projects\aitebar`.

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~TextProcessing"
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    .\installer\Build-Installer.ps1

The Release build must report zero warnings and zero errors. Focused and full tests must report zero failures. If the WPF test host remains alive after test completion, identify only processes whose command line points to this repository, stop only those processes, and rerun using the documented `dotnet vstest` fallback.

## Validation and Acceptance

Open Text Processing and observe four tabs in this order: Proofread, Typography, Cleanup, Literary Editing. Proofread is selected initially. Select Literary Editing and verify the description explains style improvement with meaning and facts preserved. Enter awkward same-language prose and process it; the result may improve wording and rhythm but must remain in the same language and must not add facts. `Repeat` must reuse Literary Editing.

Automated acceptance requires tests proving that values 0, 1, and 2 retain their old enum meanings and Literary Editing is 3; that its request uses its own prompt and temperature; that English provider-facing instructions contain no Cyrillic; that Russian-to-English output is rejected in the new mode; and that a legitimate same-language rewrite can pass the lower overlap threshold.

## Idempotence and Recovery

All source and resource edits are repeatable. No settings migration is needed because the enum value is appended. If a localized key is missing, WPF may show the resource key instead of a label; focused localization and WPF construction tests must catch that before installer creation. Existing user settings and API credentials must not be read or modified.

## Artifacts and Notes

Validation evidence:

    Release build: warnings 0, errors 0
    Focused Text Processing tests: passed 143, failed 0
    Full test suite: passed 1128, failed 0
    Installer: artifacts\installer\AiteBar-Setup.exe
    Installer size: 79,463,329 bytes
    SHA-256: 58DF1D183908B876A6491962C0E217F7864922A3980BA05A305F2ED850A2C61B

Signing was skipped because no PFX certificate was supplied.

## Interfaces and Dependencies

No new package is required. `TextProcessingMode` gains `LiteraryEdit = 3`. `TextProcessingService.GetSystemPrompt(TextProcessingMode)` and `BuildRequest(TextProcessingMode, string, int?)` remain the public prompt entry points. Add an internal mode-to-threshold helper used by `TextProcessingWindow` so pure policy is independently testable. The AI gateway, provider clients, settings schema, and credential storage remain unchanged.

Plan revision note (2026-08-02 01:00Z): Created this self-contained plan after mapping the existing three-mode implementation and identifying the need for an appended enum value and mode-specific lexical-preservation threshold.

Plan revision note (2026-08-02 01:14Z): Completed the feature after adding the fourth localized tab, prompt and mode-aware validation policy, focused and full automated coverage, current documentation, and a rebuilt hashed installer.
