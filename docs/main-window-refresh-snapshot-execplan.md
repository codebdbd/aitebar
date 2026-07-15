# Use one coherent settings snapshot during panel refresh

This ExecPlan is a living document. The sections `Progress`, `Surprises & Discoveries`, `Decision Log`, and `Outcomes & Retrospective` must be kept up to date as work proceeds.

This document must be maintained in accordance with `PLANS.md` at the repository root.

## Purpose / Big Picture

Refreshing the AiteBar panel currently clones the entire application settings repeatedly while building buttons, measuring every enabled context, selecting a monitor, applying orientation, and positioning the window. After this change, one coherent settings snapshot and one elements snapshot will flow through the refresh calculation. Users should observe identical panel geometry, context behavior, ordering, and animation on every edge, with less allocation and no chance that different parts of one refresh observe different settings generations.

The same refresh and navigation path must also traverse enabled contexts without constructing temporary lists. Because AiteBar has a fixed maximum of eight contexts, direct scans provide predictable behavior with no cache invalidation risk.

## Progress

- [x] (2026-07-15 02:24Z) Mapped every `MainWindow.AppSettings` access and the refresh/layout call graph.
- [x] (2026-07-15 02:29Z) Added snapshot-aware overloads to unified-button construction and refresh helpers.
- [x] (2026-07-15 02:29Z) Changed `RefreshPanel` to acquire settings/elements once after normalization and pass them through all refresh work.
- [x] (2026-07-15 02:29Z) Added focused regression coverage for caller-provided snapshots and existing four-edge layout behavior.
- [x] (2026-07-15 02:29Z) Ran 42 focused tests, Release build, and the 630-test full suite successfully.
- [x] (2026-07-15 02:33Z) Replaced allocating enabled-context list construction with direct count, index, lookup, and traversal operations.
- [x] (2026-07-15 02:33Z) Added a zero-allocation regression test; 38 focused tests, Release build, and the 631-test full suite passed.

## Surprises & Discoveries

- Observation: context-size stabilization is the largest repeated-snapshot source.
  Evidence: `ComputeStablePrimaryPanelMetrics` loops over up to eight contexts and calls `UnifiedButtonService.BuildUnifiedList`; each call currently clones settings and elements again.

- Observation: the refresh path normalizes settings after computing the element-version hash.
  Evidence: `RefreshPanel` enumerates `Elements` before `NormalizeAppState`, then later builders fetch new snapshots. Acquiring both snapshots after normalization produces a more coherent refresh without changing persisted state.

- Observation: `GetEnabledContexts` allocated a `List<PanelContext>` on every call, and two callers immediately copied that list again with `ToList()`.
  Evidence: after replacement, no `GetEnabledContexts` call remains in application source, and the allocation regression observes no additional bytes across 1,000 repetitions of all context-navigation queries.

## Decision Log

- Decision: pass explicit `AppSettings` and `IReadOnlyList<CustomElement>` snapshots through the refresh path instead of caching them in `MainWindow` fields.
  Rationale: an explicit operation-scoped snapshot cannot become stale between refreshes and does not expose mutable cached state to unrelated event handlers.
  Date/Author: 2026-07-15 / Codex

- Decision: keep wrapper overloads for non-refresh callers.
  Rationale: drag, startup, animation, and other isolated operations can continue to request a fresh snapshot, while the hot refresh path avoids repeated cloning without a broad behavioral refactor.
  Date/Author: 2026-07-15 / Codex

- Decision: preserve all centralized `PanelLayoutHelper` calculations and only pass their inputs explicitly.
  Rationale: layout formulas are already tested across Top, Bottom, Left, and Right. Rewriting formulas would add visual risk unrelated to snapshot allocation.
  Date/Author: 2026-07-15 / Codex

- Decision: scan the fixed eight-context snapshot directly instead of caching or exposing a filtered collection.
  Rationale: direct scans allocate nothing, remain simple at this small fixed size, and cannot become stale when context enablement changes.
  Date/Author: 2026-07-15 / Codex

## Outcomes & Retrospective

`RefreshPanel` now reads one `AppSettings` snapshot and one elements snapshot after normalization. Active-context button creation, stable sizing across enabled contexts, target-monitor selection, orientation, tooltips, transition direction, and positioning all consume those same operation-scoped values. `UnifiedButtonService` retains its existing wrapper for ordinary callers and exposes an internal snapshot-aware overload for refresh calculations.

The focused regression set passed 42/42, including the new test that deliberately gives the backing settings service different visibility state from the supplied snapshot. The complete Release suite passed 630/630, and `dotnet build .\AiteBar.sln -c Release` completed with zero warnings and zero errors. No layout formula or geometry constant changed, so existing four-edge layout tests remained the relevant visual-behavior guard.

The follow-up enabled-context milestone removed the allocating helper entirely. Context normalization, cyclic next/previous navigation, direct index selection, stable panel measurement, and the indicator now operate on the original snapshot. A dedicated test confirmed zero managed allocations across 1,000 warmed repetitions. Final validation after this milestone passed 38/38 focused tests, 631/631 complete tests, and a clean Release build.

## Context and Orientation

`MainWindow.AppSettings` delegates to `AppSettingsService.Settings`, whose getter returns a deep clone including contexts, elements, hotkeys, utility ordering, and nested Sentry settings. `MainWindow.Elements` separately returns a list snapshot. `RefreshPanel` calls helpers that repeatedly read those properties. `UnifiedButtonService.BuildUnifiedList` also obtains its own settings and elements snapshots for every context measured to keep panel size stable when switching between short and long contexts.

The refactor will add a snapshot-aware `UnifiedButtonService.BuildUnifiedList` overload and explicit inputs to private `MainWindow` helpers for monitor selection, layout metrics, context menus, orientation, tooltips, transition direction, and positioning. Existing wrapper methods retain fresh-snapshot behavior for callers outside a refresh operation.

## Plan of Work

Add `BuildUnifiedList(string activeContextId, AppSettings settings, IReadOnlyList<CustomElement> elements)` to `UnifiedButtonService`. Keep the existing one-argument method as a wrapper. The overload must use only the supplied objects.

In `MainWindow.RefreshPanel`, call `NormalizeAppState` first, then read exactly one settings snapshot and one elements snapshot. Use those snapshots for the element hash, context menu active state, active-context list, orientation, target monitor, panel percentage, stable-context sizing, context indicator, tooltip placement, transition direction, and final positioning.

Introduce private overloads or explicit parameters so non-refresh callers retain their current behavior. Do not add a long-lived settings field. Do not alter layout formulas, margins, animation durations, or panel-side behavior.

Add a behavioral test proving the snapshot-aware unified-button overload honors supplied settings and elements even when the backing service contains different state. Run existing `MainWindowIconConverterOrientationTests`, `PanelLayoutHelperTests`, and context tests as focused regression coverage.

## Concrete Steps

Work from `D:\01_Codebdbd\01_projects\aitebar`.

Run focused tests:

    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --filter "FullyQualifiedName~UtilityButtonCatalogTests|FullyQualifiedName~MainWindowIconConverterOrientationTests|FullyQualifiedName~PanelLayoutHelperTests|FullyQualifiedName~ContextStateHelperTests"

Then run:

    dotnet build .\AiteBar.sln -c Release
    dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release

## Validation and Acceptance

The new snapshot-overload test must configure backing-service settings differently from the supplied snapshot and prove the result follows only the supplied snapshot and elements. Existing four-edge icon-converter orientation tests must pass unchanged. The full suite must pass, and Release build must report zero warnings and errors.

No UI geometry constant or formula changes in this step. Therefore automated four-edge tests are the primary acceptance evidence; a manual panel check is only required if a visual formula must unexpectedly change.

## Idempotence and Recovery

The change is source-only and safe to repeat. Preserve all prior reliability and utility-catalog changes. If an overload produces a compile ambiguity, keep the one-argument public behavior and make the explicit overload internal with all three arguments.

## Artifacts and Notes

The preceding utility-catalog step is documented in `docs/typed-utility-catalog-execplan.md` and passed 629 tests before this work began.

## Interfaces and Dependencies

No package is added. `UnifiedButtonService` retains:

    public List<UnifiedButton> BuildUnifiedList(string activeContextId)

and adds:

    internal List<UnifiedButton> BuildUnifiedList(
        string activeContextId,
        AppSettings settings,
        IReadOnlyList<CustomElement> elements)

Private `MainWindow` helper signatures may gain `AppSettings`, `IReadOnlyList<CustomElement>`, `DockEdge`, monitor index, panel percentage, or `isVertical` parameters as appropriate. Public `RefreshPanel()` and external window APIs remain unchanged.

Plan revision note (2026-07-15 02:24Z): created the initial self-contained plan from the refresh/layout call graph and recorded the operation-scoped snapshot design.

Plan revision note (2026-07-15 02:29Z): recorded completed implementation, the snapshot-isolation regression test, and final Release validation.

Plan revision note (2026-07-15 02:33Z): extended the completed plan with the closely related allocation-free enabled-context milestone and recorded its measurement and validation evidence.
