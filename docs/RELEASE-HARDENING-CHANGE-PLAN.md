# Release Hardening Change Plan

## Before Release

1. Run the manual keyboard matrix documented in [release-audit.md](release-audit.md).
2. Confirm the Release solution build, tests, and installer build from the final checkout.
3. Verify installer version, checksum, and signing status in the release workflow.

## After Release

1. Add automated coverage for WPF keyboard focus, `WM_HOTKEY` dispatch, and owned-window filtering.
2. Extract keyboard navigation and hotkey orchestration decisions from `MainWindow`.
3. Raise the CI coverage threshold as non-UI behavior becomes testable.
4. Make installer signing mandatory when certificate infrastructure is available.
