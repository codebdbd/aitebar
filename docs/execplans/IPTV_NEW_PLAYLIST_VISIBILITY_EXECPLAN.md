# Show a newly saved playlist in the IPTV player

This living plan follows `PLANS.md` and records implementation and validation.

## Purpose / Big Picture

After adding an enabled playlist and saving settings, a user with IPTV open can see the new playlist immediately. While the app reads the source, the player identifies the playlist and reports that its channels have not been loaded yet. When loading finishes, the channel list appears or an error directs the user to the source settings.

## Progress

- [x] (2026-11-16) Traced source editing, settings commit, cache creation, and catalog filtering.
- [x] (2026-11-16) Unified initial catalog load and refresh, made enabled sources selectable before their channels load, and added empty states.
- [x] (2026-11-16) Built Release and passed 1815 AiteBar tests with five skipped.
- [x] (2026-11-16) Verified settings Save and background import into the open player in a disposable Windows profile.

## Surprises & Discoveries

The add dialog writes to the enclosing settings draft. Only the main Save button commits it and queues a background load. The player's previous selector was derived from cached channels, so it hid a source until that source had at least one channel. The previous initial catalog load could also apply filters using a saved selection that the controls had already reset.

## Decision Log

Use enabled saved sources for the playlist selector and use the catalog service for eligible channels. Select a newly enabled source in an already open player and clear search and group filters for that selection. Preserve filters on later refreshes. Keep the settings draft and expose its pending Save requirement in the row. Date/author 2026-11-16, Codex.

## Outcomes & Retrospective

The Release build succeeded. The AiteBar suite passed 1815 tests and skipped five. The Debug compilation completed but the existing app executable was locked during the copy step.

The settings window saved the inline source and the production background load wrote a matching cache. A separate Windows player check used the saved-source model and called the production background load. The open player selected the pending source, cleared the earlier search and group filters, displayed the channel when loading finished, and kept a failed source selectable with an error. The check passed its UI assertions before the temporary test process timed out during shutdown.

## Context and Orientation

`AiteBar/AppSettingsWindow.Iptv.cs` tracks changes to playlist sources until `AiteBar/AppSettingsWindow.xaml.cs` saves all settings. `AiteBar/MainWindow.xaml.cs` queues the source load and reports the resulting status by updating settings. `AiteBar/IptvPlayer/IptvPlaylistService.cs` parses and stores the list of channels. `AiteBar/IptvPlayer/IptvCatalogService.cs` reads that saved list if it matches the source revision. `AiteBar/IptvPlayer/IptvPlayerWindow.xaml.cs` subscribes to settings changes and reloads the catalog.

## Plan of Work

Share enabled source ordering in the catalog service and the player's selector. Route initial and subsequent catalog loads through one method so restored controls and the shown list use the same filters. Compare the previous and current settings snapshots to identify a newly enabled playlist. Show pending settings changes in the source row and show loading, failure, and filtered empty states in the player. Add a test for a new source whose cache, the saved copy of the channel list, still belongs to a previous revision.

## Concrete Steps

Run these commands from the repository root.

    rtk proxy dotnet build .\AiteBar.sln -c Release -m:1 --no-restore
    rtk proxy dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release -m:1 --no-build --no-restore

The build should succeed. The new test `Catalog_KeepsNewPlaylistSelectableBeforeItsSavedRevisionLoads` should pass.

## Validation and Acceptance

Open IPTV in a disposable profile, select an existing playlist and enter a search. Add a new inline M3U playlist and verify the settings row asks for Save. Save settings and verify the running player selects the new playlist, clears the search, and replaces its empty state with the loaded channel. Add an unreadable source and verify that its failure remains visible and that a working playlist can still be chosen.

## Idempotence and Recovery

Use a disposable profile to repeat the workflow. Saving the same unchanged playlist keeps its identifier and selected filters. Failed or pending sources stay selectable while streams are supplied only from a matching cache.

## Artifacts and Notes

Record the test counts and the observable player behavior after verification.

## Interfaces and Dependencies

Use existing WPF settings notifications, the catalog service, and the playlist parser and store. No new dependencies are needed.

Revision note, 2026-11-16. Recorded Release results and the separate Windows checks for settings Save and the open player.
