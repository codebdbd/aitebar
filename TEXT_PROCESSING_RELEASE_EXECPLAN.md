# Bring the Text Processing utility to release quality

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. This document follows `PLANS.md` at the repository root and must continue to do so.

## Purpose / Big Picture

After this work, a Windows user can open AiteBar's Text Processing utility, enter or paste text without accidental truncation, select proofreading, typography, or cleanup, choose an eligible AI model, start and cancel processing, compare the original and processed versions, repeat the last successful request, copy or clear the editor, and close or reopen the window without broken geometry. The interface remains responsive, keyboard accessible, localized, theme-consistent, and usable at Windows scaling from 100% through 200%. The result is demonstrated by a Release build, focused automated tests, the full test project, and a manual UI checklist.

## Progress

- [x] (2026-07-29) Audited all 22 findings from the external Text Processing review against the current source and separated current defects from stale, speculative, or behavior-regressing recommendations.
- [x] (2026-07-29) Hardened mode prompts, response recovery, technical-fragment protection, multilingual token estimation, and long streaming requests; added focused regression tests.
- [x] (2026-07-29) Passed 113 focused tests, a zero-warning Release build, all 976 tests with hang diagnostics, whitespace validation, and rebuilt the installer with a matching SHA-256 manifest.
- [x] (2026-07-25) Replaced connection-qualified catalogue rows with one logical row per provider/model pair, migrated saved selection away from connection identity, and routed an exact model across every enabled key that exposes it.
- [x] (2026-07-25) Added regression tests for catalogue deduplication, exact-model key failover, refusal to substitute another model, and model-first automatic routing.
- [x] (2026-07-25) Ran whitespace validation, a sequential Release solution build, focused routing tests, and the complete 897-test project with clean results.
- [x] (2026-07-24) Closed the final streaming release blockers: success is recorded only after complete enumeration, failures update connection health, reads have a reset-on-activity timeout, editor updates are throttled, both protocol families have complete fake-HTTP tests, and the minimum-height layout test includes the diff command.
- [x] (2026-07-24) Completed the post-audit improvements: excluded image/video generators, protected technical fragments, improved internal context estimation, added bounded Undo/Redo, progress feedback, diff view, provider streaming, tests, and documentation.
- [x] (2026-07-22 01:35Z) Reviewed `PLANS.md`, the current working tree, the product specification, and the initial UI implementation.
- [x] (2026-07-22 02:05Z) Inventoried the service, gateway, settings, localization, reusable styles, historical ViewModel, and tests while preserving unrelated working-tree changes.
- [x] (2026-07-22 02:32Z) Implemented adaptive, theme-consistent, accessible XAML with the complete release action set and visible data-transfer notice.
- [x] (2026-07-22 03:04Z) Implemented coherent window state, clipboard, processing/cancellation, repeat, version comparison, focus, validation, context sizing, and error behavior.
- [x] (2026-07-22 03:18Z) Added focused state, visual-contract, and real WPF layout tests.
- [x] (2026-07-22 03:30Z) Ran whitespace checks, Release build, 52 focused tests, full tests, localization-key audit, and automated WPF layout smoke validation. The full suite has one independently reproducible pre-existing MainWindow tooltip test failure recorded below.
- [x] (2026-07-22 03:35Z) Recorded validation evidence and completed the retrospective.
- [x] (2026-07-22 04:10Z) Reworked tabs, editor insets, side commands, model spacing, and the privacy notice after visual review at normal and maximized window sizes; removed decorative glyphs and added regression assertions.
- [x] (2026-07-22 04:18Z) Closed the constructed WPF window in the layout test to prevent a stale localization subscription from contaminating later tests. Release build is clean, all 53 focused tests pass, and the full suite returns to its single unrelated tooltip-offset failure.
- [x] (2026-07-22 04:45Z) Applied the seven-point UI correction without interpretation: reused the Clipboard Manager underline tab styles, restored right-side system icons and the Clear command, restored the generic Process caption, constrained the model popup to the selector width with ellipsis, and removed the resize grip.
- [x] (2026-07-22 05:00Z) Removed duplicate application of the editor padding inside the custom TextBox template so the caret and placeholder share the same origin.
- [x] (2026-07-22 05:10Z) Expanded the right command rail to 192 pixels so localized icon-plus-label commands fit, and made Proofread the default tab on every window opening instead of restoring the previous mode.
- [x] (2026-07-22 05:20Z) Removed the persistent data-transfer line from the footer and moved model loading/unavailable messages into the existing top system-message region.
- [x] (2026-07-22 05:25Z) Matched the Process button and its footer column to the 192-pixel right command rail so both edges align.
- [x] (2026-07-22 05:40Z) Rebuilt the main geometry around a shared 192-pixel action rail: mode information, editor, and model/process footer share the editor width; the model label is inline; all six commands are 52 pixels high; the initial editor minimum equals five command heights plus four gaps; blank model identifiers are excluded.
- [x] (2026-07-22 05:50Z) Shortened all three mode descriptions in four locales and moved model loading/unavailable state into the editor counter bar so transient catalogue state no longer changes the vertical layout.
- [x] (2026-07-22 06:20Z) Reworked the window to the supplied visual reference: outlined information strip, connected editor/counter surface, four icon-and-label rail commands, full-width model/action footer, mode-specific primary action, stronger tabs, readable disabled controls, delayed Repeat visibility, and writing-model filtering.
- [x] (2026-07-22 06:35Z) Made restoration of a filtered or removed saved model silently fall back to Automatic and persist that fallback; historical selection is no longer shown as a red runtime error.
- [x] (2026-07-23) Restored the one-line functional description for each processing mode; the reference constraint sentence no longer replaces information about what Proofread, Typography, and Cleanup do.
- [x] (2026-07-23) Restored one generic primary caption for all idle modes: `Обработать`/`Process`; only an active request changes it to Cancel.
- [x] (2026-07-23) Explicitly set every button glyph to `FontWeight=Normal` so button text weight cannot distort Segoe MDL2 icon rendering.
- [x] (2026-07-23) Removed the editor's blue focus border; its outline remains neutral in focused and unfocused states.
- [x] (2026-07-23) Restored the blue editor focus cue only for keyboard navigation by using the application's shared `KeyboardFocusVisualService`; mouse focus keeps the neutral outline.
- [x] (2026-07-23) Removed visually empty model rows by rejecting whitespace/control/Unicode-format-only identifiers and normalizing catalogue labels before populating the ComboBox.
- [x] (2026-07-24) Audited the current implementation after the release-readiness review and reproduced the remaining state, model-routing, cache, clipboard, response-cleaning, layout, minimize, and persisted-mode defects in source.
- [x] (2026-07-24) Made free-model eligibility an explicit `AiChatRequest` invariant and proved that paid models are rejected even if the global AI setting changes.
- [x] (2026-07-24) Made explicit model selection target the exact configured connection and reject silent fallback to another model.
- [x] (2026-07-24) Made response handling content-preserving, corrected Repeat state, implemented true model-cache refresh, and made clipboard/status transitions atomic.
- [x] (2026-07-24) Restored the fixed 738-pixel editor width, vertically stretching centered layout, reachable narrow-screen viewport, normal taskbar minimization, and persisted mode.
- [x] (2026-07-24) Added focused regression coverage and documentation, passed whitespace checks, built Release with zero warnings, and passed all 864 tests.

## Surprises & Discoveries

- Observation: The review's three critical prompt-truncation findings describe text that is not present in the current source.
  Evidence: `AiteBar/TextProcessingService.cs` already contains complete prohibitions for shortening or expanding text, translating text, and deleting meaningful content in all three mode prompts.
- Observation: Model capabilities cannot replace the writing-model name filter because the shared descriptor models input capabilities, not output modality.
  Evidence: `AiCapabilities.Text` admits both text-writing models and some output generators whose provider catalogues still advertise text input; `AiGateway` already applies the capability requirement before `IsSuitableForWritingModel`.
- Observation: Reducing Cleanup output capacity to one quarter of the input could truncate a valid unchanged result.
  Evidence: Cleanup is explicitly instructed to preserve uncertain or meaningful content, so its worst-case valid output remains approximately the input length.
- Observation: Two ordinary full-suite invocations left `testhost` active without an outcome, but the same assembly completed all tests under the repository's diagnostic fallback.
  Evidence: The final `dotnet test` run with `--blame-hang --blame-hang-timeout 60s --blame-hang-dump-type none` passed 976 tests in 13.3 seconds and reported that no blame sequence was needed.
- Observation: The visible catalogue treats an API connection as part of a model's identity, and exact selection later restricts routing to that same connection.
  Evidence: `TextProcessingWindow.LoadModelsAsync` deduplicates with `connection.Id + model.ModelId`, while `CopyRequestWithModel` supplies `PreferredConnectionId` and `AiGateway.BuildCandidates` then returns only that connection.
- Observation: Parallel solution builds can race while creating the isolated test output tree on this Windows workspace even after a complete test build succeeds there.
  Evidence: The parallel build reported access denied under the isolated `AiteBar.Tests` tree; rerunning the same Release build with `-m:1 -nr:false` completed with zero warnings and zero errors.
- Observation: The initial Release build could not complete because WPF's markup compiler was denied access to `AiteBar/obj/Release/net10.0-windows/win-x64/AiteBar_MarkupCompile.cache`.
  Evidence: `dotnet build .\AiteBar.sln -c Release --no-restore` reported `MC1000` and `UnauthorizedAccessException` for that cache file. Validation must use an isolated intermediate directory if the lock persists.
- Observation: The current working tree already contains substantial Text Processing edits and deleted ViewModel files.
  Evidence: `git status --short` lists modifications to the window, service, AI gateway/models, tests, and deletions of `TextProcessingViewModel.cs` and its tests. These are treated as user-owned changes and will be evolved in place rather than reset.
- Observation: The existing code-behind file could be patched and moved but could not be atomically deleted by the workspace patch mechanism.
  Evidence: Direct `Delete File` operations failed, while safe `Move to` operations succeeded. The replaced legacy file was moved under the already ignored `artifacts/text-processing-verify` validation area, and the final implementation now occupies the canonical `AiteBar/TextProcessingWindow.xaml.cs` path without a project-file exclusion.
- Observation: The only full-suite failure is unrelated to Text Processing and reproduces when run alone.
  Evidence: 846 of 847 tests pass; `MainWindowIconConverterOrientationTests.IconConverterButton_IsVisibleAndPlacedCorrectly_OnAllPanelEdges` expects tooltip offset `0` but receives `1000`. The same failure occurs with a filter that runs only that test. All 59 tests whose fully qualified names contain `TextProcessing` pass.
- Observation: A constructed but unclosed `DarkWindow` in the WPF layout test retains a static `LocalizationService.CultureChanged` subscription after its dispatcher shuts down.
  Evidence: A full run initially produced cascading `TaskCanceledException` failures in localization tests. Closing `TextProcessingWindow` in the test's `finally` block removed all nine secondary failures.
- Observation: A green full test suite did not cover several contradictory states in the reopened release audit.
  Evidence: `TextProcessingUiState.Create` enables Repeat from non-empty text without requiring `HasSuccessfulResult`; the refresh command calls the 15-minute cached `GetModelsAsync`; and the visual-contract test explicitly changed its assertion from a 738-pixel editor to a star-sized editor.
- Observation: The model selector displays a connection-qualified tooltip but loses the connection identity before submission.
  Evidence: `ModelItem` stores only provider and model identifiers, while `AiGateway.SelectModel` falls back to the first eligible model when the requested model is absent from an earlier connection of the same provider.
- Observation: Refreshing the catalogue selected the temporary Automatic row while `_isLoadingState` was already false, which could overwrite a saved explicit selection before restoration.
  Evidence: Suppressing `CmbModels_SelectionChanged` while `_isLoadingModels` is true preserves the stored connection/model until `RestoreModelSelection` deliberately applies it.
- Observation: A startup smoke launch could not safely start the newly built executable because one AiteBar instance was already running.
  Evidence: The guarded smoke command returned `SKIPPED_EXISTING_INSTANCE:1` and intentionally did not stop or replace the user's process.
- Observation: Source-aware tests cannot locate `AiteBar.sln` when their isolated output is placed under `%TEMP%`.
  Evidence: The first full post-audit run passed 801 tests and failed 85 with only `Repository root with AiteBar.sln was not found`; the same assembly built under an isolated `artifacts/text-processing-final-tests-*` directory passed all 887 tests.
- Observation: A successful HTTP response header is not sufficient evidence of a successful streaming request.
  Evidence: The new failure-stream test yields one SSE delta and then throws an `IOException`; the gateway now records the connection as `Unavailable` instead of leaving it `Available`.

## Decision Log

- Decision: Apply only findings that improve deterministic safety without narrowing valid multilingual output: per-mode temperatures, a 10% recovery-distance ceiling, stronger protected-marker wording, CLI/data-URI protection, conservative CJK estimation, prompt boundary clarifications, and a longer streaming-compatible HTTP timeout.
  Rationale: These changes address concrete corruption, context-sizing, or request-lifetime risks and can be proven with unit tests. Few-shot examples, Russian-only typography rules, raw JSON block protection, a reduced Cleanup output budget, and capability-only output filtering either add ambiguity, consume context, or regress valid behavior.
  Date/Author: 2026-07-29 / Codex
- Decision: Keep the existing Russian system instructions with explicit per-fragment language handling instead of duplicating every prompt in Russian and English.
  Rationale: The utility already instructs the model to detect and preserve each input language. Duplicating long prompts increases context usage without a deterministic language detector or evidence that it fixes a current failure.
  Date/Author: 2026-07-29 / Codex
- Decision: Define a logical model as a case-insensitive provider/model pair and keep API connection identity out of the ComboBox and persisted Text Processing selection.
  Rationale: API keys are interchangeable routes to a provider model, not user-facing model choices. Provider identity remains part of the key because equal display names from different providers do not prove compatible endpoints or model identifiers.
  Date/Author: 2026-07-25 / Codex
- Decision: For an explicit model, preserve exact provider/model selection while trying every enabled connection for that provider; never fall back to another model.
  Rationale: This gives transparent key failover without violating the user's chosen model. Automatic mode may still choose another eligible model after routes fail because the user delegated model choice.
  Date/Author: 2026-07-25 / Codex
- Decision: Track rate-limit and quota cooldown by connection plus model when a model is known, while keeping authentication, permission, and network health connection-wide.
  Rationale: One key/model route can be exhausted while another route remains usable. Authentication and endpoint failures invalidate the connection itself.
  Date/Author: 2026-07-25 / Codex
- Decision: Treat a streaming connection as successful only after its response stream completes, and apply connection failure state when stream enumeration fails.
  Rationale: HTTP 200 headers prove only that a stream started. Marking success before consuming it hides mid-stream provider failures and leaves automatic routing with stale health information.
  Date/Author: 2026-07-24 / Codex
- Decision: Use a reset-on-read inactivity timeout and throttle full editor replacement to at most once every 50 milliseconds.
  Rationale: A stream can stall after headers, while replacing the complete WPF text value for every small token chunk causes quadratic allocation and layout work. The timeout preserves cancellation semantics; throttling keeps visible progress without overwhelming the UI thread.
  Date/Author: 2026-07-24 / Codex
- Decision: Do not add a custom-prompt mode, persistent processing history, or token/cost counters to the interface.
  Rationale: The user explicitly rejected these product changes. Token estimation remains internal only, where it protects requests from exceeding a model context window.
  Date/Author: 2026-07-24 / Codex
- Decision: Classify output-only image and video generators by centralized model identifier/display-name exclusions, while retaining multimodal models that can return text.
  Rationale: Provider model catalogues do not expose a reliable common output-modality field. Rejecting every model with vision input would incorrectly hide useful writing models, so the filter must target known image/video generation families and naming markers.
  Date/Author: 2026-07-24 / Codex
- Decision: Implement streaming for both supported protocol families: OpenAI-compatible server-sent events and Gemini `streamGenerateContent`.
  Rationale: A UI-only timer does not solve the original period of apparent inactivity. Starting and parsing the provider stream lets the editor show real generated content while preserving the existing cancellation token and exact-model routing.
  Date/Author: 2026-07-24 / Codex
- Decision: Protect technical fragments with unique local markers and reject a final response that removes or duplicates a marker.
  Rationale: Prompt instructions alone cannot guarantee that URLs, paths, code, versions, or identifiers survive. Local substitution makes unchanged restoration deterministic, and rejecting damaged marker structure prevents silently returning corrupted technical data.
  Date/Author: 2026-07-24 / Codex
- Decision: Keep the current code-behind direction instead of restoring the deleted ViewModel merely to recover bindings.
  Rationale: The working tree deliberately removed the ViewModel and already moved behavior into `TextProcessingWindow.xaml.cs`. Release quality can be achieved with a small testable UI-state helper while respecting the user's current architectural direction.
  Date/Author: 2026-07-22 / Codex
- Decision: Remove the editor's `MaxLength` and enforce the 50,000-character limit through validation state.
  Rationale: The specification requires preserving oversized text so the user can copy or shorten it; a WPF `MaxLength` silently discards the excess and makes the warning state unreachable.
  Date/Author: 2026-07-22 / Codex
- Decision: Use an adaptive grid with a star-sized editor and a fixed action column, with a compact fallback for constrained widths.
  Rationale: This preserves the specified proportions while avoiding the current narrow island inside a maximized window and supporting high DPI.
  Date/Author: 2026-07-22 / Codex
- Decision: Add `RequiredContextTokens` to `AiChatRequest` and make the gateway exclude models whose declared context is too small.
  Rationale: UI-only validation handles a selected model but cannot safely validate Automatic selection. Carrying the requirement with the request makes both explicit and automatic selection reject insufficient models before sending text.
  Date/Author: 2026-07-22 / Codex
- Decision: Centralize enabled-state calculations in `TextProcessingUiState` and test it independently of WPF.
  Rationale: Empty, oversized, loading, unavailable-model, processing, success, clipboard, repeat, and comparison states otherwise drift across event handlers. A deterministic helper gives one release contract and fast regression coverage.
  Date/Author: 2026-07-22 / Codex
- Decision: Present modes as text-only tabs with a two-pixel active indicator and commands as standard compact buttons.
  Rationale: Visual review showed that the filled segmented control, oversized icon cards, and ambiguous font glyphs conflicted with AiteBar's restrained UI and clipped at the lower edge. The simpler controls reuse the program's command styling and remain readable at both window sizes.
  Date/Author: 2026-07-22 / Codex
- Decision: Remove the persistent data-transfer disclosure from the window and keep the behavior documented in the user manual.
  Rationale: The user explicitly requested that the footer contain controls only. Runtime model-loading and unavailable-model messages now appear in the same top status region as errors, avoiding a second status area.
  Date/Author: 2026-07-22 / Codex
- Decision: Supersede the earlier star-sized editor decision with a fixed 738-pixel editor surface centered together with a content-sized command rail.
  Rationale: The explicit product requirement is that maximization must add vertical working space without stretching the editor or command controls horizontally. When the monitor is narrower than the fixed composition, a local horizontal viewport must preserve reachability instead of clipping controls.
  Date/Author: 2026-07-24 / Codex
- Decision: Treat the side Paste command as standard insertion at the current selection and read the clipboard before mutating result history.
  Rationale: A command labelled Paste should follow the established text-editor convention, and clipboard failure must not destroy the Before/After history.
  Date/Author: 2026-07-24 / Codex
- Decision: Carry connection identity and a strict-model requirement through `AiChatRequest`.
  Rationale: Provider plus model is not unique when multiple accounts or endpoints use the same provider. An explicit user choice must never silently execute through another connection or another model.
  Date/Author: 2026-07-24 / Codex
- Decision: Limit response cleanup to surrounding transport whitespace.
  Rationale: Removing fences, localized prefixes, or outer quotes cannot be distinguished reliably from deleting legitimate user content. The prompt already requests a plain response, so preservation wins over heuristic wrapper removal.
  Date/Author: 2026-07-24 / Codex

## Outcomes & Retrospective

The fact-checked review follow-up is complete. Text Processing now uses deterministic temperatures per mode, accepts an explicit final answer from a truncated reasoning block only within ten percent edit distance of the protected fallback, protects CLI switches and data URIs, estimates CJK and other writing systems conservatively, states stricter marker/case/paragraph/header boundaries, and permits five-minute provider requests while retaining the thirty-second stream-inactivity guard. The three reported critical truncations were stale, and recommendations that could truncate Cleanup output or hide valid text models were deliberately not applied. Automated evidence is 113 focused tests, 976 total tests, a zero-warning Release build, and installer SHA-256 `8299CAC11A4945D98932F6EBC7B887ECC04FAD874724B5426234D75399EA4D01`.

The Text Processing utility now has a responsive release layout matching the supplied visual reference, aligned editor/placeholder insets, a connected editor/counter surface, four icon-and-label rail commands, a full-width model/action footer, accessible focus and automation metadata, non-destructive size-limit handling, functioning cancellation and repeat, monitor-aware geometry restoration, provider-aware model persistence, writing-model and context-capacity filtering, localized clipboard/model states, and updated user documentation. The Release solution builds with zero warnings and zero errors, and all 59 focused tests pass.

The plan was reopened on 2026-07-24 after the release-readiness review found behavior that the earlier source-contract tests had incorrectly accepted. The hardening follow-up is now complete. Explicit selection carries connection identity and rejects model substitution; Automatic and explicit processing require free writing-capable models; response cleanup preserves legitimate content; Repeat requires a successful result and a ready model; refresh invalidates the model cache and reliably clears loading resources; Paste inserts at the selection only after clipboard read succeeds; stale informational state is cleared; the original comparison view is read-only; the editor remains exactly 738 pixels wide while growing vertically; narrow monitors receive a local horizontal viewport; the standalone taskbar window no longer has a WPF owner; and the chosen mode is restored.

Automated evidence is clean: `git diff --check` reports no whitespace errors, the Release solution builds with zero warnings and zero errors, 116 focused tests pass, and the complete suite passes 864 of 864. The only remaining environment-dependent check is a live visual walkthrough and external AI request. The guarded startup smoke did not replace the user's already-running AiteBar instance.

The post-audit improvement milestone is also complete. The utility streams OpenAI-compatible and Gemini responses into the editor; shows elapsed processing time; provides a separate, read-only red/green changes view without removing original/result switching; supports operation-level `Ctrl+Z`/`Ctrl+Y`; protects technical fragments with validated markers; estimates Cyrillic and mixed-language context internally; and excludes Nano Banana, Imagen, Veo, and equivalent image/video generators while retaining text-returning multimodal models. The final Release build has zero warnings and errors, and the complete suite passes 887 of 887. The application was deliberately not launched because the user had instructed that the running instance must not be disturbed.

The final streaming hardening is complete. Provider health now reflects completion or failure of the response body rather than headers alone, stalled streams fail after 30 seconds without activity, and WPF receives at most one full-text replacement per 50 milliseconds. Fake HTTP integration tests exercise complete OpenAI-compatible and Gemini SSE requests, mid-stream failure, and inactivity timeout. The expanded WPF test proves the new diff command fits inside the rail at minimum client height. Release builds with zero warnings and errors, and all 893 tests pass.

The logical-model routing follow-up is complete. The selector now contains one row per case-insensitive provider/model pair rather than one row per API connection. Persisted Text Processing selection contains provider and model only; the obsolete connection field is cleared during restoration or saving. Both synchronous and streaming gateway paths build model-first route groups, try every eligible key for the selected model, and keep rate-limit/quota cooldown scoped to the affected connection/model route. Exact selection never changes provider or model, while Automatic proceeds to another logical model only after the current model's routes fail. The sequential Release solution build has zero warnings and errors, 30 focused model/streaming tests pass, and the complete suite passes 897 of 897.

## Context and Orientation

`AiteBar` is a .NET 10 WPF desktop application for Windows. WPF is the Windows UI framework used by the project. The utility window is declared in `AiteBar/TextProcessingWindow.xaml`, while event handling and UI state currently live in `AiteBar/TextProcessingWindow.xaml.cs`. `AiteBar/TextProcessingService.cs` creates prompts and sanitizes AI responses. `AiteBar/AiGateway.cs` chooses a configured connection and sends requests. Persistent fields are defined in `AiteBar/Models.cs` and copied safely by `AiteBar/AppSettingsService.cs`. User-visible strings are stored in `AiteBar/Resources/Strings.resx` plus `.ru`, `.uk`, and `.de` variants. Shared WPF resources live in `AiteBar/UtilityWindowResources.xaml`, `AiteBar/FormControlsResources.xaml`, and `AiteBar/SettingsWindowResources.xaml`.

The window must expose three modes, one editor, an eligible writing-model selector, side actions for paste, copy, repeat, and clear, plus the bottom process/cancel action. Model catalogue state appears in the editor counter bar, while processing errors use the message region above the editor. A logical selection means either Automatic or a model identifier; it does not store an API key or a specific secret. A successful run stores the original text, result, mode, and logical model choice only for the lifetime of the window so Repeat can replay that request. Text content is never persisted between launches.

The current implementation has a second set of release blockers discovered after the original work. Explicit selection loses the configured connection identifier and can silently fall back to another model. Response cleanup can delete legitimate fences, prefixes, and quotes. Repeat can be enabled before any successful result. Manual model refresh reuses the gateway cache. Clipboard mutation is not atomic, informational status can become stale, the editor was changed from 738 pixels to star sizing, the owned-window/taskbar combination leaves minimize behavior ambiguous, and `TextProcessingLastMode` is persisted but ignored.

## Plan of Work

First, inspect the complete service, gateway contracts, settings model, localization keys, resource styles, and historical Text Processing implementation in Git. Preserve useful existing behavior and identify pure decisions that can be represented by a small helper independent of WPF. The helper should calculate whether Process, Copy, Clear, Repeat, Paste, model selection, mode selection, and version switching are enabled from explicit state inputs; it should also expose the character-limit result. Unit tests will cover these transitions.

Second, replace `AiteBar/TextProcessingWindow.xaml` with a responsive layout based on existing AiteBar resources. Restore the title and description hierarchy, shared underline tabs, full-width mode explanation, editor typography, counter, fixed action column, a single top system-message region, and bottom model/process row. Use dynamic or shared brushes instead of inventing a separate palette. Give interactive and informative elements automation names or help text, preserve visible keyboard focus, wrap localized labels, and ensure no whole-window scrollbar is needed.

Third, revise `AiteBar/TextProcessingWindow.xaml.cs` so one `RefreshUiState` path controls every enabled, visible, warning, label, and focus state. Clicking Process while idle starts only when state permits; clicking it while busy cancels the active token. Paste and copy catch clipboard failures and report localized errors. Paste replaces the whole editor without truncation and resets result history. Repeat uses the last successful original text, mode, and logical model identifier. Version switching must not falsely mark the text as manually edited. Clear and successful processing return focus to the editor. Model-loading state and absence of eligible models are visible and prevent submission. Closing during a request cancels safely, and dirty-text confirmation follows the specification.

Fourth, make size restoration monitor-aware. Restore normal width and height, clamp normal bounds to the nearest working area, preserve maximized state, and do not force maximization on every launch or force Normal when reopening a hidden existing window. Extract geometry arithmetic if needed so it can be tested without creating a WPF window.

Finally, update all four localization resources for new accessible names, visible notices, clipboard messages, loading/empty model states, and action labels. Run automated validation with isolated intermediate/output directories if the existing `obj` remains locked. Exercise the UI manually when launching a GUI is available, covering empty, valid, oversized, processing, cancellation, successful, comparison, repeat, error, and close-confirmation states.

The 2026-07-24 hardening follow-up changes the model request contract in `AiteBar/AiModels.cs` and `AiteBar/AiGateway.cs` so an explicit selection carries the configured connection identifier and cannot degrade to another model. `AiteBar/TextProcessingWindow.xaml.cs` must retain that identity in catalogue items, saved settings, repeat state, and response display. A force-refresh path must invalidate only model cache entries, restore loading state in `finally`, and dispose the matching cancellation source.

The follow-up also makes `TextProcessingService.CleanResponse` preserve all content except surrounding transport whitespace. `TextProcessingUiState` must require a successful result and ready eligible models before enabling Repeat. Paste must first read clipboard text, then replace the active selection, reset historical comparison state, position the caret after the insertion, and clear stale informational status. Clear, manual edits, new requests, and model changes must not leave an obsolete “model used” message visible.

Finally, `AiteBar/TextProcessingWindow.xaml` must center a fixed-width composition whose editor card remains exactly 738 device-independent pixels wide while its height expands with the window. Command buttons keep one content-derived width. A local horizontal viewport is allowed only when the monitor cannot contain the fixed composition; controls must remain reachable. The utility window must not be owned by the hidden edge panel when it is intended to minimize normally to the taskbar. The `MainWindow` reference needed to open AI settings is passed separately without setting WPF ownership. The selected processing mode is restored and saved through `TextProcessingLastMode`.

## Concrete Steps

All commands run from `D:\01_Codebdbd\01_projects\aitebar` in PowerShell.

Inspect relevant files and history:

    git diff -- AiteBar/TextProcessingWindow.xaml AiteBar/TextProcessingWindow.xaml.cs AiteBar/TextProcessingService.cs
    git show HEAD:AiteBar/TextProcessingViewModel.cs
    rg -n "TextProcessing_" AiteBar/Resources AiteBar.Tests

After implementation, check whitespace and build with the normal command:

    git diff --check
    dotnet build .\AiteBar.sln -c Release

If the existing WPF intermediate cache is locked, build with project-specific isolated intermediate and output paths as described in the validation transcript added later to this plan.

Run tests:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` encounters the documented WPF temporary-file issue after a successful build, use:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

Automated acceptance requires a successful Release compile and all Text Processing service/state tests passing. The state tests must demonstrate that Process is disabled for empty, whitespace-only, oversized, busy, loading-model, and no-model states; that it is enabled for valid text with an eligible automatic or explicitly selected model; that Cancel is available only while processing; and that Repeat is available only after a successful result and disabled while processing.

Manual acceptance starts the app and opens Text Processing. At first launch the window fits within 90% of the working monitor in Normal state, focuses the editor, and shows zero counters and an inactive process command. Resizing horizontally keeps the editor at exactly 738 pixels and the content-sized command buttons unchanged while keeping the complete composition centered; resizing vertically expands the editor. If the working area is narrower than the composition, a local horizontal viewport keeps every action reachable instead of clipping it. Tab navigation shows a visible focus cue and reaches modes, editor, side actions, model selector, Clear, and Process in a logical order.

Paste inserts clipboard text at the current selection and does not alter the editor or comparison history when clipboard reading fails. Pasting more than 50,000 characters preserves the entire text, changes the counter to a warning state, displays a localized limit explanation, and disables Process while leaving editor, Copy, and Clear usable. With valid text and a model, Process starts once, disables editing controls, and changes the main command to Cancel. Clicking Cancel or pressing Escape cancels and restores the prior text. A successful response replaces the editor without removing legitimate quotes, service-like prefixes, or Markdown fences, enables Repeat and version comparison, and focuses the editor. Original/result switching shows the appropriate text without marking it as a manual edit. Repeat is disabled before the first success and while models are unavailable, then reuses the stored original, mode, exact connection, and exact model. Clearing resets text and runtime history but retains mode, model, and window geometry.

At 100%, 125%, 150%, and 200% Windows scaling, labels remain legible, localized German labels wrap instead of clipping, the model name ellipsizes or fits inside its selector, and all critical commands remain reachable. Light and dark theme resources remain coherent wherever the application supports them.

## Idempotence and Recovery

The edits are ordinary source changes and can be reapplied safely. No persistent user settings migration or destructive data operation is required. If WPF build caches are locked, do not delete broad directories; use a verified task-specific temporary intermediate directory or stop only the known AiteBar/MSBuild process after confirming it owns the lock. If a validation step fails, preserve its output in `Surprises & Discoveries`, correct the smallest relevant source area, and rerun the same command.

## Artifacts and Notes

Initial review evidence:

    TextProcessingWindow.xaml: Width="874", WindowState="Maximized"
    TextProcessingWindow.xaml: editor column Width="738"
    TextProcessingWindow.xaml.cs: if (_isProcessing) return;
    TextProcessingWindow.xaml: MaxLength="50000"

These lines explain the initial narrow maximized layout, broken Cancel button, and unreachable oversized-input state.

Final validation evidence:

    dotnet build .\AiteBar.sln -c Release --no-restore
    Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~TextProcessing"
    Пройдено: 59, не пройдено: 0, пропущено: 0.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --no-restore
    Пройдено: 846, не пройдено: 1; единственный сбой — MainWindowIconConverterOrientationTests, воспроизводится отдельно.

Hardening follow-up validation evidence (2026-07-24):

    git diff --check
    Exit code: 0.

    dotnet build .\AiteBar.sln -c Release --no-restore
    Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-restore
    Пройдено: 864, не пройдено: 0, пропущено: 0.

Post-audit improvement validation evidence (2026-07-24):

    git diff --check
    Exit code: 0.

    dotnet build .\AiteBar.sln -c Release -p:ReleaseVerificationRoot=<isolated path>
    Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release -p:ReleaseVerificationRoot=<repository artifacts path>
    Пройдено: 887, не пройдено: 0, пропущено: 0.

Final streaming hardening validation evidence (2026-07-24):

    dotnet build .\AiteBar.sln -c Release -p:ReleaseVerificationRoot=<repository artifacts path>
    Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release -p:ReleaseVerificationRoot=<repository artifacts path>
    Пройдено: 893, не пройдено: 0, пропущено: 0.

Logical-model routing validation evidence (2026-07-25):

    git diff --check
    Exit code: 0.

    dotnet build .\AiteBar.sln -c Release -p:ReleaseVerificationRoot=<repository artifacts path> -m:1 -nr:false
    Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release -p:ReleaseVerificationRoot=<repository artifacts path>
    Пройдено: 897, не пройдено: 0, пропущено: 0.

## Interfaces and Dependencies

No new external package is required. The implementation uses WPF controls, existing AiteBar localization and resource dictionaries, `TextProcessingService`, `AiGateway`, `AppSettingsService`, and `DarkDialog`. If a pure helper is introduced, place it under `AiteBar` with an internal immutable input record and deterministic methods that do not reference `Window`, `Dispatcher`, clipboard, or network types, so `AiteBar.Tests` can exercise it directly.

The window continues to be created by `TextProcessingUtility.CreateWindow` and opened by `TextProcessingUtility.ShowWindow`. `TextProcessingService.BuildRequest(TextProcessingMode, string)` remains the prompt entry point. Text Processing uses `AiGateway.GenerateStreamingAsync(AiChatRequest, CancellationToken)`, while `AiGateway.GenerateAsync` remains available to other callers. `TextDiff.Create` and `TextProcessingUndoHistory` are internal deterministic helpers. The final window never accesses or persists API keys.

Plan revision note (2026-07-22): Created the initial self-contained release plan after reviewing the UI, current working tree, project handbook, and the first failed build. The plan records the existing uncommitted state and the WPF cache lock so a future contributor can continue safely.

Plan revision note (2026-07-22): Completed every implementation milestone, added build/test evidence and the independently reproducible unrelated suite failure, documented the context-capacity and centralized-state decisions, and replaced the provisional outcome with the delivered release behavior and remaining environment-dependent checks.

Plan revision note (2026-07-22): Incorporated visual feedback from normal and maximized screenshots, replaced the icon-heavy segmented UI with compact native-looking controls, aligned editor and footer content, and corrected the WPF test lifetime discovered during the final full-suite run.

Plan revision note (2026-07-22): Corrected the earlier over-broad glyph and label changes against the user's explicit seven-point list. The final tabs now reuse the same shared resources as another shipped utility rather than a local approximation.

Plan revision note (2026-07-22): Fixed the final editor-origin discrepancy. WPF already applies `TextBox.Padding` to its text view; the custom template had also copied that padding to the content host margin, shifting the caret a second time while the placeholder was shifted once.

Plan revision note (2026-07-22): Removed the persistent AI-transfer footer text by user request and consolidated all transient model status messages in the top message area.

Plan revision note (2026-07-22): The supplied visual reference superseded the interim compact icon-only interpretation. The final rail uses four 220-pixel icon-and-label buttons, while the primary action stays in the footer and changes caption with the active mode. Speech, embedding, reranking, moderation, prompt-guard, and safety-only catalogue entries are excluded from this writing utility.

Plan revision note (2026-07-24): Reopened the completed plan after a release-readiness audit exposed untested contradictions in model routing, response preservation, Repeat state, cache refresh, clipboard atomicity, status lifetime, fixed-width geometry, minimize ownership, and mode persistence. The follow-up decisions supersede the earlier star-sized editor and whole-editor Paste decisions because the user's explicit interaction contract requires a 738-pixel editor and standard insertion behavior.

Plan revision note (2026-07-24): Completed the hardening follow-up, added exact connection/model and free-writing-model request invariants, replaced destructive response heuristics, corrected editor and catalogue state transitions, restored the fixed centered geometry and standalone minimize behavior, synchronized documentation, and recorded the clean 864-test Release validation.

Plan revision note (2026-07-24): Completed the requested post-audit improvements without adding custom prompts, persistent text history, or token/cost counters. Recorded streaming, diff, technical-marker validation, model filtering, Undo/Redo, progress feedback, updated documentation, and the clean 887-test Release validation.

Plan revision note (2026-07-24): Closed the release audit's streaming and layout gaps with lifecycle-aware connection status, inactivity timeout, throttled WPF updates, complete fake-HTTP protocol tests, minimum-height command-rail coverage, and clean 893-test Release validation.

Plan revision note (2026-07-25): Reopened the plan to remove API-key duplication from the model selector and move failover behind a logical provider/model selection. Recorded the existing connection-pinned behavior, the provider/model identity rule, exact-model routing semantics, and route-scoped quota tracking before implementation.

Plan revision note (2026-07-25): Completed logical-model catalogue deduplication, model-first connection routing, connection/model-scoped quota cooldown, saved-selection migration, focused regression coverage, documentation updates, and clean 897-test Release validation. Recorded the need for a single MSBuild node in this workspace when validating isolated output trees.

Plan revision note (2026-07-26): Reopened the release hardening pass after a provider exposed chain-of-thought markup in streamed content. Added filtering for closed and truncated `<think>`, `<thinking>`, `<analysis>`, and `<reasoning>` blocks in both live preview and final output, deterministic recovery of an explicitly marked final answer with an original-text fallback, and regression coverage for the reported Russian example. The same focused UI correction aligns the single generic “Обработать” button with the right command column and removes only its glyph.

Reasoning-leak and footer-alignment validation evidence (2026-07-26):

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~TextProcessingServiceTests|FullyQualifiedName~AiStreamingTests|FullyQualifiedName~TextProcessingWindowLayoutTests" -m:1 -nr:false
    Пройдено: 56, не пройдено: 0, пропущено: 0.

    dotnet build .\AiteBar.sln -c Release -m:1 -nr:false
    Сборка успешно завершена. Предупреждений: 0. Ошибок: 0.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release -m:1 -nr:false --no-build
    Пройдено: 904, не пройдено: 0, пропущено: 0.

Plan revision note (2026-07-29): Reopened the plan for a fact-checked external code review. The current source disproved the reported prompt truncations and already applies text capability filtering before output-generator name heuristics. The follow-up is limited to testable safety improvements: mode-specific determinism, conservative reasoning recovery, additional protected technical syntax, multilingual context estimation, prompt boundary clauses, and a request timeout that does not abort ordinary long generations after thirty seconds.

Plan revision note (2026-07-29): Completed the fact-checked hardening pass. Added regression coverage for per-mode temperatures, rejection of distant recovered output, CLI/data-URI round trips, CJK and other-script estimates, prompt safeguards, and the five-minute provider timeout. Recorded two non-deterministic ordinary testhost stalls and the successful diagnostic run of all 976 tests, then rebuilt the unsigned installer and verified its generated SHA-256 manifest.
