# Add the professional prompt builder utility

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept current as work proceeds. This document follows `PLANS.md` at the repository root.

## Purpose / Big Picture

AiteBar will gain a separate built-in utility that turns a short task description into one complete professional prompt for an AI model. The user opens the utility, chooses Programming, Images, Texts, Video and Audio, or Analysis and Ideas, enters a short description, and presses Create prompt. The utility sends one request containing the category-specific instruction and returns one finished prompt. It never asks follow-up questions and never becomes a chat.

The new window must look and behave exactly like the existing Text Processing window: identical dimensions, layout, editor, command rail, model selector, repeat/source/result/diff states, persistence behavior, cancellation, streaming, and fixed error overlay. Only the utility identity, five tabs, explanatory copy, button labels, and AI instructions differ.

## Progress

- [x] (2026-08-02) Confirmed the user-visible contract and inspected the Text Processing service, window, registry, panel catalog, settings, localization, and tests.
- [x] (2026-08-02) Introduced a prompt-building domain service with five stable categories and professional one-shot instructions.
- [x] (2026-08-02) Added the Prompt Builder utility and a Text Processing-derived window without weakening the existing Text Processing safeguards.
- [x] (2026-08-02) Integrated visibility, ordering, settings, hotkey, localization, documentation, and launch paths.
- [x] (2026-08-02) Added focused tests for prompts, UI behavior, integration, localization, and settings cloning.
- [x] (2026-08-02) Ran the final Release build with zero warnings, focused Prompt Builder tests (12/12), the complete suite (1167/1167), installer build, checksum verification, and published-executable startup smoke validation.

## Surprises & Discoveries

- Observation: Text Processing is intentionally fail-closed and rejects language changes or low word overlap.
  Evidence: `TextProcessingWindow.ProcessAsync` uses protected fragments and `TextProcessingService.ViolatesContentPreservation`. A prompt builder must not reuse that validation because a detailed prompt is expected to be substantially longer and structurally different from the short input.

- Observation: The requested visual clone contains substantial mature behavior rather than only XAML.
  Evidence: `AiteBar/TextProcessingWindow.xaml.cs` is about 1,448 lines and owns streaming, model eligibility, retry, undo, window state, and error layout. Sharing its visual contract while keeping prompt-specific generation semantics is safer than making unrelated design changes.

- Observation: The reported `Скопировано` defect combined two separate presentation problems: a transient informational event was routed through persistent status state, and the status surface was placed over the editor grid.
  Evidence: the corrected windows now show copy confirmation through the existing footer information line for two seconds, while failures replace the fixed-height mode-description row. Neither path changes editor geometry or covers the command rail.

## Decision Log

- Decision: Add a separate `PromptBuilderService`, `PromptBuilderWindow`, and `PromptBuilderUtility` rather than adding prompt categories to `TextProcessingMode`.
  Rationale: Text correction must preserve content and language, while prompt generation must expand and restructure the input. Separate domain types prevent prompt generation from weakening the correctness guarantees of the released Text Processing utility.
  Date/Author: 2026-08-02 / Codex

- Decision: Preserve the Text Processing interaction model and visual values exactly, then change only resource keys, five tabs, and request semantics.
  Rationale: The user explicitly requested the same interface and behavior. A fork also lets future prompt-specific copy evolve without changing the text editor.
  Date/Author: 2026-08-02 / Codex

- Decision: Each category system instruction must demand a single ready-to-use prompt, prohibit questions and commentary, preserve the task's language unless another output language is requested, and fill reasonable details without inventing critical facts.
  Rationale: This directly implements “one request — one finished prompt” and makes the output useful across providers.
  Date/Author: 2026-08-02 / Codex

- Decision: Reuse already localized generic Text Processing commands and provider-error messages, while giving Prompt Builder its own identity, category, task, source/result, and primary-action strings.
  Rationale: The two windows intentionally share interaction behavior; reusing identical strings prevents translation drift while the prompt-specific language remains accurate in all four supported cultures.
  Date/Author: 2026-08-02 / Codex

## Outcomes & Retrospective

Implementation is complete. Prompt Builder is independently launchable, uses five localized categories and professional one-shot instructions, and is hidden by default until enabled in Quick Tools. Text Processing retains its independent content-preservation safeguards. Both windows now keep errors inside the fixed mode-description row, while copy confirmation appears in the footer for two seconds and cannot cover the editor or buttons.

The final Release build completed with zero warnings and errors. Focused Prompt Builder tests passed 12/12; the complete test suite passed 1167/1167. `artifacts/installer/AiteBar-Setup.exe` was rebuilt at 79,480,545 bytes with SHA-256 `FBDCA4BF5CCB31A93D465F570D72FDE87C8BF06FD92F9E7C4ADFB251866E4748`. The published `AiteBar.exe` remained alive during the five-second startup smoke check. Provider-backed visual generation still requires a configured AI connection for the final interactive user check.

## Context and Orientation

`AiteBar/TextProcessingWindow.xaml` and `.xaml.cs` implement the reference UI. `AiteBar/TextProcessingService.cs` builds the text-correction requests. `AiteBar/TextProcessingUtility.cs` registers the window as a built-in utility. `AiteBar/UtilityButtonCatalog.cs`, `AiteBar/UnifiedButtonService.cs`, and `AiteBar/MainWindow.xaml.cs` expose utilities on the edge panel. `AiteBar/AppSettingsWindow.xaml` and `.xaml.cs` expose quick-tool visibility and hotkeys. `AiteBar/Models.cs` stores settings, while `AiteBar/AppSettingsService.cs` clones and normalizes them. `AiteBar/Resources/Strings*.resx` contains English, Russian, German, and Ukrainian text.

An AI request is an `AiChatRequest` containing system and user messages plus capability and token constraints. The existing `AiGateway.GenerateTextProcessingStreamingAsync` supplies deterministic writing-model routing suitable for this new utility, but prompt output validation belongs to the new service and must not call content-preservation checks.

The working tree already contains two user-requested fixes in `MainWindow.xaml`, `CommandButtonStyleTests.cs`, `TextProcessingWindow.xaml`, and `TextProcessingVisualContractTests.cs`. They must be preserved and validated together with this feature.

## Plan of Work

First create `PromptBuilderService` with a stable `PromptBuilderCategory` enum. Its request builder calculates a sufficient context/output budget, uses writing-capable text models, and produces a low-temperature one-shot request. Unit tests will verify all five category prompts, the no-questions/no-commentary contract, message structure, and response cleanup.

Next create `PromptBuilderWindow.xaml` from the released Text Processing geometry and controls, and adapt its code-behind to use the prompt service and category enum. Remove content-preservation and protected-marker enforcement only from the new window. Keep streaming preview, cancellation, model selection, automatic routing, repeat from the original task, source/result toggling, diff, clipboard actions, undo, fixed status overlay, and window persistence. The five tab headers and descriptions use Prompt Builder resource keys.

Then add `PromptBuilderUtility`, a catalog entry, main-panel launch switch, settings fields, cloning, normalization, quick-tool setting, hotkey setting and command, localized strings, and documentation entries. The utility is hidden by default to avoid unexpectedly changing existing panels after upgrade, but users can enable it in Settings and launch it through its hotkey once configured.

Finally add integration and visual contract tests. Build and test in Release, run WPF tests in isolated hosts if the combined host stalls, rebuild `artifacts/installer/AiteBar-Setup.exe`, calculate SHA-256, and smoke-start the published executable.

## Concrete Steps

Run all commands from `D:\01_Codebdbd\01_projects\aitebar`.

Focused validation during implementation:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~PromptBuilder"

Final build and main test command:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If the combined WPF host stalls, use the release workflow's non-WPF exclusion filter and run each WPF test class separately. Build the installer only after tests pass:

    .\installer\Build-Installer.ps1

## Validation and Acceptance

Open Prompt Builder from the panel after enabling it in Settings. The window must have the same size and control layout as Text Processing. Select each of the five tabs and verify its category description changes. Enter a short request such as “Сделай лендинг для приложения учёта финансов” under Programming and press Create prompt. The result must be one detailed Russian prompt ready to paste into an AI tool, with no greeting, explanation, follow-up question, or multiple conversational turns.

Repeat must regenerate from the original short task rather than recursively expanding the generated prompt. Show source/result and Show changes must behave as in Text Processing. Empty input, unavailable model, cancellation, and provider errors must use the same fixed status area and must not move the editor. Existing Text Processing tests must remain green.

## Idempotence and Recovery

The work is additive. Re-running builds, tests, resource generation, publish, and installer generation is safe. Existing user settings load with defaults for new fields. If implementation fails, remove only Prompt Builder-specific files and entries; do not revert the four pre-existing user-requested UI fixes or rewrite the published v1.13.0 tag.

## Artifacts and Notes

The final notes will record focused/full test totals, installer path, size, and SHA-256. The GitHub v1.13.0 release is immutable for this task unless the user explicitly requests another release.

## Interfaces and Dependencies

No new package is required. Add `PromptBuilderCategory` with five values: `Programming`, `Images`, `Texts`, `VideoAudio`, and `AnalysisIdeas`. Add `PromptBuilderService.GetSystemPrompt`, `BuildRequest`, and `CleanResponse`. Add `[Utility] PromptBuilderUtility` with ID `PromptBuilder`. Add `PromptBuilderWindow` using existing `AiGateway`, `ModelItem`, `TextProcessingModelPolicy`, shared resource styles, and the established utility lifecycle.

Plan revision note (2026-08-02): Created after confirming that the requested utility is an exact interaction clone with separate prompt-generation semantics.
