# QR Code Generator Enhancements

This ExecPlan is a living document. It follows `PLANS.md` and is updated as the QR generator feature moves from design to working behavior.

## Purpose

Improve the built-in QR code generator from a basic text-to-code utility into a practical business tool for common use cases: URL QR codes, plain text, Wi-Fi access, branded codes with a centered logo, brand colors with contrast validation, quality presets, selectable output PNG size, SVG copy/export, and restrained module/eye styling.

The finished behavior should be easy for a non-technical user: choose a QR type, choose a quality preset, optionally brand it with colors and a logo, see a realistic preview, then copy or save PNG/SVG.

## Progress

- [x] Read the current QR service, QR window, existing tests, and available image dependencies.
- [x] Create this ExecPlan before broad implementation.
- [x] Extend QR models and service for content types, presets, colors, logo, output size, shapes, and SVG copy support.
- [x] Add focused unit tests for non-UI behavior.
- [x] Redesign the QR generator window around compact, logical groups without overloading the layout.
- [x] Build, run focused tests, and rebuild the installer.

## Surprises & Discoveries

- The project already references SkiaSharp and Svg.Skia, so custom PNG rendering can be implemented without adding packages.
- Existing QR tests target the service API directly; preserving existing `Text`, `PixelSize`, `Margin`, and `EccLevel` semantics keeps older tests meaningful while extending the model.

## Decision Log

- Use custom SkiaSharp rendering for PNG and a custom SVG writer instead of relying only on QRCoder renderers. This is needed for centered logo, rounded/circle modules, eye styling, and consistent output sizing.
- Keep presets user-facing and keep raw error correction as an internal/output consequence. A logo forces high error correction because logo overlap intentionally hides QR modules.
- Enforce color validity and report contrast risk. Low contrast should be visible to the user because a visually attractive QR can become hard to scan.

## Outcomes & Retrospective

Implemented the QR generator enhancements with a custom QR renderer, business-focused presets, URL/text/Wi-Fi payloads, logo insertion, color and contrast support, final PNG sizing, SVG copy/export, and module/eye styling. Focused QR service tests pass. Release build and installer generation completed successfully. Full test suite still has pre-existing non-QR failures in integration/layout/localization checks, including IconConverter literal text and app icon resource lookup.

## Context

Current implementation:

- `AiteBar/QRCodeModels.cs` has only error correction, text, pixel size, margin, and two colors.
- `AiteBar/QRCodeService.cs` uses QRCoder PNG/SVG/XAML renderers.
- `AiteBar/QRCodeGeneratorWindow.xaml` is already compact with a preview column and a parameters/actions column.
- `AiteBar.Tests/QRCodeServiceTests.cs` verifies basic PNG/SVG generation, text validation, ECC behavior, and render helpers.

Important project constraints:

- Follow the existing WPF style and dark compact utility window design.
- Avoid adding new dependencies unless unavoidable.
- Add tests for non-UI logic.
- After installer-related or publish-impacting changes, run `installer/Build-Installer.ps1` and confirm output in `artifacts/installer`.

## Design

### Service model

Add enums and options for:

- `QRCodeContentType`: `Text`, `Url`, `Wifi`.
- `QRCodeQualityPreset`: `Screen`, `Print`, `Logo`.
- `QRCodeModuleShape`: `Square`, `Rounded`, `Circle`.
- `QRCodeEyeStyle`: `Square`, `Rounded`.
- `QRCodeWifiSecurity`: `Wpa`, `Wep`, `None`.

Extend `QRCodeGenerationOptions` with:

- QR type and Wi-Fi fields.
- preset and final output size in pixels.
- module and eye style.
- optional logo path and constrained logo scale.

Extend result with:

- normalized payload, PNG width/height, contrast ratio, and warnings.

### Rendering

The service will build QR matrix data through QRCoder, then render:

- PNG through SkiaSharp at exact `OutputSize`.
- SVG by writing vector modules and finder patterns directly.
- Center logo by drawing a white rounded backing square plus the logo image, capped to a conservative percentage of the final code size and forcing high error correction.

Finder pattern areas are rendered separately so module styling does not damage the three scan anchors. Regular dark modules outside finder patterns use the selected shape.

### UI

Keep the utility compact and avoid a cluttered “settings wall”:

- Top: QR type and primary input.
- Wi-Fi fields appear only for Wi-Fi type.
- Left: preview with final PNG size/status.
- Right: practical controls grouped as quality, output, branding, style, actions.
- Actions remain in the right column one per row: copy PNG, copy SVG, save PNG, save SVG.

### Validation

- Empty required payloads produce existing localized errors.
- URL mode normalizes URLs without a scheme to `https://...`.
- Wi-Fi payload escapes QR Wi-Fi special characters.
- Colors must be valid hex values; contrast ratio is calculated and shown in warnings if too low.
- Logo file must exist and decode as an image. Logo overlay is capped.

## Implementation Steps

1. Extend `QRCodeModels.cs` with enums/options/result fields while preserving existing defaults.
2. Rewrite `QRCodeService.cs` around normalized options, payload creation, contrast calculation, custom PNG/SVG rendering, and logo handling.
3. Add or update `QRCodeServiceTests.cs` for URL normalization, Wi-Fi payload escaping, contrast warnings, exact output PNG dimensions, SVG generation, and logo preset behavior.
4. Update `QRCodeGeneratorWindow.xaml` and `.xaml.cs` to expose the new controls and actions.
5. Add required localization keys in English, Russian, Ukrainian, and German resources.
6. Run focused QR tests, then Release build, then installer build.

## Verification

Expected commands:

```powershell
dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter QRCodeServiceTests
dotnet build .\AiteBar.sln -c Release
.\installer\Build-Installer.ps1
```

If full tests are run and unrelated existing WPF/integration failures remain, record them in the final response without masking the QR focused result.

- Service implementation now uses QRCoder for matrix generation and custom Skia/SVG rendering for exact output size, branding, logo, module shapes, and eye styles.


- Focused QRCodeServiceTests pass: 22/22 after service changes.




- Design refinement after visual review: widened the QR generator window, simplified the settings panel into labeled rows, moved actions into a footer, restored visible default color values, and rebuilt the installer.

