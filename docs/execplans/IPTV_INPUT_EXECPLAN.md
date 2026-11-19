# Put playback controls on the IPTV picture

This living plan follows `PLANS.md`. Keep Progress, Surprises & Discoveries, Decision Log, and Outcomes & Retrospective current.

## Purpose / Big Picture

Viewers can click the picture or press Space to pause, double-click for fullscreen, and use the same keys after the picture gains focus. Moving the mouse reveals buttons over the video without resizing it. In picture-in-picture, a short click pauses and a drag moves the window.

## Progress

- [x] (2026-11-17) Traced the player layout, input handlers, and the video library's input boundary.
- [x] (2026-11-17) Added video-surface input, transient controls, translated descriptions, and a test for the double-click decision.
- [x] (2026-11-17) Verified Release build and tests. 1841 passed, 5 skipped, 0 failed.

## Surprises & Discoveries

LibVLCSharp.WPF places the content inside its VideoView in a separate window above the native video. Input received in that window needs its own binding to the player commands. The previous fullscreen timer hid a separate row, which resized the viewing area.

## Decision Log

- Decision: Use the existing VideoView content window for input and playback controls.
  Rationale: The content window already receives input over the video in picture-in-picture. Reusing it keeps the existing playback engine and layout.
  Date/Author: 2026-11-17, Codex.
- Decision: Wait for the Windows double-click duration before executing a single click in the main player. Recognize picture-in-picture dragging after the Windows drag-distance threshold.
  Rationale: Fullscreen changes and dragging must avoid unintended pause commands.
  Date/Author: 2026-11-17, Codex.

## Outcomes & Retrospective

The main player and fullscreen now share controls drawn over the picture. The picture accepts keyboard focus directly. Picture-in-picture distinguishes short clicks from drags. The automated checks exercised the window and the double-click decision without opening the desktop player. Manual interaction during a live stream remains unverified.

## Context and Orientation

`AiteBar/IptvPlayer/IptvPlayerWindow.xaml` defines the video and controls. `IptvPlayerWindow.xaml.cs` handles keyboard input and connects the UI to `IptvPlaybackService.cs`. `IptvPlayerWindow.Modes.cs` owns fullscreen and picture-in-picture transitions. The video library receives frames from the same playback service in every mode.

## Plan of Work

Place a focusable WPF element inside the VideoView. Route its keyboard input to the existing command handler. Add mouse handlers in the window partials to distinguish a short click, a double-click and a drag. Keep the video controls near the bottom of the picture and hide them after 2.5 seconds of inactivity while playing. Pause availability comes from the existing playback service.

## Concrete Steps

From the repository root, run `rtk proxy dotnet build .\AiteBar.sln -c Release -m:1 --no-restore --tl:off` and `rtk proxy dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release -m:1 --no-build --no-restore`. Check the test totals and any tracked failures.

## Validation and Acceptance

Exercise a pausable stream. Expect one pause for a short click and a fullscreen transition without a pause for a double-click. Move the mouse to reveal the controls and wait to see the same picture size after they hide. A paused stream keeps controls visible. Check that Space in search types a space and Space after a click on the picture runs the playback command. In picture-in-picture, drag from the picture and verify the window moves with the stream still playing.

## Idempotence and Recovery

Repeat validation with the existing playlist or a local test stream. Changes only affect transient interaction state and require no settings migration.

## Artifacts and Notes

Release built successfully with the existing SQLitePCLRaw NU1903 warning. The AiteBar test project reported 1841 passed, 5 skipped, and 0 failed.

## Interfaces and Dependencies

Use WPF keyboard and mouse events on VideoView content. Read the Windows double-click duration and drag thresholds. Send pause, resume, mute and view mode commands through the same code paths used by the window buttons. `AiteBar/IptvPlayer/IptvPlayerWindow.Input.cs` contains the timers, input handlers and button presentation. The playback service stays responsible for pausing and resuming the same stream.

2026-11-17 revision. Recorded the implemented input path, the test for cancellation of the first click, and the Release results.
