using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

// Псевдонимы
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using FontFamily = System.Windows.Media.FontFamily;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace SmartScreenDock
{
    public partial class SettingsWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private string _selectedIcon = "\uE710";
        private string _selectedColor = "#E3E3E3";
        private readonly MainWindow _mainWindow;
        private CustomElement? _editingElement = null;

        public SettingsWindow(MainWindow main, CustomElement? el = null)
        {
            InitializeComponent();
            _mainWindow = main;
            _editingElement = el;

            LoadIcons();
            LoadColors();
            LoadChromeProfiles();
            LoadKeyList();

            if (_editingElement != null) {
                this.Title = "Редактировать кнопку";
                LoadElementData();
            } else {
                UpdateActionUI();
                CmbBlock.SelectedIndex = 2; 
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int darkTheme = 1;
            DwmSetWindowAttribute(hwnd, 20, ref darkTheme, sizeof(int));
        }

        private void LogError(Exception ex)
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Codebdbd", "Aite Deck", "error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
            catch { }
        }

        private void LoadKeyList()
        {
            CmbKey.Items.Clear();
            CmbKey.Items.Add(new ComboBoxItem { Content = "Выберите клавишу...", Tag = "None" });
            for (char c = 'A'; c <= 'Z'; c++) CmbKey.Items.Add(new ComboBoxItem { Content = c.ToString(), Tag = c.ToString() });
            for (int i = 0; i <= 9; i++) CmbKey.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = "D" + i });
            for (int i = 1; i <= 12; i++) CmbKey.Items.Add(new ComboBoxItem { Content = "F" + i, Tag = "F" + i });
            CmbKey.SelectedIndex = 0;
        }

        private void LoadElementData()
        {
            TxtName.Text = _editingElement!.Name;
            TxtActionValue.Text = _editingElement.ActionValue;
            _selectedIcon = _editingElement.Icon;
            _selectedColor = _editingElement.Color;
            TxtHexColor.Text = _selectedColor;
            ChkAppMode.IsChecked = _editingElement.IsAppMode;
            ChkIncognito.IsChecked = _editingElement.IsIncognito;
            ChkTopmost.IsChecked = _editingElement.IsTopmost;
            ChkRotation.IsChecked = _editingElement.UseRotation;
            ChkCtrl.IsChecked = _editingElement.Ctrl;
            ChkShift.IsChecked = _editingElement.Shift;
            ChkAlt.IsChecked = _editingElement.Alt;
            ChkWin.IsChecked = _editingElement.Win;
            SetComboValue(CmbBlock, _editingElement.BlockId.ToString());
            SetComboValue(CmbActionType, _editingElement.ActionType);
            SetComboValue(CmbChromeProfile, _editingElement.ChromeProfile);
            SetComboValue(CmbKey, _editingElement.Key);
            UpdatePreview();
            UpdateActionUI();
        }

        private void SetComboValue(ComboBox combo, string value)
        {
            foreach (ComboBoxItem item in combo.Items) {
                if (item.Tag?.ToString() == value) { combo.SelectedItem = item; return; }
            }
        }

        private void LoadIcons()
        {
            string[] icons = { "\uE756", "\uE943", "\uE945", "\uEAF1", "\uE774", "\uE8C1", "\uE71B", "\uE8BD", "\uE8B7", "\uE70B", "\uE753", "\uE721", "\uE734", "\uEBE8", "\uE72C", "\uE715" };
            GridIcons.Children.Clear();
            foreach (var icon in icons) {
                var btn = new Button { Content = icon, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 20, Width = 38, Height = 38, Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, Style = (Style)FindResource("IconButtonStyle") };
                btn.Click += (s, e) => { _selectedIcon = icon; UpdatePreview(); };
                GridIcons.Children.Add(btn);
            }
        }

        private void LoadColors()
        {
            string[] colors = { "#E3E3E3", "#4285F4", "#FFD700", "#10A37F", "#D97757", "#FF5722", "#E91E63", "#9C27B0", "#00BCD4", "#8BC34A" };
            GridColors.Children.Clear();
            foreach (var hex in colors) {
                var btn = new Button { Background = new BrushConverter().ConvertFromString(hex) as Brush ?? Brushes.White, Width = 30, Height = 30, Margin = new Thickness(2), BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
                btn.Click += (s, e) => { _selectedColor = hex; TxtHexColor.Text = hex; UpdatePreview(); };
                GridColors.Children.Add(btn);
            }
        }

        private void TxtHexColor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try {
                var colorStr = TxtHexColor.Text.Trim();
                if (string.IsNullOrEmpty(colorStr)) return;
                if (!colorStr.StartsWith("#")) colorStr = "#" + colorStr;
                if (colorStr.Length == 7 || colorStr.Length == 9) { 
                    var brush = new BrushConverter().ConvertFromString(colorStr) as Brush;
                    if (brush != null) { _selectedColor = colorStr; UpdatePreview(); }
                }
            } catch (Exception ex) { LogError(ex); }
        }

        private void BtnOpenCatalog_Click(object sender, RoutedEventArgs e)
        {
            var picker = new IconPickerWindow { Owner = this };
            if (picker.ShowDialog() == true) { _selectedIcon = picker.SelectedIcon; UpdatePreview(); }
        }

        private void UpdatePreview()
        {
            if (PreviewIcon == null) return;
            PreviewIcon.Text = _selectedIcon;
            PreviewIcon.Foreground = new BrushConverter().ConvertFromString(_selectedColor) as Brush ?? Brushes.White;
        }

        private void LoadChromeProfiles()
        {
            string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data");
            CmbChromeProfile.Items.Clear();
            CmbChromeProfile.Items.Add(new ComboBoxItem { Content = "Без профиля", Tag = "" });
            if (!Directory.Exists(basePath)) return;
            var profileDirs = Directory.GetDirectories(basePath, "Profile *").ToList();
            profileDirs.Insert(0, Path.Combine(basePath, "Default"));
            foreach (var dir in profileDirs) {
                if (!Directory.Exists(dir)) continue;
                string prefFile = Path.Combine(dir, "Preferences"); string displayName = Path.GetFileName(dir);
                if (File.Exists(prefFile)) {
                    try {
                        using var stream = File.OpenRead(prefFile);
                        using var doc = JsonDocument.Parse(stream);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("account_info", out var accounts) &&
                            accounts.ValueKind == JsonValueKind.Array &&
                            accounts.GetArrayLength() > 0)
                        {
                            var first = accounts[0];
                            if (first.TryGetProperty("email", out var emailProp) &&
                                !string.IsNullOrWhiteSpace(emailProp.GetString()))
                            {
                                displayName = emailProp.GetString()!;
                            }
                        }
                        else if (root.TryGetProperty("profile", out var profile) &&
                                 profile.TryGetProperty("name", out var nameProp))
                        {
                            displayName = nameProp.GetString() ?? displayName;
                        }
                    } catch (Exception ex) { LogError(ex); }
                }
                CmbChromeProfile.Items.Add(new ComboBoxItem { Content = displayName, Tag = dir });
            }
            CmbChromeProfile.SelectedIndex = 0;
        }

        private void UpdateActionUI()
        {
            if (PanelHotkeyAction == null || PanelStandardAction == null || CmbActionType.SelectedItem == null) return;
            string type = ((ComboBoxItem)CmbActionType.SelectedItem).Tag?.ToString() ?? "Web";
            if (type == "Hotkey") { PanelStandardAction.Visibility = Visibility.Collapsed; PanelHotkeyAction.Visibility = Visibility.Visible; }
            else {
                PanelStandardAction.Visibility = Visibility.Visible; PanelHotkeyAction.Visibility = Visibility.Collapsed;
                PanelWebSettings.Visibility = type == "Web" ? Visibility.Visible : Visibility.Collapsed;
                BtnBrowse.Visibility = (type == "Exe" || type == "ScriptFile") ? Visibility.Visible : Visibility.Collapsed;
                if (type == "Web") LblActionValue.Text = "Ссылка (URL):";
                else if (type == "Command") LblActionValue.Text = "Команда:";
                else LblActionValue.Text = "Путь к файлу:";
            }
        }

        private void CmbActionType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateActionUI();
        private void BtnBrowse_Click(object sender, RoutedEventArgs e) { var dlg = new Microsoft.Win32.OpenFileDialog(); if (dlg.ShowDialog() == true) TxtActionValue.Text = dlg.FileName; }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string type = ((ComboBoxItem)CmbActionType.SelectedItem).Tag?.ToString() ?? "Web";
                string selectedKey = ((ComboBoxItem)CmbKey.SelectedItem)?.Tag?.ToString() ?? "None";
                if (type != "Hotkey" && (string.IsNullOrWhiteSpace(TxtName.Text) || string.IsNullOrWhiteSpace(TxtActionValue.Text))) {
                    new DarkDialog("Заполните поля!") { Owner = this }.ShowDialog();
                    return;
                }

                var newElement = new CustomElement {
                    Id = _editingElement?.Id ?? Guid.NewGuid().ToString(),
                    Name = TxtName.Text,
                    BlockId = int.Parse(((ComboBoxItem)CmbBlock.SelectedItem).Tag?.ToString() ?? "4"),
                    ActionType = type, ActionValue = type == "Hotkey" ? "" : TxtActionValue.Text,
                    Icon = _selectedIcon, Color = _selectedColor, ChromeProfile = ((ComboBoxItem)CmbChromeProfile.SelectedItem)?.Tag?.ToString() ?? "",
                    IsAppMode = ChkAppMode.IsChecked ?? false, IsIncognito = ChkIncognito.IsChecked ?? false,
                    UseRotation = ChkRotation.IsChecked ?? false, IsTopmost = ChkTopmost.IsChecked ?? false,
                    LastUsedProfile = _editingElement?.LastUsedProfile ?? "",
                    Ctrl = ChkCtrl.IsChecked ?? false, Shift = ChkShift.IsChecked ?? false, Alt = ChkAlt.IsChecked ?? false, Win = ChkWin.IsChecked ?? false, Key = selectedKey
                };

                await _mainWindow.SaveElement(newElement, _editingElement?.Id);
                Close();
            }
            catch (Exception ex)
            {
                LogError(ex);
                new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog();
            }
        }
    }
}
