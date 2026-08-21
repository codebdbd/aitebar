# Lock Prompt Builder Golden Contracts

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept current as work proceeds. It follows `PLANS.md` from the repository root.

## Purpose / Big Picture

Prompt Builder relies on an external AI model to turn a user's brief into a finished prompt. External models are non-deterministic: their wording can change between calls even when the same API request is sent. After this work, the deterministic request contract sent by AiteBar is protected by golden tests. A contributor can run one test class and prove that realistic Grok Imagine, video, and Suno scenarios retain their user brief, target-specific direction, and key safety rules.

## Progress

- [x] (2026-08-21 15:02+03:00) Audited the request construction path and selected three production-like scenarios: Grok Imagine product editing, video advertising, and Suno music style.
- [x] (2026-08-21 15:07+03:00) Added three deterministic golden contract tests without a network call or API key.
- [x] (2026-08-21 15:10+03:00) Golden test class passed 3/3; Release build completed with 0 warnings and 0 errors; full Release suite passed 1362/1362.

## Surprises & Discoveries

- Observation: `PromptBuilderService.BuildRequest` is deterministic and puts the template in the system message and the normalized user brief in the user message.
  Evidence: the method creates exactly two `AiChatMessage` values after all category and selector substitutions.
- Observation: all Prompt Builder calls require a text-capable writing model even when the completed prompt targets an image, video, or music generator.
  Evidence: `BuildRequest` sets `RequiredCapabilities = AiCapabilities.Text` and `RequireWritingModel = true`; AiteBar generates the prompt, not the media.

## Decision Log

- Decision: test request contracts locally rather than invoke Grok Imagine, a video provider, or Suno in CI.
  Rationale: live provider outputs require credentials, cost money, change with provider model revisions, and cannot be a reliable release gate. The request contract is fully deterministic and is the code AiteBar owns.
  Date/Author: 2026-08-21 / Codex.
- Decision: use realistic briefs and assert both required and forbidden fragments.
  Rationale: a small exact-output snapshot is fragile and can miss a harmful instruction. Required and forbidden checks make each production expectation explicit while allowing legitimate template wording improvements.
  Date/Author: 2026-08-21 / Codex.

## Outcomes & Retrospective

The work added three CI-enforced golden contracts: Grok Imagine product-image editing, video advertising, and Suno music style. Each contract preserves the exact user brief and locks the request rules that matter to its destination. The focused class passed 3/3, the Release build had 0 warnings and 0 errors, and the full suite passed 1362/1362. The remaining limitation is intentional: these tests do not judge a live external model's prose, because that result is neither deterministic nor free to run in CI.

## Context and Orientation

`AiteBar/PromptBuilderService.cs` owns the deterministic prompt-request builder. `BuildRequest` receives a `PromptBuilderCategory`, a brief, and the selected style or direction. It returns `AiChatRequest` with a system message containing AiteBar's instructions and a user message containing the trimmed brief. `AiteBar/PromptBuilderWindow.xaml.cs` sends that request to the configured text AI through `AiGateway`; it does not call an image, video, or music generation API directly.

`AiteBar.Tests/PromptBuilderServiceTests.cs` already checks individual rules. The new `AiteBar.Tests/PromptBuilderGoldenScenarioTests.cs` will test complete, product-like request assemblies. A golden contract is a deterministic collection of required and forbidden substrings that expresses what must remain true about a request. It is not a snapshot of an external model answer.

## Plan of Work

Create `AiteBar.Tests/PromptBuilderGoldenScenarioTests.cs`. Add one test for a Russian product-image editing brief with `VisualTargetModel.GrokImagine`, one test for a product launch video with `VideoDirection.Advertising`, and one test for a Suno style brief. Each test will assert that the brief is preserved exactly after trimming, the selected direction reaches the system message, the expected category contract remains present, and irrelevant or harmful parameters are absent.

The Grok Imagine test must reject `4:5`, `16:9`, `1:1`, `--ar`, and named photographer references. The video test must reject output geometry and generic negative-prompt instructions while retaining chronology, continuity, and the selected advertising direction. The Suno test must retain the Styles-field contract and reject lyrics, song titles, and artist-name copying.

No production code or dependencies are necessary. The tests use the existing xUnit project and instantiate `PromptBuilderService` directly.

## Concrete Steps

From `D:\01_Codebdbd\01_projects\aitebar`, create the focused test file using the repository's existing xUnit style. Run:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~PromptBuilderGoldenScenarioTests"
    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

The focused run must show three passing tests. The Release build must finish with zero errors, and the full suite must have zero failures.

## Validation and Acceptance

The Grok Imagine test is accepted when its request contains the selected product style and Grok-specific instruction to leave interface settings to the target UI, while no ratio or command flag appears. The video test is accepted when its request contains the advertising direction and source-continuity rules while no output geometry appears. The Suno test is accepted when its request contains the direct Styles-field contract and restrictions against lyrics, titles, and artist names. All three must preserve the exact user brief supplied in the test.

## Idempotence and Recovery

The tests have no file, network, account, or API-key dependency and can be run repeatedly. If a test fails after a legitimate wording update, update its required or forbidden contract only after confirming the product behavior still matches this plan; do not weaken an assertion merely to make CI green.

## Artifacts and Notes

The observed focused transcript after implementation is:

    Passed: 3, Failed: 0

The observed full-suite transcript after implementation is:

    Passed: 1362, Failed: 0

## Interfaces and Dependencies

The new test file depends only on existing public types in the `AiteBar` namespace: `PromptBuilderService`, `PromptBuilderCategory`, `VisualTargetModel`, `PhotoSection`, `PhotoStyle`, `VideoDirection`, `AiChatRequest`, and `AiCapabilities`. No external service or package is added.

Change note: created and completed on 2026-08-21 to turn the prompt-quality audit into deterministic CI coverage for Grok Imagine, video, and Suno.
