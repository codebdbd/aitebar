# Add Artists Filter To Prompt Builder Paintings

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with [PLANS.md](/D:/01_Codebdbd/01_projects/aitebar/PLANS.md).

## Purpose / Big Picture

The `Paintings` mode in Prompt Builder already lets the user narrow the result by section and by painting style, but it cannot yet steer the model toward a specific painterly reference. After this change, the `Paintings` toolbar will expose a separate `Artists` filter with a curated top-35 catalog of names that modern image models commonly understand. A user will be able to combine `Section`, `Artist`, and `Style` and immediately see that the generated English prompt includes the selected artist as a stylistic orientation rather than as an identity guarantee.

The change is visible in one place: open Prompt Builder, switch to `Paintings`, and confirm that the visual-options row shows a third painting-specific selector named `Artists`. Choosing `Picasso - cubist geometry` and generating a prompt must inject a matching English artist direction into the painting system prompt while preserving the existing style and section behavior.

## Progress

- [x] (2026-08-08 11:45Z) Reviewed `PLANS.md`, the existing `Paintings` implementation, settings persistence, localization resources, and current tests.
- [x] (2026-08-08 12:05Z) Added the persisted `PaintingArtist` enum, top-35 catalog, painting prompt placeholder, and request plumbing in `PromptBuilderService`.
- [x] (2026-08-08 12:12Z) Added the `Artists` ComboBox to the `Paintings` visual-options row and persisted/restored it through `PromptBuilderWindow`, `Models`, and `AppSettingsService`.
- [x] (2026-08-08 12:18Z) Added localized `PaintingArtist` labels in `Strings.resx`, `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx` using the requested `Name - descriptor` format.
- [x] (2026-08-08 12:36Z) Extended Prompt Builder tests and passed `dotnet build .\AiteBar.sln -c Release` plus `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` (`1226/1226` passed).
- [x] (2026-08-09 09:20Z) Expanded the paintings taxonomy with 8 more artists, 8 more painterly techniques, and 3 new section buckets (`Landscape`, `Portrait`, `Printmaking`) plus synchronized `en/ru/uk/de` translations and tests.

## Surprises & Discoveries

- Observation: `PromptBuilderPaintingSection` is already stored in `Models.cs` and used by `PromptBuilderWindow.xaml.cs`, but it is not copied in the `AppSettingsService` clone block.
  Evidence: `AiteBar/Models.cs` contains `PromptBuilderPaintingSection`, while the `AppSettingsService` copy block only copied `PromptBuilderPaintingStyle` before this feature work.

- Observation: the existing painting options row could absorb the extra filter without a separate panel, but it needed extra width columns and narrower per-filter widths to stay compact.
  Evidence: `PromptBuilderWindow.xaml` now keeps one shared `VisualOptionsHost` row and uses painting-only artist columns toggled from `PromptBuilderWindow.xaml.cs`.

## Decision Log

- Decision: The new artist selector will be a separate painting-only filter, not a replacement for the existing style catalog.
  Rationale: The user explicitly asked for a separate `Artists` filter in `Paintings`, and the current style catalog already carries media and movement directions that still provide useful control.
  Date/Author: 2026-08-08 / Codex

- Decision: The service will inject the selected artist as a soft orientation sentence, explicitly stating that exact identity matching is not guaranteed.
  Rationale: This matches the user requirement and keeps the prompt aligned with modern model behavior and project safety rules around deterministic descriptors.
  Date/Author: 2026-08-08 / Codex

- Decision: `PaintingArtist` entries keep English prompt descriptors in code while localized UI labels live only in resource files.
  Rationale: This matches the existing Prompt Builder pattern where model-facing prompt fragments stay deterministic and English, while the UI remains localized per culture.
  Date/Author: 2026-08-08 / Codex

## Outcomes & Retrospective

The `Paintings` mode now has a third painting-specific filter, `Artists`, that sits alongside `Section` and `Style`. The service now accepts a `PaintingArtist` selection, injects its English descriptor into the painting system prompt, and explicitly frames artist references as orientation rather than guaranteed identity matching.

The feature also fixed a neighboring persistence gap by copying `PromptBuilderPaintingSection` in `AppSettingsService` together with the new `PromptBuilderPaintingArtist`. Release build and the full Release test suite passed, so the feature is closed with source-based validation and compiled behavior.

Follow-up expansion: the paintings catalogs were later broadened to cover 43 artist references total, additional media/printmaking techniques, and clearer section labels for landscape-, portrait-, and print-oriented browsing. That change stayed within the same UI model instead of adding another filter layer, which kept the user interaction aligned with the original compact `Paintings` toolbar.

## Context and Orientation

Prompt Builder lives in four main places for this task. `AiteBar/PromptBuilderService.cs` defines the category enums, localized catalog definitions, and the `BuildRequest` method that converts the selected UI state into the system prompt sent to the AI provider. `AiteBar/PromptBuilderWindow.xaml` contains the WPF markup for the visual-options row in the utility window. `AiteBar/PromptBuilderWindow.xaml.cs` stores the selected values, restores them from settings, repopulates ComboBoxes when the mode changes, and passes the selected options into `PromptBuilderService.BuildRequest`. `AiteBar/Models.cs` and `AiteBar/AppSettingsService.cs` define the application settings object and its clone behavior so Prompt Builder state survives app restarts and in-memory settings updates.

The term "filter" in this repository means a ComboBox that narrows or guides the generated prompt inside one Prompt Builder mode. The `Paintings` mode already has a `Section` filter (`PaintingStyleSection`) and a `Style` filter (`PaintingStyle`). This task adds a third filter, `Artist`, available only in `Paintings`.

Localized text comes from `AiteBar/Resources/Strings.resx` for the default English text and from `AiteBar/Resources/Strings.ru.resx`, `AiteBar/Resources/Strings.uk.resx`, and `AiteBar/Resources/Strings.de.resx` for runtime culture switching. Source-based tests in `AiteBar.Tests` verify that the XAML, code-behind, and prompt catalogs contain the expected hooks and strings.

## Plan of Work

First, extend `AiteBar/PromptBuilderService.cs` with a new `PaintingArtist` enum, a `PaintingArtistDefinition` record, and a `PaintingArtists` catalog containing `Auto` plus the requested 35 artists. Each catalog entry must have a localized resource key for the UI and a deterministic English prompt descriptor for the model. Update `PaintingsInstruction` to include a new `{paintingArtist}` placeholder and describe it as an orientation rather than a guaranteed identity match. Extend `BuildRequest` so the `Paintings` branch replaces both `{paintingStyle}` and `{paintingArtist}`.

Second, extend persisted state. Add `PromptBuilderPaintingArtist` to `AiteBar/Models.cs`. Update the `AppSettingsService` clone block so it copies both `PromptBuilderPaintingSection` and the new `PromptBuilderPaintingArtist`. In `AiteBar/PromptBuilderWindow.xaml.cs`, add a `_paintingArtist` field, restore and validate it alongside `_paintingSection` and `_paintingStyle`, save it in `SaveCurrentMode`, and pass it into `BuildRequest`.

Third, update the `Paintings` UI in `AiteBar/PromptBuilderWindow.xaml` and code-behind. The painting visual-options row currently uses `VisualTarget`, `PaintingSection`, and `VisualStyle`. Expand that row so `Paintings` also shows a `PaintingArtist` label and ComboBox while other modes remain unaffected. Add `RefreshPaintingArtists`, `CmbPaintingArtist_SelectionChanged`, and selection wiring similar to the existing section/style pattern. Keep the localized `Auto` option first, followed by localized alphabetical ordering through the existing `OrderAutoFirst` helper.

Fourth, add localized resource strings. Introduce `PaintingArtist_Label` and `PaintingArtist_*` keys in all four resource files. The visible labels must use the requested format of `Name - descriptor`, not a bare surname.

Finally, extend tests. Add service tests that verify the paintings prompt injects the selected artist descriptor, that the catalog contains all requested artists, and that `Auto` plus an explicit artist behave predictably. Add integration tests that verify the new `CmbPaintingArtist` control exists in XAML and that the code-behind refreshes and persists it.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

1. Edit `docs/execplans/PAINTINGS_ARTISTS_FILTER_EXECPLAN.md` as the living task record.
2. Edit `AiteBar/PromptBuilderService.cs`, `AiteBar/PromptBuilderWindow.xaml`, `AiteBar/PromptBuilderWindow.xaml.cs`, `AiteBar/Models.cs`, `AiteBar/AppSettingsService.cs`, and the four `Strings*.resx` files.
3. Extend `AiteBar.Tests/PromptBuilderServiceTests.cs` and `AiteBar.Tests/PromptBuilderIntegrationTests.cs`.
4. Run:

    dotnet build .\AiteBar.sln -c Release

5. Run:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

6. If `dotnet test` hits the known WPF temp-file issue, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

Acceptance is behavioral and source-based.

In the UI, opening Prompt Builder and switching to `Paintings` must show four painting controls in the top options row: target model, section, artists, and style. Changing the artist must not hide or replace the existing section/style controls.

In prompt generation, building a paintings request with `PaintingArtist.Picasso` must place a Picasso-oriented English descriptor into the system prompt, while the system prompt must still state that artist references are guidance and cannot guarantee an identical style.

In persistence, closing and reopening Prompt Builder must restore the previously selected painting artist in the same way it already restores the painting section and style.

In tests, the release build and test command must pass, and the new tests must explicitly cover the artist catalog and UI hook.

## Idempotence and Recovery

These edits are additive and safe to repeat. Re-running the build or tests should not mutate repository state beyond normal `bin/obj` outputs. If a resource edit introduces malformed XML, restore that file from Git history for inspection and re-apply the specific `<data>` blocks carefully. If the new ComboBox causes a layout regression, the safe rollback path is to remove only the artist-specific XAML columns and code-behind wiring while leaving the service catalog intact until the UI is corrected.

## Artifacts and Notes

The key proof for this feature is the updated XAML control names, the `PaintingsInstruction` placeholder replacement, and the passing Prompt Builder tests.

Evidence:

    dotnet build .\AiteBar.sln -c Release
    Build succeeded.
        Warnings: 0
        Errors: 0

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Passed!  : failed     0, passed  1226, skipped     0, total  1226

## Interfaces and Dependencies

At the end of this task, the following interfaces must exist and be used consistently:

In `AiteBar/PromptBuilderService.cs`, define:

    public enum PaintingArtist
    public sealed record PaintingArtistDefinition(PaintingArtist Artist, string LocalizationKey, string PromptDescriptor);
    public static readonly IReadOnlyList<PaintingArtistDefinition> PaintingArtists

Update the request API so `BuildRequest` accepts:

    PaintingArtist paintingArtist = PaintingArtist.Auto

In `AiteBar/Models.cs`, add:

    public PaintingArtist PromptBuilderPaintingArtist { get; set; } = PaintingArtist.Auto;

In `AiteBar/PromptBuilderWindow.xaml`, add:

    x:Name="TxtPaintingArtistLabel"
    x:Name="CmbPaintingArtist"

In `AiteBar/PromptBuilderWindow.xaml.cs`, add:

    private PaintingArtist _paintingArtist = PaintingArtist.Auto;
    private void RefreshPaintingArtists()
    private void CmbPaintingArtist_SelectionChanged(object sender, SelectionChangedEventArgs e)

Revision note: updated after implementation to record the completed catalog, the compact shared-row UI solution, the persistence fix in `AppSettingsService`, and the successful Release build/test evidence.
