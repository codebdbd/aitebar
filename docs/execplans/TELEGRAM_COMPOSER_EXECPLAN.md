# Build the Telegram composer and publication flow

Maintain this living ExecPlan in accordance with PLANS.md at the repository root.

## Purpose / Big Picture

AiteBar users can write a Telegram post in one focused window, open a formatted preview when needed, and choose recipients and timing on a dedicated publication screen. After an attempt they can see an outcome for each channel. A failed direct delivery can be retried from the result without repeating successful deliveries. Scheduled posts appear in a queue and retain their server acknowledgement after restarting AiteBar.

## Progress

- [x] (2026-11-17) Inspected the editor, Telegram delivery and queue services, related tests, and the approved mockup.
- [x] (2026-11-17) Implemented the composer, draft library, review, results and queue. Added translations and user documentation.
- [x] (2026-11-17) Verified the editor, preview, draft list, recipient selection, scheduling controls and cached queue in an isolated WPF window. Built Release and ran the complete test suite.

## Surprises & Discoveries

The old window already has asynchronous direct delivery and retry services. It also submits scheduled posts to a server, but the window only displays a transient job ID. The existing local publication target table can associate channels, revisions and times with a server receipt.

The server create endpoint returns a job ID and one receipt per channel. The status endpoint takes the channel receipt ID. Queue rows therefore store those per-channel IDs, keeping retries of the same schedule idempotent in the local database.

Native WPF inspection revealed that the system date field, radio labels and expander headers needed explicit dark styling. The default standalone FlowDocument also used a serif face and justified alignment. The composer now applies its own document font and left alignment to the preview.

## Decision Log

- Decision: Use independent views inside one existing WPF window. Rationale: The editor, library, channel selection and queue need their own space. Keeping one window preserves the AiteBar entry point and keeps the current draft in memory during navigation. Date/Author: 2026-11-17 / Codex.
- Decision: Reuse the Telegram document, draft, media, Bot API and pairing services. Rationale: The redesign changes the workflow. Existing draft files and delivery semantics are already supported by these services. Date/Author: 2026-11-17 / Codex.

## Outcomes & Retrospective

The WPF editor and publication screens implement the composer flow. Tests exercised a two-channel direct publication with a partial failure, confirmed that retry used only the failed target, and checked that a server receipt remained available after reopening the publication store. The manual check used a local draft and simulated channels. It did not send a Telegram message or contact a live scheduling server. Jobs created before local per-channel receipts were retained cannot be listed from the local queue unless their receipt is recorded by a later submission.

## Context and Orientation

The launcher creates AiteBar/TelegramPostEditor/TelegramPostEditorWindow.xaml and its code-behind. The WPF rich text editor converts between a FlowDocument, which is WPF's formatted text representation, and the stored post through TelegramDocumentSerializer. Drafts and attachments are managed by PostRepository, PostAutosaveService and PostMediaStore in AiteBar/TelegramPostEditor/Services. PublicationService delivers through Telegram and records a target for each channel. PublicationSyncService pairs and sends jobs to TelegramPostEditor.Server. A target tracks one post revision and one channel. Localized strings reside in the four AiteBar/Resources/Strings*.resx files.

## Plan of Work

First replace the three-column view with a composer, an optional preview, and separate draft and publication pages. Retain autosave, templates, media, formatting and AI controls using the existing handlers. The channel screen should allow explicit selection and offer setup when the list is empty.

Then add a publication result view for direct deliveries and a queue for scheduled deliveries. Capture the revision at submission, allow retry of failed targets with that revision, and leave ambiguous deliveries in an explicit unknown state. Store server acceptance as local target records and query server status from the queue. Show setup and composition errors on the view where they can be resolved.

Finally update translations, describe the flow in user documentation and exercise the normal entry point. Use fake transports or channels for verification to avoid publishing to a user's Telegram channel.

## Concrete Steps

From the repository root, run the build and test commands after the changes.

    rtk proxy dotnet build .\AiteBar.sln -c Release -m:1 -p:UseSharedCompilation=false
    rtk proxy dotnet test .\AiteBar.sln -c Release --no-build --no-restore -m:1

Expect a successful Release build. If VSTest fails to start its host, rerun with the environment's local-loopback approval or use the documented dotnet vstest fallback.

## Validation and Acceptance

Open the editor from its utility entry point or an isolated WPF window. Type a post, add formatting, open and close preview, switch to drafts and reopen the post. Enter publication with no recipient, select a named channel, then switch to scheduling and verify the time zone and pairing controls. With a fake delivery transport verify mixed results, failed-only retry and preservation of unknown deliveries. Verify that server acknowledgements can be read after reopening the queue. Verify keyboard navigation and that resizing the window leaves the current view usable.

## Idempotence and Recovery

The redesigned window reads the same database. Scheduling records may be created only after server acceptance. Retrying the same server submission uses the existing stable request key. Immediate publication retry reuses the recorded targets and avoids creating a second delivery for a success or an unknown outcome.

## Artifacts and Notes

The approved interaction is a centered post editor with a single action to proceed to publication. Channel names and timing are visible at the point of final submission.

The final Release run passed 1840 desktop tests and 3 server tests. Five desktop Quick Note tests reported a skip. The solution build completed with no errors.

## Interfaces and Dependencies

Use WPF and the application's existing DarkWindow, localization resources, SQLite target store and server API. Keep publishing and scheduling commands inside TelegramPostEditorWindow and let PublicationService and PublicationSyncService perform persistence and transport.

Revision note, 2026-11-17. Recorded the implemented views, translations and per-channel receipt discovery. Added focused checks for the WPF publication flow and persisted server targets.

Revision note, 2026-11-17. Recorded the native-window findings and final build and test results.
