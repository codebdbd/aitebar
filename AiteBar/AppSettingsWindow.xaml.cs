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

            LoadModifierList(CmbShowPanelModifier);
            LoadModifierList(CmbPanelModifier, includeMixed: true);
            LoadKeyList();
            LoadSettings();
        }

        private static void LoadModifierList(ComboBox combo, bool includeMixed = false)
        {
            combo.Items.Clear();
            combo.Items.Add(new ComboBoxItem { Content = "Не назначено", Tag = "None" });
            if (includeMixed)
            {
                combo.Items.Add(new ComboBoxItem { Content = "Смешанные", Tag = "Mixed" });
            }
            combo.Items.Add(new ComboBoxItem { Content = "Ctrl", Tag = "C" });
            combo.Items.Add(new ComboBoxItem { Content = "Alt", Tag = "A" });
            combo.Items.Add(new ComboBoxItem { Content = "Shift", Tag = "S" });
            combo.Items.Add(new ComboBoxItem { Content = "Win", Tag = "W" });
            combo.Items.Add(new ComboBoxItem { Content = "Ctrl + Alt", Tag = "CA" });
            combo.Items.Add(new ComboBoxItem { Content = "Ctrl + Shift", Tag = "CS" });
            combo.Items.Add(new ComboBoxItem { Content = "Alt + Shift", Tag = "AS" });
            combo.Items.Add(new ComboBoxItem { Content = "Ctrl + Win", Tag = "CW" });
            combo.Items.Add(new ComboBoxItem { Content = "Alt + Win", Tag = "AW" });
            combo.Items.Add(new ComboBoxItem { Content = "Ctrl + Alt + Shift", Tag = "CAS" });
            combo.SelectedIndex = 0;
        }

        private void LoadKeyList()
        {
            foreach (var combo in GetHotkeyCombos())
            {
                combo.Items.Clear();
                combo.Items.Add(new ComboBoxItem { Content = "Не назначено", Tag = "None" });
                combo.Items.Add(new ComboBoxItem { Content = "Space", Tag = "Space" });
                combo.Items.Add(new ComboBoxItem { Content = "[", Tag = "Oem4" });
                combo.Items.Add(new ComboBoxItem { Content = "]", Tag = "Oem6" });
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

        private static string GetModifierToken(bool ctrl, bool alt, bool shift, bool win)
        {
            if (!ctrl && !alt && !shift && !win) return "None";
            if (ctrl && !alt && !shift && !win) return "C";
            if (!ctrl && alt && !shift && !win) return "A";
            if (!ctrl && !alt && shift && !win) return "S";
            if (!ctrl && !alt && !shift && win) return "W";
            if (ctrl && alt && !shift && !win) return "CA";
            if (ctrl && !alt && shift && !win) return "CS";
            if (!ctrl && alt && shift && !win) return "AS";
            if (ctrl && !alt && !shift && win) return "CW";
            if (!ctrl && alt && !shift && win) return "AW";
            if (ctrl && alt && shift && !win) return "CAS";
            return "None";
        }

        private static void ApplyModifierToken(string? token, HotkeyBinding binding)
        {
            binding.Ctrl = false;
            binding.Alt = false;
            binding.Shift = false;
            binding.Win = false;

            switch (token)
            {
                case "C":
                    binding.Ctrl = true;
                    break;
                case "A":
                    binding.Alt = true;
                    break;
                case "S":
                    binding.Shift = true;
                    break;
                case "W":
                    binding.Win = true;
                    break;
                case "CA":
                    binding.Ctrl = true;
                    binding.Alt = true;
                    break;
                case "CS":
                    binding.Ctrl = true;
                    binding.Shift = true;
                    break;
                case "AS":
                    binding.Alt = true;
                    binding.Shift = true;
                    break;
                case "CW":
                    binding.Ctrl = true;
                    binding.Win = true;
                    break;
                case "AW":
                    binding.Alt = true;
                    binding.Win = true;
                    break;
                case "CAS":
                    binding.Ctrl = true;
                    binding.Alt = true;
                    binding.Shift = true;
                    break;
            }
        }

        private static void SetModifierComboValue(ComboBox combo, string token)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (string.Equals(item.Tag?.ToString(), token, StringComparison.Ordinal))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }

            combo.SelectedIndex = 0;
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

        private static void LoadHotkeyBinding(HotkeyBinding binding, ComboBox cmbModifier, ComboBox cmbKey)
        {
            SetModifierComboValue(cmbModifier, GetModifierToken(binding.Ctrl, binding.Alt, binding.Shift, binding.Win));
            SetKeyComboValue(cmbKey, binding.Key);
        }

        private static HotkeyBinding BuildHotkeyBinding(ComboBox cmbModifier, ComboBox cmbKey)
        {
            var binding = new HotkeyBinding();
            ApplyModifierToken((cmbModifier.SelectedItem as ComboBoxItem)?.Tag?.ToString(), binding);
            binding.Key = (cmbKey.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None";
            return binding;
        }

        private static HotkeyBinding BuildPanelActionHotkey(ComboBox cmbSharedModifier, ComboBox cmbKey, HotkeyBinding existingBinding)
        {
            var binding = new HotkeyBinding
            {
                Ctrl = existingBinding.Ctrl,
                Alt = existingBinding.Alt,
                Shift = existingBinding.Shift,
                Win = existingBinding.Win
            };

            binding.Key = (cmbKey.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "None";
            if (string.Equals(binding.Key, "None", StringComparison.OrdinalIgnoreCase))
            {
                binding.Ctrl = false;
                binding.Alt = false;
                binding.Shift = false;
                binding.Win = false;
                return binding;
            }

            var modifierToken = (cmbSharedModifier.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (!string.Equals(modifierToken, "Mixed", StringComparison.Ordinal))
            {
                ApplyModifierToken(modifierToken, binding);
            }

            return binding;
        }

        private string GetSharedPanelModifierToken()
        {
            var panelBindings = new[]
            {
                _settings.NextContextHotkey,
                _settings.PreviousContextHotkey,
                _settings.Context1Hotkey,
                _settings.Context2Hotkey,
                _settings.Context3Hotkey,
                _settings.Context4Hotkey
            };

            var modifierTokens = panelBindings
                .Where(HasAssignedKey)
                .Select(binding => GetModifierToken(binding.Ctrl, binding.Alt, binding.Shift, binding.Win))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (modifierTokens.Count == 0)
            {
                return "None";
            }

            return modifierTokens.Count == 1 ? modifierTokens[0] : "Mixed";
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
            return binding != null
                && !string.IsNullOrWhiteSpace(binding.Key)
                && !string.Equals(binding.Key, "None", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateContextHotkeyLabels()
        {
            if (LblContext1Hotkey == null)
            {
                return;
            }

            LblContext1Hotkey.Text = TxtContext1Name.Text.TrimOrDefault("Панель 1");
            LblContext2Hotkey.Text = TxtContext2Name.Text.TrimOrDefault("Панель 2");
            LblContext3Hotkey.Text = TxtContext3Name.Text.TrimOrDefault("Панель 3");
            LblContext4Hotkey.Text = TxtContext4Name.Text.TrimOrDefault("Панель 4");
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
                ("Показать панель", globalBinding),
                ("Следующая панель", nextBinding),
                ("Предыдущая панель", previousBinding),
                (TxtContext1Name.Text.TrimOrDefault("Панель 1"), context1Binding),
                (TxtContext2Name.Text.TrimOrDefault("Панель 2"), context2Binding),
                (TxtContext3Name.Text.TrimOrDefault("Панель 3"), context3Binding),
                (TxtContext4Name.Text.TrimOrDefault("Панель 4"), context4Binding)
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
            ChkShowPresetSearch.IsChecked = _settings.ShowPresetSearch;
            ChkShowPresetScreenshot.IsChecked = _settings.ShowPresetScreenshot;
            ChkShowPresetVideo.IsChecked = _settings.ShowPresetVideo;
            ChkShowPresetCalc.IsChecked = _settings.ShowPresetCalc;

            LoadHotkeyBinding(
                new HotkeyBinding
                {
                    Ctrl = _settings.GlobalHotkeyCtrl,
                    Alt = _settings.GlobalHotkeyAlt,
                    Shift = _settings.GlobalHotkeyShift,
                    Win = _settings.GlobalHotkeyWin,
                    Key = _settings.GlobalHotkeyKey
                },
                CmbShowPanelModifier,
                CmbKey);

            SetModifierComboValue(CmbPanelModifier, GetSharedPanelModifierToken());
            SetKeyComboValue(CmbNextContextKey, _settings.NextContextHotkey.Key);
            SetKeyComboValue(CmbPrevContextKey, _settings.PreviousContextHotkey.Key);
            SetKeyComboValue(CmbContext1Key, _settings.Context1Hotkey.Key);
            SetKeyComboValue(CmbContext2Key, _settings.Context2Hotkey.Key);
            SetKeyComboValue(CmbContext3Key, _settings.Context3Hotkey.Key);
            SetKeyComboValue(CmbContext4Key, _settings.Context4Hotkey.Key);

            CmbEdge.Items.Clear();
            CmbEdge.Items.Add(new ComboBoxItem { Content = "Сверху", Tag = DockEdge.Top });
            CmbEdge.Items.Add(new ComboBoxItem { Content = "Снизу", Tag = DockEdge.Bottom });
            CmbEdge.Items.Add(new ComboBoxItem { Content = "Слева", Tag = DockEdge.Left });
            CmbEdge.Items.Add(new ComboBoxItem { Content = "Справа", Tag = DockEdge.Right });

            foreach (ComboBoxItem item in CmbEdge.Items)
            {
                if (item.Tag is DockEdge edge && edge == _settings.Edge)
                {
                    CmbEdge.SelectedItem = item;
                    break;
                }
            }

            if (CmbEdge.SelectedIndex < 0)
            {
                CmbEdge.SelectedIndex = 0;
            }

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
        }

        private void SldPanelSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtPanelSize != null) TxtPanelSize.Text = $"{(int)e.NewValue}%";
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
            var globalBinding = BuildHotkeyBinding(CmbShowPanelModifier, CmbKey);
            var nextBinding = BuildPanelActionHotkey(CmbPanelModifier, CmbNextContextKey, _settings.NextContextHotkey);
            var previousBinding = BuildPanelActionHotkey(CmbPanelModifier, CmbPrevContextKey, _settings.PreviousContextHotkey);
            var context1Binding = BuildPanelActionHotkey(CmbPanelModifier, CmbContext1Key, _settings.Context1Hotkey);
            var context2Binding = BuildPanelActionHotkey(CmbPanelModifier, CmbContext2Key, _settings.Context2Hotkey);
            var context3Binding = BuildPanelActionHotkey(CmbPanelModifier, CmbContext3Key, _settings.Context3Hotkey);
            var context4Binding = BuildPanelActionHotkey(CmbPanelModifier, CmbContext4Key, _settings.Context4Hotkey);

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

            if (CmbEdge.SelectedItem is ComboBoxItem edgeItem && edgeItem.Tag is DockEdge edge)
            {
                _settings.Edge = edge;
            }
            
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
                    ? $"Панель {i + 1}"
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
