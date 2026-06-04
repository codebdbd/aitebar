# Complete Internationalization of User-Visible Text

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

AiteBar already supports English, German, Ukrainian, and Russian, but some user-visible text can still bypass the localization resources and appear in one language regardless of the selected UI language. After this work, all text shown by the application will use the existing localization system, and automated tests will detect missing translations and common hard-coded UI strings.

The result is visible by changing the language in application settings and opening every window, menu, dialog, tray action, and built-in utility. Text should change consistently, with no `[[Resource_Key]]` placeholders and no English-only labels left in XAML or code.

## Progress

- [x] (2026-06-04) Read `PLANS.md`, inspected the existing localization service, resource files, tests, and current user changes.
- [x] (2026-06-04) Audited XAML, C# UI code, service exceptions shown in dialogs, file-sorter output folder names, and all four resource files.
- [x] (2026-06-04) Added complete English, German, Ukrainian, and Russian resources and replaced remaining user-visible literals.
- [x] (2026-06-04) Added resource parity, format-placeholder, XAML literal, and localized file-sorter tests.
- [x] (2026-06-04) Completed the Release build and full test run using an isolated artifacts path because the shared `obj` folders were locked.

## Surprises & Discoveries

- Observation: The repository already has a single localization mechanism suitable for this work.
  Evidence: `AiteBar/LocalizationService.cs` exposes `Get`, `Format`, and the `LocExtension` markup extension backed by `AiteBar/Resources/Strings*.resx`.

- Observation: The working tree contains pre-existing edits in UI and installer files.
  Evidence: `git status --short` reported modifications in `App.xaml.cs`, `FileSorterWindow.xaml`, `MainWindow.xaml`, `SettingsWindow.xaml`, and installer files before this implementation began.

- Observation: Most visible English text was caused by incomplete culture-specific resource files rather than missing localization calls.
  Evidence: Before implementation, `Strings.resx` contained 300 keys while German, Russian, and Ukrainian contained 208, 206, and 151 keys.

- Observation: The file sorter created Russian category folder names regardless of the selected UI language.
  Evidence: `AiteBar/FileSorterService.cs` mapped extensions directly to literals such as `Изображения` and `Документы`.

- Observation: Standard WPF build output folders were locked by external processes in this environment.
  Evidence: `dotnet build .\AiteBar.sln -c Release` failed with access denied errors for generated `obj` and `.wpftmp.csproj` files, while a sequential build with `--artifacts-path` succeeded.

## Decision Log

- Decision: Extend the existing RESX-based localization system instead of introducing a second abstraction.
  Rationale: The current system already supports runtime culture changes in both XAML and C# and is used throughout the application.
  Date/Author: 2026-06-04 / Codex

- Decision: Preserve all pre-existing working-tree changes and make only additive or narrowly scoped edits around them.
  Rationale: Those changes belong to the user and may be part of concurrent work.
  Date/Author: 2026-06-04 / Codex

- Decision: Localize file-sorter category folder names according to the active UI culture.
  Rationale: These folder names are user-visible output created by the program and were previously always Russian.
  Date/Author: 2026-06-04 / Codex

- Decision: Treat service exception messages as user-visible when they are surfaced through import, export, sorter, or icon-catalog dialogs.
  Rationale: A localized dialog wrapper does not prevent an untranslated `ex.Message` from appearing to the user.
  Date/Author: 2026-06-04 / Codex

## Outcomes & Retrospective

AiteBar now has a key-compatible set of 333 localized strings in English, German, Ukrainian, and Russian. Remaining translatable XAML labels, file-sorter output folder names, update/version fallback text, search errors, icon-catalog errors, and panel package validation errors use the existing localization service.

Automated coverage now prevents missing culture keys, mismatched format placeholders, and common hard-coded XAML text properties. The Release solution build completed with zero warnings and zero errors, and all 405 tests passed. Manual interactive UI verification was not performed in this non-interactive run.

## Context and Orientation

`AiteBar/LocalizationService.cs` is the central localization service. It loads the neutral English resource file `AiteBar/Resources/Strings.resx` and culture-specific files for German, Ukrainian, and Russian. XAML views should use `{local:Loc ResourceKey=...}` for localized dependency properties such as `Title`, `Text`, `Content`, `Header`, and `ToolTip`. C# code should use `LocalizationService.Get` for plain strings and `LocalizationService.Format` for strings with values inserted into them.

The application windows live in `AiteBar/*.xaml` with code-behind files beside them. The tests live in `AiteBar.Tests`, and `AiteBar.Tests/LocalizationServiceTests.cs` is the natural location for resource integrity checks.

## Plan of Work

First, enumerate text-bearing XAML attributes and C# string literals near UI APIs, then compare all resource keys and values across the four RESX files. Classify technical strings such as file extensions, URLs, process names, enum tags, glyphs, and format patterns as non-user-visible; only text a user can read belongs in localization resources.

Next, add resource keys for each remaining user-visible string, with complete translations in English, German, Ukrainian, and Russian. Replace hard-coded XAML values with `LocExtension` bindings and hard-coded C# values with `LocalizationService.Get` or `LocalizationService.Format`.

Finally, strengthen localization tests so every culture-specific resource file contains the same keys as the neutral English file and so common text-bearing XAML properties do not contain literal alphabetic labels. Run the required Release build and test commands.

## Concrete Steps

All commands run from `D:\01_Codebdbd\01_projects\aitebar`.

Inspect candidate user-visible strings:

    rg -n "Text=|Content=|Header=|Title=|ToolTip=" AiteBar -g "*.xaml"
    rg -n "\"[^\"]*[A-Za-zА-Яа-яІіЇїЄє][^\"]*\"" AiteBar -g "*.cs"

Build and test after implementation:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

In this environment, shared generated folders were locked, so validation used:

    dotnet build .\AiteBar.sln -c Release --artifacts-path .\artifacts\i18n-solution-build4 -m:1
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --artifacts-path .\artifacts\i18n-test-build

If `dotnet test` fails because of WPF temporary generated files, run:

    dotnet vstest .\AiteBar.Tests\bin\Release\net8.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

The automated acceptance condition is that the Release build succeeds and all tests pass. New localization integrity tests prove that every English resource key has German, Ukrainian, and Russian translations, that translated format placeholders match the English source, and that XAML text-bearing properties do not contain hard-coded alphabetic labels.

The manual acceptance condition is that selecting each supported language and opening the main panel, tray menu, settings windows, dialogs, and built-in utility windows shows translated text without `[[...]]` placeholders. Existing panel show/hide behavior, hotkeys, contexts, and tray access must continue to work.

## Idempotence and Recovery

Resource additions and code replacements are safe to repeat. If a translation key is renamed, update all four RESX files and every reference in the same change. Do not revert unrelated working-tree edits. If WPF generated files interfere with tests, use the documented `dotnet vstest` fallback rather than deleting user files.

## Artifacts and Notes

Resource parity audit:

    Strings.de.resx: keys=333, missing=0, extra=0, dupes=0
    Strings.ru.resx: keys=333, missing=0, extra=0, dupes=0
    Strings.uk.resx: keys=333, missing=0, extra=0, dupes=0

Release build:

    Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0

Test run:

    Пройдено 405, не пройдено 0, пропущено 0

## Interfaces and Dependencies

No new dependency is required. The final implementation continues to use:

    LocalizationService.Get(string key)
    LocalizationService.Format(string key, params object?[] args)
    {local:Loc ResourceKey=Resource_Key}

The four resource files must remain key-compatible:

    AiteBar/Resources/Strings.resx
    AiteBar/Resources/Strings.de.resx
    AiteBar/Resources/Strings.uk.resx
    AiteBar/Resources/Strings.ru.resx

Plan revision note: Updated the completed plan with the discovered missing-resource root cause, localized file-sorter output, service exception policy, automated validation, and final build/test evidence.
