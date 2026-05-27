# AiteBar Release Hardening Change Plan

**Date**: 2026-05-27  
**Scope**: Practical release-quality changes after the 2026 release-readiness review.

## P0: Before the next public release

1. ~~Correct the 2026 release analysis~~ ✅ COMPLETED
   - ~~Reduce over-optimistic scores from "done" to evidence-based partial status.~~ ✅ Done
   - ~~Fix the incorrect statement that CodeQL is missing; `.github/workflows/codeql.yml` already exists.~~ ✅ Done
   - ~~Remove `AITEBAR_SENTRY_DSN in GitHub Actions` from blocker status because desktop runtime telemetry is not enabled by CI secrets alone.~~ ✅ Done
   - ~~Separate "Sentry SDK is integrated" from "production monitoring is operational".~~ ✅ Done
   - ~~Add code signing as the main release blocker for a Windows desktop installer.~~ ✅ Done

2. Add code signing for the installer
   - [ ] Select and purchase a code signing certificate. DEFERRED until budget is available.
   - [x] Add conditional signing support after Inno Setup builds the installer.
   - [x] Verify the signature in CI when signing secrets are configured.
   - [x] Update the release checklist and README.

3. Prove the release workflow with a staging tag
   - [x] Add a manual dry-run path to the release workflow.
   - [x] Run the release workflow on a non-production test tag or dry-run path.
   - [x] Verify installer generation, release notes extraction, and dry-run artifact upload.
   - [x] Record the result in release documentation.
   - Result: `Release #1` workflow_dispatch on `master` completed successfully on 2026-05-27 for commit `55b7c6c`; artifact `release-artifacts-1` was uploaded (52,059,580 bytes, expires 2026-06-10). Run: https://github.com/codebdbd/aitebar/actions/runs/26534464645

4. Decide the Sentry production model
   - [x] Keep telemetry as dev/support-only through environment variables.
   - [x] Document that production installer does not enable crash reporting by default.
   - [ ] Revisit production telemetry only with privacy documentation and a user-facing opt-in or opt-out setting. DEFERRED unless production telemetry becomes a product requirement.

5. Harden update checking
   - [x] Validate GitHub release and installer URLs before opening or displaying them.
   - [x] Allow only HTTPS URLs under `github.com/codebdbd/aitebar` or `github.com/codebdbd/aitebar/releases`.
   - [x] Replace raw exception messages with user-oriented offline/API failure messages.
   - [x] Add tests for URL validation and failure classification.
   - [x] Do not implement automatic install until code signing is in place.

## P1: Next quality increment

6. Add Dependabot
   - [x] Track NuGet dependencies.
   - [x] Track GitHub Actions dependencies.

7. Replace coverage cosmetics with a coverage policy
   - [x] Publish coverage summary in CI.
   - [x] Define a baseline line coverage threshold in CI.
   - [x] Avoid artificial whole-WPF coverage goals that do not reduce real risk.

8. Harden the release workflow
   - [x] Log the Inno Setup version.
   - [x] Validate installer artifact version metadata.
   - [x] Add release asset checksums.
   - [x] Configure artifact retention.

9. Update user-facing documentation
   - [x] Document "Check for updates".
   - [x] State that update checking opens the GitHub release page and does not auto-install.
   - [x] Add privacy/telemetry documentation for the current dev/support-only Sentry model.

## P2: Architecture work without a high-risk rewrite

10. Refactor `MainWindow` only through scoped increments
    - [ ] Panel layout calculation.
    - [ ] Tray and hotkeys.
    - [ ] Context switching.
    - [ ] Drag-and-drop edge switching.
    - [ ] Icon and image loading.

11. Add service interfaces before adding a DI container
    - [ ] Update checking.
    - [ ] Telemetry.
    - [ ] Process/browser launching.
    - [ ] Filesystem/package operations.

12. Add a full auto-updater only after signing
    - [ ] Start with signed manual installer downloads.
    - [ ] Then consider secure download.
    - [ ] Only then consider assisted or silent install.

## Execution order

1. Correct the release analysis document.
2. Add Dependabot.
3. Harden update-check URL validation and failure UX.
4. Update tests and user documentation.
5. Run Release build and tests.
6. Add CI coverage summary and baseline threshold.
7. Leave certificate procurement, optional production telemetry, and P2 architecture work as explicit follow-ups. Code signing certificate purchase is intentionally deferred until budget is available.
