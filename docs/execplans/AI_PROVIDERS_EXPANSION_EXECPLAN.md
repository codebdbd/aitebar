# Expand AI Provider Catalog and Add Custom OpenAI-Compatible Endpoint Support

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan is maintained in accordance with `PLANS.md` in the repository root.

## Purpose / Big Picture

Users currently have only three AI providers (`cerebras`, `groq`, `mistral`) in `AiteBar/AiProviderCatalog.cs` and cannot connect any other OpenAI-compatible provider or router without modifying source code. After this change, users can:

1. Choose from an expanded curated list of major OpenAI-compatible providers in the Add AI Connection dialog (`Google AI Studio`, `NVIDIA NIM`, `OpenRouter` with automatic `:free` model filtering, `SambaNova`, `HuggingFace Router`, `Scaleway`, `OVHcloud AI`, `Novita AI`, `SiliconFlow`, `Ollama Cloud`, `Mistral Codestral`, alongside `Cerebras`, `Groq`, and `Mistral AI`).
2. Add any other OpenAI-compatible provider or aggregator (such as niche routers or local endpoints) via a `Custom (OpenAI-compatible)` option by specifying a `Base URL` in `AiConnectionDialog`.

You can observe this working by opening `AppSettingsWindow` -> `AI Providers` -> `Add Connection`, seeing the expanded provider list and the `Base URL` field when `Custom (OpenAI-compatible)` is selected, and running `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`.

## Progress

- [x] (2026-11-15 02:53Z) Analyzed existing AI provider architecture (`AiProviderCatalog.cs`, `AiProviderClient.cs`, `AiGateway.cs`, `AiModels.cs`, `AiConnectionDialog.xaml*`, `AppSettingsWindow.xaml.cs`, `TextProcessingModelPolicy.cs`, `AiProviderTests.cs`) and verified official OpenAI-compatible endpoints for major providers.
- [x] (2026-11-15 02:56Z) Updated `AiModels.cs`, `AiProviderCatalog.cs`, `AppSettingsService.cs`, and `AppSettingsWindow.xaml.cs` to add `CustomBaseUrl`, `RequiresCustomBaseUrl`, the 11 new providers plus `custom`, and URL normalization rules.
- [x] (2026-11-15 02:56Z) Updated `AiProviderClient.cs` to resolve `CustomBaseUrl` endpoints and classify `openrouter` free vs. paid models.
- [x] (2026-11-15 02:56Z) Updated `AiConnectionDialog.xaml`, `AiConnectionDialog.xaml.cs`, and `Resources/Strings*.resx` to support `Base URL` input for custom providers.
- [x] (2026-11-15 02:56Z) Updated `TextProcessingModelPolicy.cs` with certified model markers for the new providers.
- [x] (2026-11-15 03:05Z) Updated `AiteBar.Tests/AiProviderTests.cs` and verified `Release` build (0 warnings, 0 errors) and `vstest` execution (`AiProviderTests`: 38/38 passed; full `AiteBar.Tests.dll`: 1746 passed, 0 failed, 5 existing `QuickNoteSnapIntegrationTests` skipped).

## Surprises & Discoveries

- Observation: `OpenRouter` returns both paid and `:free` models from `/api/v1/models`, while `AiGateway` and `AiModelSelectionHelper` enforce `FreeTierOnly = true` and `AiProviderClient.ParseOpenAiModels` previously assigned `provider.DefaultCostStatus` uniformly to every model.
  Evidence: `AiteBar/AiProviderClient.cs:296` and `AiteBar/AiGateway.cs:560-564`.

## Decision Log

- Decision: Add 11 major OpenAI-compatible providers to `AiProviderCatalog.All` plus a `custom` (`Custom (OpenAI-compatible)`) provider instead of hardcoding 40+ niche/unstable routers.
  Rationale: Keeps the provider dropdown clean and maintainable while allowing users to connect any OpenAI-compatible endpoint via `CustomBaseUrl`.
  Date/Author: 2026-11-15 / Antigravity
- Decision: For `openrouter`, classify models with `:free` suffix or zero `pricing.prompt` & `pricing.completion` as `AiCostStatus.VerifiedFree`, and all others as `AiCostStatus.Paid`.
  Rationale: Prevents paid OpenRouter models from being selected or causing `402 Payment Required` errors under `FreeTierOnly = true`.
  Date/Author: 2026-11-15 / Antigravity
- Decision: Allow `https://` for any host and `http://` only for loopback hosts (`localhost`, `127.0.0.1`, `::1`) in `CustomBaseUrl`.
  Rationale: Preserves transport security for remote APIs while enabling local OpenAI-compatible servers (e.g., Ollama).
  Date/Author: 2026-11-15 / Antigravity

## Outcomes & Retrospective

Completed the expansion of `AiProviderCatalog.All` with 11 major OpenAI-compatible providers plus `custom` (`Custom (OpenAI-compatible)`), added `CustomBaseUrl` persistence/validation and UI input in `AiConnectionDialog`, added per-model `:free` / zero-pricing detection for `openrouter`, and updated `TextProcessingModelPolicy` and `AiProviderTests`. Release build succeeded with 0 warnings/errors and all non-skipped tests in `AiteBar.Tests.dll` passed (1746 passed, 5 skipped). Manual UI verification with live API keys remains for the user.

## Context and Orientation

Key files involved:
- `AiteBar/AiModels.cs`: Defines `AiConnectionSettings` (persisted per-connection configuration).
- `AiteBar/AiProviderCatalog.cs`: Defines `AiProviderDefinition`, `AiProviderCatalog.All`, and `AiSettingsNormalizer.Normalize`.
- `AiteBar/AiProviderClient.cs`: Executes HTTP calls for `/models` and `/chat/completions` and parses OpenAI-compatible responses.
- `AiteBar/AiConnectionDialog.xaml` and `AiteBar/AiConnectionDialog.xaml.cs`: Modal dialog to add a new AI connection.
- `AiteBar/AppSettingsWindow.xaml.cs` and `AiteBar/AppSettingsService.cs`: Clone, display, and persist `AiConnectionSettings`.
- `AiteBar/TextProcessingModelPolicy.cs`: Classifies models into `CertifiedAutomatic`, `ManualOnly`, or `Unsupported`.
- `AiteBar/Resources/Strings*.resx`: Localization strings (`Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, `Strings.de.resx`).
- `AiteBar.Tests/AiProviderTests.cs`: Unit tests for catalog, normalization, client parsing, and routing.

## Plan of Work

1. Add `string? CustomBaseUrl` to `AiConnectionSettings` in `AiteBar/AiModels.cs`, and preserve it in `AppSettingsService.cs` and `AppSettingsWindow.xaml.cs` (`CloneAiConnection`, `BtnAiAddConnection_Click`, and `BuildAiConnectionRows`).
2. Add `bool RequiresCustomBaseUrl = false` to `AiProviderDefinition`, add the 11 major providers and `custom` to `AiProviderCatalog.All`, and add `TryNormalizeCustomBaseUrl` used by `AiSettingsNormalizer.Normalize`.
3. Update `AiProviderClient.cs` to resolve `ModelsUri` and `ChatCompletionsUri` from `connection.CustomBaseUrl` when `provider.RequiresCustomBaseUrl` is true, and to resolve per-model `AiCostStatus` for `openrouter`.
4. Update `AiConnectionDialog.xaml`, `AiConnectionDialog.xaml.cs`, and `Strings*.resx` to show/validate the `Base URL` input when `custom` is selected.
5. Add certified model markers for the new providers in `TextProcessingModelPolicy.cs`.
6. Update `AiProviderTests.cs` for the expanded catalog, `CustomBaseUrl` normalization/requests, and OpenRouter free-model filtering, then build and test in `Release`.
