# Prompt Builder Evaluations

The automated catalog verifies prompt contracts, not image aesthetics or a provider's hidden model behavior. It never sends requests to external providers and never uses an API key.

## Automated gate

`AiteBar/PromptBuilderEvaluationCatalog.cs` defines short, representative briefs for GPT Image, FLUX, Nano Banana, paintings, programming, analytics, texts, video, and Suno music. The matching test asserts that every generated system prompt includes the required contract for that scenario.

When changing a prompt template, update the catalog only if the product behavior intentionally changes. Do not weaken an assertion merely to accommodate a regression.

## Manual provider evaluation

For a candidate change, run every visual scenario with the same selected target model and compare it to the current release. Use at least three generations per case where the provider is nondeterministic.

Score each generation from 0 to 2 on these criteria:

- Core request is preserved: subject, action, explicit objects, setting, and visible text.
- Composition is coherent and matches the requested framing or aspect ratio.
- The selected style or direction is recognizable without unwanted artifacts.
- The target-specific failure is absent: HDR/crushed blacks for GPT Image, vague relationships for FLUX, or loss of unmentioned elements in Nano Banana edits.
- No forbidden presentation artifact appears: negative prompt, frame for paintings, unwanted text, or unrelated objects.

For text, programming, analytics, video, and music, score instruction adherence, factual boundaries, useful structure, and direct usability in the destination model.

Only accept a template change when the candidate has a higher median score across the same cases and does not regress a previously passing criterion.
