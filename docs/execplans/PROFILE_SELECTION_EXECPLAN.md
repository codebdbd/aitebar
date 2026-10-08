# ExecPlan: Unified Browser Profile Selection Dialog and Settings Integration

## Purpose and Intent

This ExecPlan defines the transformation of browser profile configuration in AiteBar. Previously, selecting a browser profile and configuring profile rotation were split across separate, fragmented controls in SettingsWindow (a single-profile ComboBox, a disconnected rotation CheckBox, and a tiny icon button opening an unstyled rotation dialog where checkboxes were virtually invisible against a dark background). This plan replaces that scattered UX with a unified "Profile" entry point matching the AiteCommander paradigm: an interactive profile selector field placed right after the URL field in SettingsWindow, opening a comprehensive Profile Selection Dialog supporting both Single Profile mode and Profile Rotation mode, with high-contrast, fully visible checkboxes and row highlights.

## User-Visible Changes

1. In `SettingsWindow`, when configuring a Web action:
   - Below the URL field, a unified "Profile" input field appears with a label, read-only preview text of the current selection, and a browse/select button.
   - Clicking either the profile text box or the browse button opens the unified Browser Profile Selection dialog.
   - The fragmented single-profile ComboBox, the standalone "Profile rotation" CheckBox, and the tiny gear icon button are removed from `PanelWebSettings`.
   - The remaining browser options (App mode, Incognito, Fullscreen) become neatly arranged without clutter.
   - The preview text inside the Profile field dynamically reflects the active choice:
     - When no profile is selected: "Default (no profile)" / "По умолчанию (без профиля)".
     - When a single profile is selected: the profile display name (e.g. "Work (osteen@gmail.com)").
     - When rotation is active: "Rotation: all profiles (N)" or "Rotation: M of N".
2. In `RotationProfileSelectionWindow` (the Profile Selection Dialog):
   - At the top of the dialog, a segmented mode selector allows switching between "Single profile" and "Profile rotation".
   - In "Single profile" mode:
     - The user can select one specific browser profile or choose "[Default (no profile)]".
     - Clicking an item selects it immediately.
     - Search filters the list in real-time.
   - In "Profile rotation" mode:
     - Profiles are displayed with distinct, high-contrast checkboxes featuring visible borders, recessed box background, blue checked fill with a crisp white checkmark, and row hover highlighting.
     - "Select all" and "Clear selection" quick-action buttons operate smoothly.
     - A live badge displays "M of N" selected profiles.
     - Search filters the list in real-time while preserving selection state.
   - Container background is changed from low-contrast #383838 to standard dark panel #252526 matching AiteBar design guidelines.

## Non-Goals

- Changing the underlying launch mechanism in `ActionService`: `AdvanceRotationProfile` and `ChromeProfile` launching behavior remain completely intact.
- Moving the Browser selection dropdown (`CmbBrowser`) into the dialog: per user preference, the browser selector remains on the main form, and the profile dialog loads profiles matching the currently selected browser.
- Altering persistent JSON schema: `BarElementConfig` already holds `ChromeProfile`, `UseRotation`, and `RotationProfilePaths`.

## Key Files and Components

- `AiteBar/FormControlsResources.xaml`: CheckBox template and styles (`BaseCheckBoxStyle`, `SelectionListCheckBoxStyle`) updated with crisp border brush and distinct background so checkboxes are clearly visible on dark panels.
- `AiteBar/RotationProfileSelectionWindow.xaml`: Dialog UI updated with segmented mode switcher (Single vs Rotation), improved list panel container background (#252526), count badge, and clean action buttons.
- `AiteBar/RotationProfileSelectionWindow.xaml.cs`: Support for single-profile selection alongside rotation selection, mode switching, filter preservation, and result properties.
- `AiteBar/SettingsWindow.xaml`: Replacement of `CmbChromeProfile`, `ChkRotation`, and `BtnRotationProfiles` with unified `TxtProfile` selector row and browse button.
- `AiteBar/SettingsWindow.xaml.cs`: Wiring `TxtProfile` and `BtnRotationProfiles` to open the updated dialog, updating display text on load/selection, and saving single/rotation state to `BarElementConfig`.
- `AiteBar/Resources/Strings*.resx`: Localization strings for Single profile mode, Rotation mode, status descriptions, and hints.
- `AiteBar.Tests`: Existing contracts and regression tests verified and expanded.

## Progress

- [ ] Milestone 1: CheckBox Contrast and Visual Hierarchy in FormControlsResources
- [ ] Milestone 2: Multi-Mode Profile Selection Window (Single + Rotation)
- [ ] Milestone 3: SettingsWindow Integration and Unified Profile Field
- [ ] Milestone 4: Localization, Regression Verification, and Test Suite Validation

## Surprises & Discoveries

- Discovery: `FormControlHeightTests.cs` explicitly asserts that a button named `BtnRotationProfiles` exists in `SettingsWindow.xaml` and has `Style="{StaticResource FormSelectionButtonStyle}"`. Retaining `BtnRotationProfiles` as the name of the browse button next to `TxtProfile` allows existing automated tests to pass without breaking contracts.
- Discovery: `RuntimeLocalizationWindowSourceTests.cs` requires `RenderProfiles()` and `protected override void OnLocalizationChanged()` in `RotationProfileSelectionWindow.xaml.cs`, as well as `LoadProfilesAsync(selectedProfile)` in `SettingsWindow.xaml.cs`. These method contracts must be preserved.
- Discovery: The checkbox in `BaseCheckBoxStyle` used `#3A3A3E` for its border and `#383838` for its fill, while `RotationProfileSelectionWindow.xaml` container was `#383838`, creating a 0-contrast scenario for unchecked boxes.

## Decision Log

- Decision: Retain `RotationProfileSelectionWindow` as the type and file name rather than creating a new window class, ensuring existing tests and localization bindings continue to resolve directly while expanding its capabilities to handle both single and rotation profile modes.
- Decision: Use `BtnRotationProfiles` for the selection button beside `TxtProfile`. This satisfies `FormControlHeightTests` while providing a browse button for users who prefer clicking a dedicated button rather than the text field.
- Decision: Style `SelectionListCheckBoxStyle` and single-profile selection rows with row hover backgrounds (`#18FFFFFF` or `#2E323A`) so the entire row is interactive and easily clickable.

## Milestones

### Milestone 1: CheckBox Contrast and Visual Hierarchy in FormControlsResources
Ensure checkboxes in dark themes have a distinct, crisp outline (#707074 / #767676) and a slightly darker/recessed inner fill (#252526 / #2D2D2D) when unchecked, transitioning to AccentColor (#007ACC) when checked. Add row hover styling to `SelectionListCheckBoxStyle`.
Acceptance: Checkboxes inside dark containers are immediately visible and distinguishable when unchecked.

### Milestone 2: Multi-Mode Profile Selection Window (Single + Rotation)
Update `RotationProfileSelectionWindow.xaml` and `.xaml.cs` to feature a Segmented control at the top: "Single profile" and "Profile rotation".
In Single profile mode, display "[Default (no profile)]" and the list of profiles; clicking an item selects it.
In Profile rotation mode, display checkboxes with "Select all", "Clear selection", and live selection count badge.
Update container background to #252526.
Acceptance: The dialog supports switching between single and rotation modes, filters both correctly via search, and returns the chosen mode, single profile path, and rotation profile paths.

### Milestone 3: SettingsWindow Integration and Unified Profile Field
In `SettingsWindow.xaml`, replace `CmbChromeProfile`, `ChkRotation`, and the separate rotation row with a clean "Profile" row: `TxtProfile` (read-only, click-to-open, hand cursor) and `BtnRotationProfiles` ("Browse...").
Update `SettingsWindow.xaml.cs` to display formatted profile status, open the updated dialog, and save `ChromeProfile`, `UseRotation`, and `RotationProfilePaths` correctly.
Acceptance: Opening SettingsWindow for a Web button displays the unified profile field; clicking opens the dialog; choices update the display and save accurately.

### Milestone 4: Localization, Regression Verification, and Test Suite Validation
Add all required localization entries to `Strings.resx`, `Strings.ru.resx`, `Strings.de.resx`, and `Strings.uk.resx`.
Run `dotnet build .\AiteBar.sln -c Release` and `dotnet test .\AiteBar.Tests\AiteBar.Tests.csproj -c Release`.
Acceptance: All 1,664+ unit tests pass, installer builds without warnings.

## Outcomes & Retrospective

(To be populated upon completion of milestones)
