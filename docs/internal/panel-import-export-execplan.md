# Panel Import And Export

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document follows [PLANS.md](/d:/01_Codebdbd/01_projects/aitebar/PLANS.md) from the repository root and must be maintained in accordance with it.

## Purpose / Big Picture

After this change, a user can export the active AiteBar panel into one portable `.aitebarpanel` file and import such a file back into the current panel from either the panel context menu or the tray menu. The package must carry all button metadata and any file-based icons so the imported result looks and behaves like the exported panel. The feature is observable by exporting a panel with mixed icon types, importing it into another panel, and seeing the imported buttons appear at the end of the current panel with restored icons.

## Progress

- [x] (2026-04-24 09:55Z) Researched the existing settings storage, icon storage, panel menus, and project planning requirements.
- [x] (2026-04-24 10:10Z) Defined the package contract as a renamed zip archive with `manifest.json` plus optional `icons/`.
- [x] (2026-04-24 10:32Z) Implemented package DTOs, mapping helpers, and the core export/import service.
- [x] (2026-04-24 10:54Z) Added unit tests for empty panels, built-in icons, file icons, context reassignment, ID regeneration, and invalid manifests.
- [x] (2026-04-24 11:07Z) Wired import/export into the RootBorder context menu and tray menu.
- [x] (2026-04-24 11:15Z) Updated user-facing documentation in `README.md` and `USER_MANUAL.md`.
- [x] (2026-04-24 11:22Z) Ran `dotnet build .\AiteBar.sln -c Release -p:UseSharedCompilation=false`; build passed with 0 warnings and 0 errors.
- [x] (2026-04-24 11:22Z) Ran `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`; full suite passed 28/28 including the 6 new package tests.
- [x] (2026-04-24 11:23Z) Ran `.\installer\Build-Installer.ps1`; rebuilt `artifacts\installer\AiteBar-Setup.exe`.

## Surprises & Discoveries

- Observation: `AppSettingsService.Elements` is exposed as `IReadOnlyList`, so import could not append items atomically without a small service extension.
  Evidence: `AiteBar/AppSettingsService.cs` keeps `_elements` private and only exposes single-element mutation methods.

- Observation: Empty exported panels do not need a physical `icons/` directory inside the package; zip extraction remains valid without it.
  Evidence: The package contract only requires `manifest.json`, and the import path validates missing icons per element rather than scanning directories up front.

## Decision Log

- Decision: The `.aitebarpanel` format is a normal zip archive with a custom extension.
  Rationale: This keeps the package debuggable and avoids inventing a private binary container.
  Date/Author: 2026-04-24 / Codex

- Decision: Import in v1 always targets the currently active panel and ignores exported panel name and icon metadata.
  Rationale: The approved UX entry points are “Import into current panel” and “Export current panel”, so rewriting the target panel would be surprising.
  Date/Author: 2026-04-24 / Codex

- Decision: File-based icons are always recopied into `PathHelper.IconsFolder`.
  Rationale: Absolute paths from another machine are not portable and the project already has one canonical icon store.
  Date/Author: 2026-04-24 / Codex

- Decision: Import is confirm-gated and does not support undo in v1.
  Rationale: This makes the limitation explicit without adding a half-implemented rollback system.
  Date/Author: 2026-04-24 / Codex

## Outcomes & Retrospective

Implementation is complete in code and documentation. The release verification path passed: build succeeded cleanly, the test suite passed 28/28, and the installer was rebuilt. The remaining gap is manual UI smoke through the two entry points so a human can verify the dialogs and menu wording in the live application.

## Context and Orientation

`AiteBar/AppSettingsService.cs` owns the persisted settings model and the in-memory list of `CustomElement` buttons. `AiteBar/PathHelper.cs` defines the `%AppData%` folder layout, including `IconsFolder`, which is the only valid destination for imported file-based icons. `AiteBar/MainWindow.xaml.cs` builds both the panel-wide context menu on `RootBorder` and the tray context menu; those two methods are the only UI integration points needed for v1. `AiteBar.Tests` already contains small focused unit tests against internal helpers and services, enabled by `InternalsVisibleTo` in `AiteBar/AssemblyInfo.cs`.

The package format uses a transport model that is intentionally separate from `CustomElement`. That transport model lives in dedicated DTO classes so future runtime changes do not accidentally break import/export compatibility. Import always regenerates `Id`, rewrites `ContextId` to the current active panel, and drops transient state such as `LastUsedProfile`.

## Plan of Work

Add three new source files to `AiteBar`: one file for DTOs, one for mapping helpers, and one for the import/export service. Extend `AppSettingsService` with a method that appends a prepared list of imported elements and saves once. Add tests that exercise export and import end to end using temporary package files and temporary icon stores. Then modify `MainWindow.xaml.cs` to instantiate the service, add the two menu items to the RootBorder context menu and the tray menu, and route both entry points through common import/export methods that use `OpenFileDialog`, `SaveFileDialog`, preview, and confirm dialogs. Finish by documenting the feature in `README.md` and `USER_MANUAL.md`.

## Concrete Steps

Work from the repository root `D:\01_Codebdbd\01_projects\aitebar`.

1. Create `AiteBar/PanelPackageManifest.cs`, `AiteBar/PanelPackageMapper.cs`, and `AiteBar/PanelPackageService.cs`.
2. Extend `AiteBar/AppSettingsService.cs` with one append-and-save method for imported elements.
3. Create `AiteBar.Tests/PanelPackageServiceTests.cs`.
4. Update `AiteBar/MainWindow.xaml.cs` to add the menu items and handlers.
5. Update `README.md` and `USER_MANUAL.md`.
6. Run:

   dotnet build .\AiteBar.sln -c Release
   dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
   .\installer\Build-Installer.ps1

## Validation and Acceptance

Acceptance is:

- exporting the current panel writes a `.aitebarpanel` file through either menu entry point;
- importing a valid package into the current panel appends its buttons to the active panel;
- built-in glyph icons survive export/import through metadata alone;
- file-based icons are restored from files copied into `PathHelper.IconsFolder`;
- invalid package manifests are rejected with an error rather than partially modifying settings;
- automated tests pass and the release installer is rebuilt successfully.

## Idempotence and Recovery

Export is naturally repeatable and overwrites only the selected destination file. Import is additive and confirm-gated; repeating the same import adds another copy of the buttons because duplicate names are allowed by design. If an import fails before the final save, settings stay unchanged because elements are prepared before the append-and-save call. Package extraction uses a temporary directory that is deleted after each operation.

## Artifacts and Notes

Expected validation transcript after the final step:

   dotnet build .\AiteBar.sln -c Release
   Build succeeded.

   dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release
   Passed!  28 tests passed

   .\installer\Build-Installer.ps1
   Installer created at artifacts\installer\AiteBar-Setup.exe

Resulting installer artifact:

   artifacts\installer\AiteBar-Setup.exe
   size: 52,407,343 bytes
   timestamp: 2026-04-24 07:07:33

## Interfaces and Dependencies

Define these new internal types in `AiteBar`:

- `PanelPackageManifest`, `PanelPackageAppInfo`, `PanelPackagePanelInfo`, `PanelPackageElement`, `PanelPackageImageInfo`
- `PanelPackageMapper`
- `PanelPackageService`
- `PanelExportResult`, `PanelImportResult`, `PanelImportPreview`

Extend `AppSettingsService` with:

    internal Task AddElementsAsync(IEnumerable<CustomElement> elements)

The `PanelPackageService` surface must contain:

    internal Task<PanelExportResult> ExportCurrentPanelAsync(string packagePath, CancellationToken cancellationToken = default)
    internal Task<PanelImportPreview> ReadImportPreviewAsync(string packagePath, CancellationToken cancellationToken = default)
    internal Task<PanelImportResult> ImportIntoCurrentPanelAsync(string packagePath, CancellationToken cancellationToken = default)

Revision note: created this ExecPlan at implementation start to satisfy the repository requirement that significant features be carried by a living plan. Updated after implementation to record the completed validation commands and rebuilt installer artifact.
