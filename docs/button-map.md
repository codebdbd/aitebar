# Button Style Map for AiteBar

Complete map of all button styles and their usage.

---

## Core Button Styles (Reusable)

### PrimaryButtonStyle
- **File:** [FormControlsResources.xaml](../AiteBar/FormControlsResources.xaml#L25-L79)
- **Description:** Main accent-colored button for primary actions (e.g., OK, Save)
- **Usage:** Primary actions in small forms, inline buttons

### SecondaryButtonStyle
- **File:** [FormControlsResources.xaml](../AiteBar/FormControlsResources.xaml#L82-L95)
- **Description:** Neutral-colored button for secondary actions (e.g., Cancel)
- **Usage:** Secondary actions in small forms

### FormSelectionButtonStyle
- **File:** [FormControlsResources.xaml](../AiteBar/FormControlsResources.xaml#L97-L99)
- **Description:** Button for selection actions (e.g., Browse, Choose from Library)
- **Usage:** Settings forms, file pickers

### IconButtonStyle (Core)
- **File:** [FormControlsResources.xaml](../AiteBar/FormControlsResources.xaml#L104-L149)
- **Description:** Small square icon-only button
- **Usage:** Toolbars, quick actions

### ToolbarButtonStyle
- **File:** [FormControlsResources.xaml](../AiteBar/FormControlsResources.xaml#L153-L187)
- **Description:** Button specifically for toolbars
- **Usage:** Quick Note toolbar

---

## Dialog Button Styles (Large Buttons)

### CommandButtonBaseStyle
- **File:** [UtilityWindowResources.xaml](../AiteBar/UtilityWindowResources.xaml#L5-L10)
- **Description:** Base style for large dialog buttons
- **Usage:** Base for CommandButtonStyle and PrimaryCommandButtonStyle

### CommandButtonStyle
- **File:** [UtilityWindowResources.xaml](../AiteBar/UtilityWindowResources.xaml#L12-L25)
- **Description:** Neutral large dialog button (Cancel)
- **Usage:** Dialogs, settings windows

### PrimaryCommandButtonStyle
- **File:** [UtilityWindowResources.xaml](../AiteBar/UtilityWindowResources.xaml#L27-L38)
- **Description:** Accent large dialog button (Save, OK)
- **Usage:** Dialogs, settings windows

---

## Main Window Styles

### Default MainWindow Button Style
- **File:** [MainWindow.xaml](../AiteBar/MainWindow.xaml#L14-L50)
- **Description:** Implicit style for all buttons on main panel
- **Usage:** All main panel buttons

### PanelButtonStyle
- **File:** [MainWindow.xaml](../AiteBar/MainWindow.xaml#L52-L126)
- **Description:** Custom panel button with icon, text, context indicator
- **Usage:** Main panel buttons

---

## Quick Note Specific Styles

### IconButtonStyle (QuickNote)
- **File:** [QuickNoteWindow.xaml](../AiteBar/QuickNoteWindow.xaml#L17-L44)
- **Description:** Quick Note specific icon button
- **Usage:** Quick Note toolbar icons

### DangerIconButtonStyle
- **File:** [QuickNoteWindow.xaml](../AiteBar/QuickNoteWindow.xaml#L46-L52)
- **Description:** Danger variant of QuickNote icon button
- **Usage:** Delete button

### FormatButtonStyle
- **File:** [QuickNoteWindow.xaml](../AiteBar/QuickNoteWindow.xaml#L87-L128)
- **Description:** Quick Note text formatting button
- **Usage:** Bold, Italic, etc.

### IconToggleButtonStyle
- **File:** [QuickNoteWindow.xaml](../AiteBar/QuickNoteWindow.xaml#L54-L85)
- **Description:** Quick Note toggle button (e.g., Pin)
- **Usage:** Pin button

---

## Module-Specific Styles

### IconPicker
- **IconBtnStyle:** [IconPickerWindow.xaml](../AiteBar/IconPickerWindow.xaml#L18-L56) - Icon grid buttons
- **TabButtonStyle:** [IconPickerWindow.xaml](../AiteBar/IconPickerWindow.xaml#L58-L109) - Icon tabs (All/Custom/Solid/Outline)

### ColorPicker
- **ColorSwatchButtonStyle:** [ColorPickerDialog.xaml](../AiteBar/ColorPickerDialog.xaml#L23-L49) - Color grid buttons

### Clipboard Manager
- **ClipboardActionButtonStyle:** [ClipboardManagerWindow.xaml](../AiteBar/ClipboardManagerWindow.xaml#L73-L94) - History item actions
- **ClipboardFooterIconButtonStyle:** [ClipboardManagerWindow.xaml](../AiteBar/ClipboardManagerWindow.xaml#L204-L234) - Footer actions

### App Settings
- **AboutActionButtonStyle:** [AppSettingsWindow.xaml](../AiteBar/AppSettingsWindow.xaml#L669-L706) - About section links/actions

---

## Window-Specific Usage

### MainWindow.xaml
- Panel buttons: PanelButtonStyle

### AppSettingsWindow.xaml
- Add Connection: CommandButtonStyle
- Test/Remove (AI): CommandButtonStyle
- About section: AboutActionButtonStyle
- Cancel/Save: CommandButtonStyle / PrimaryCommandButtonStyle

### SettingsWindow.xaml
- Browse: FormSelectionButtonStyle
- Rotation Profiles: FormSelectionButtonStyle
- Choose from Library: FormSelectionButtonStyle
- Custom Icon: FormSelectionButtonStyle
- Cancel/Save: CommandButtonStyle / PrimaryCommandButtonStyle

### AiConnectionDialog.xaml
- Cancel/Add: CommandButtonStyle / PrimaryCommandButtonStyle

### QuickNoteWindow.xaml
- Formatting: FormatButtonStyle
- Icons (Pin/Delete): IconButtonStyle / DangerIconButtonStyle
- Toggles (Pin): IconToggleButtonStyle

### QuickNoteLinkDialog.xaml
- Cancel/Save: SecondaryButtonStyle / ActionButtonStyle

### ClipboardManagerWindow.xaml
- History actions: ClipboardActionButtonStyle
- Footer icons: ClipboardFooterIconButtonStyle

### IconPickerWindow.xaml
- Icons: IconBtnStyle
- Tabs: TabButtonStyle

### ColorPickerDialog.xaml
- Colors: ColorSwatchButtonStyle
- Cancel/OK: SecondaryButtonStyle / ActionButtonStyle

### IconConverterWindow.xaml
- Select File: CommandButtonStyle
- Save: PrimaryCommandButtonStyle

### FileSorterWindow.xaml
- Sort: PrimaryCommandButtonStyle
- Settings: SecondaryButtonStyle

### QRCodeGeneratorWindow.xaml
- Save PNG/SVG, Copy PNG/SVG: CommandButtonStyle

### RotationProfileSelectionWindow.xaml
- Select All/Clear/Cancel/Save: SecondaryButtonStyle / ActionButtonStyle

### DarkDialog.xaml
- Yes/No/OK: DialogPrimaryButtonStyle / DialogSecondaryButtonStyle

### TextPromptDialog.xaml
- Cancel/OK: SecondaryButtonStyle / ActionButtonStyle

---

## Legacy/Redundant Styles (Can be removed)
- **ActionButtonStyle**: Alias for PrimaryButtonStyle
- **UtilityHeaderButtonStyle**: Alias for IconButtonStyle
- **HeaderButtonStyle**: Alias for IconButtonStyle
- **DialogPrimaryButtonStyle**: Alias for PrimaryCommandButtonStyle
- **DialogSecondaryButtonStyle**: Alias for CommandButtonStyle

---

## ToggleButton/RadioButton Styles (Not buttons)
- **HotkeyModifierButtonStyle**: AppSettingsWindow.xaml
- **ModernSelectionCardStyle**: AppSettingsWindow.xaml
- **SettingsTopmostToggleStyle**: AppSettingsWindow.xaml
- **SegmentedRadioButtonStyle**: SettingsResources.xaml
- **OptionRadioButtonStyle**: IconConverterWindow.xaml
- **ModernSwitchStyle**: AppSettingsWindow.xaml (CheckBox)
