# Prompt Builder: Predictable Prompts for Non-Experts

## Purpose

Make Prompt Builder an understandable tool for users who have a short idea but need a high-quality prompt for a downstream AI system. A choice in the UI must state what the user will receive, and the generated prompt must enforce that result structure rather than merely name a topic.

## Progress

- [x] Identify the current collision: broad shared instructions remain active even when a specific analytical or engineering direction is selected.
- [x] Replace analytical directions with explicit outcome contracts.
- [x] Present the selected option's expected outcome in the UI.
- [x] Strengthen engineering task contracts and make repeat useful outside visual modes.
- [x] Add localized tests, build, and focused verification.

## Discoveries

- The analytics combo box currently displays only category names such as `Comparison`; its single hint is generic and does not tell a novice what the generated prompt will demand.
- Direction descriptors are injected into a broad base instruction, so several unrelated rules can remain active and make the generated prompt less deterministic.
- Retry variation currently applies only to image, painting, and animation categories.

## Decisions

- Keep one tab per broad domain. Use a selector for task/result type to avoid a row of narrow tabs.
- Name analytical options by the expected deliverable, for example `Compare options - criteria table and conclusion`.
- Keep prompts model-agnostic and do not invent user-specific technical facts. When a critical input is absent, instruct the downstream AI to make its assumptions visible or request only blocking information.
- Preserve stored enum values and settings so existing users keep their choices.

## Verification

Focused tests will assert that selected contracts replace placeholders, contain the expected output structure, and that retry directives exist for each category. WPF integration tests will assert that the outcome explanation is present in the UI. Release build must be warning-free.

## Outcome

Analytics now exposes result-oriented names and an in-context explanation of the selected deliverable. Its prompt contracts are mutually focused: the selected direction supplies the required output structure, while shared instructions enforce evidence boundaries and uncertainty disclosure. Repeat now requests a useful alternative for every Prompt Builder category. Release build completed with zero warnings and the complete test suite passed (1206 tests).

## Extended Outcome

The same in-context outcome explanation now covers Programming, Video, Texts, Photo, Paintings, Animation, and Music. Music also has a persisted direction selector whose descriptor is inserted into the Suno Styles prompt.

## Target-Model Profiles

Visual tabs now expose a separate persisted target-model selector: Universal, GPT Image, FLUX, or Nano Banana. It is intentionally independent of the model used inside AiteBar to generate the prompt, because the resulting prompt may be pasted into a different service. The profile only changes prompt wording and constraints; it never changes the user's requested subject or style.

## Evaluation Harness

`PromptBuilderEvaluationCatalog` now covers GPT Image, FLUX, Nano Banana, paintings, programming, analytics, texts, video, and Suno music. Its automated test prevents contract regressions without consuming provider credits. `docs/prompt-evaluations.md` defines the manual, provider-level scorecard and acceptance rule for changes to prompt templates.

## Per-Tab Drafts

Prompt Builder now persists one bounded draft per category rather than an unbounded history. Each draft contains the original brief, latest result, and original/result view state. Switching tabs saves the outgoing draft and restores the selected one; Clear updates only the active draft.

## Verification Note

Release build completed with zero warnings. Focused Prompt Builder and localization tests passed (87 tests). A subsequent full VSTest run did not report a failing test but timed out after 124 seconds in the WPF test host; a previous full run before this isolated Prompt Builder change passed. This host-level timeout remains a test-infrastructure risk, not a known product failure.
