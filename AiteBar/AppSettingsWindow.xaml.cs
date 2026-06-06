using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;

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
    private readonly MainWindow _mainWindow;
    private readonly AppSettings _settings;
    private readonly List<(CheckBox EnabledCheckBox, TextBox NameTextBox)> _contextRows = new();
    private bool _isLoadingSettings;

    public AppSettingsWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _settings = _mainWindow.GetAppSettings();

        _isLoadingSettings = true;
        LoadLanguageList();
        LoadKeyList();
        LoadSettings();
        _isLoadingSettings = false;
    }

    private void LoadLanguageList()
    {
        CmbLanguage.Items.Clear();
        CmbLanguage.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("AppSettingsWindow_LanguageAuto"), Tag = LocalizationService.AutoCulture });
        CmbLanguage.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
        CmbLanguage.Items.Add(new ComboBoxItem { Content = "Deutsch", Tag = "de" });
        CmbLanguage.Items.Add(new ComboBoxItem { Content = "Українська", Tag = "uk" });
        CmbLanguage.Items.Add(new ComboBoxItem { Content = "Русский", Tag = "ru" });
        CmbLanguage.SelectedIndex = 0;
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
    }

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
        string language = GetComboTag(CmbLanguage) ?? LocalizationService.AutoCulture;
        string showPanelKey = GetComboTag(CmbShowPanelKey) ?? "None";
        string nextContextKey = GetComboTag(CmbNextContextKey) ?? "None";
        string previousContextKey = GetComboTag(CmbPrevContextKey) ?? "None";
        string addButtonKey = GetComboTag(CmbAddButtonKey) ?? "None";
        string fileSorterKey = GetComboTag(CmbFileSorterKey) ?? "None";
        string quickNoteKey = GetComboTag(CmbQuickNoteKey) ?? "None";
        string colorPickerKey = GetComboTag(CmbColorPickerKey) ?? "None";
        string timerStopwatchKey = GetComboTag(CmbTimerStopwatchKey) ?? "None";
        object? edgeTag = (CmbEdge.SelectedItem as ComboBoxItem)?.Tag;
        object? monitorTag = (CmbMonitor.SelectedItem as ComboBoxItem)?.Tag;

        _isLoadingSettings = true;
        try
        {
            LoadLanguageList();
            SetComboValue(CmbLanguage, language);

            LoadKeyList();
            SetKeyComboValue(CmbShowPanelKey, showPanelKey);
            SetKeyComboValue(CmbNextContextKey, nextContextKey);
            SetKeyComboValue(CmbPrevContextKey, previousContextKey);
            SetKeyComboValue(CmbAddButtonKey, addButtonKey);
            SetKeyComboValue(CmbFileSorterKey, fileSorterKey);
            SetKeyComboValue(CmbQuickNoteKey, quickNoteKey);
            SetKeyComboValue(CmbColorPickerKey, colorPickerKey);
            SetKeyComboValue(CmbTimerStopwatchKey, timerStopwatchKey);

            ReloadEdgeList(edgeTag);
            ReloadMonitorList(monitorTag);
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void ReloadEdgeList(object? selectedEdge)
    {
        CmbEdge.Items.Clear();
        CmbEdge.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Dock_Top"), Tag = DockEdge.Top });
        CmbEdge.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Dock_Bottom"), Tag = DockEdge.Bottom });
        CmbEdge.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Dock_Left"), Tag = DockEdge.Left });
        CmbEdge.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Dock_Right"), Tag = DockEdge.Right });

        foreach (ComboBoxItem item in CmbEdge.Items)
        {
            if (Equals(item.Tag, selectedEdge))
            {
                CmbEdge.SelectedItem = item;
                break;
            }
        }

        if (CmbEdge.SelectedIndex < 0)
        {
            CmbEdge.SelectedIndex = 0;
        }
    }

    private void ReloadMonitorList(object? selectedMonitor)
    {
        CmbMonitor.Items.Clear();
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            CmbMonitor.Items.Add(new ComboBoxItem
            {
                Content = LocalizationService.Format("Monitor_Format", i + 1, screens[i].Primary ? LocalizationService.Get("Monitor_PrimarySuffix") : string.Empty),
                Tag = i
            });
        }

        foreach (ComboBoxItem item in CmbMonitor.Items)
        {
            if (Equals(item.Tag, selectedMonitor))
            {
                CmbMonitor.SelectedItem = item;
                break;
            }
        }

        if (CmbMonitor.SelectedIndex < 0)
        {
            CmbMonitor.SelectedIndex = CmbMonitor.Items.Count > 0 ? 0 : -1;
        }
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
        HotkeyBinding timerStopwatchBinding)
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
            (LocalizationService.Get("Tool_TimerStopwatch"), timerStopwatchBinding)
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
        ChkShowPresetSearch.IsChecked = _settings.ShowPresetSearch;
        ChkShowPresetScreenshot.IsChecked = _settings.ShowPresetScreenshot;
        ChkShowPresetVideo.IsChecked = _settings.ShowPresetVideo;
        ChkShowPresetCalc.IsChecked = _settings.ShowPresetCalc;
        ChkShowPresetExplorer.IsChecked = _settings.ShowPresetExplorer;
        ChkShowPresetDownloads.IsChecked = _settings.ShowPresetDownloads;
        ChkShowPresetFileSorter.IsChecked = _settings.ShowPresetFileSorter;
        ChkShowPresetTimerStopwatch.IsChecked = _settings.ShowPresetTimerStopwatch;
        ChkShowPresetColorPicker.IsChecked = _settings.ShowPresetColorPicker;
        ChkShowPresetQuickNote.IsChecked = _settings.ShowPresetQuickNote;
        ChkCheckForUpdatesEnabled.IsChecked = _settings.CheckForUpdatesEnabled;
        SetComboValue(CmbLanguage, LocalizationService.NormalizeCultureName(_settings.UiCulture));

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

        ReloadEdgeList(_settings.Edge);
        ReloadMonitorList(_settings.MonitorIndex);

        SldZoneSize.Value = _settings.ActivationZoneSizePercent;
        TxtZoneSize.Text = $"{(int)SldZoneSize.Value}%";
        SldPanelSize.Value = _settings.PanelSizePercent;
        TxtPanelSize.Text = $"{(int)SldPanelSize.Value}%";
        SldDelay.Value = _settings.ActivationDelayMs;
        TxtDelay.Text = $"{(int)SldDelay.Value}";

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
                Background = GetPanelBadgeBrush(i),
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
            _contextRows.Add((enabledCheckBox, nameTextBox));
        }
    }

    private System.Windows.Media.Brush GetPanelBadgeBrush(int index)
    {
        string[] colors =
        [
            "#2563EB",
            "#059669",
            "#D97706",
            "#7C3AED",
            "#0891B2",
            "#BE123C",
            "#4D7C0F",
            "#6D28D9"
        ];

        var converter = new System.Windows.Media.BrushConverter();
        return (System.Windows.Media.Brush)(converter.ConvertFromString(colors[index % colors.Length]) ?? System.Windows.Media.Brushes.DimGray);
    }

    private void SldZoneSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtZoneSize != null) TxtZoneSize.Text = $"{(int)e.NewValue}%";
    }

    private void SldPanelSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtPanelSize != null) TxtPanelSize.Text = $"{(int)e.NewValue}%";
    }

    private void SldDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtDelay != null) TxtDelay.Text = $"{(int)e.NewValue}";
    }

    private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        string selectedCulture = (CmbLanguage.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? LocalizationService.AutoCulture;
        LocalizationService.ApplyCulture(selectedCulture);
        _settings.UiCulture = selectedCulture;
        ReloadLocalizedChoiceLists();
        _mainWindow.ApplyLocalizedText();
    }

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

        if (!ValidateHotkeyBindings(globalBinding, nextBinding, previousBinding, addButtonBinding, fileSorterBinding, quickNoteBinding, colorPickerBinding, timerStopwatchBinding))
        {
            return;
        }

        _settings.GlobalHotkeyCtrl = globalBinding.Ctrl;
        _settings.GlobalHotkeyAlt = globalBinding.Alt;
        _settings.GlobalHotkeyShift = globalBinding.Shift;
        _settings.GlobalHotkeyWin = globalBinding.Win;
        _settings.GlobalHotkeyKey = globalBinding.Key;

        _settings.NextContextHotkey = nextBinding;
        _settings.PreviousContextHotkey = previousBinding;
        _settings.AddButtonHotkey = addButtonBinding;
        _settings.FileSorterHotkey = fileSorterBinding;
        _settings.QuickNoteHotkey = quickNoteBinding;
        _settings.ColorPickerHotkey = colorPickerBinding;
        _settings.TimerStopwatchHotkey = timerStopwatchBinding;

        _settings.ShowPresetSearch = ChkShowPresetSearch.IsChecked ?? false;
        _settings.ShowPresetScreenshot = ChkShowPresetScreenshot.IsChecked ?? false;
        _settings.ShowPresetVideo = ChkShowPresetVideo.IsChecked ?? false;
        _settings.ShowPresetCalc = ChkShowPresetCalc.IsChecked ?? false;
        _settings.ShowPresetExplorer = ChkShowPresetExplorer.IsChecked ?? false;
        _settings.ShowPresetDownloads = ChkShowPresetDownloads.IsChecked ?? false;
        _settings.ShowPresetFileSorter = ChkShowPresetFileSorter.IsChecked ?? false;
        _settings.ShowPresetTimerStopwatch = ChkShowPresetTimerStopwatch.IsChecked ?? false;
        _settings.ShowPresetColorPicker = ChkShowPresetColorPicker.IsChecked ?? false;
        _settings.ShowPresetQuickNote = ChkShowPresetQuickNote.IsChecked ?? false;
        _settings.CheckForUpdatesEnabled = ChkCheckForUpdatesEnabled.IsChecked ?? true;
        _settings.UiCulture = (CmbLanguage.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? LocalizationService.AutoCulture;

        if (CmbEdge.SelectedItem is ComboBoxItem edgeItem && edgeItem.Tag is DockEdge edge)
        {
            _settings.Edge = edge;
        }

        if (CmbMonitor.SelectedItem is ComboBoxItem monitorItem)
            _settings.MonitorIndex = (int)(monitorItem.Tag ?? 0);

        _settings.ActivationZoneSizePercent = SldZoneSize.Value;
        _settings.PanelSizePercent = SldPanelSize.Value;
        _settings.ActivationDelayMs = (int)SldDelay.Value;

        for (int i = 0; i < _settings.Contexts.Count && i < _contextRows.Count; i++)
        {
            string contextName = _contextRows[i].NameTextBox.Text;
            _settings.Contexts[i].Name = string.IsNullOrWhiteSpace(contextName)
                ? LocalizationService.Format("Panel_DefaultNameFormat", i + 1)
                : contextName.Trim();
            _settings.Contexts[i].IsEnabled = i == 0 || (_contextRows[i].EnabledCheckBox.IsChecked ?? false);
        }

        IReadOnlyList<string> failedHotkeys = await _mainWindow.SaveAppSettings();
        LocalizationService.ApplyCulture(_settings.UiCulture);
        _mainWindow.ApplyLocalizedText();
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
        this.Close();
    }
}

internal static class StringExtensions
{
    public static string TrimOrDefault(this string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
