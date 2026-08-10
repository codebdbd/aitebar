# Add Icons and Graphics Prompt Builder Tabs

This ExecPlan is a living document. Maintain it according to `PLANS.md` in the repository root.

## Purpose / Big Picture

Prompt Builder currently treats app icons, stickers, logos, and UI graphics as incidental cases of a general image prompt. After this change a user can open `Иконки`, choose a target platform and icon style, describe an app, and obtain an English prompt that prioritizes small-size legibility. A separate `Графика` tab will cover stickers, logos, UI elements, banners, infographics, and vector illustration without pushing those tasks into photo or painting workflows.

## Progress

- [x] (2026-08-08 00:00Z) Identified the existing category, persisted-selector, per-tab draft, and visual-target flow.
- [x] (2026-08-08 00:00Z) Define additive categories, selectors, and prompt contracts.
- [x] (2026-08-08 00:00Z) Add tabs and one-row controls without changing the window, column, scroll, or command-button geometry.
- [x] (2026-08-08 00:00Z) Persist choices, preserve old settings, and bind per-tab drafts.
- [x] (2026-08-08 00:00Z) Add Russian and neutral-language labels, focused tests, and release build verification.
- [ ] Manually inspect the new tabs in the user's existing window placement without resizing it.

## Surprises & Discoveries

- Observation: The existing image template already detects icons and logos in free text, but no UI control explains that path to the user.
  Evidence: `PromptBuilderService.cs` asks the model to use a suitable non-photographic medium for logos, icons, UI, and illustrations.
- Observation: `PromptBuilderDrafts` uses the numeric category value as a string key, so appending category values preserves every existing draft.
  Evidence: Existing categories retain fixed values 0 through 8 and legacy `Ideas` is mapped to Analytics.

## Decision Log

- Decision: Add `Icons` and `Graphics` only at the end of `PromptBuilderCategory`.
  Rationale: Existing persisted category values and draft keys remain valid.
  Date/Author: 2026-08-08 / Codex.
- Decision: Keep target-model selection for both new visual tabs and expose platform/type and style in the same existing-height selector card.
  Rationale: Target model changes prompt wording, while platform/type and style define the asset. No new vertical row is needed.
  Date/Author: 2026-08-08 / Codex.
- Decision: Use separate prompt instructions rather than extending the photo instruction.
  Rationale: Icons require scalable simple geometry and graphics require design-specific output constraints; these conflict with photo realism.
  Date/Author: 2026-08-08 / Codex.

## Outcomes & Retrospective

The application now has additive `Icons` (numeric value 9) and `Graphics` (numeric value 10) categories. Icons support Auto, macOS, iOS/iPadOS, Windows 11, Android Material You, and cross-platform treatments plus 17 icon styles. Graphics supports nine asset types and 13 graphic styles. Both categories retain their own draft through the existing category-keyed draft store and persist their selected controls in settings.

Focused verification completed on 2026-08-08: `dotnet test .\\AiteBar.Tests\\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~PromptBuilderServiceTests|FullyQualifiedName~PromptBuilderIntegrationTests"` passed 51 of 51 tests. `dotnet build .\\AiteBar\\AiteBar.csproj -c Release` completed with zero warnings and zero errors. Manual visual inspection remains intentionally pending because this plan must not reset or resize the user's saved Prompt Builder window.

## Context and Orientation

`AiteBar/PromptBuilderService.cs` defines categories, selector enums, and prompt instruction templates. `AiteBar/PromptBuilderWindow.xaml` contains the tabs and selector cards. `AiteBar/PromptBuilderWindow.xaml.cs` restores and saves choices, switches tabs, and passes selected values into `BuildRequest`. `AiteBar/Models.cs` is the JSON-persisted application settings model. `AiteBar/Resources/Strings*.resx` holds UI labels in English, Russian, German, and Ukrainian. The existing visual target is the downstream generator for which the resulting prompt is written; it is independent of the AI model which creates the prompt inside AiteBar.

## Plan of Work

Add `Icons` and `Graphics` categories after `Animation`. Define icon platform values `Auto`, `MacOS`, `IOS`, `Windows11`, `AndroidMaterialYou`, and `CrossPlatform`; define icon styles `Auto`, `Flat`, `GradientFlat`, `Monochrome`, `Line`, `Glyph`, `Filled`, `Duotone`, `Isometric`, `Glassmorphism`, `Neumorphism`, `ThreeDimensional`, `ClayThreeDimensional`, `PixelArt`, `Retro`, `HandDrawn`, and `Mascot`. Define graphics types `Auto`, `Sticker`, `StickerPack`, `Logo`, `UiElement`, `VectorIllustration`, `Poster`, `Banner`, and `Infographic`, plus practical graphic styles. Each definition must carry a localization key and an English prompt descriptor.

Create a dedicated Icon instruction that returns one English prompt, preserves explicit user requirements, selects 1:1 unless specified, enforces a clear silhouette, safe padding, no incidental lettering, and the platform constraints selected by the user. Create a Graphics instruction that produces the requested design medium rather than photography and applies type, style, output clarity, and aspect-ratio constraints. Use the existing visual-target profile in both instructions.

In XAML add two tabs and two selector cards. Both cards must remain one row high. The Icons card holds target model, platform, and style. The Graphics card holds target model, graphic type, and style. Do not change window width, height, `MinWidth`, scrollbar settings, editor dimensions, or command-column calculations.

Extend settings with the new selector values, load and validate them in `RestoreCurrentMode`, save them in `SaveCurrentMode`, expose them through combo-box refresh and selection handlers, and pass them to `BuildRequest`. Existing per-tab draft save/restore must work unchanged because it keys state by the category value.

Add all UI labels, mode descriptions, selector names, and option names to each `Strings*.resx`. Extend service tests to prove that each tab produces its own contract and that selected platform/type/style descriptors appear in the system instruction. Extend the XAML integration test to assert the two tabs and selector hosts exist.

## Concrete Steps

From `D:\01_Codebdbd\01_projects\aitebar`, run:

    dotnet build .\AiteBar\AiteBar.csproj -c Release
    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll --Tests:PromptBuilderServiceTests,PromptBuilderIntegrationTests

The build must finish with zero errors. The focused test run must report all selected tests passing.

## Validation and Acceptance

Start the release executable and open Prompt Builder. `Иконки` must show only its single-row selectors. Choose `macOS` and `Флэт`, enter `трекер воды с каплей и галочкой`, and create a prompt. The result must be English, be an app-icon description, mention the selected platform/style contract, use a square aspect ratio unless the user specifies another, and avoid photo treatment. Switch to `Графика`, choose `Стикерпак`, enter a short concept, and confirm a graphics prompt is produced without photo framing. Switch away and back in both tabs to confirm each draft remains distinct. The window must remain within its existing dimensions with no window-level vertical scrollbar.

## Idempotence and Recovery

The setting additions are additive. If an old settings file lacks them, C# defaults select `Auto`. If a persisted enum value is unknown, restore code must fall back to `Auto`. Re-running the build and tests is safe. Do not delete prior settings or drafts.

## Artifacts and Notes

Expected build tail:

    AiteBar -> ...\\bin\\Release\\net10.0-windows\\win-x64\\AiteBar.dll
    Build succeeded.
    0 Warning(s)
    0 Error(s)

## Interfaces and Dependencies

`PromptBuilderService.BuildRequest` must accept optional `IconPlatform`, `IconStyle`, `GraphicType`, and `GraphicStyle` values in addition to the existing visual options. The method must choose an Icons or Graphics system instruction when its category is selected. No external package or network call is needed.

Plan update (2026-08-08): implementation and focused verification completed. The remaining manual check is deliberately isolated from persisted window geometry.
