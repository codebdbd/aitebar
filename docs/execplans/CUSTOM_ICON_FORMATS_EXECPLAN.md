# Support ICO and SVG as custom button icons

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document follows `PLANS.md` in the repository root. It is self-contained so a contributor can continue the work from this file and the current working tree alone.

## Purpose / Big Picture

AiteBar lets users assign a custom image to a panel button. Before this work, the file picker already exposed common raster image formats and ICO, and `IconHelper.SaveCustomIcon` converted ICO files into PNG files in the application icons folder. Users also commonly have SVG icon assets from modern icon packs. After this change, the custom icon picker accepts SVG files too, converts them into a PNG copy at import time, and stores that PNG path on the button. This keeps the main panel simple because it still displays normal bitmap images and does not need to render SVG every time it refreshes.

The behavior can be observed by opening the add/edit button window, clicking the custom icon button, selecting a `.svg` file, and seeing the preview update. Saving the button should keep the PNG copy under AiteBar's icons folder and the panel should show that image like any other custom icon. Existing `.ico` selection should continue to work and continue saving a PNG copy.

## Progress

- [x] (2026-08-11 00:00Z) Read `PLANS.md`, located existing custom icon code in `AiteBar/IconHelper.cs`, and confirmed `Svg.Skia` plus `SkiaSharp` are already project dependencies.
- [x] (2026-08-11 00:00Z) Confirmed current ICO support already exists in `IconHelper.SaveCustomIcon` and is covered by `AiteBar.Tests/IconHelperTests.cs`.
- [x] (2026-08-11 00:00Z) Add SVG import support to `IconHelper.SaveCustomIcon`.
- [x] (2026-08-11 00:00Z) Extend custom icon file picker filters in `AiteBar/Resources/Strings*.resx` to include `.svg`.
- [x] (2026-08-11 00:00Z) Add focused tests for SVG save success and unsafe SVG rejection.
- [x] (2026-08-11 00:00Z) Run Release build and tests, recording any unrelated failures.

## Surprises & Discoveries

- Observation: The repository already contains `Svg.Skia` and a hardened SVG safety check inside `AiteBar/IconConverterService.cs`.
  Evidence: `AiteBar/AiteBar.csproj` references `Svg.Skia` version `5.1.1`, and `IconConverterService` rejects external references and script-like SVG content before rendering.
- Observation: ICO is already accepted by the custom icon picker and converted to PNG.
  Evidence: `SettingsWindow_ImageFilter` includes `*.ico`, and `IconHelperTests.SaveCustomIcon_IcoFile_ConvertsToPng` already asserts `.ico` conversion.

## Decision Log

- Decision: Treat SVG as an import format and save a normalized PNG copy instead of storing SVG paths on buttons.
  Rationale: `MainWindow` and `SettingsWindow` already display custom icons through WPF `BitmapImage`; converting once keeps runtime rendering unchanged and avoids needing SVG support in every display path.
  Date/Author: 2026-08-11 / Codex.
- Decision: Reuse `IconConverterService.GeneratePreviewsAsync` for SVG rasterization instead of adding another SVG parser.
  Rationale: The service already owns SVG safety validation and uses the existing `Svg.Skia` dependency, so this keeps security behavior consistent.
  Date/Author: 2026-08-11 / Codex.

## Outcomes & Retrospective

SVG custom icon import is implemented without changing the runtime panel image path model. Users can now select `.svg` files in the custom icon dialog; AiteBar renders the SVG once into a 128 pixel PNG and stores that PNG in the icons folder. Existing ICO support remains in place and continues to save a PNG copy.

Validation showed the new focused tests pass and the solution builds cleanly. The full test suite still has one unrelated failure in `PromptBuilderIntegrationTests.Generator_ShowsAutomaticOptionBeforeModelsFinishLoading`, which was already observed before this feature work and checks for an exact source-code substring in PromptBuilder UI code.

Release follow-up (2026-08-11): the platform-dependent `LF`/`CRLF` assertion in that unrelated test was normalized. The final 1.15.10 validation passes all 1,322 tests, and the installer reports ProductVersion 1.15.10.

## Context and Orientation

The custom button editor lives in `AiteBar/SettingsWindow.xaml.cs`. Its `BtnSelectCustomIcon_Click` method opens `Microsoft.Win32.OpenFileDialog` with the localized filter key `SettingsWindow_ImageFilter`, then calls `IconHelper.SaveCustomIcon` and stores the returned path in `_selectedImagePath`.

`AiteBar/IconHelper.cs` is the central helper for button image acquisition. It downloads favicons, extracts associated icons from executables, and saves user-picked icon files into `PathHelper.IconsFolder`. `SaveCustomIcon` returns `null` for unsupported or invalid inputs and returns the saved path for valid inputs. The main panel later reads the saved path as a normal bitmap in `MainWindow.LoadUnifiedButtonImageAsync`.

`AiteBar/IconConverterService.cs` converts source images into ICO files for the standalone icon converter utility. It already supports SVG input, validates SVG safety, renders SVG through `Svg.Skia`, and exposes `GeneratePreviewsAsync`, which returns PNG preview bytes for requested sizes. A "preview" here is just a rendered PNG byte array at a chosen pixel size.

## Plan of Work

First, update `AiteBar/IconHelper.cs` so `SaveCustomIcon` recognizes `.svg`. For SVG files it should call `IconConverterService.GeneratePreviewsAsync` with a single 128 pixel size, transparent background, fit mode, and zero padding, then write the returned PNG bytes to `PathHelper.IconsFolder` using a generated `custom_<guid>.png` filename. If rendering fails, it should log the exception through the existing catch block and return `null`.

Second, update the localized `SettingsWindow_ImageFilter` values in `AiteBar/Resources/Strings.resx`, `AiteBar/Resources/Strings.ru.resx`, `AiteBar/Resources/Strings.uk.resx`, and `AiteBar/Resources/Strings.de.resx` so users can select `.svg` files directly from the custom icon dialog.

Third, add focused tests to `AiteBar.Tests/IconHelperTests.cs`. One test should write a small safe SVG, call `SaveCustomIcon`, and assert the saved file exists, uses `.png`, and is a supported image. Another test should write an unsafe SVG containing an external image reference, call `SaveCustomIcon`, and assert it returns `null`.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Edit files with small patches:

    AiteBar/IconHelper.cs
    AiteBar/Resources/Strings.resx
    AiteBar/Resources/Strings.ru.resx
    AiteBar/Resources/Strings.uk.resx
    AiteBar/Resources/Strings.de.resx
    AiteBar.Tests/IconHelperTests.cs

Validate with:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` fails because of an existing unrelated WPF/MSBuild temp-file issue, run the documented fallback:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

The feature is accepted when `SaveCustomIcon` returns a saved PNG path for a valid SVG file and returns `null` for an unsafe SVG file. The file picker must visibly include `.svg` in the images filter for English, Russian, Ukrainian, and German resources. `dotnet build .\AiteBar.sln -c Release` should complete with zero errors. The focused `IconHelperTests` should pass.

Manual UI verification, when a desktop session is available, is to open the add/edit button window, click the custom icon button, select a safe `.svg`, confirm that the preview appears, save the button, and confirm the panel displays the custom icon. Existing `.ico` selection should still save a `.png` copy and display correctly.

## Idempotence and Recovery

The code changes are idempotent. Re-running the tests creates temporary files under the system temp folder and deletes them in `finally` blocks. If a generated custom icon file remains after a failed manual run, it is a normal app data icon file and can be removed manually from the configured icons folder.

The repository currently contains unrelated modified files. This plan deliberately touches only the custom icon feature files listed above and does not revert unrelated changes.

## Artifacts and Notes

Implementation notes:

    `IconHelper.SaveCustomIcon` now handles `.svg` by calling `IconConverterService.GeneratePreviewsAsync` for a single 128 pixel transparent PNG and writing that PNG into `PathHelper.IconsFolder`.
    `SettingsWindow_ImageFilter` now includes `*.svg` in English, Russian, Ukrainian, and German resources.
    `IconHelperTests` now covers successful SVG-to-PNG import and rejection of an unsafe SVG with an external image reference.

Validation transcripts:

    dotnet build .\AiteBar.sln -c Release
    Сборка успешно завершена.
    Предупреждений: 0
    Ошибок: 0

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Не пройдено: 1, пройдено: 1310, всего: 1311
    Failing test: AiteBar.Tests.PromptBuilderIntegrationTests.Generator_ShowsAutomaticOptionBeforeModelsFinishLoading
    Failure reason: Assert.Contains could not find "CmbModels.ItemsSource = _models;\n        " in the source string.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter IconHelperTests
    Пройдено: 15, не пройдено: 0, всего: 15

## Interfaces and Dependencies

`IconHelper.SaveCustomIcon(string sourcePath)` remains synchronous and returns `string?`. It must keep returning `null` for missing, invalid, or unsupported files. It must save all normalized imports into `PathHelper.IconsFolder`.

`IconConverterService.GeneratePreviewsAsync(string sourcePath, IconConversionOptions options, CancellationToken cancellationToken = default)` is reused for SVG rasterization. The desired options are one size, 128 pixels, transparent background, fit mode, and zero padding, producing one PNG byte array.

Revision note, 2026-08-11 / Codex: Initial plan created after repository inspection to guide SVG custom icon support while preserving existing ICO behavior.

Revision note, 2026-08-11 / Codex: Updated progress and notes after implementing SVG import, file picker filters, and focused tests.

Revision note, 2026-08-11 / Codex: Added validation results and retrospective after running Release build, full tests, and focused IconHelper tests.
