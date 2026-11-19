# Redesign the IPTV channel browser

This living plan follows `PLANS.md`.

## Purpose / Big Picture

The channel browser should let a viewer find a station, save it to a visible Favorites view, and recognize the current station while browsing. The playing video should report the format provided by the decoder.

## Progress

- [x] (2026-11-16) Inspected the current list, cached logos, favorite storage, and playback engine.
- [x] (2026-11-16) Implemented the browser, independent favorite and filter state, playback marker, and decoder details.
- [x] (2026-11-16) Built Release and exercised the final panel on Windows with the local HTTP fixture.
- [x] (2026-11-16) Passed 1818 tests in the final Release with five skipped.
- [x] (2026-11-16) Reduced the catalog header to a search field with two actions and removed placeholder tiles.
- [x] (2026-11-16) Verified the single-row layout at 230 DIP, the favorite toggle, and the filter disclosure. Release built and 1818 AiteBar tests passed with five skipped.
- [x] (2026-11-16) Checked the divider, the shared 26 DIP logo slot, and the television fallback trigger in the compiled layout and source markup.
- [x] (2026-11-16) Built the Release with the television icon and passed 1818 AiteBar tests, with five skipped.
- [ ] Verify the updated rows visually in a narrow window. The test-window launch was declined.
- [x] (2026-11-16) Built Release, checked the filter layout in the markup, and passed 1836 AiteBar tests with five skipped.
- [x] (2026-11-16) Verified separate category choices and filtering against a saved playlist cache. Release built and 1837 AiteBar tests passed, with five skipped.
- [x] (2026-11-17) Built Release, checked the independent button row in XAML, and passed all ten resolution cases on the final build.

## Surprises & Discoveries

M3U channel logos already reach the channel model and an image cache. The row template omits the logo. The old Favorites view is represented by a special group identifier in saved player state.

Making the lazy logo property visible makes its network requests part of normal browsing. Logo requests now share the playlist downloader's destination and redirect checks, stop at a fixed response size, and stay under the existing concurrency limit.

The first Windows capture showed the Filters caption inheriting a dark brush from the default toggle style. The caption now uses the same foreground as the neighboring labels. The disclosure closes from its toggle or Escape, so it no longer needs a large secondary close button.

The screenshot from a playlist with few logos showed that fallback tiles dominated the list and the two control rows took space above it. Remove the tiles entirely. Keep a logo only when an image is available.

The next screenshots showed consecutive titles and source labels merging visually, and channels with logos had a different starting point for their titles. Draw a thin divider as part of each virtualized row and use separate padding for the content and highlight. At the user's request, reserve the same logo column for every channel and show a television outline when its image is unavailable.

The filter screenshot showed its actions wrapping into a second row below closely spaced fields. Give each field its own label and spacing. Put the shorter Sources label and Reset in a two-column footer under a divider, and place the filter panel before the list in keyboard traversal order.

A later screenshot exposed lists that use semicolons to assign multiple categories to the same channel. Keep the playlist and cache fields intact. Split the stored value at the catalog layer and use the individual categories to populate and apply the group filter. A channel with grouped stream variants uses the categories from all of its entries.

The follow-up screenshot still showed combined category names. The working tree and Release assembly both contain the category splitting code. The prebuilt Debug and workspace publish assemblies were older and did not contain it. The test launcher uses Debug by default, including when invoked with `--quick`.

On 2026-11-17, the running AiteBar process was confirmed to use the Debug output. A Debug rebuild reached the output copy step and failed with MSB3021 because the running application locked its executable. The already built Release executable is available to launch after exiting the Debug process from the tray.

## Decision Log

Reuse the existing store and virtualized list. Separate the Favorites view from the group filter, migrate the previous setting on open, and use the existing playback command queue to read the active video's format. Date and author 2026-11-16, Codex.

Use one compact toolbar with search and two toggle buttons. The star opens Favorites and changes the search hint. The sliders open full-width filter controls and the Sources command. Active filters can still be removed above the list. Date and author 2026-11-16, Codex.

## Outcomes & Retrospective

The initial Release test run passed 1818 tests with five skipped. A Windows fixture exercised favorite toggling, the full-width filters in the minimum window, the on-air marker, and the absence of tuning on selection changes. The local clip reported `640×360 · 8 fps` from the active decoder. The final capture showed a light Filters caption and both full-width selectors in the narrow window.

The final Release build succeeded. A concurrent test run hit a Quick Note timeout, and a subsequent run without the separate player fixture passed 1818 tests with five skipped.

The compact follow-up also passed 1818 Release tests with five skipped. A Windows fixture at 230 DIP confirmed search and both actions share a row, Favorites toggles the catalog, the full-width filter opens, and only rows with actual images occupy logo space.

The latest row layout builds successfully and passed 1818 AiteBar tests with five skipped. The logo and television outline share a 26 DIP visual slot inside each row, and the divider occupies a separate 1 DIP row in the item template. Visual verification of this follow-up remains pending.

The filter follow-up builds successfully and passed 1836 AiteBar tests with five skipped. The markup check confirmed distinct playlist and group sections and one shared action row. The filter layout was checked without opening the player.

The resolution buttons have a separate row beneath search. The AiteBar suite passed 1834 tests with five skipped during implementation. After moving title classification into the catalog build, Release built again and the ten targeted resolution tests passed. The tests cover numeric and named labels, unknown titles, and intersecting the filter with a selected group while preserving grouped variants.

## Context and Orientation

`AiteBar/IptvPlayer/IptvPlayerWindow.xaml` defines the browser and transport. The code behind restores the player's view and filters. `IptvCatalogService.cs` reads playlist channels and favorites. `IptvPlaybackService.cs` owns the running LibVLC player.

## Plan of Work

Put search and a small source menu on the top row. Add All and Favorites choices, show filters in a full-width disclosure, and display removable active filters. Bind each channel's logo and show distinguishing metadata for duplicate names. Mark the active stream independently from keyboard selection. Read decoder resolution and optional frame rate and show format information only when available.

## Concrete Steps

From the repository root run the following commands.

    rtk proxy dotnet build .\AiteBar.sln -c Release -m:1 --no-restore
    rtk proxy dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build --no-restore

## Validation and Acceptance

Open a disposable player profile with multiple channels and an actual local video stream. Check normal and narrow layout, filter dismissal, favorite access, current channel, and format display. Confirm that browsing with the arrow keys does not tune a stream and that Enter does. Unit tests cover filtering and choosing the secondary row text.

## Idempotence and Recovery

Use a disposable profile for verification. Reopening the window should restore filters and favorites from existing files, including older favorite-group state.

## Artifacts and Notes

Record visible Windows behavior and test results after verification.

## Interfaces and Dependencies

Use existing WPF, LibVLCSharp, and IPTV services. No new dependency is required.

Revision note, 2026-11-16. Recorded the responsive layout and local playback checks.

Revision note, 2026-11-16. Revised the header and logo fallback based on the screenshot of a playlist with few logos.

Revision note, 2026-11-16. Added row separation, consistent content spacing, and a television icon in the shared logo slot.

Revision note, 2026-11-16. Separated the playlist and group fields, moved Sources and Reset to a single footer, and made Escape close an open dropdown before closing the filter panel.

Revision note, 2026-11-16. Updated the group filter to show and match individual categories from a composite M3U group value.

Revision note, 2026-11-17. Verified which build the running process used and documented the file lock and Release launch path.

Revision note, 2026-11-17. Added the resolution button row. The filter uses labels already present in cached channel and variant titles. Each button denotes a resolution band, all titles with unknown resolution remain available under All, and a matching variant includes the channel without affecting playback.
