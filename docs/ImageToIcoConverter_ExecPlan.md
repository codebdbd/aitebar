# Add an Image to ICO Converter Utility

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This plan follows the repository rules in `PLANS.md`. It is self-contained so a contributor can implement the feature using only this file and the current repository.

## Purpose / Big Picture

After this change, AiteBar users can open a built-in utility that converts common image files into real Windows `.ico` files. The utility must create an ICO container with multiple embedded sizes, preserve transparency when available, avoid distorted proportions, preview the important small icon sizes, and warn users when input quality is likely too low for the requested output.

The user-visible result is a new quick tool on the AiteBar panel, named "ICO converter" or equivalent in each supported language. A user can drag a PNG, JPG, WEBP, BMP, TIFF, or SVG file into the tool, preview generated 16x16, 32x32, 48x48, and 256x256 images, choose sizes and padding, then save a valid `.ico` file that Windows Explorer and application resources can display.

## Progress

- [x] (2026-06-11) Read `PLANS.md` and confirmed this feature should be planned as an ExecPlan because it adds a new utility, UI, dependencies, conversion services, and tests.
- [x] (2026-06-11) Reviewed the existing utility architecture: `AiteBar/UtilityRegistry.cs`, `AiteBar/App.xaml.cs`, `AiteBar/MainWindow.xaml`, `AiteBar/MainWindow.xaml.cs`, `AiteBar/AppSettingsWindow.xaml`, `AiteBar/AppSettingsWindow.xaml.cs`, `AiteBar/HotkeyService.cs`, and `AiteBar/Models.cs`.
- [x] (2026-06-11) Captured Windows ICO requirements from the user-provided reference: multi-size ICO, PNG payloads, 32-bit RGBA, alpha preservation, padding, quality resize, overwrite confirmation, and no fake `.png` renamed to `.ico`.
- [x] (2026-06-11) Implemented conversion models, WPF/WIC raster loading, high-quality WPF rendering, PNG payload generation, ICO encoding, and file save flow.
- [x] (2026-06-11) Added focused unit tests for ICO encoder headers, 256-size metadata, duplicate/empty validation, option normalization, and fit/fill geometry.
- [x] (2026-06-11) Added `IconConverterWindow` and integrated `IconConverterUtility` with registration, panel button, quick-tool visibility settings, and localization.
- [x] (2026-06-11) Ran Release build successfully: `dotnet build .\AiteBar.sln -c Release`.
- [x] (2026-06-11) Ran tests successfully: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`, 419 passed.
- [x] (2026-06-11) Upgraded the converter pipeline to SkiaSharp 3.119.2 and Svg.Skia 5.0.0.
- [x] (2026-06-11) Added SVG rendering, Windows DPI sizes 20 and 40, Fit/Fill UI selection, PNG payload validation, and end-to-end PNG/JPG/SVG service tests.
- [x] (2026-06-11) Ran tests after best-practice upgrade: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`, 426 passed.
- [x] (2026-06-11) Ran `dotnet publish .\AiteBar\AiteBar.csproj -c Release -r win-x64` and verified SkiaSharp/Svg.Skia native/runtime DLLs in publish output.
- [x] (2026-06-11) Ran `.\installer\Build-Installer.ps1`; installer output was created in `artifacts\installer`.
- [x] (2026-06-12) Hardened the converter toward 2026 best practices: resizable/adaptive utility window, debounced preview generation, save-only ICO generation, raster file/pixel limits, EXIF orientation handling, solid color validation, SVG external/script content rejection, and PNG IHDR dimension validation in `IcoEncoder`.
- [x] (2026-06-12) Ran tests after hardening: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`, 430 passed.
- [x] (2026-06-12) Ran Release build after hardening: `dotnet build .\AiteBar.sln -c Release`, 0 warnings, 0 errors.
- [x] (2026-06-12) Ran `dotnet publish .\AiteBar\AiteBar.csproj -c Release -r win-x64` after hardening and verified SkiaSharp/Svg.Skia runtime DLLs in publish output.
- [x] (2026-06-12) Ran `.\installer\Build-Installer.ps1` after hardening; installer output was recreated in `artifacts\installer`.
- [x] (2026-06-12) Added a WPF layout regression test for the ICO converter window at minimum size under Russian localization, including critical controls and button desired sizes.
- [x] (2026-06-12) Ran tests after layout regression coverage: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`, 431 passed.
- [x] (2026-06-12) Ran final build, publish, and installer generation after layout regression coverage.
- [x] (2026-06-12) Wired all size checkboxes to refresh previews and added a XAML wiring regression test.
- [x] (2026-06-12) Ran tests after checkbox wiring fix: `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`, 432 passed.
- [x] (2026-06-12) Ran published-app smoke check from `artifacts\publish\win-x64\AiteBar.exe`; process started and stayed alive for 6 seconds.
- [x] (2026-06-12) Added a Windows icon loader compatibility test: generated multi-size ICO opens through `System.Drawing.Icon`.
- [x] (2026-06-12) Replaced string-based SVG safety checks with XML-reader validation using disabled DTD resolution and namespace-aware attribute inspection, including `xlink:href` coverage.
- [x] (2026-06-12) Disabled the background color field unless solid background mode is selected, avoiding misleading controls and unnecessary preview refreshes.
- [x] (2026-06-12) Added integration wiring tests for panel button, utility registry, quick-tool visibility settings, context menu detach, focus enumeration, and launch action id.
- [x] (2026-06-12) Hardened SVG validation from denylist-only checks to an SVG namespace allowlist for safe elements and attributes; added regression tests for event handlers, `foreignObject`, CSS imports, data URLs, and safe local `use` references.
- [ ] Manually verify the panel utility in all four panel orientations on a live desktop session.

## Surprises & Discoveries

- Observation: `docs/UTILITIES.md` is useful but partially stale.
  Evidence: It describes an `IUtility.Launch(...)` method, while the current code uses `IUtility.LaunchAsync(...)` in `AiteBar/UtilityRegistry.cs`.

- Observation: Built-in utilities are registered through `UtilityRegistry`, but panel buttons, visibility settings, and hotkeys are still wired explicitly per utility.
  Evidence: `AiteBar/App.xaml.cs` registers `QuickNoteUtility`, `TimerStopwatchUtility`, `ColorPickerUtility`, and `FileSorterUtility`; `AiteBar/MainWindow.xaml` still contains individual `BtnFileSorter`, `BtnTimerStopwatch`, `BtnColorPicker`, and `BtnQuickNote` buttons.

- Observation: Standard sandboxed WPF builds can fail on generated `obj` files even when source code is valid.
  Evidence: sandboxed `dotnet build` failed with `Access to the path ... App.g.cs is denied`; the same command outside sandbox succeeded.

- Observation: XAML localization tests treat `16x16` preview labels as translatable literal text because `x` is a letter.
  Evidence: `LocalizationServiceTests.XamlTextProperties_DoNotContainTranslatableLiteralText` failed until preview labels were changed to numeric `16`, `32`, `48`, and `256`.

## Decision Log

- Decision: Implement this as a first-class AiteBar built-in utility, not as an external executable.
  Rationale: Existing quick tools follow `IUtility` and are launched from the panel through `ActionService.LaunchUtilityAsync`. Keeping the converter in-process gives consistent window ownership, theming, localization, and panel hide behavior.
  Date/Author: 2026-06-11 / Codex

- Decision: Use a service-based architecture and keep image conversion out of WPF code-behind.
  Rationale: The project handbook says pure calculation or normalizing logic should be extracted into helpers. ICO generation is non-UI logic and must be unit-testable without opening WPF windows.
  Date/Author: 2026-06-11 / Codex

- Decision: Store PNG images inside the ICO container and write a small repository-owned ICO encoder.
  Rationale: Modern Windows supports PNG payloads in ICO, especially for 256x256. A small encoder is easy to test because ICO headers are simple, and it avoids relying on a broad image package to produce correct icon container metadata. The encoder must write width and height as `0` for 256x256 entries.
  Date/Author: 2026-06-11 / Codex

- Decision: Prefer SkiaSharp plus Svg.Skia for rendering if adding dependencies is acceptable during implementation; otherwise fall back to ImageSharp for raster-only support and defer SVG.
  Rationale: The converter needs high-quality resize and SVG-to-raster rendering per output size. SkiaSharp and Svg.Skia are a strong fit for WPF/.NET image rendering. If dependency size or restore risk becomes unacceptable, raster formats should ship first and SVG should be explicitly disabled with a clear message.
  Date/Author: 2026-06-11 / Codex

- Decision: The first production version should support single-file conversion, not batch conversion.
  Rationale: Single-file conversion covers the requested utility and keeps the first integration small enough to test thoroughly. Batch conversion adds conflict handling, progress reporting, partial failure logging, and more UI states; it can be a follow-up once the core converter is proven.
  Date/Author: 2026-06-11 / Codex

- Decision: Ship the first version on built-in WPF/WIC codecs instead of adding SkiaSharp/Svg.Skia immediately. Superseded by the later best-practices upgrade below.
  Rationale: The repository did not already include image-processing packages, and a dependency restore/package decision added native packaging risk. The follow-up pass accepted that risk and verified publish/installer output after adding SkiaSharp/Svg.Skia.
  Date/Author: 2026-06-11 / Codex

- Decision: Upgrade the converter to SkiaSharp 3.119.2 and Svg.Skia 5.0.0 for the best-practices pass.
  Rationale: The requested follow-up requires stable non-WPF rendering, SVG rendered directly to each target size, and deterministic tests for PNG payloads. Svg.Skia 5.0.0 requires SkiaSharp 3.119.2, so the Skia package versions are aligned.
  Date/Author: 2026-06-11 / Codex

- Decision: Keep JPG output opaque when the source has no transparent pixels, even when the UI background mode is transparent.
  Rationale: JPG has no alpha channel; adding transparent padding would create fake transparency. PNG/SVG sources still preserve transparent backgrounds.
  Date/Author: 2026-06-11 / Codex

- Decision: Do not add a global hotkey for the ICO converter in the first version.
  Rationale: The utility is available from the panel and quick-tool settings. Adding a hotkey would require expanding `HotkeyService`, settings UI, and tests; preserving scope keeps the first version focused on conversion correctness.
  Date/Author: 2026-06-11 / Codex

- Decision: Preview generation should not build a full ICO payload on every option change.
  Rationale: Slider/radio changes can fire rapidly. The UI now debounces preview refreshes and uses a preview-only service method; the final ICO is generated from current options only when the user saves.
  Date/Author: 2026-06-12 / Codex

- Decision: Reject unsafe or oversized inputs before rendering.
  Rationale: A desktop image utility should avoid unbounded memory spikes and avoid SVGs that load external/script/data content. Raster inputs are checked by file size and decoded dimensions; SVG inputs are parsed through an XML reader with DTD resolution disabled and then validated against a namespace-aware allowlist of safe SVG elements and attributes before Svg.Skia loads them.
  Date/Author: 2026-06-12 / Codex

- Decision: Reject SVG `<image>`, `<foreignObject>`, event-handler attributes, external/data/file/javascript references, CSS imports, and non-SVG element namespaces.
  Rationale: The converter is intended for static icon artwork. A stricter allowlist is preferable to rendering arbitrary SVG content in-process because it reduces the attack surface while still supporting common vector icon constructs such as paths, shapes, gradients, masks, filters, and local fragment references.
  Date/Author: 2026-06-12 / Codex

- Decision: Validate embedded PNG dimensions in `IcoEncoder`.
  Rationale: The service produces correct PNG payloads, but the encoder is its own correctness boundary. It now verifies that each PNG IHDR width/height matches the declared ICO entry size.
  Date/Author: 2026-06-12 / Codex

## Outcomes & Retrospective

Implemented the ICO converter utility and upgraded it to the best-practices pass. It is launched from the AiteBar panel, supports drag-and-drop or file picker input, generates previews, writes a real ICO container with PNG payloads, preserves transparency for alpha-capable sources, supports SVG through Svg.Skia, supports Windows DPI sizes 20 and 40, and asks before overwriting existing files. Batch conversion, background removal, history, and PNG set export remain follow-ups.

Validation completed on 2026-06-11:

    dotnet build .\AiteBar.sln -c Release
    Result: success, 0 warnings, 0 errors.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Result before Skia/SVG upgrade: success, 419 passed.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Result after Skia/SVG upgrade: success, 426 passed.

    dotnet publish .\AiteBar\AiteBar.csproj -c Release -r win-x64
    Result: success; publish output contains libSkiaSharp.dll, libHarfBuzzSharp.dll, SkiaSharp.dll, and Svg.Skia.dll.

    .\installer\Build-Installer.ps1
    Result: success; installer created in artifacts\installer.

    artifacts\publish\win-x64\AiteBar.exe
    Result: started successfully and stayed alive for 6 seconds in smoke check.

Final verification after layout regression coverage:

    dotnet build .\AiteBar.sln -c Release
    Result: success, 0 warnings, 0 errors.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Result: success, 434 passed.

    dotnet publish .\AiteBar\AiteBar.csproj -c Release -r win-x64
    Result: success; publish output contains libSkiaSharp.dll, libHarfBuzzSharp.dll, SkiaSharp.dll, and Svg.Skia.dll.

    .\installer\Build-Installer.ps1
    Result: success; installer created in artifacts\installer.

Validation completed on 2026-06-12 after hardening:

    dotnet build .\AiteBar.sln -c Release
    Result: success, 0 warnings, 0 errors.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Result: success, 431 passed.

    dotnet publish .\AiteBar\AiteBar.csproj -c Release -r win-x64
    Result: success; publish output contains libSkiaSharp.dll, libHarfBuzzSharp.dll, SkiaSharp.dll, and Svg.Skia.dll.

    .\installer\Build-Installer.ps1
    Result: success; installer created in artifacts\installer.

Validation completed on 2026-06-12 after SVG allowlist hardening:

    dotnet build .\AiteBar.sln -c Release
    Result: success, 0 warnings, 0 errors.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
    Result: success, 441 passed.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~IconConverter|FullyQualifiedName~IcoEncoder"
    Result: success, 31 passed.

    dotnet publish .\AiteBar\AiteBar.csproj -c Release -r win-x64
    Result: success; publish output contains libSkiaSharp.dll, libHarfBuzzSharp.dll, SkiaSharp.dll, Svg.Skia.dll, and related Svg.* runtime assemblies.

    .\installer\Build-Installer.ps1
    Result: success; installer created in artifacts\installer and artifacts\publish\win-x64 contains the SkiaSharp/Svg.Skia native/runtime DLLs.

    artifacts\publish\win-x64\AiteBar.exe
    Result: started successfully and stayed alive for 6 seconds in smoke check.

    dotnet list .\AiteBar.sln package --vulnerable --include-transitive
    Result: no vulnerable packages reported for AiteBar or AiteBar.Tests from the configured sources.

## Context and Orientation

AiteBar is a .NET 8 WPF Windows desktop utility. Built-in utilities are represented by the `IUtility` interface in `AiteBar/UtilityRegistry.cs`. Existing utility classes such as `QuickNoteUtility`, `TimerStopwatchUtility`, `ColorPickerUtility`, and `FileSorterUtility` are registered in `AiteBar/App.xaml.cs` inside `RegisterUtilities()`.

The main panel UI is in `AiteBar/MainWindow.xaml`. Existing built-in utility buttons live inside `SystemUtilsPanel`. Their tooltips, context menus, visibility, visible count, and click handlers are wired in `AiteBar/MainWindow.xaml.cs`. The panel layout is sensitive to the number of visible system buttons, so any new panel button must be included in the existing centralized visibility and count methods instead of being patched only in XAML.

Application settings are defined in `AiteBar/Models.cs` under `AppSettings`. Built-in quick tool visibility is controlled by booleans such as `ShowPresetFileSorter`, `ShowPresetColorPicker`, and `ShowPresetTimerStopwatch`. The program settings window is implemented by `AiteBar/AppSettingsWindow.xaml` and `AiteBar/AppSettingsWindow.xaml.cs`. It includes checkboxes for quick tool visibility and controls for global hotkeys.

Localization lives in `AiteBar/Resources/Strings.resx` and culture-specific files `Strings.ru.resx`, `Strings.uk.resx`, and `Strings.de.resx`. `AiteBar.Tests/LocalizationServiceTests.cs` requires all resource files to contain the same keys, so every new UI string must be added to all four files.

An ICO file is a container, not merely a PNG with a different extension. It begins with an `ICONDIR` header, followed by one `ICONDIRENTRY` for each embedded image, followed by the image data blocks. Each embedded image should be a PNG byte array for this implementation. In the entry metadata, width and height are stored as one byte, and the value `0` means `256`.

The converter must generate several icon sizes from one source image. The default practical size set is 16, 24, 32, 48, 64, 128, and 256 pixels. The richer Windows/DPI-aware set is 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixels. The UI should offer both presets, defaulting to the practical set unless the implementation decides the richer set fits cleanly in the compact settings UI.

## Plan of Work

Start by adding non-UI conversion types. Create a new file `AiteBar/IconConverterModels.cs` with small immutable or simple mutable types that describe conversion inputs and outputs. At minimum define an `IconSizeOption` or use integers for selected sizes, an `IconBackgroundMode` enum with transparent and solid color choices, an `IconFitMode` enum with `Fit` and `Fill`, an `IconConversionOptions` class, an `IconPreviewImage` class for generated preview bytes or WPF-ready image data, and an `IconConversionResult` class containing generated ICO bytes plus warnings.

Add `AiteBar/IcoEncoder.cs`. This file should contain a small encoder that accepts a list of PNG image payloads paired with square sizes. The encoder writes `ICONDIR`, then an `ICONDIRENTRY` per image, then the PNG payloads. It must validate that sizes are between 1 and 256, reject duplicate sizes, reject empty image payloads, write `type = 1`, write `count = image count`, and write width or height as `0` when the size is 256. Unit tests should inspect bytes directly to prove the header is valid.

Add `AiteBar/IconConverterService.cs`. This service owns image loading, preprocessing, resizing, and conversion. It should expose an async method similar to:

    public Task<IconConversionResult> ConvertAsync(string sourcePath, IconConversionOptions options, CancellationToken cancellationToken = default)

If UI previews need incremental updates, also expose a method that returns generated PNG previews without writing the ICO file:

    public Task<IReadOnlyList<IconPreviewImage>> GeneratePreviewsAsync(string sourcePath, IconConversionOptions options, CancellationToken cancellationToken = default)

For raster input, load the source with the selected image library, normalize to sRGB-like 32-bit RGBA where the library supports it, preserve alpha, and render each selected icon size independently from the original source rather than repeatedly resizing from a previous smaller size. For SVG input, render the SVG directly to each target size rather than rendering once at 256 and downscaling. If SVG support is deferred, validate the extension and show a localized "SVG is not supported in this build" message.

The default rendering behavior should be `Fit` plus transparent background plus 8 percent padding. `Fit` means the whole source image is scaled to fit inside the square canvas without changing its proportions. `Fill` means the image fills the square and may be cropped. Padding means the content box is smaller than the full icon canvas, leaving transparent or colored space around the image. The generated image should be centered.

Small-size quality matters. Use high-quality sampling from the rendering library. For 16x16 and 24x24, avoid aggressive sharpening by default. If a sharpening option is added, keep it subtle and off or low by default. If the source is smaller than the largest requested output size, add a warning such as "The source image is smaller than 256x256; upscaling may reduce quality."

Add `AiteBar/IconConverterWindow.xaml` and `AiteBar/IconConverterWindow.xaml.cs`. Follow the existing dark compact style used by other utility windows. The first version should include a drag-and-drop area, a "Choose image" button, selected file name, source dimensions when known, preview tiles for 16x16, 32x32, 48x48, and 256x256, size preset controls, individual size checkboxes, padding control from 0 to 20 percent, background mode selection, a solid background color field or button when solid background is selected, and a "Save ICO" button. Do not add a full-window vertical scroll unless the existing fixed window size cannot reasonably fit; prefer compact grouped controls.

Add `AiteBar/IconConverterUtility.cs`, probably inheriting from `UtilityBase<IconConverterWindow>`. Its `Id` should be stable, for example `IconConverter`. Its `DisplayNameKey` should be `Tool_IconConverter`. Choose a Fluent icon glyph that visually reads as image/icon conversion and a color that fits the existing muted dark palette without over-dominating the panel.

Register the utility in `AiteBar/App.xaml.cs` inside `RegisterUtilities()`:

    UtilityRegistry.Register(new IconConverterUtility());

Integrate the panel button. In `AiteBar/MainWindow.xaml`, add `BtnIconConverter` to `SystemUtilsPanel` near other utility buttons, not among user custom buttons. In `AiteBar/MainWindow.xaml.cs`, add its localized tooltip in `ApplyLocalizedText()`, add a right-click context menu in `AttachSystemUtilityContextMenus()`, include the setting in `GetVisibleSystemButtonCount()`, add it to `EnumeratePanelButtons()` if keyboard focus handling requires it, add it to `ApplySystemUtilityVisibility()`, include it in the `hasSystemUtils` calculation, and add a click handler that calls:

    await RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("IconConverter", HideDock));

Add a new setting in `AiteBar/Models.cs`:

    public bool ShowPresetIconConverter { get; set; } = true;

Wire this setting into `AiteBar/AppSettingsWindow.xaml` under the quick tools checkboxes and in `AiteBar/AppSettingsWindow.xaml.cs` under load and save. If hotkey support is included in the first version, also add `IconConverterHotkey`, extend `HotkeyCommand`, add a unique ID in `HotkeyService`, add descriptor and binding mapping, update settings window hotkey controls, and add execution in `MainWindow.ExecuteHotkeyCommand`. If schedule pressure appears, defer hotkey support to keep the converter high quality; the panel button and settings visibility are mandatory.

Add localization keys to all resource files. Required keys include `Tool_IconConverter`, `Main_IconConverterTooltip`, `IconConverter_Title`, `IconConverter_ChooseImage`, `IconConverter_DropHint`, `IconConverter_SaveIco`, `IconConverter_SizePreset`, `IconConverter_Padding`, `IconConverter_Background`, `IconConverter_BackgroundTransparent`, `IconConverter_BackgroundSolid`, `IconConverter_WarningSourceTooSmall`, `IconConverter_ErrorUnsupportedFormat`, `IconConverter_ErrorNoSizesSelected`, `IconConverter_OverwriteConfirm`, and any labels actually used by the XAML. Keep the wording short because AiteBar settings are compact.

Add tests before or alongside implementation. `AiteBar.Tests/IcoEncoderTests.cs` should assert that the encoder writes `reserved = 0`, `type = 1`, correct count, correct offsets, correct byte sizes, and `0` width/height for 256. It should also assert duplicate sizes and empty payloads are rejected. `AiteBar.Tests/IconConverterServiceTests.cs` should cover size validation and warning generation. If the image library can run in tests without WPF dispatcher, include a small generated in-memory PNG fixture and verify the output ICO contains the selected sizes. Avoid tests that depend on screen DPI or opening windows.

Update `docs/UTILITIES.md` after implementation because it currently describes the older `Launch` shape. The update should mention `LaunchAsync`, `UtilityBase<TWindow>`, and the actual files that must be touched for panel visibility and settings.

## Concrete Steps

From repository root `D:\01_Codebdbd\01_projects\aitebar`, inspect current state before editing:

    git status --short
    rg -n "QuickNote|TimerStopwatch|ColorPicker|FileSorter|ShowPreset" AiteBar AiteBar.Tests

Add dependencies only after confirming the package choice. If using SkiaSharp and Svg.Skia, add packages to `AiteBar/AiteBar.csproj` with pinned versions compatible with .NET 8 WPF. If package restore fails because of sandboxed network access, request escalation for restore/build rather than changing the design silently.

Implement in this order:

1. Add `IconConverterModels`, `IcoEncoder`, and tests for `IcoEncoder`.
2. Add `IconConverterService` for raster input and tests for validation and warnings.
3. Add SVG support if the selected library validates cleanly in a small service test; otherwise keep SVG blocked with a localized message and record that decision in this plan.
4. Add `IconConverterWindow` and `IconConverterUtility`.
5. Register and wire the utility into panel, settings, and localization.
6. Update utility documentation.
7. Run build, tests, and manual verification.

Expected build command:

    dotnet build .\AiteBar.sln -c Release

Expected test command:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

If `dotnet test` fails due to WPF/MSBuild temporary generated files such as `wpftmp`, run the documented fallback after a successful build:

    dotnet vstest .\AiteBar.Tests\bin\Release\net8.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

Unit-level acceptance:

Running `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release` should pass all existing tests plus the new ICO tests. The new encoder tests must prove that a generated ICO has multiple entries and handles 256x256 as `0` in the ICO directory entry. A test using selected sizes 16, 32, 48, and 256 should confirm the count is 4 and offsets point to the expected PNG payloads.

Service-level acceptance:

Given a transparent PNG source, the conversion service should generate an ICO that contains the default sizes 16, 24, 32, 48, 64, 128, and 256. The service should not flatten transparent pixels to white or black. Given a 64x64 source and selected size 256, the service should return a warning that upscaling can reduce quality. Given no selected sizes, the service should reject conversion with a localized error.

UI acceptance:

After starting AiteBar, the panel should show the new ICO converter button when quick tools are visible for the active context and `ShowPresetIconConverter` is true. Clicking the button should hide the panel and open the converter window. Dragging a supported image onto the window should show previews. Clicking "Save ICO" should prompt for a save path. If the target file exists, the utility should confirm before overwriting. Saving should produce a file with `.ico` extension that Windows Explorer can display.

Panel acceptance:

Because this change adds a system utility button, manually verify all four panel edges: `Top`, `Bottom`, `Left`, and `Right`. For each edge, verify panel show, hide, positioning, button visibility, tooltip placement, and that user buttons still wrap correctly. Also verify the panel still supports drag-and-drop by handle to change side and monitor.

Settings acceptance:

Open program settings and verify the quick tools tab includes the ICO converter checkbox. Turning it off should hide the panel button after saving settings. Turning it on should restore the button. If hotkey support is included, assign a hotkey with a modifier, save, restart registration, and verify it opens the converter. Also verify duplicate hotkeys are rejected.

Release acceptance:

Run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

Both commands should complete successfully. If this feature changes publish or installer files, also run:

    .\installer\Build-Installer.ps1

This plan does not require installer changes unless new native dependencies require explicit packaging adjustments.

## Idempotence and Recovery

Most implementation steps are additive and can be retried. If dependency installation fails, keep the working tree unchanged except for planned project file edits, record the failure in `Surprises & Discoveries`, and either request network approval or switch to a dependency already available in the project only after recording the decision.

Do not delete user files while testing conversion. Use temporary input files under the test output directory or a controlled temp path. Do not overwrite an existing user-selected `.ico` without a confirmation dialog. Tests should create and delete their own temporary files through test framework cleanup patterns.

If UI wiring breaks panel layout, revert only the new converter-specific edits and keep unrelated user changes intact. The likely places to check are `GetVisibleSystemButtonCount()`, `ApplySystemUtilityVisibility()`, `EnumeratePanelButtons()`, and `SystemUtilsPanel` in `MainWindow.xaml`.

## Artifacts and Notes

The ICO encoder must produce this conceptual structure:

    ICONDIR
      reserved: 0
      type: 1
      count: number of selected sizes
    ICONDIRENTRY for each size
      width: size, or 0 when size is 256
      height: size, or 0 when size is 256
      color count: 0
      reserved: 0
      planes: 1
      bit count: 32
      bytes in resource: PNG payload byte length
      image offset: absolute byte offset from start of ICO file
    PNG payloads

Default conversion options:

    Sizes: 16, 24, 32, 48, 64, 128, 256
    Payload format: PNG inside ICO
    Background: transparent
    Padding: 8 percent
    Fit mode: Fit
    Color: 32-bit RGBA
    Overwrite: only after user confirmation

Supported input formats for the target feature:

    Required in first version: PNG, JPG/JPEG, WEBP, BMP, TIFF
    Supported: SVG
    Deferred follow-up: batch conversion, background removal, Windows Explorer style preview, PNG set export, recent files

## Interfaces and Dependencies

Define these files and public/internal surfaces unless implementation discoveries justify a recorded change.

In `AiteBar/IconConverterModels.cs`, define:

    public enum IconBackgroundMode
    {
        Transparent,
        SolidColor
    }

    public enum IconFitMode
    {
        Fit,
        Fill
    }

    public sealed class IconConversionOptions
    {
        public IReadOnlyList<int> Sizes { get; init; }
        public double PaddingPercent { get; init; }
        public IconBackgroundMode BackgroundMode { get; init; }
        public string BackgroundColor { get; init; }
        public IconFitMode FitMode { get; init; }
    }

    public sealed class IconPreviewImage
    {
        public int Size { get; init; }
        public byte[] PngBytes { get; init; }
    }

    public sealed class IconConversionResult
    {
        public byte[] IcoBytes { get; init; }
        public IReadOnlyList<IconPreviewImage> Previews { get; init; }
        public IReadOnlyList<string> Warnings { get; init; }
    }

Adjust constructors or property defaults to match the repository's style, but preserve these concepts.

In `AiteBar/IcoEncoder.cs`, define:

    internal sealed record IcoImageEntry(int Size, byte[] PngBytes);

    internal static class IcoEncoder
    {
        public static byte[] Encode(IReadOnlyList<IcoImageEntry> images);
    }

In `AiteBar/IconConverterService.cs`, define:

    public sealed class IconConverterService
    {
        public Task<IconConversionResult> ConvertAsync(
            string sourcePath,
            IconConversionOptions options,
            CancellationToken cancellationToken = default);

        public Task<IReadOnlyList<IconPreviewImage>> GeneratePreviewsAsync(
            string sourcePath,
            IconConversionOptions options,
            CancellationToken cancellationToken = default);
    }

In `AiteBar/IconConverterUtility.cs`, define:

    [SupportedOSPlatform("windows6.1")]
    public sealed class IconConverterUtility : UtilityBase<IconConverterWindow>
    {
        public override string Id => "IconConverter";
        public override string DisplayNameKey => "Tool_IconConverter";
    }

The exact glyph and color can be chosen during implementation, but they must be added consistently to the panel button and utility class.

Recommended dependencies:

Use SkiaSharp for raster decode, resize, and PNG encode, and Svg.Skia for SVG rendering if the package works cleanly in this WPF project. The implementation should render from the original source to each selected target size independently. If a dependency introduces native runtime assets, verify `dotnet publish` includes them before release.

Change Note 2026-06-11: Initial ExecPlan created from the user's ICO converter requirements and the current AiteBar utility architecture. The plan intentionally scopes batch conversion and background removal as follow-ups so the first version can focus on correctness, previews, and Windows ICO quality.
