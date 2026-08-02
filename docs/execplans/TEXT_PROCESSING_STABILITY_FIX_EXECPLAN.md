# Restore predictable Text Processing startup, AI errors, and layout stability

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

The Text Processing utility must open on Proofread every time, must not claim that no AI connections exist when configured connections are merely cooling down or rate-limited, and must not resize the editor when an error appears. A user can verify the result by opening the utility after previously selecting another tab, repeatedly processing until a provider returns a limit, and observing both the accurate message and unchanged editor geometry.

## Progress

- [x] (2026-08-01 08:26Z) Inspected the active user settings, current Text Processing window, AI gateway, recent commits, and existing tests.
- [x] (2026-08-01 08:26Z) Confirmed that ten AI connections are enabled while the displayed message incorrectly says none are configured.
- [x] (2026-08-01 08:26Z) Made Proofread the unconditional startup mode and removed tab-selection persistence from the window.
- [x] (2026-08-01 08:26Z) Added typed AI availability failure reasons and mapped cooldown, quota, authentication, network, timeout, and temporary availability to distinct localized messages.
- [x] (2026-08-01 08:26Z) Moved the error banner from the DockPanel layout flow into an overlay over the work grid.
- [x] (2026-08-01 08:26Z) Passed a zero-warning Release build and 146 focused Text Processing, AI routing, and streaming tests.
- [x] (2026-08-01 08:33Z) Passed all 1098 tests, rendered and inspected the real WPF window, rebuilt the installer, and recorded its SHA-256.
- [x] (2026-08-01 10:04Z) Reproduced a repeated-processing translation from user screenshots and added a mandatory language/content contract plus fail-closed response validation.
- [x] (2026-08-01 10:04Z) Added RU/UK/EN/DE rejection messages and passed 84 focused service, window-contract, and localization tests.
- [x] (2026-08-01 10:07Z) Passed all 1106 tests and rebuilt the installer for the language-preservation revision.
- [x] (2026-08-01 11:02Z) Added a Text Processing-specific three-tier model policy, enforced it in both the UI and gateway, passed 1116 tests, and rebuilt the installer.
- [x] (2026-08-01 11:50Z) Replaced the Proofread prompt with one direct sentence, made streaming fallback skip empty routes before the first chunk, passed 1118 tests, and rebuilt the installer.
- [x] (2026-08-02 00:42Z) Rewrote all Text Processing system prompts as concise professional English instructions, passed 1121 tests, and rebuilt the installer.

## Surprises & Discoveries

- Observation: The active settings file stores `TextProcessingLastMode` as `2`, which is Cleanup, and the constructor explicitly restores it.
  Evidence: `%APPDATA%\Codebdbd\Aite Bar\settings.json` reports `TextProcessingLastMode: 2`; `TextProcessingWindow` assigned `ParseSavedMode(...)` before `InitializeComponent`.

- Observation: The application has ten enabled AI connections, so the screenshot message saying no neural network was added is factually false.
  Evidence: The active settings contain ten enabled Cerebras, Gemini, and Groq connections. When every route is skipped by cooldown or quota state, `NoAvailableConnectionException` had no inner exception and the window mapped that case to `TextProcessing_ErrorNoModels`.

- Observation: `StatusBorder` was a top-docked child whose visibility changed between Collapsed and Visible.
  Evidence: In WPF a visible top-docked child consumes height before the remaining editor grid is arranged, so the error banner reduced the editor and moved its footer informer.

- Observation: The prompts already prohibited translation, but a model still translated a Russian sentence to English after repeated processing.
  Evidence: User screenshots show the Russian original and an accepted English result from the same Proofread workflow. Prompt-only constraints are therefore insufficient for preserving user text.

- Observation: Automatic routing treated every non-technical free text model as equally suitable, including the Arabic-focused ALLaM-2-7b model shown in the failed run.
  Evidence: `TextProcessingWindow.IsEligibleModel` and `AiGateway.GetEligibleModels` only rejected technical modalities; neither had a multilingual quality allowlist or a Text Processing-specific exclusion policy.

- Observation: The gateway returned an `AiGatewayStream` immediately after receiving HTTP success, before verifying that the provider stream contained text.
  Evidence: `GenerateStreamingCoreAsync` selected the first route after `GenerateStreamingAsync`; an empty SSE stream was marked successful by `ObserveStreamAsync`, leaving the window to report `TextProcessing_ErrorEmptyResponse` without trying another route.

## Decision Log

- Decision: Always initialize a newly created Text Processing window with `TextProcessingMode.Proofread` and do not persist tab changes.
  Rationale: The required first workflow is error checking. Restoring an incidental previous tab makes startup unpredictable and directly contradicts the requested behavior.
  Date/Author: 2026-08-01 / Codex

- Decision: Carry a typed `AiAvailabilityFailureReason` on `NoAvailableConnectionException`.
  Rationale: Parsing exception text is fragile. The AI gateway owns cooldown and quota state and is the only layer that can accurately distinguish missing configuration from temporary unavailability; the window remains responsible only for localized presentation.
  Date/Author: 2026-08-01 / Codex

- Decision: Render the error banner in the editor-and-command grid as a top overlay.
  Rationale: An overlay remains visible and assertive without participating in the vertical layout calculation, so editor height and the bottom informer remain stable.
  Date/Author: 2026-08-01 / Codex

- Decision: Validate every completed response before committing it as a successful result.
  Rationale: The utility is corrective, not generative. A dominant-script change or very low word overlap indicates translation or an excessive rewrite. Such output must be rejected, the pre-request editor text restored, and a localized error shown. The validator removes shared protected technical fragments before comparison and runs in linear time for the 50,000-character limit.
  Date/Author: 2026-08-01 / Codex

- Decision: Classify Text Processing models as certified for automatic routing, manual-only, or unsupported.
  Rationale: Automatic mode must be conservative and predictable. Known multilingual writing families may route automatically; unknown text models remain available for an explicit user choice; technical and known narrow-language families such as ALLaM are hidden and rejected by the gateway. The policy is scoped to Text Processing so other AI utilities keep their existing model access.
  Date/Author: 2026-08-01 / Codex

- Decision: Track automatic availability separately from manual list availability.
  Rationale: When only unknown but otherwise compatible models are connected, automatic processing must remain disabled while the model selector stays enabled so the user can make an explicit manual choice.
  Date/Author: 2026-08-01 / Codex

- Decision: Proofread uses exactly one system-prompt sentence, while the gateway prefetches the first text chunk before committing to a route.
  Rationale: Proofreading is a narrow task and does not need a long rule sheet. Route reliability is a transport concern: a route that produces no text must be cooled down and skipped before the UI sees it, preserving streaming after the first real chunk.
  Date/Author: 2026-08-01 / Codex

- Decision: Keep all provider-facing system instructions in concise professional English while preserving user text in its original language.
  Rationale: English gives a consistent instruction surface across providers and input languages. Each prompt must define one narrow transformation, explicit non-goals, protected content, and output format without conversational wording or redundant rule lists.
  Date/Author: 2026-08-02 / Codex

## Outcomes & Retrospective

The startup, availability, overlay, language-preservation, model-routing, empty-stream, and prompt-quality repairs are complete. All provider-facing system instructions are now concise professional English. Proofread remains one direct sentence with no appended rule blocks; Typography and Cleanup use narrowly scoped transformations followed by compact shared language/content and protected-token contracts. Deterministic response validation still rejects translation or excessive rewriting after generation. Automatic routing accepts only explicitly certified multilingual model families, and empty streams are skipped before the UI commits to a route. Final validation passed 1121 of 1121 tests and rebuilt the installer.

## Context and Orientation

`AiteBar/TextProcessingWindow.xaml` defines the WPF window. Its three tabs select Proofread, Typography, and Cleanup. `AiteBar/TextProcessingWindow.xaml.cs` owns startup state, model loading, processing, and localized UI messages. `AiteBar/AiGateway.cs` enumerates configured connections and models, applies rate-limit or failure cooldowns, and tries fallback routes. `AiteBar/AiModels.cs` contains the exception types shared between the gateway and the window.

`AiteBar/TextProcessingService.cs` builds the system prompt, protects technical fragments, cleans the model response, and now checks language/content preservation. The response check strips identical protected URLs, paths, code, and identifiers before comparing scripts and word overlap so technical text cannot hide a translated prose fragment.

The active user settings are loaded by `AppSettingsService` from `%APPDATA%\Codebdbd\Aite Bar\settings.json`; API keys are not stored there and must not be read or logged. A cooldown is a temporary period during which the gateway avoids a route after a rate limit or transport failure. It is not equivalent to an absent connection.

## Plan of Work

In `TextProcessingWindow.xaml.cs`, initialize `_currentMode` to Proofread and remove the saved-mode parsing and saving calls. Preserve mode switching during the lifetime of the open window.

In `AiModels.cs`, add an internal availability-reason enum and attach it to `NoAvailableConnectionException`. In `AiGateway.cs`, set `NoConnectionsConfigured` only when candidate connections are actually empty. When routes fail or are skipped, derive the reason from the last provider exception and recorded quota or connection state. In `TextProcessingWindow.xaml.cs`, translate each reason using existing localization keys instead of using `TextProcessing_ErrorNoModels` as a catch-all.

In `TextProcessingWindow.xaml`, remove the error border from the top-docked sequence and add it as a high-z-order child of the remaining editor grid. It spans the editor and command rail but does not create a new row. Update runtime tests to compare editor height before and after showing an error.

For repeated processing, append one language-preservation contract to every mode prompt in `TextProcessingService.BuildRequest`. After restoring protected fragments, compare the input and cleaned output before updating `_originalText`, `_processedText`, or history. Reject a dominant Latin/Cyrillic/CJK/other script change. For longer same-script text, reject output whose distinct-word overlap is below 35 percent; normal spelling, punctuation, typography, and line-cleanup edits retain substantial overlap. Restore `textShownBeforeRequest` on rejection.

## Concrete Steps

Run commands from `D:\01_Codebdbd\01_projects\aitebar`.

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~TextProcessing|FullyQualifiedName~AiProviderTests|FullyQualifiedName~AiStreamingTests"
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    .\installer\Build-Installer.ps1

The build must report zero warnings and zero errors. Focused and full test runs must have zero failed tests. If the WPF test host stalls, identify only the `testhost.exe` whose command line points to this repository before stopping it and use the documented `dotnet vstest` fallback.

## Validation and Acceptance

Construct the real WPF window with settings whose saved mode is Typography or Cleanup and confirm Proofread is selected. Show a status error through the window method, arrange the root at the same size, and confirm the editor's actual height is unchanged. Simulate an AI rate limit and confirm the gateway exception reason is `RateLimited` and the window text is `TextProcessing_ErrorRateLimit`, not `TextProcessing_ErrorNoModels`.

Manual acceptance requires opening the utility with the active settings, observing Proofread selected, entering text, and processing it. If providers are cooling down, the red message must say the request limit or temporary service availability rather than saying no connections were added. Showing and clearing that message must not move the editor footer or model informer.

Repeat Proofread several times for a Russian sentence. A Russian corrected result may replace the previous result. If a provider returns English or otherwise rewrites the content, the result must not become successful state: the text visible before that request remains in the editor and the localized `TextProcessing_ErrorContentChanged` message appears.

## Idempotence and Recovery

Builds and tests are repeatable. Tests use temporary settings and in-memory credential stores; they must never read or mutate the user's API keys. The diagnostic inspection reads only non-secret connection metadata from the active JSON settings. No user settings are rewritten by this fix.

## Artifacts and Notes

Validation after implementation:

    Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0

    Focused: пройдено 146, не пройдено 0
    Full after availability/layout repair: пройдено 1098, не пройдено 0
    Language-preservation focused: пройдено 84, не пройдено 0
    Model-policy focused: пройдено 114, не пройдено 0
    Short-prompt and empty-stream focused: пройдено 124, не пройдено 0
    Professional-prompt focused: пройдено 98, не пройдено 0
    Final full: пройдено 1121, не пройдено 0

The rendered preview is stored outside the repository at `C:\Users\ostee\.codex\visualizations\2026\08\01\019fbbd6-f341-7f32-a69a-4037ee56c8dc\text-processing-wpf-preview.png`.

`installer\Build-Installer.ps1` produced the final `artifacts\installer\AiteBar-Setup.exe` at 79,462,316 bytes with SHA-256 `C37478DF63EC3FA82AFB6BED41CA0801B30E7CB9D2F5A3C41757B1CB19FCC66F`. Signing was skipped because no PFX certificate was supplied.

## Interfaces and Dependencies

No new dependency is required. `NoAvailableConnectionException` exposes `AiAvailabilityFailureReason Reason`. `AiGateway` remains responsible for deriving that reason from provider responses and its in-memory cooldown state. `TextProcessingWindow.GetAvailabilityError` maps the reason to existing `TextProcessing_Error*` localization resources. The status overlay remains named `StatusBorder` so existing automation and code-behind references continue to work.

Revision note (2026-08-01 08:26Z): Created this focused repair plan after reproducing the three regressions from source and active non-secret settings metadata, then recorded the implemented fixes and focused validation.

Revision note (2026-08-01 08:33Z): Completed the plan after direct WPF rendering, the 1098-test full suite, installer rebuild, and final artifact hashing.

Revision note (2026-08-01 10:04Z): Reopened the plan after a user reproduced translation during Repeat; added the universal language contract, deterministic fail-closed validation, localization, tests, and pending final validation steps.

Revision note (2026-08-01 10:07Z): Completed the reopened milestone after 1106 passing tests, installer rebuild, and final SHA-256 capture.

Revision note (2026-08-01 11:02Z): Reopened and completed the plan for task-specific model filtering. Added certified automatic, manual-only, and unsupported tiers; enforced them in the UI and gateway; preserved manual selection when automatic routing is unavailable; passed 1116 tests; and rebuilt and hashed the installer.

Revision note (2026-08-01 11:50Z): Reopened after a real empty-response failure. Reduced Proofread to one system-prompt sentence, added first-chunk prefetch and automatic route fallback for empty streams, hid additional non-writing Gemini/speech families, passed 1118 tests, and rebuilt and hashed the installer.

Revision note (2026-08-02 00:42Z): Reopened to standardize provider-facing instructions. Replaced Russian conversational rule sets with concise professional English prompts and shared contracts, added a no-Cyrillic system-instruction test, passed 1121 tests, and rebuilt and hashed the installer.
