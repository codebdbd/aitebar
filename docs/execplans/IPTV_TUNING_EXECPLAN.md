# Bound IPTV channel switching time

This living plan follows `PLANS.md`. Keep Progress, Surprises & Discoveries, Decision Log, and Outcomes & Retrospective current.

## Purpose / Big Picture

Pressing PgUp or PgDn repeatedly moves the highlight immediately and opens the final channel after a brief pause. A click opens a channel immediately. A broken stream advances to another source for the same channel. The player shows an error when the tune budget expires.

## Progress

- [x] (2026-11-17) Traced keyboard navigation, native events and retry scheduling.
- [x] (2026-11-17) Implemented shared navigation, bounded startup and order-preserving channel filtering.
- [x] (2026-11-17) Measured six native startup cases with a local server and an in-memory video renderer.
- [x] (2026-11-17) Confirmed audio-only startup and replacing an in-flight request to a silent server.
- [x] (2026-11-17) Rebuilt Release and ran the suite after updating the navigation test. 1839 passed, 5 skipped, 0 failed.

## Surprises & Discoveries

- The existing watchdog stops checking once VLC emits `Playing`. The WPF window can still show that it is waiting for a frame. A single unresponsive stream could consume four 12-second attempts plus 9.5 seconds of retry delays before the next source.
- The build catalog already assigns ascending sort order. The browser can filter those entries in place in that order and preserve its list binding when the results are identical.
- The existing WPF quality-selection test asserted that one navigation call immediately queued playback with the engine. The revised test verifies the immediate selection updates and commits only the last destination when the timer fires.

## Decision Log

- Decision: Reuse the existing 180 ms channel navigation timer across window modes and maintain a 12-second budget for each tune, including fallback.
  Rationale: Preserve the feedback from list selection and use the current single VLC player and serialized command queue.
  Date/Author: 2026-11-17, Codex.
- Decision: Allow four seconds for an attempt with another address available and up to six seconds before the one timeout retry for a single address. Advance immediately on an explicit startup error.
  Rationale: Reserve time for available fallback within the shared budget and allow one recovery attempt for a silent single address.
  Date/Author: 2026-11-17, Codex.

## Outcomes & Retrospective

Keyboard and picture-in-picture navigation use the same timer. Native playback tests confirmed a working stream, delayed startup, successful fallback, an error after 12 seconds for a hanging server, audio-only playback, and rapid replacement of a pending request. The native tests ran without displaying a window. The Release build succeeded with the existing NU1903 warning for SQLitePCLRaw. The desktop test suite passed with five Quick Note integration tests skipped.

## Context and Orientation

`AiteBar/IptvPlayer/IptvPlayerWindow.xaml.cs` handles input and status. Its `.Modes.cs` partial handles picture-in-picture. `AiteBar/IptvPlayer/IptvPlaybackService.cs` serializes native player commands and uses a version counter to invalidate obsolete operations. A variant is an alternate URL of the same channel.

## Plan of Work

Share the navigation timer between window modes. In the playback service, check VLC statistics for the first displayed picture or for played audio on an audio-only stream. Route startup failures through one policy for deadlines and alternate variants. Keep every native query on the existing command queue and guard its result with the current version. Preserve catalog order in `IptvCatalogService.FilterChannels` and skip rebinding when filter results contain the same channel instances in the same order.

## Concrete Steps

At the repository root run `rtk proxy dotnet build .\AiteBar.sln -c Release -m:1`, then `rtk proxy dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release --no-build`. Exercise a local clip, a delayed stream, and a broken stream with the native playback service.

## Validation and Acceptance

Policy tests cover available fallback, an exhausted budget, and a limited retry. A working stream reports ready with real output. A broken first URL reaches a working alternate. A silent URL reports failure within the shared budget. Navigation uses the same timer for normal, fullscreen and picture-in-picture modes.

## Idempotence and Recovery

Build and tests can be repeated. Local fixtures use loopback and temporary data. Stopping playback or choosing another channel invalidates pending work for the previous attempt.

## Artifacts and Notes

The local HTTP server was tested with the application playback service and video callbacks writing into memory. The last run observed 659 ms for a working MP4, 4253 ms with a 1200 ms delay on each HTTP request, 541 ms for a 404 followed by a working alternate, 4507 ms for a silent server followed by a working alternate, 23 ms for a lone 404, 12001 ms for a lone silent server, 555 ms for a new channel selected while an older request was stalled, and 449 ms for an audio-only WAV.

## Interfaces and Dependencies

Use LibVLCSharp `Media.Statistics`, `DisplayedPictures` and `PlayedAudioBuffers`. Keep the `IptvPlaybackService` public playback API and use the version-aware `IptvPlaybackCommandQueue` for native access.

2026-11-17 revision. Recorded the startup limits, catalog behavior and eight native measurements. Updated the navigation test for the delayed commit and recorded the final Release results.
