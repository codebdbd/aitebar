# AiteBar Release Hardening Change Plan

**Date**: 2026-05-27  
**Scope**: Practical release-quality changes after `RELEASE-2026-UPDATED-ANALYSIS.md` review.

## P0: Before the next public release

1. Correct `docs/RELEASE-2026-UPDATED-ANALYSIS.md`
   - Reduce over-optimistic scores from "done" to evidence-based partial status.
   - Fix the incorrect statement that CodeQL is missing; `.github/workflows/codeql.yml` already exists.
   - Remove `AITEBAR_SENTRY_DSN in GitHub Actions` from blocker status because desktop runtime telemetry is not enabled by CI secrets alone.
   - Separate "Sentry SDK is integrated" from "production monitoring is operational".
   - Add code signing as the main release blocker for a Windows desktop installer.

2. Add code signing for the installer
   - [ ] Select a code signing certificate.
   - [x] Add conditional signing support after Inno Setup builds the installer.
   - [x] Verify the signature in CI when signing secrets are configured.
   - [x] Update the release checklist and README.

3. Prove the release workflow with a staging tag
   - [x] Add a manual dry-run path to the release workflow.
   - [ ] Run the release workflow on a non-production test tag or dry-run path.
   - Verify installer generation, release notes extraction, and release asset upload.
   - Record the result in release documentation.

4. Decide the Sentry production model
   - [x] Keep telemetry as dev/support-only through environment variables.
   - [x] Document that production installer does not enable crash reporting by default.
   - [ ] Revisit production telemetry only with privacy documentation and a user-facing opt-in or opt-out setting.

5. Harden update checking
   - Validate GitHub release and installer URLs before opening or displaying them.
   - Allow only HTTPS URLs under `github.com/codebdbd/aitebar` or `github.com/codebdbd/aitebar/releases`.
   - Replace raw exception messages with user-oriented offline/API failure messages.
   - Add tests for URL validation and failure classification.
   - Do not implement automatic install until code signing is in place.

## P1: Next quality increment

6. Add Dependabot
   - Track NuGet dependencies.
   - Track GitHub Actions dependencies.

7. Replace coverage cosmetics with a coverage policy
   - Publish coverage summary in CI.
   - Define a threshold for testable non-UI logic.
   - Avoid artificial whole-WPF coverage goals that do not reduce real risk.

8. Harden the release workflow
   - [x] Log the Inno Setup version.
   - [x] Validate installer artifact version metadata.
   - [x] Add release asset checksums.
   - Configure artifact retention.

9. Update user-facing documentation
   - Document "Check for updates".
   - State that update checking opens the GitHub release page and does not auto-install.
   - Add privacy/telemetry documentation if Sentry is enabled for production builds.

## P2: Architecture work without a high-risk rewrite

10. Refactor `MainWindow` only through scoped increments
    - Panel layout calculation.
    - Tray and hotkeys.
    - Context switching.
    - Drag-and-drop edge switching.
    - Icon and image loading.

11. Add service interfaces before adding a DI container
    - Update checking.
    - Telemetry.
    - Process/browser launching.
    - Filesystem/package operations.

12. Add a full auto-updater only after signing
    - Start with signed manual installer downloads.
    - Then consider secure download.
    - Only then consider assisted or silent install.

## Execution order

1. Correct the release analysis document.
2. Add Dependabot.
3. Harden update-check URL validation and failure UX.
4. Update tests and user documentation.
5. Run Release build and tests.
6. Leave certificate procurement, optional production telemetry, and staging release proof as explicit external follow-ups.
