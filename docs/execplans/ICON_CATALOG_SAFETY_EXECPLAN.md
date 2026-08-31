# Safely extract icon catalog metadata without changing saved buttons

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds. This document is maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

AiteBar users must keep every existing button icon after the icon catalog implementation is cleaned up. This first safety milestone extracts Fluent and brand metadata from `IconPickerWindow` into a testable service while preserving the window's public selection result and the persisted `CustomElement.Icon`, `CustomElement.IconFont`, and `CustomElement.ImagePath` fields. A developer can demonstrate the outcome by running focused compatibility tests and the full Release suite; existing Fluent, Font Awesome Brands, legacy Material, supplementary Unicode, and image-backed button representations must round-trip unchanged.

## Progress

- [x] (2026-08-31 12:35Z) Audited the current picker, metadata loading, saved icon fields, import/export mapping, and existing tests.
- [x] (2026-08-31 12:36Z) Created this ExecPlan and constrained the milestone to a non-breaking service extraction plus characterization tests.
- [x] (2026-08-31 12:44Z) Added compatibility tests that lock settings persistence and package round trips for Fluent, Brands, Material, supplementary glyphs, and image-backed buttons.
- [x] (2026-08-31 12:47Z) Added `AiteBar/IconCatalogService.cs` and moved metadata parsing, display-code selection, name formatting, and search-key construction into it.
- [x] (2026-08-31 12:48Z) Adapted `AiteBar/IconPickerWindow.xaml.cs` to consume the service without changing `SelectedIcon`, `SelectedFont`, `SelectedImagePath`, tab behavior, or saved settings.
- [x] (2026-08-31 12:53Z) Passed 43 focused tests, a zero-warning Release build, and the complete suite with 1,531 passed and 5 expected skips.
- [x] (2026-08-31 12:55Z) Smoke-started the current Release executable successfully and reviewed the final diff for persisted-schema changes.

## Surprises & Discoveries

- Observation: the picker is not virtualized and creates one WPF button per catalog entry, but pagination and virtualization are deliberately outside this milestone.
  Evidence: `AiteBar/IconPickerWindow.xaml` uses `ScrollViewer` plus `WrapPanel`, and `LoadIcons` adds every display code to `IconPanel.Children`.
- Observation: a Fluent metadata warmup already runs on the UI dispatcher during application idle.
  Evidence: `AiteBar/MainWindow.xaml.cs` calls `Dispatcher.BeginInvoke(IconPickerWindow.WarmupCatalogMetadata, DispatcherPriority.ApplicationIdle)`.
- Observation: parallel solution restore can end with a failed status while reporting zero warnings and zero errors in this workspace, whereas single-node MSBuild completes normally.
  Evidence: `dotnet build .\AiteBar.sln -c Release -p:NuGetAudit=false -m:1` completed with zero warnings and zero errors.
- Observation: panel packaging intentionally relocates an image path while preserving its legacy glyph and font fallback fields.
  Evidence: `PanelPackageRoundTrip_PreservesImageReferenceAndLegacyFallbackFields` expects the imported image destination and the original `Icon` and `IconFont`.

## Decision Log

- Decision: preserve the current persisted icon schema exactly in this milestone.
  Rationale: changing storage while extracting catalog logic would combine unrelated risks and could alter existing user buttons.
  Date/Author: 2026-08-31 / Codex.
- Decision: retain both the Brands tab and legacy Material renderer.
  Rationale: brand logos are user content rather than application chrome, while Material remains necessary to render previously saved buttons.
  Date/Author: 2026-08-31 / Codex.
- Decision: do not implement a custom virtualizing wrap panel yet.
  Rationale: WPF measure, scrolling, keyboard, and DPI behavior require a separate independently verifiable milestone.
  Date/Author: 2026-08-31 / Codex.

## Outcomes & Retrospective

The safety milestone is complete. `IconCatalogService` now owns catalog metadata and search entry construction, while `IconPickerWindow` still owns WPF controls and returns the same three selection values. Ten new tests cover service behavior and icon compatibility; the full suite increased from 1,521 to 1,531 passing tests, with the same five opt-in native Quick Note integration tests skipped. The Release executable smoke-started successfully. No `CustomElement` property, settings schema, import/export DTO, font asset, tab, or selected-value contract changed. Pagination, cancellation of overlapping picker loads, localized aliases, and UI virtualization remain intentionally deferred.

## Context and Orientation

`AiteBar/IconPickerWindow.xaml` defines the modal icon selection window. `AiteBar/IconPickerWindow.xaml.cs` currently reads the bundled Fluent JSON metadata, inspects glyphs in the selected font, builds search strings, creates WPF buttons, and returns the chosen values through `SelectedIcon`, `SelectedFont`, and `SelectedImagePath`. `AiteBar/FontHelper.cs` maps stable string keys to the bundled Fluent, Font Awesome Brands, and legacy Material fonts. `AiteBar/Models.cs` defines `CustomElement`, whose `Icon`, `IconFont`, and `ImagePath` properties are serialized to user settings. `AiteBar/PanelPackageMapper.cs` maps those same values during panel export and import.

An icon tuple means the three persisted values `Icon`, `IconFont`, and `ImagePath`. A characterization test records existing behavior before refactoring so a later implementation must produce the same result. A supplementary Unicode glyph is a character above `U+FFFF` represented by a surrogate pair in a .NET string; the picker already supports these by using `char.ConvertFromUtf32` and a `TextBlock`.

## Plan of Work

First add focused tests around the icon tuple. Test cloning or serialization for Fluent, Brands, legacy Material, a supplementary character, and an image-backed element. Extend package mapper coverage where necessary so export followed by import preserves the icon and font whenever no image replaces the glyph.

Next create `AiteBar/IconCatalogService.cs`. Define a small immutable `IconCatalogEntry` carrying code point, symbol, display name, tooltip, and normalized search key. The service will parse the existing Fluent resource through an injected stream factory, format official names exactly as the window does today, inspect a supplied glyph map, and create entries in the same numeric order. Brand alias data remains available to the service and produces the same current search keys. The service must contain no WPF controls and must not modify settings.

Then adapt `IconPickerWindow` to ask the service for entries and create the same buttons from those entries. Keep tab selection, batches of 100, active-font guard, tooltips, returned selection properties, and error dialogs unchanged. Move `WarmupCatalogMetadata` to warm the service cache. Do not add pagination, a new storage field, automatic migration, or asset deletion.

Finally run the focused catalog and compatibility tests, the standard Release build, and the full test suite. Inspect the diff to confirm there are no modifications to `CustomElement` fields or serialization behavior.

## Concrete Steps

Run all commands from `D:\01_Codebdbd\01_projects\aitebar`.

Inspect focused behavior with:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~IconCatalogServiceTests|FullyQualifiedName~IconCompatibilityTests|FullyQualifiedName~PanelPackageMapperTests"

Build the solution with a single MSBuild node because parallel solution restore has shown a zero-error restore failure in this workspace:

    dotnet build .\AiteBar.sln -c Release -p:NuGetAudit=false -m:1

Run the complete suite:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-restore -m:1

If the WPF test host fails because of generated temporary files, run the repository fallback:

    dotnet vstest .\AiteBar.Tests\bin\Release\net10.0-windows\AiteBar.Tests.dll

## Validation and Acceptance

The new focused tests must prove that all five icon representations retain their exact icon tuple. Fluent metadata must still expose 2,426 unique 24-pixel regular entries from the bundled resource, brand entries must remain selectable and searchable by their existing aliases, and supplementary glyphs must remain valid two-character .NET strings. The application must compile in Release with zero errors. The complete suite must pass with only the five existing opt-in Quick Note native integration tests allowed to remain skipped.

Manual acceptance for this internal milestone is limited to opening the picker, switching between Fluent and Brands, searching, and selecting one item from each tab. Saving a button must continue to show the selected glyph after the settings window is reopened. No settings migration or automatic rewrite should occur.

## Idempotence and Recovery

The extraction is additive and safe to repeat. The bundled font and metadata assets remain untouched. If the service adaptation fails, `IconPickerWindow.xaml.cs` can temporarily resume its local parsing methods without changing persisted data. Tests should use in-memory streams or existing bundled resources and must not write user settings. Do not delete legacy fonts or rewrite user configuration files.

## Artifacts and Notes

Initial audit evidence:

    Fluent metadata entries: 9410
    Fluent 24 regular entries: 2426
    Font Awesome private-use glyph codepoints: 561
    Current picker batch size: 100

Final validation evidence:

    Focused tests: 43 passed, 0 failed
    Release build: 0 warnings, 0 errors
    Full tests: 1531 passed, 5 skipped, 0 failed
    Release smoke start: STARTED

## Interfaces and Dependencies

Create `AiteBar/IconCatalogService.cs` with an immutable entry type and an internal service. The service must use only .NET collections, JSON parsing, streams, and the glyph-map interface already provided by WPF's `GlyphTypeface.CharacterToGlyphMap`. It must not depend on `Button`, `TextBlock`, `Window`, `CustomElement`, or the settings service.

The final service interface should provide operations equivalent to:

    IReadOnlyList<IconCatalogEntry> BuildEntries(
        string fontName,
        IDictionary<int, ushort> glyphMap);

    void Warmup();

`IconCatalogEntry` must expose `CodePoint`, `Symbol`, `DisplayName`, `Tooltip`, and `SearchKey`. `IconPickerWindow` remains responsible for creating WPF controls and setting `SelectedIcon`, `SelectedFont`, and `SelectedImagePath`.

Plan revision note: 2026-08-31. Initial plan created to isolate a non-breaking catalog-service extraction from future pagination, virtualization, or persisted-schema changes. Updated after implementation to record compatibility coverage, the single-node build requirement observed in this workspace, validation results, and deferred work.
