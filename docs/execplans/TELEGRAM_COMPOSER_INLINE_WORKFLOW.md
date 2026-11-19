# Keep Telegram writing, AI assistance, and delivery settings together

Maintain this living ExecPlan according to PLANS.md.

## Purpose / Big Picture

Write a Telegram post, ask AI to draft or revise it, choose its recipient and time, and submit from the same editor. The delivered result links to Telegram when Telegram returned a message ID. Accepted scheduled targets can be moved to another time in the queue.

## Progress

- [x] (2026-11-17) Traced editor state, AI prompt handling and the server's scheduled-target endpoint.
- [x] (2026-11-17) Implemented the inline recipient and timing panels, AI prompts and selection replacement, delivery results and time changes.
- [x] (2026-11-17) Put every formatting command directly on the toolbar as a Fluent icon, gave the icons descriptive accessible names, and shortened the main labels.
- [x] (2026-11-17) Exercised the WPF workflow and server contracts with fake Telegram and AI transports. Release built and the complete test suite passed.

## Surprises & Discoveries

The current editor already sends directly and has an AI provider picker, but opens publication options as a separate view. Its AI service accepts a user instruction, while the editor always passes null. The server already exposes an authenticated PATCH endpoint for changing a scheduled target.

The worker uses a separate attempt time for retrying targets. A schedule change for a target in that state must update both times. The server test verifies the update and that a different paired device cannot perform it.

The headless WPF test had no dispatcher synchronization context for AI streaming callbacks. The test now installs the same context present when users click controls in the running editor.

## Decision Log

- Decision: Reuse the existing recipient, pairing and scheduling controls in a pane beside the editor. Rationale: This preserves setup and review behavior while keeping the draft in view. Date/Author: 2026-11-17 / Codex.
- Decision: Capture the text and position of a selected passage before making an AI request. Rationale: Applying a suggestion must never replace a different passage after an intervening edit. Date/Author: 2026-11-17 / Codex.
- Decision: Reschedule individual server targets with the existing authenticated API. Rationale: Each channel has a separate delivery state and the server can reject a change if sending has already started. Date/Author: 2026-11-17 / Codex.
- Decision: Use direct icon controls for all seven existing formatting commands. Rationale: The user could not see or identify formatting because most commands were under an ellipsis. Date/Author: 2026-11-17 / Codex.

## Outcomes & Retrospective

The editor shows post text alongside recipient selection, timing and delivery results. The WPF test exercises generation from a brief, replacement of a selection while keeping the surrounding text, stale suggestion rejection, direct delivery and retry, and scheduling from the editor footer. Service and server tests verified time changes on the existing target, including retry timing and device authorization. Manual inspection covered the editor and recipient pane before the user stopped computer control. The final toolbar was verified by the build and the WPF control test.

## Context and Orientation

AiteBar/TelegramPostEditor/TelegramPostEditorWindow.xaml defines the WPF controls. The Composer and Publication partial classes navigate and maintain the editor and delivery state. The main code-behind uses PostAiService to ask a writing model for a suggestion. A FlowDocument is the formatted WPF editor document. PublicationSyncService sends authenticated requests to TelegramPostEditor.Server. A target is a single delivery to one channel, with its own server ID and status.

## Plan of Work

Place the existing recipient and scheduling controls in an editor pane. Present recipient and time summaries and one delivery action in the editor footer. Retain command labels that indicate whether pressing the action sends now or schedules. Add a prompt and contextual short commands to the AI pane. Capture a selected range when generating, and apply changes only after explicit acceptance and a check that the input is still current. Add result links and a time change action for editable queue targets using existing server status checks.

## Concrete Steps

From the repository root, run these after implementation.

    rtk proxy dotnet build .\AiteBar.sln -c Release -m:1 -p:UseSharedCompilation=false --no-restore
    rtk proxy dotnet test .\AiteBar.sln -c Release --no-build --no-restore -m:1

Expect a successful build and no failed tests. VSTest may need authorization for its local testhost connection.

## Validation and Acceptance

Open a draft, choose a recipient and open time settings. The same text editor must remain visible. Switch between immediate and scheduled delivery and verify the main action and destination summaries. With a fake AI provider, generate from a brief, revise a selection, and verify the surrounding text is preserved after acceptance. Edit the target during a request and verify that a stale suggestion cannot overwrite new content. With fake Telegram and scheduler transports, verify the delivery link and confirm that changing a queued time uses PATCH, preserving its server ID.

## Idempotence and Recovery

Scheduled changes patch the original target. A rejected request remains visible as an error and leaves the cached time unchanged until the server confirms a new one. An AI suggestion is separate from the draft until accepted.

## Artifacts and Notes

The editor footer gives the user the recipients, time and delivery action needed to answer what will be published, where and when.

## Interfaces and Dependencies

Use WPF RichTextBox, the existing draft repository and AI gateway, and the shared authenticated server client. Extend existing tests with fake transports. Keep user-facing text in AiteBar/Resources/Strings*.resx.

Revision note, 2026-11-17. Recorded the implemented workflow, time change behavior for retrying targets and dispatcher setup for the AI test.

Revision note, 2026-11-17. Recorded the completed Release build and test run. The desktop suite passed 1840 tests and skipped five. The server suite passed three.
