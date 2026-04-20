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
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;

namespace AiteBar
{
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

        public AppSettingsWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _settings = _mainWindow.GetAppSettings();

            LoadKeyList();
            LoadSettings();
        }

        private void LoadKeyList()
        {
            foreach (var combo in GetHotkeyCombos())
            {
                combo.Items.Clear();
                combo.Items.Add(new ComboBoxItem { Content = "Не назначено", Tag = "None" });
                for (char c = 'A'; c <= 'Z'; c++) combo.Items.Add(new ComboBoxItem { Content = c.ToString(), Tag = c.ToString() });
                for (int i = 0; i <= 9; i++) combo.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = "D" + i });
                for (int i = 1; i <= 12; i++) combo.Items.Add(new ComboBoxItem { Content = "F" + i, Tag = "F" + i });
                combo.SelectedIndex = 0;
            }
        }

        private IEnumerable<ComboBox> GetHotkeyCombos()
        {
            yield return CmbKey;
            yield return CmbNextContextKey;
            yield return CmbPrevContextKey;
            yield return CmbContext1Key;
            yield return CmbContext2Key;
            yield return CmbContext3Key;
            yield return CmbContext4Key;
        }

        private static void LoadHotkeyBinding(
            HotkeyBinding binding,
            CheckBox chkCtrl,
            CheckBox chkShift,
            CheckBox chkAlt,
            CheckBox chkWin,
            ComboBox cmbKey)
        {
            chkCtrl.IsChecked = binding.Ctrl;
            chkShift.IsChecked = binding.Shift;
            chkAlt.IsChecked = binding.Alt;
            chkWin.IsChecked = binding.Win;

            foreach (ComboBoxItem item in cmbKey.Items)
            {
                if (item.Tag?.ToString() == binding.Key)
                {
                    cmbKey.SelectedItem = item;
                    break;
                }
            }

            if (cmbKey.SelectedIndex < 0)
            {
                cmbKey.SelectedIndex = 0;
            }
        }

        private static void SaveHotkeyBinding(
            HotkeyBinding binding,
            CheckBox chkCtrl,
            CheckBox chkShift,
            CheckBox chkAlt,
            CheckBox chkWin,
            ComboBox cmbKey)
        {
            binding.Ctrl = chkCtrl.IsChecked ?? false;
            binding.Shift = chkShift.IsChecked ?? false;
            binding.Alt = chkAlt.IsChecked ?? false;
            binding.Win = chkWin.IsChecked ?? false;
            binding.Key = (cmbKey.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None";
        }

        private static HotkeyBinding BuildHotkeyBinding(
            CheckBox chkCtrl,
            CheckBox chkShift,
            CheckBox chkAlt,
            CheckBox chkWin,
            ComboBox cmbKey)
        {
            var binding = new HotkeyBinding();
            SaveHotkeyBinding(binding, chkCtrl, chkShift, chkAlt, chkWin, cmbKey);
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

        private void UpdateContextHotkeyLabels()
        {
            if (LblContext1Hotkey == null)
            {
                return;
            }

            LblContext1Hotkey.Text = TxtContext1Name.Text.TrimOrDefault("Контекст 1");
            LblContext2Hotkey.Text = TxtContext2Name.Text.TrimOrDefault("Контекст 2");
            LblContext3Hotkey.Text = TxtContext3Name.Text.TrimOrDefault("Контекст 3");
            LblContext4Hotkey.Text = TxtContext4Name.Text.TrimOrDefault("Контекст 4");
        }

        private bool ValidateHotkeyBindings(
            HotkeyBinding globalBinding,
            HotkeyBinding nextBinding,
            HotkeyBinding previousBinding,
            HotkeyBinding context1Binding,
            HotkeyBinding context2Binding,
            HotkeyBinding context3Binding,
            HotkeyBinding context4Binding)
        {
            var registrations = new (string Name, HotkeyBinding Binding)[]
            {
                ("Показ панели", globalBinding),
                ("Следующий контекст", nextBinding),
                ("Предыдущий контекст", previousBinding),
                (TxtContext1Name.Text.TrimOrDefault("Контекст 1"), context1Binding),
                (TxtContext2Name.Text.TrimOrDefault("Контекст 2"), context2Binding),
                (TxtContext3Name.Text.TrimOrDefault("Контекст 3"), context3Binding),
                (TxtContext4Name.Text.TrimOrDefault("Контекст 4"), context4Binding)
            };

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
                $"Обнаружены конфликтующие горячие клавиши:\n{string.Join("\n", duplicates)}\n\nНазначьте разные сочетания.")
            {
                Owner = this
            }.ShowDialog();
            return false;
        }

        private void LoadSettings()
        {
            ChkCtrl.IsChecked = _settings.GlobalHotkeyCtrl;
            ChkAlt.IsChecked = _settings.GlobalHotkeyAlt;
            ChkShift.IsChecked = _settings.GlobalHotkeyShift;
            ChkWin.IsChecked = _settings.GlobalHotkeyWin;

            ChkShowPresetSearch.IsChecked = _settings.ShowPresetSearch;
            ChkShowPresetScreenshot.IsChecked = _settings.ShowPresetScreenshot;
            ChkShowPresetVideo.IsChecked = _settings.ShowPresetVideo;
            ChkShowPresetCalc.IsChecked = _settings.ShowPresetCalc;

            foreach (ComboBoxItem item in CmbKey.Items)
            {
                if (item.Tag?.ToString() == _settings.GlobalHotkeyKey)
                {
                    CmbKey.SelectedItem = item;
                    break;
                }
            }
            if (CmbKey.SelectedIndex < 0) CmbKey.SelectedIndex = 0;

            LoadHotkeyBinding(_settings.NextContextHotkey, ChkNextContextCtrl, ChkNextContextShift, ChkNextContextAlt, ChkNextContextWin, CmbNextContextKey);
            LoadHotkeyBinding(_settings.PreviousContextHotkey, ChkPrevContextCtrl, ChkPrevContextShift, ChkPrevContextAlt, ChkPrevContextWin, CmbPrevContextKey);
            LoadHotkeyBinding(_settings.Context1Hotkey, ChkContext1Ctrl, ChkContext1Shift, ChkContext1Alt, ChkContext1Win, CmbContext1Key);
            LoadHotkeyBinding(_settings.Context2Hotkey, ChkContext2Ctrl, ChkContext2Shift, ChkContext2Alt, ChkContext2Win, CmbContext2Key);
            LoadHotkeyBinding(_settings.Context3Hotkey, ChkContext3Ctrl, ChkContext3Shift, ChkContext3Alt, ChkContext3Win, CmbContext3Key);
            LoadHotkeyBinding(_settings.Context4Hotkey, ChkContext4Ctrl, ChkContext4Shift, ChkContext4Alt, ChkContext4Win, CmbContext4Key);

            // Умная зона активации
            EdgePicker.SelectedEdge = _settings.Edge;
            EdgePicker.PanelPercent = _settings.PanelSizePercent;
            EdgePicker.ActivationPercent = _settings.ActivationZoneSizePercent;

            CmbMonitor.Items.Clear();
            var screens = System.Windows.Forms.Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                CmbMonitor.Items.Add(new ComboBoxItem 
                { 
                    Content = $"Монитор {i + 1}{(screens[i].Primary ? " (Осн.)" : "")}", 
                    Tag = i 
                });
            }
            if (_settings.MonitorIndex >= 0 && _settings.MonitorIndex < screens.Length)
                CmbMonitor.SelectedIndex = _settings.MonitorIndex;
            else
                CmbMonitor.SelectedIndex = 0;

            SldZoneSize.Value = _settings.ActivationZoneSizePercent;
            TxtZoneSize.Text = $"{(int)SldZoneSize.Value}%";
            SldPanelSize.Value = _settings.PanelSizePercent;
            TxtPanelSize.Text = $"{(int)SldPanelSize.Value}%";
            SldDelay.Value = _settings.ActivationDelayMs;
            TxtDelay.Text = $"{(int)SldDelay.Value}";

            var contexts = _mainWindow.GetContextsSnapshot();
            if (contexts.Count >= 4)
            {
                TxtContext1Name.Text = contexts[0].Name;
                TxtContext2Name.Text = contexts[1].Name;
                TxtContext3Name.Text = contexts[2].Name;
                TxtContext4Name.Text = contexts[3].Name;
            }

            UpdateContextHotkeyLabels();
        }

        private void SldZoneSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtZoneSize != null) TxtZoneSize.Text = $"{(int)e.NewValue}%";
            if (EdgePicker != null) EdgePicker.ActivationPercent = e.NewValue;
        }

        private void SldPanelSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtPanelSize != null) TxtPanelSize.Text = $"{(int)e.NewValue}%";
            if (EdgePicker != null) EdgePicker.PanelPercent = e.NewValue;
        }

        private void SldDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtDelay != null) TxtDelay.Text = $"{(int)e.NewValue}";
        }

        private void ContextName_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateContextHotkeyLabels();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var globalBinding = BuildHotkeyBinding(ChkCtrl, ChkShift, ChkAlt, ChkWin, CmbKey);
            var nextBinding = BuildHotkeyBinding(ChkNextContextCtrl, ChkNextContextShift, ChkNextContextAlt, ChkNextContextWin, CmbNextContextKey);
            var previousBinding = BuildHotkeyBinding(ChkPrevContextCtrl, ChkPrevContextShift, ChkPrevContextAlt, ChkPrevContextWin, CmbPrevContextKey);
            var context1Binding = BuildHotkeyBinding(ChkContext1Ctrl, ChkContext1Shift, ChkContext1Alt, ChkContext1Win, CmbContext1Key);
            var context2Binding = BuildHotkeyBinding(ChkContext2Ctrl, ChkContext2Shift, ChkContext2Alt, ChkContext2Win, CmbContext2Key);
            var context3Binding = BuildHotkeyBinding(ChkContext3Ctrl, ChkContext3Shift, ChkContext3Alt, ChkContext3Win, CmbContext3Key);
            var context4Binding = BuildHotkeyBinding(ChkContext4Ctrl, ChkContext4Shift, ChkContext4Alt, ChkContext4Win, CmbContext4Key);

            if (!ValidateHotkeyBindings(globalBinding, nextBinding, previousBinding, context1Binding, context2Binding, context3Binding, context4Binding))
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
            _settings.Context1Hotkey = context1Binding;
            _settings.Context2Hotkey = context2Binding;
            _settings.Context3Hotkey = context3Binding;
            _settings.Context4Hotkey = context4Binding;

            _settings.ShowPresetSearch = ChkShowPresetSearch.IsChecked ?? false;
            _settings.ShowPresetScreenshot = ChkShowPresetScreenshot.IsChecked ?? false;
            _settings.ShowPresetVideo = ChkShowPresetVideo.IsChecked ?? false;
            _settings.ShowPresetCalc = ChkShowPresetCalc.IsChecked ?? false;

            // Сохранение настроек активации
            _settings.Edge = EdgePicker.SelectedEdge;
            
            if (CmbMonitor.SelectedItem is ComboBoxItem monitorItem)
                _settings.MonitorIndex = (int)(monitorItem.Tag ?? 0);

            _settings.ActivationZoneSizePercent = SldZoneSize.Value;
            _settings.PanelSizePercent = SldPanelSize.Value;
            _settings.ActivationDelayMs = (int)SldDelay.Value;

            var contextNames = new[]
            {
                TxtContext1Name.Text,
                TxtContext2Name.Text,
                TxtContext3Name.Text,
                TxtContext4Name.Text
            };

            for (int i = 0; i < _settings.Contexts.Count && i < contextNames.Length; i++)
            {
                _settings.Contexts[i].Name = string.IsNullOrWhiteSpace(contextNames[i])
                    ? $"Контекст {i + 1}"
                    : contextNames[i].Trim();
            }

            await _mainWindow.SaveAppSettings(_settings);
            _mainWindow.RefreshPanel();
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
}
