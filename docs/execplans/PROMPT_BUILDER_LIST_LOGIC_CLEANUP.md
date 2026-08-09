# Cleanup Prompt Builder filter logic

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. It follows `PLANS.md` from the repository root.

## Purpose / Big Picture

The Prompt Builder must give a short, predictable route from a user's rough idea to a usable prompt. After this work, the Code tab will expose only the working pair of filters, Photo's `All` section will work like the other visual tabs, and dead list entries from removed concepts will no longer remain in code or translations.

## Progress

- [x] (2026-08-09 12:00Z) Reviewed every tab's definitions, prompt composition, settings, and translations.
- [x] (2026-08-09 12:00Z) Removed inactive code-task and icon-platform paths without changing the active Code or Graphics UI.
- [x] (2026-08-09 12:00Z) Aligned the active Photo and Theme lists with the intended filter model and added the training-ground localization.
- [x] (2026-08-09 12:00Z) Added focused regression coverage; Release build and 78 focused Prompt Builder tests pass.

## Surprises & Discoveries

- Observation: `ProgrammingTaskType` is persisted and passed to `BuildRequest`, but no prompt instruction contains or consumes it.
  Evidence: `BuildRequest` accepts `programmingTaskType` in `AiteBar/PromptBuilderService.cs` but only replaces `{programmingProjectType}` and `{programmingStyle}`.
- Observation: icon styles remain active in Graphics when type is Icon; only icon platform selection and its dedicated system instruction are obsolete.
  Evidence: `RefreshGraphicOptions` populates `IconStyles` for `GraphicType.Icon`.
- Observation: the full test suite fails in `LocalizationServiceTests` because Ukrainian and German resource files predate the expanded Photo and Programming catalogs and have different key sets from `Strings.resx`.
  Evidence: 1,240 tests pass and only the resource-key parity test fails; focused Prompt Builder tests pass 78/78.

## Decision Log

- Decision: Remove the unused Code task-type model rather than adding a third Code filter.
  Rationale: the established product model is Type plus Style; an invisible task selection must not survive as persisted state or misleading source code.
  Date/Author: 2026-08-09 / Codex
- Decision: Keep `IconStyle` as an active Graphics subtype and remove only `IconPlatform` and `IconsInstruction`.
  Rationale: icon styles are the user-approved style list for Graphics type Icon, while platform choice is neither exposed nor inserted into the active prompt.
  Date/Author: 2026-08-09 / Codex
- Decision: Retain old inactive painting and theme enum values and their translations as compatibility values.
  Rationale: saved settings serialize enum numbers. The active definition lists no longer expose those values, so they cannot reappear in the UI, while old settings safely fall back to Auto.
  Date/Author: 2026-08-09 / Codex

## Outcomes & Retrospective

The active Prompt Builder filter model is now coherent: Code is Type plus Style, Graphics type Icon retains its style catalog without an unused platform branch, Photo All exposes all styles, and Sports uses a concrete training environment. The remaining localized-resource parity failure predates this cleanup and requires a dedicated translation pass for the Ukrainian and German Photo and Programming catalogs.

## Context and Orientation

`AiteBar/PromptBuilderService.cs` owns filter lists and converts selected values into a system prompt. `AiteBar/PromptBuilderWindow.xaml.cs` stores current selections and fills WPF ComboBoxes. `AiteBar/Models.cs` and `AiteBar/AppSettingsService.cs` persist these selections. The `AiteBar/Resources/Strings*.resx` files contain the visible translations. Tests in `AiteBar.Tests/PromptBuilderIntegrationTests.cs` inspect this wiring.

## Plan of Work

Remove `ProgrammingTaskType` from the service, settings, and window because it is not a visible or consumed filter. Keep `ProgrammingProjectType` and `ProgrammingPromptStyle` as the Code tab's two filters. Preserve legacy enum values for removed visual sections because saved settings store their numeric representation; active lists are the user-visible source of truth.

Remove `IconPlatform` and its unused prompt parameter and instruction. Preserve `IconStyle`, because Graphics type Icon actively uses it. Keep `PromptBuilderCategory.Icons` only as a compatibility alias that restores old saved drafts as Graphics.

Make `GetPhotoStyles(PhotoSection.All)` return the complete photo list. Remove inactive theme profession values and painting section values from enumerations, resource files, and tests. Rename the sports scene from montage language to a concrete training setting in every localization and descriptor.

## Concrete Steps

From `D:\01_Codebdbd\01_projects\aitebar`, run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If WPF locks the test host, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

The Code tab shows only project type and product style. Selecting an HTML game and a game style produces a prompt that includes both selected directions. Photo section `All` exposes the full style list. Themes no longer expose professions or role-based entries, and Sports contains a concrete training environment. Graphics type Icon continues to expose the existing icon styles and retains full-bleed, 95-98% glyph guidance.

## Idempotence and Recovery

All edits are source and resource changes. Re-running build and test commands is safe. Old JSON settings may retain unknown property names, which .NET ignores; new saves omit removed properties.

## Artifacts and Notes

No new runtime dependencies or generated assets are required.

## Interfaces and Dependencies

The public `PromptBuilderService.BuildRequest` signature will no longer accept unused `ProgrammingTaskType` or `IconPlatform` values. Active callers in `PromptBuilderWindow` and focused tests must be updated in the same edit.

Revision 2026-08-09: implementation completed; recorded the resource-key parity failure that remains outside this cleanup.
