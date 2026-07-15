using System.Globalization;
using System.Runtime.Versioning;
using System.Windows.Data;

namespace AiteBar;

public class FontNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => FontHelper.Resolve(value?.ToString() ?? FontHelper.FluentKey);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ColorBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.BrushConverter _bc = new();
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => _bc.ConvertFromString(value?.ToString() ?? "#FFD700") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Gold;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

[SupportedOSPlatform("windows6.1")]
public partial class AppSettingsWindow : DarkWindow
{
    private sealed record ContextRowDraft(string Name, bool IsEnabled, bool IsNameCustomized);

    private readonly MainWindow _mainWindow;
    private readonly AppSettings _settings;
    private readonly List<(CheckBox EnabledCheckBox, TextBox NameTextBox, Border BadgeBorder)> _contextRows = new();
    private bool _isLoadingSettings;
    private bool _panelSizeSelectionChanged;
    private bool _activationZoneSelectionChanged;
    private bool _activationDelaySelectionChanged;
    private readonly string _originalUiCulture;
    private string _selectedUiCulture = LocalizationService.AutoCulture;

    public AppSettingsWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        LocalizationService.EnsureAppliedCulture();
        LocalizationService.RefreshLocalizedBindings(this);
        _mainWindow = mainWindow;
        _settings = _mainWindow.GetAppSettings();
        _originalUiCulture = LocalizationService.NormalizeCultureName(_settings.UiCulture);

        _isLoadingSettings = true;
        LoadKeyList();
        LoadSettings();
        _isLoadingSettings = false;
        RefreshLocalizedUi();
    }

    private void LoadKeyList()
    {
        foreach (var combo in GetHotkeyCombos())
        {
            combo.Items.Clear();
            combo.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Common_NotAssigned"), Tag = "None" });
            foreach (HotkeyKeyOption key in HotkeyKeyCatalog.GlobalHotkeyKeys)
            {
                combo.Items.Add(new ComboBoxItem { Content = key.DisplayName, Tag = key.Key });
            }
            combo.SelectedIndex = 0;
        }
    }

    private IEnumerable<ComboBox> GetHotkeyCombos()
    {
        yield return CmbShowPanelKey;
        yield return CmbNextContextKey;
        yield return CmbPrevContextKey;
        yield return CmbAddButtonKey;
        yield return CmbFileSorterKey;
        yield return CmbQuickNoteKey;
        yield return CmbColorPickerKey;
        yield return CmbTimerStopwatchKey;
        yield return CmbQRCodeGeneratorKey;
    }

    private (CheckBox CheckBox, UtilityButtonDefinition Definition)[] GetUtilityVisibilityBindings() =>
    [
        (ChkShowPresetSearch, UtilityButtonCatalog.Search),
        (ChkShowPresetScreenshot, UtilityButtonCatalog.Screenshot),
        (ChkShowPresetVideo, UtilityButtonCatalog.Record),
        (ChkShowPresetCalc, UtilityButtonCatalog.Calculator),
        (ChkShowPresetExplorer, UtilityButtonCatalog.Explorer),
        (ChkShowPresetDownloads, UtilityButtonCatalog.Downloads),
        (ChkShowPresetFileSorter, UtilityButtonCatalog.FileSorter),
        (ChkShowPresetIconConverter, UtilityButtonCatalog.IconConverter),
        (ChkShowPresetTimerStopwatch, UtilityButtonCatalog.TimerStopwatch),
        (ChkShowPresetColorPicker, UtilityButtonCatalog.ColorPicker),
        (ChkShowPresetQuickNote, UtilityButtonCatalog.QuickNote),
        (ChkShowPresetQRCodeGenerator, UtilityButtonCatalog.QRCodeGenerator),
        (ChkShowPresetClipboardManager, UtilityButtonCatalog.ClipboardManager),
        (ChkShowPresetShowDesktop, UtilityButtonCatalog.ShowDesktop),
        (ChkShowPresetAppsFolder, UtilityButtonCatalog.AppsFolder),
        (ChkShowPresetCopilot, UtilityButtonCatalog.Copilot)
    ];

    private static void SetKeyComboValue(ComboBox combo, string? key)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (string.Equals(item.Tag?.ToString(), key, StringComparison.Ordinal))
            {
                combo.SelectedItem = item;
                break;
            }
        }

        if (combo.SelectedIndex < 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private static void SetComboValue(ComboBox combo, string? value)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (string.Equals(item.Tag?.ToString(), value, StringComparison.Ordinal))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        if (combo.SelectedIndex < 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private static string? GetComboTag(ComboBox combo) =>
        (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

    private void ReloadLocalizedChoiceLists()
    {
        string language = _selectedUiCulture;
        string showPanelKey = GetComboTag(CmbShowPanelKey) ?? "None";
        string nextContextKey = GetComboTag(CmbNextContextKey) ?? "None";
        string previousContextKey = GetComboTag(CmbPrevContextKey) ?? "None";
        string addButtonKey = GetComboTag(CmbAddButtonKey) ?? "None";
        string fileSorterKey = GetComboTag(CmbFileSorterKey) ?? "None";
        string quickNoteKey = GetComboTag(CmbQuickNoteKey) ?? "None";
        string colorPickerKey = GetComboTag(CmbColorPickerKey) ?? "None";
        string timerStopwatchKey = GetComboTag(CmbTimerStopwatchKey) ?? "None";
        string qrCodeGeneratorKey = GetComboTag(CmbQRCodeGeneratorKey) ?? "None";
        string edge = GetSelectedSegmentTag(SegEdgeTop, SegEdgeBottom, SegEdgeLeft, SegEdgeRight);
        bool isSecondaryMonitor = ChkSecondaryMonitor.IsChecked == true;

        _isLoadingSettings = true;
        try
        {
            LoadKeyList();
            SetKeyComboValue(CmbShowPanelKey, showPanelKey);
            SetKeyComboValue(CmbNextContextKey, nextContextKey);
            SetKeyComboValue(CmbPrevContextKey, previousContextKey);
            SetKeyComboValue(CmbAddButtonKey, addButtonKey);
            SetKeyComboValue(CmbFileSorterKey, fileSorterKey);
            SetKeyComboValue(CmbQuickNoteKey, quickNoteKey);
            SetKeyComboValue(CmbColorPickerKey, colorPickerKey);
            SetKeyComboValue(CmbTimerStopwatchKey, timerStopwatchKey);
            SetKeyComboValue(CmbQRCodeGeneratorKey, qrCodeGeneratorKey);

            SelectSegmentByTag(language, SegLangAuto, SegLangEn, SegLangDe, SegLangUk, SegLangRu);
            SelectSegmentByTag(edge, SegEdgeTop, SegEdgeBottom, SegEdgeLeft, SegEdgeRight);
            ChkSecondaryMonitor.IsChecked = isSecondaryMonitor;
            UpdateMonitorCheckbox();
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void RefreshLocalizedUi()
    {
        List<ContextRowDraft> drafts = CaptureContextRowDrafts();
        ReloadLocalizedChoiceLists();
        BuildContextRows(_mainWindow.GetAllContextsSnapshot());
        ApplyContextRowDrafts(drafts);
        RefreshContextRowTooltips();
    }

    private List<ContextRowDraft> CaptureContextRowDrafts()
    {
        var drafts = new List<ContextRowDraft>(_contextRows.Count);
        for (int i = 0; i < _contextRows.Count; i++)
        {
            string draftName = _contextRows[i].NameTextBox.Text;
            drafts.Add(new ContextRowDraft(
                draftName,
                _contextRows[i].EnabledCheckBox.IsChecked ?? false,
                ContextStateHelper.IsCustomizedContextNameInput(draftName, i)));
        }

        return drafts;
    }

    private void ApplyContextRowDrafts(IReadOnlyList<ContextRowDraft> drafts)
    {
        for (int i = 0; i < _contextRows.Count && i < drafts.Count; i++)
        {
            if (drafts[i].IsNameCustomized)
            {
                _contextRows[i].NameTextBox.Text = drafts[i].Name.Trim();
            }

            _contextRows[i].EnabledCheckBox.IsChecked = i == 0 || drafts[i].IsEnabled;

            // Цвет всегда фиксированный, обновляем бейдж
            SetBadgeColor(_contextRows[i].BadgeBorder, ContextStateHelper.GetContextColor(i));
        }
    }

    private void RefreshContextRowTooltips()
    {
        for (int i = 0; i < _contextRows.Count; i++)
        {
            _contextRows[i].EnabledCheckBox.ToolTip = i == 0
                ? LocalizationService.Get("AppSettingsWindow_PrimaryPanelAlwaysEnabled")
                : LocalizationService.Get("AppSettingsWindow_PanelEnabled");
        }
    }

    private static void SetBadgeColor(Border badgeBorder, string colorString)
    {
        var converter = new System.Windows.Media.BrushConverter();
        badgeBorder.Background = (System.Windows.Media.Brush)(converter.ConvertFromString(colorString) ?? System.Windows.Media.Brushes.DimGray);
    }

    private static string GetSelectedSegmentTag(System.Windows.Controls.RadioButton a, System.Windows.Controls.RadioButton b, System.Windows.Controls.RadioButton c, System.Windows.Controls.RadioButton d)
    {
        if (a.IsChecked == true) return a.Tag!.ToString()!;
        if (b.IsChecked == true) return b.Tag!.ToString()!;
        if (c.IsChecked == true) return c.Tag!.ToString()!;
        return d.Tag!.ToString()!;
    }

    private static string? GetSelectedSegmentTag(System.Windows.Controls.RadioButton a, System.Windows.Controls.RadioButton b, System.Windows.Controls.RadioButton c, System.Windows.Controls.RadioButton d, System.Windows.Controls.RadioButton e)
    {
        if (a.IsChecked == true) return a.Tag!.ToString()!;
        if (b.IsChecked == true) return b.Tag!.ToString()!;
        if (c.IsChecked == true) return c.Tag!.ToString()!;
        if (d.IsChecked == true) return d.Tag!.ToString()!;
        return e.Tag!.ToString()!;
    }

    private static void SelectSegmentByTag(string tag, System.Windows.Controls.RadioButton a, System.Windows.Controls.RadioButton b, System.Windows.Controls.RadioButton c, System.Windows.Controls.RadioButton d)
    {
        if (string.Equals(a.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) a.IsChecked = true;
        else if (string.Equals(b.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) b.IsChecked = true;
        else if (string.Equals(c.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) c.IsChecked = true;
        else d.IsChecked = true;
    }

    private static void SelectSegmentByTag(string tag, System.Windows.Controls.RadioButton a, System.Windows.Controls.RadioButton b, System.Windows.Controls.RadioButton c, System.Windows.Controls.RadioButton d, System.Windows.Controls.RadioButton e)
    {
        if (string.Equals(a.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) a.IsChecked = true;
        else if (string.Equals(b.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) b.IsChecked = true;
        else if (string.Equals(c.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) c.IsChecked = true;
        else if (string.Equals(d.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) d.IsChecked = true;
        else e.IsChecked = true;
    }

    private void UpdateMonitorCheckbox()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        bool hasSecondary = screens.Length > 1;
        ChkSecondaryMonitor.IsEnabled = hasSecondary;
        if (!hasSecondary) ChkSecondaryMonitor.IsChecked = false;
    }

    private static void LoadHotkeyBinding(HotkeyBinding binding, ToggleButton chkCtrl, ToggleButton chkAlt, ToggleButton chkShift, ToggleButton chkWin, ComboBox cmbKey)
    {
        chkCtrl.IsChecked = binding.Ctrl;
        chkAlt.IsChecked = binding.Alt;
        chkShift.IsChecked = binding.Shift;
        chkWin.IsChecked = binding.Win;
        SetKeyComboValue(cmbKey, binding.Key);
    }

    private static HotkeyBinding BuildHotkeyBinding(ToggleButton chkCtrl, ToggleButton chkAlt, ToggleButton chkShift, ToggleButton chkWin, ComboBox cmbKey)
    {
        var binding = new HotkeyBinding();
        binding.Ctrl = chkCtrl.IsChecked ?? false;
        binding.Alt = chkAlt.IsChecked ?? false;
        binding.Shift = chkShift.IsChecked ?? false;
        binding.Win = chkWin.IsChecked ?? false;
        binding.Key = (cmbKey.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None";
        return binding;
    }

    private static string? GetHotkeyToken(HotkeyBinding binding)
    {
        if (binding == null || string.IsNullOrWhiteSpace(binding.Key) || string.Equals(binding.Key, "None", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"{(binding.Ctrl ? "C" : "-")}{(binding.Shift ? "S" : "-")}{(binding.Alt ? "A" : "-")}{(binding.Win ? "W" : "-")}:{binding.Key.ToUpperInvariant()}";
    }

    private static bool HasAssignedKey(HotkeyBinding binding)
    {
        return HotkeyValidationHelper.HasAssignedKey(binding);
    }

    private static bool HasModifier(HotkeyBinding binding)
    {
        return HotkeyValidationHelper.HasModifier(binding);
    }

    private bool ValidateHotkeyBindings(
        HotkeyBinding globalBinding,
        HotkeyBinding nextBinding,
        HotkeyBinding previousBinding,
        HotkeyBinding addButtonBinding,
        HotkeyBinding fileSorterBinding,
        HotkeyBinding quickNoteBinding,
        HotkeyBinding colorPickerBinding,
        HotkeyBinding timerStopwatchBinding,
        HotkeyBinding qrCodeGeneratorBinding)
    {
        var registrations = new (string Name, HotkeyBinding Binding)[]
        {
            (LocalizationService.Get("AppSettingsWindow_ShowPanel"), globalBinding),
            (LocalizationService.Get("AppSettingsWindow_NextPanel"), nextBinding),
            (LocalizationService.Get("AppSettingsWindow_PreviousPanel"), previousBinding),
            (LocalizationService.Get("AppSettingsWindow_AddButton"), addButtonBinding),
            (LocalizationService.Get("Tool_FileSorter"), fileSorterBinding),
            (LocalizationService.Get("Tool_QuickNote"), quickNoteBinding),
            (LocalizationService.Get("Tool_ColorPicker"), colorPickerBinding),
            (LocalizationService.Get("Tool_TimerStopwatch"), timerStopwatchBinding),
            (LocalizationService.Get("Tool_QRCodeGenerator"), qrCodeGeneratorBinding)
        };

        var missingModifiers = registrations
            .Where(item => HasAssignedKey(item.Binding) && !HasModifier(item.Binding))
            .Select(item => item.Name)
            .ToList();

        if (missingModifiers.Count > 0)
        {
            new DarkDialog(
                LocalizationService.Format("HotkeyModifierRequiredMessage", string.Join("\n", missingModifiers)))
            {
                Owner = this
            }.ShowDialog();
            return false;
        }

        var duplicates = registrations
            .Select(item => new { item.Name, Token = GetHotkeyToken(item.Binding) })
            .Where(item => item.Token != null)
            .GroupBy(item => item.Token!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(", ", group.Select(item => item.Name)))
            .ToList();

        if (duplicates.Count == 0)
        {
            return true;
        }

        new DarkDialog(
            LocalizationService.Format("HotkeyConflictMessage", string.Join("\n", duplicates)))
        {
            Owner = this
        }.ShowDialog();
        return false;
    }

    private void LoadSettings()
    {
        // Load all settings from config
        foreach (var (checkBox, definition) in GetUtilityVisibilityBindings())
        {
            checkBox.IsChecked = definition.IsVisible(_settings);
        }
        ChkClipboardManagerPersistHistory.IsChecked = _settings.ClipboardManagerPersistHistory;
        ChkShowTaskbarPositionIndicator.IsChecked = _settings.ShowTaskbarPositionIndicator.GetValueOrDefault(true);
        ChkCheckForUpdatesEnabled.IsChecked = _settings.CheckForUpdatesEnabled;
        _selectedUiCulture = LocalizationService.NormalizeCultureName(_settings.UiCulture);
        SelectSegmentByTag(_selectedUiCulture, SegLangAuto, SegLangEn, SegLangDe, SegLangUk, SegLangRu);

        LoadHotkeyBinding(
            new HotkeyBinding
            {
                Ctrl = _settings.GlobalHotkeyCtrl,
                Alt = _settings.GlobalHotkeyAlt,
                Shift = _settings.GlobalHotkeyShift,
                Win = _settings.GlobalHotkeyWin,
                Key = _settings.GlobalHotkeyKey
            },
            ChkShowPanelCtrl,
            ChkShowPanelAlt,
            ChkShowPanelShift,
            ChkShowPanelWin,
            CmbShowPanelKey);

        LoadHotkeyBinding(_settings.NextContextHotkey, ChkNextContextCtrl, ChkNextContextAlt, ChkNextContextShift, ChkNextContextWin, CmbNextContextKey);
        LoadHotkeyBinding(_settings.PreviousContextHotkey, ChkPrevContextCtrl, ChkPrevContextAlt, ChkPrevContextShift, ChkPrevContextWin, CmbPrevContextKey);
        LoadHotkeyBinding(_settings.AddButtonHotkey, ChkAddButtonCtrl, ChkAddButtonAlt, ChkAddButtonShift, ChkAddButtonWin, CmbAddButtonKey);
        LoadHotkeyBinding(_settings.FileSorterHotkey, ChkFileSorterCtrl, ChkFileSorterAlt, ChkFileSorterShift, ChkFileSorterWin, CmbFileSorterKey);
        LoadHotkeyBinding(_settings.QuickNoteHotkey, ChkQuickNoteCtrl, ChkQuickNoteAlt, ChkQuickNoteShift, ChkQuickNoteWin, CmbQuickNoteKey);
        LoadHotkeyBinding(_settings.ColorPickerHotkey, ChkColorPickerCtrl, ChkColorPickerAlt, ChkColorPickerShift, ChkColorPickerWin, CmbColorPickerKey);
        LoadHotkeyBinding(_settings.TimerStopwatchHotkey, ChkTimerStopwatchCtrl, ChkTimerStopwatchAlt, ChkTimerStopwatchShift, ChkTimerStopwatchWin, CmbTimerStopwatchKey);
        LoadHotkeyBinding(_settings.QRCodeGeneratorHotkey, ChkQRCodeGeneratorCtrl, ChkQRCodeGeneratorAlt, ChkQRCodeGeneratorShift, ChkQRCodeGeneratorWin, CmbQRCodeGeneratorKey);

        SelectSegmentByTag(_settings.Edge.ToString(), SegEdgeTop, SegEdgeBottom, SegEdgeLeft, SegEdgeRight);
        ChkSecondaryMonitor.IsChecked = _settings.MonitorIndex > 0;
        UpdateMonitorCheckbox();

        SelectSegment(SegPanelSize50, SegPanelSize70, SegPanelSize90, SegPanelSize100, (int)_settings.PanelSizePercent);
        SelectSegment(SegZone10, SegZone30, SegZone50, SegZone100, (int)_settings.ActivationZoneSizePercent);
        SelectSegment(SegDelay100, SegDelay200, SegDelay300, SegDelay500, (int)_settings.ActivationDelayMs);

        BuildContextRows(_mainWindow.GetAllContextsSnapshot());
    }

    private void BuildContextRows(IReadOnlyList<PanelContext> contexts)
    {
        PanelContextsList.Children.Clear();
        _contextRows.Clear();

        for (int i = 0; i < contexts.Count; i++)
        {
            PanelContext context = contexts[i];
            var row = new Grid { Height = 34, Margin = new Thickness(0, 0, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

            var badge = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = GetPanelBadgeBrush(ContextStateHelper.GetContextColor(i)),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = (i + 1).ToString(CultureInfo.InvariantCulture),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                TextAlignment = System.Windows.TextAlignment.Center
            };
            Grid.SetColumn(badge, 0);
            row.Children.Add(badge);

            var nameTextBox = new TextBox { Text = context.Name };
            Grid.SetColumn(nameTextBox, 1);
            row.Children.Add(nameTextBox);

            var enabledCheckBox = new CheckBox
            {
                IsChecked = context.IsEnabled,
                IsEnabled = i != 0,
                Style = (Style)FindResource("CenteredCheckBoxStyle"),
                ToolTip = i == 0
                    ? LocalizationService.Get("AppSettingsWindow_PrimaryPanelAlwaysEnabled")
                    : LocalizationService.Get("AppSettingsWindow_PanelEnabled")
            };
            Grid.SetColumn(enabledCheckBox, 2);
            row.Children.Add(enabledCheckBox);

            PanelContextsList.Children.Add(row);
            _contextRows.Add((enabledCheckBox, nameTextBox, badge));
        }
    }

    private System.Windows.Media.Brush GetPanelBadgeBrush(string colorString)
    {
        var converter = new System.Windows.Media.BrushConverter();
        return (System.Windows.Media.Brush)(converter.ConvertFromString(colorString) ?? System.Windows.Media.Brushes.DimGray);
    }

    private static void SelectSegment(System.Windows.Controls.RadioButton a, System.Windows.Controls.RadioButton b, System.Windows.Controls.RadioButton c, System.Windows.Controls.RadioButton d, int value)
    {
        int valA = int.Parse(a.Tag!.ToString()!);
        int valB = int.Parse(b.Tag!.ToString()!);
        int valC = int.Parse(c.Tag!.ToString()!);
        int valD = int.Parse(d.Tag!.ToString()!);

        int distA = Math.Abs(value - valA);
        int distB = Math.Abs(value - valB);
        int distC = Math.Abs(value - valC);
        int distD = Math.Abs(value - valD);

        int min = Math.Min(distA, Math.Min(distB, Math.Min(distC, distD)));
        if (min == distA) a.IsChecked = true;
        else if (min == distB) b.IsChecked = true;
        else if (min == distC) c.IsChecked = true;
        else d.IsChecked = true;
    }

    private static int GetSegmentValue(System.Windows.Controls.RadioButton a, System.Windows.Controls.RadioButton b, System.Windows.Controls.RadioButton c, System.Windows.Controls.RadioButton d)
    {
        if (a.IsChecked == true) return int.Parse(a.Tag!.ToString()!);
        if (b.IsChecked == true) return int.Parse(b.Tag!.ToString()!);
        if (c.IsChecked == true) return int.Parse(c.Tag!.ToString()!);
        return int.Parse(d.Tag!.ToString()!);
    }

    private void SegPanelSize_Click(object sender, RoutedEventArgs e) => _panelSizeSelectionChanged = true;
    private void SegZone_Click(object sender, RoutedEventArgs e) => _activationZoneSelectionChanged = true;
    private void SegDelay_Click(object sender, RoutedEventArgs e) => _activationDelaySelectionChanged = true;

    private async void SegLanguage_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        string selectedCulture = GetSelectedSegmentTag(SegLangAuto, SegLangEn, SegLangDe, SegLangUk, SegLangRu)!;
        _selectedUiCulture = LocalizationService.NormalizeCultureName(selectedCulture);
        _settings.UiCulture = _selectedUiCulture;
        LocalizationService.ApplyCulture(_selectedUiCulture);
        _mainWindow.GetSettingsService().NormalizeAppState();
        await _mainWindow.GetSettingsService().SaveAsync();
        LocalizationService.EnsureAppliedCulture();
        LocalizationService.RefreshLocalizedBindings(this);
        RefreshLocalizedUi();
    }

    private void SegEdge_Click(object sender, RoutedEventArgs e) { }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var globalBinding = BuildHotkeyBinding(ChkShowPanelCtrl, ChkShowPanelAlt, ChkShowPanelShift, ChkShowPanelWin, CmbShowPanelKey);
        var nextBinding = BuildHotkeyBinding(ChkNextContextCtrl, ChkNextContextAlt, ChkNextContextShift, ChkNextContextWin, CmbNextContextKey);
        var previousBinding = BuildHotkeyBinding(ChkPrevContextCtrl, ChkPrevContextAlt, ChkPrevContextShift, ChkPrevContextWin, CmbPrevContextKey);
        var addButtonBinding = BuildHotkeyBinding(ChkAddButtonCtrl, ChkAddButtonAlt, ChkAddButtonShift, ChkAddButtonWin, CmbAddButtonKey);
        var fileSorterBinding = BuildHotkeyBinding(ChkFileSorterCtrl, ChkFileSorterAlt, ChkFileSorterShift, ChkFileSorterWin, CmbFileSorterKey);
        var quickNoteBinding = BuildHotkeyBinding(ChkQuickNoteCtrl, ChkQuickNoteAlt, ChkQuickNoteShift, ChkQuickNoteWin, CmbQuickNoteKey);
        var colorPickerBinding = BuildHotkeyBinding(ChkColorPickerCtrl, ChkColorPickerAlt, ChkColorPickerShift, ChkColorPickerWin, CmbColorPickerKey);
        var timerStopwatchBinding = BuildHotkeyBinding(ChkTimerStopwatchCtrl, ChkTimerStopwatchAlt, ChkTimerStopwatchShift, ChkTimerStopwatchWin, CmbTimerStopwatchKey);
        var qrCodeGeneratorBinding = BuildHotkeyBinding(ChkQRCodeGeneratorCtrl, ChkQRCodeGeneratorAlt, ChkQRCodeGeneratorShift, ChkQRCodeGeneratorWin, CmbQRCodeGeneratorKey);

        if (!ValidateHotkeyBindings(globalBinding, nextBinding, previousBinding, addButtonBinding, fileSorterBinding, quickNoteBinding, colorPickerBinding, timerStopwatchBinding, qrCodeGeneratorBinding))
        {
            return;
        }

        _mainWindow.GetSettingsService().UpdateSettings(settings =>
        {
            settings.GlobalHotkeyCtrl = globalBinding.Ctrl;
            settings.GlobalHotkeyAlt = globalBinding.Alt;
            settings.GlobalHotkeyShift = globalBinding.Shift;
            settings.GlobalHotkeyWin = globalBinding.Win;
            settings.GlobalHotkeyKey = globalBinding.Key;

            settings.NextContextHotkey = nextBinding;
            settings.PreviousContextHotkey = previousBinding;
            settings.AddButtonHotkey = addButtonBinding;
            settings.FileSorterHotkey = fileSorterBinding;
            settings.QuickNoteHotkey = quickNoteBinding;
            settings.ColorPickerHotkey = colorPickerBinding;
            settings.TimerStopwatchHotkey = timerStopwatchBinding;
            settings.QRCodeGeneratorHotkey = qrCodeGeneratorBinding;

            foreach (var (checkBox, definition) in GetUtilityVisibilityBindings())
            {
                definition.SetVisible(settings, checkBox.IsChecked ?? false);
            }
            settings.ClipboardManagerPersistHistory = ChkClipboardManagerPersistHistory.IsChecked ?? true;
            settings.ShowTaskbarPositionIndicator = ChkShowTaskbarPositionIndicator.IsChecked ?? true;
            settings.CheckForUpdatesEnabled = ChkCheckForUpdatesEnabled.IsChecked ?? true;
            settings.UiCulture = _selectedUiCulture;

            string edgeStr = GetSelectedSegmentTag(SegEdgeTop, SegEdgeBottom, SegEdgeLeft, SegEdgeRight);
            if (Enum.TryParse<DockEdge>(edgeStr, out var edge))
            {
                settings.Edge = edge;
            }

            settings.MonitorIndex = AppSettingsSelectionHelper.ResolveMonitorIndex(
                settings.MonitorIndex,
                ChkSecondaryMonitor.IsChecked == true);

            settings.ActivationZoneSizePercent = AppSettingsSelectionHelper.ResolveSegmentedValue(
                settings.ActivationZoneSizePercent,
                GetSegmentValue(SegZone10, SegZone30, SegZone50, SegZone100),
                _activationZoneSelectionChanged);
            settings.PanelSizePercent = AppSettingsSelectionHelper.ResolveSegmentedValue(
                settings.PanelSizePercent,
                GetSegmentValue(SegPanelSize50, SegPanelSize70, SegPanelSize90, SegPanelSize100),
                _panelSizeSelectionChanged);
            settings.ActivationDelayMs = AppSettingsSelectionHelper.ResolveSegmentedValue(
                settings.ActivationDelayMs,
                GetSegmentValue(SegDelay100, SegDelay200, SegDelay300, SegDelay500),
                _activationDelaySelectionChanged);

            for (int i = 0; i < settings.Contexts.Count && i < _contextRows.Count; i++)
            {
                string contextName = _contextRows[i].NameTextBox.Text;
                bool isNameCustomized = ContextStateHelper.IsCustomizedContextNameInput(contextName, i);
                settings.Contexts[i].Name = isNameCustomized
                    ? contextName.Trim()
                    : ContextStateHelper.GetDefaultContextName(i);
                settings.Contexts[i].IsNameCustomized = isNameCustomized;
                settings.Contexts[i].IsEnabled = i == 0 || (_contextRows[i].EnabledCheckBox.IsChecked ?? false);
            }
        });
        IReadOnlyList<string> failedHotkeys;
        try
        {
            failedHotkeys = await _mainWindow.SaveAppSettings();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Settings_SaveFailed", ex.Message))
            {
                Owner = this
            }.ShowDialog();
            return;
        }

        LocalizationService.ApplyCulture(_selectedUiCulture);
        _mainWindow.RefreshPanel();

        if (failedHotkeys.Count > 0)
        {
            new DarkDialog(LocalizationService.Format("HotkeyRegistrationFailed", string.Join("\n", failedHotkeys)))
            {
                Owner = this
            }.ShowDialog();
        }

        this.Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        LocalizationService.ApplyCulture(_originalUiCulture);
        this.Close();
    }

    protected override void OnLocalizationChanged()
    {
        RefreshLocalizedUi();
    }
}

internal static class StringExtensions
{
    public static string TrimOrDefault(this string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
