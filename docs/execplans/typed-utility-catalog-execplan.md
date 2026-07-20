# Centralize built-in utility metadata and visibility access

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

Adding or changing a built-in AiteBar utility currently requires repeating the same `ShowPreset...` property name and metadata in several runtime switches and UI handlers. After this change, one typed catalog will define each utility's stable ID, icon, color, tooltip, and visibility getter/setter. The panel, settings window, visibility count, and context-menu hide action will consume that catalog, so a missing string case can no longer silently hide or misconfigure a utility.

## Progress

- [x] (2026-07-15 02:18Z) Located all runtime and test dependencies on `ShowPreset...`, utility ordering, visibility counting, settings checkboxes, and context-menu hiding.
- [x] (2026-07-15 02:20Z) Added the typed utility catalog while preserving serialized `AppSettings` properties and stable utility IDs.
- [x] (2026-07-15 02:20Z) Migrated `UnifiedButtonService`, `AppSettingsService`, `MainWindow`, `UnifiedButton`, and `AppSettingsWindow` to the catalog.
- [x] (2026-07-15 02:21Z) Replaced brittle source-text integration assertions with behavioral catalog tests.
- [x] (2026-07-15 02:23Z) Completed focused tests, Release build, and the complete test suite.

## Surprises & Discoveries

- Observation: the 48 quoted `ShowPreset...` literals are concentrated in one metadata list and two switches, but direct property chains are also repeated in `MainWindow` and `AppSettingsWindow`.
  Evidence: `rg` found 16 catalog strings in `UnifiedButtonService`, 32 switch strings in `AppSettingsService`, 16 manual count branches in `MainWindow`, and two 16-line checkbox blocks in `AppSettingsWindow`.

- Observation: `UnifiedButton.SettingsKey` is not a serialization key; it is used only to route the context-menu hide action.
  Evidence: the only consumers are `UnifiedButtonService` assignment and `MainWindow.BuildUnifiedButtonContextMenu`, so the already-present stable utility `Id` can replace it.

- Observation: centralization also removes repeated settings snapshots during panel construction.
  Evidence: the previous `UnifiedButtonService` called `AppSettingsService.GetUtilityVisibility` once per definition, and each call cloned all settings. The migrated service reads one `AppSettings` snapshot before filtering all catalog entries.

## Decision Log

- Decision: retain every public `AppSettings.ShowPreset...` Boolean property.
  Rationale: those property names are the existing JSON contract. Removing or renaming them would require a settings migration and would not help the runtime duplication problem.
  Date/Author: 2026-07-15 / Codex

- Decision: define visibility with `Func<AppSettings, bool>` and `Action<AppSettings, bool>` delegates in the catalog instead of reflection or property-name strings.
  Rationale: delegates are compile-time checked, avoid reflection failure modes, and preserve direct property access performance.
  Date/Author: 2026-07-15 / Codex

- Decision: use stable utility IDs such as `Search` and `ClipboardManager` for lookup and ordering, and remove `UnifiedButton.SettingsKey`.
  Rationale: these IDs already drive `UtilityButtonOrder`, action dispatch, and button identity. A second identifier carrying a C# property name creates unnecessary synchronization risk.
  Date/Author: 2026-07-15 / Codex

## Outcomes & Retrospective

The typed utility catalog is complete. All sixteen existing utilities retain their stable IDs, fallback order, icons, colors, localization keys, serialized visibility properties, and settings checkboxes. Runtime code contains no quoted `ShowPreset...` property keys. `UnifiedButton.SettingsKey` and the visibility getter/setter switches were removed; context-menu hiding now routes through the utility ID.

Four new behavioral tests cover catalog order and metadata, independent typed visibility accessors, primary-context panel composition and ordering, and stable-ID visibility updates. Focused tests passed 12/12, the full suite passed 629/629, and Release build completed with zero warnings and errors.

## Context and Orientation

`AiteBar/Models.cs` contains sixteen persisted `ShowPreset...` Boolean properties. `AiteBar/UnifiedButtonService.cs` separately defines sixteen metadata rows whose `SettingsKey` values are those property names. `AiteBar/AppSettingsService.cs` translates the strings through getter and setter switches. `AiteBar/MainWindow.xaml.cs` manually counts each property and uses `UnifiedButton.SettingsKey` to hide a utility. `AiteBar/AppSettingsWindow.xaml.cs` manually copies the same properties to and from sixteen named WPF checkboxes. `UtilityButtonOrder` stores stable IDs, not property names.

The new `AiteBar/UtilityButtonCatalog.cs` will be the runtime source of truth. A utility definition is a small immutable record containing its stable ID, visual metadata, localization key, and typed visibility delegates. Named definitions support readable WPF checkbox mappings; an ordered `All` list supports panel construction and counting.

## Plan of Work

Create `UtilityButtonCatalog.cs` with one named definition per existing built-in utility and an ordered `All` collection matching the current panel fallback order. Add ordinal ID lookup and a visible-count helper.

Change `UnifiedButtonService.BuildUnifiedList` to take one `AppSettings` snapshot, filter catalog definitions through typed getters, and order them by `UtilityButtonOrder`. Remove its local metadata list, settings-key lookup helper, and old `UtilityButtonDef` record. Populate utility `UnifiedButton` instances without a settings key.

Change `AppSettingsService.SetUtilityVisibility` to resolve a stable utility ID in the catalog and invoke its setter inside `UpdateSettings`. Remove the getter and both property-name switches. Change `MainWindow` to count visible catalog definitions and hide by `item.Id`. Remove `UnifiedButton.SettingsKey`.

In `AppSettingsWindow`, add one method returning checkbox/definition pairs. Use a loop to load and save visibility rather than repeating direct property assignments. Named XAML checkboxes remain unchanged, preserving the compact settings layout and localization bindings.

Update integration tests that search source text for old implementation details. Add behavioral tests proving IDs and metadata are unique, every typed getter/setter round-trips its property without changing another definition, the panel honors catalog visibility and ordering, and `AppSettingsService` hides by stable ID while ignoring an unknown ID.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

After edits, run focused tests:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~UtilityButtonCatalogTests|FullyQualifiedName~ClipboardManagerIntegrationTests|FullyQualifiedName~IconConverterIntegrationTests|FullyQualifiedName~MainWindowIconConverterOrientationTests"

Then run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

## Validation and Acceptance

The catalog test must enumerate exactly the existing sixteen stable IDs in the existing fallback order, with no duplicate IDs. Toggling a definition through its setter must be observable through its getter and must not change any other definition. Building the primary-context unified list with only one enabled utility must produce that utility, while a non-primary context must contain no system utilities.

The settings-service test must hide a utility by stable ID and leave settings unchanged for an unknown ID. Existing settings JSON properties, XAML checkbox names, utility ordering, drag-and-drop, and action dispatch IDs must remain unchanged.

Release build must complete with zero warnings and errors, and the complete test suite must pass. Because this refactor does not change panel geometry, manual four-edge layout verification is not required; existing orientation tests provide regression coverage for the panel composition path.

## Idempotence and Recovery

The change is source-only and can be applied repeatedly. Do not reset or overwrite the existing reliability changes or the pre-existing `ActionService.cs` continuation edits. If WPF generated files are locked, stop only testhost or build processes started by this task and retry outside the filesystem sandbox.

## Artifacts and Notes

The working tree already contains the completed reliability-hardening changes documented in `docs/reliability-hardening-execplan.md`. This plan adds a separate architectural step on top of that state.

Final validation evidence:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~UtilityButtonCatalogTests|FullyQualifiedName~ClipboardManagerIntegrationTests|FullyQualifiedName~IconConverterIntegrationTests|FullyQualifiedName~MainWindowIconConverterOrientationTests"
    Passed: 12, Failed: 0, Skipped: 0.

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --no-restore
    Passed: 629, Failed: 0, Skipped: 0.

    dotnet build .\AiteBar.sln -c Release
    Build succeeded. 0 Warning(s), 0 Error(s).

## Interfaces and Dependencies

No package is added. Define an internal immutable `UtilityButtonDefinition` and internal static `UtilityButtonCatalog`. `UtilityButtonCatalog.All` is an ordered `IReadOnlyList<UtilityButtonDefinition>`. `UtilityButtonCatalog.TryGet(string id, out UtilityButtonDefinition definition)` performs ordinal ID lookup. Each definition exposes `bool IsVisible(AppSettings settings)` and `void SetVisibility(AppSettings settings, bool visible)` through stored delegates.

`AppSettingsService.SetUtilityVisibility` retains a public string parameter, but the string now means stable utility ID rather than a C# settings-property name. All in-repository callers are migrated in the same change.

Plan revision note (2026-07-15 02:18Z): created the initial self-contained plan after enumerating every runtime and test dependency on utility visibility.

Plan revision note (2026-07-15 02:23Z): marked the catalog migration complete and recorded the one-snapshot optimization plus focused and full validation evidence.
