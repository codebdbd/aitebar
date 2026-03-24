using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
    public partial class SettingsWindow : DarkWindow
    {
        private static readonly string[] AllowedScriptExtensions = [".bat", ".cmd", ".ps1", ".py"];
        private string _selectedIcon = "\ue710";
        private string _selectedFont = FontHelper.FluentKey;
        private string _selectedColor = "#E3E3E3";
        private static readonly BrushConverter _brushConverter = new();
        private readonly MainWindow _mainWindow;
        private CustomElement? _editingElement = null;

        public SettingsWindow(MainWindow main, CustomElement? el = null)
        {
            InitializeComponent();
            _mainWindow = main;
            _editingElement = el;

            LoadColors();

            _ = LoadChromeProfilesAsync().ContinueWith(
                t => Logger.Log(t.Exception!.GetBaseException()),
                TaskContinuationOptions.OnlyOnFaulted);
            LoadKeyList();

            if (_editingElement != null) {
                this.Title = "Редактировать кнопку";
                LoadElementData();
            } else {
                UpdateActionUI();
                CmbBlock.SelectedIndex = 0; 
            }
        }

        private void LoadKeyList()
        {
            CmbKey.Items.Clear();
            CmbKey.Items.Add(new ComboBoxItem { Content = "Выберите клавишу...", Tag = "None" });
            for (char c = 'A'; c <= 'Z'; c++) CmbKey.Items.Add(new ComboBoxItem { Content = c.ToString(), Tag = c.ToString() });
            for (int i = 0; i <= 9; i++) CmbKey.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = "D" + i });
            for (int i = 1; i <= 12; i++) CmbKey.Items.Add(new ComboBoxItem { Content = "F" + i, Tag = "F" + i });
            CmbKey.Items.Add(new ComboBoxItem { Content = "PrntSc", Tag = "PrintScreen" });
            CmbKey.SelectedIndex = 0;
        }

        private void LoadElementData()
        {
            TxtName.Text = _editingElement!.Name;
            TxtActionValue.Text = _editingElement.ActionValue;
            _selectedIcon = _editingElement.Icon;
            _selectedFont = _editingElement.IconFont;
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

        private void LoadColors()
        {
            string[] colors = { "#E3E3E3", "#4285F4", "#FFD700", "#10A37F", "#D97757", "#FF5722", "#E91E63", "#9C27B0", "#00BCD4", "#8BC34A" };
            GridColors.Children.Clear();
            foreach (var hex in colors) {
                var btn = new Button { Background = _brushConverter.ConvertFromString(hex) as Brush ?? Brushes.White, Width = 30, Height = 30, Margin = new Thickness(2), BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
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
                    var brush = _brushConverter.ConvertFromString(colorStr) as Brush;
                    if (brush != null) { _selectedColor = colorStr; UpdatePreview(); }
                }
            } catch (Exception ex) { Logger.Log(ex); }
        }

        private void BtnOpenCatalog_Click(object sender, RoutedEventArgs e)
        {
            var picker = new IconPickerWindow { Owner = this };
            if (picker.ShowDialog() == true)
            {
                _selectedIcon = picker.SelectedIcon;
                _selectedFont = picker.SelectedFont;
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            if (PreviewIcon == null) return;
            PreviewIcon.Text = _selectedIcon;
            PreviewIcon.FontFamily = FontHelper.Resolve(_selectedFont);
            PreviewIcon.Foreground = _brushConverter.ConvertFromString(_selectedColor) as Brush ?? Brushes.White;
        }

        private async Task LoadChromeProfilesAsync()
        {
            string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data");
            CmbChromeProfile.Items.Clear();
            CmbChromeProfile.Items.Add(new ComboBoxItem { Content = "Без профиля", Tag = "" });
            if (!Directory.Exists(basePath)) return;

            var profileItems = await Task.Run(() =>
            {
                var result = new List<(string displayName, string dirPath)>();
                var profileDirs = Directory.GetDirectories(basePath, "Profile *").ToList();
                profileDirs.Insert(0, Path.Combine(basePath, "Default"));

                foreach (var dir in profileDirs)
                {
                    if (!Directory.Exists(dir)) continue;
                    string prefFile = Path.Combine(dir, "Preferences");
                    string displayName = Path.GetFileName(dir);

                    if (File.Exists(prefFile))
                    {
                        try
                        {
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
                        }
                        catch (Exception ex) { Logger.Log(ex); }
                    }

                    result.Add((displayName, dir));
                }
                return result;
            });

            foreach (var (displayName, dir) in profileItems.OrderBy(p => p.displayName))
                CmbChromeProfile.Items.Add(new ComboBoxItem { Content = displayName, Tag = dir });
            CmbChromeProfile.SelectedIndex = 0;
            if (_editingElement != null)
                SetComboValue(CmbChromeProfile, _editingElement.ChromeProfile);
        }

        private void UpdateActionUI()
        {
            if (PanelHotkeyAction == null || PanelStandardAction == null || CmbActionType.SelectedItem == null || ActionHelpBox == null || TxtActionHelp == null || TxtActionPlaceholder == null || TxtActionValue == null) return;
            string typeStr = ((ComboBoxItem)CmbActionType.SelectedItem).Tag?.ToString() ?? "Web";
            if (Enum.TryParse<SmartScreenDock.ActionType>(typeStr, out var actionType))
            {
                switch (actionType)
                {
                    case SmartScreenDock.ActionType.Hotkey:
                        PanelStandardAction.Visibility = Visibility.Collapsed;
                        PanelHotkeyAction.Visibility = Visibility.Visible;
                        ActionHelpBox.Visibility = Visibility.Collapsed;
                        TxtActionPlaceholder.Visibility = Visibility.Collapsed;
                        break;
                    default:
                        PanelStandardAction.Visibility = Visibility.Visible;
                        PanelHotkeyAction.Visibility = Visibility.Collapsed;
                        PanelWebSettings.Visibility = actionType == SmartScreenDock.ActionType.Web ? Visibility.Visible : Visibility.Collapsed;
                        BtnBrowse.Visibility = (actionType == SmartScreenDock.ActionType.Exe || actionType == SmartScreenDock.ActionType.ScriptFile)
                            ? Visibility.Visible : Visibility.Collapsed;
                        LblActionValue.Text = actionType switch {
                            SmartScreenDock.ActionType.Web => "URL:",
                            SmartScreenDock.ActionType.Command => "Команда:",
                            _ => "Путь к файлу:"
                        };
                        switch (actionType)
                        {
                            case SmartScreenDock.ActionType.Web:
                                TxtActionPlaceholder.Text = "https://example.com";
                                ActionHelpBox.Visibility = Visibility.Collapsed;
                                break;
                            case SmartScreenDock.ActionType.Exe:
                                TxtActionPlaceholder.Text = "calc.exe";
                                ActionHelpBox.Visibility = Visibility.Collapsed;
                                break;
                            case SmartScreenDock.ActionType.Command:
                                TxtActionPlaceholder.Text = "cmd";
                                TxtActionHelp.Text = "Примеры:\ncmd, powershell, explorer, control, appwiz.cpl, ncpa.cpl, services.msc, taskmgr, regedit, msconfig\n\nPython-модуль:\ncd /d \"B:\\имя_проекта\" && py -m app.main";
                                ActionHelpBox.Visibility = Visibility.Visible;
                                break;
                            case SmartScreenDock.ActionType.ScriptFile:
                                TxtActionPlaceholder.Text = "script.bat";
                                TxtActionHelp.Text = "Поддерживаются .bat, .cmd, .ps1 и standalone .py.\nДля модульных Python-проектов используйте тип \"Консольная команда\".";
                                ActionHelpBox.Visibility = Visibility.Visible;
                                break;
                            default:
                                TxtActionPlaceholder.Text = "";
                                ActionHelpBox.Visibility = Visibility.Collapsed;
                                break;
                        }
                        UpdateActionPlaceholderVisibility();
                        break;
                }
            }
        }

        private void CmbActionType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateActionUI();
        private void TxtActionValue_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateActionPlaceholderVisibility();
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            string typeStr = ((ComboBoxItem)CmbActionType.SelectedItem).Tag?.ToString() ?? "Web";
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = typeStr switch
                {
                    nameof(SmartScreenDock.ActionType.Exe) => "Программы (*.exe)|*.exe|Все файлы (*.*)|*.*",
                    nameof(SmartScreenDock.ActionType.ScriptFile) => "Скрипты (*.bat;*.cmd;*.ps1;*.py)|*.bat;*.cmd;*.ps1;*.py",
                    _ => "Все файлы (*.*)|*.*"
                }
            };

            if (dlg.ShowDialog() == true)
                TxtActionValue.Text = dlg.FileName;
        }

        private static bool IsAllowedScriptFile(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return AllowedScriptExtensions.Contains(extension);
        }

        private void UpdateActionPlaceholderVisibility()
        {
            if (TxtActionPlaceholder == null || TxtActionValue == null)
                return;

            TxtActionPlaceholder.Visibility =
                string.IsNullOrWhiteSpace(TxtActionPlaceholder.Text) || !string.IsNullOrEmpty(TxtActionValue.Text)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var saveButton = sender as Button;
            if (saveButton != null) saveButton.IsEnabled = false;
            try
            {
                string typeStr = ((ComboBoxItem)CmbActionType.SelectedItem).Tag?.ToString() ?? "Web";
                if (!Enum.TryParse<SmartScreenDock.ActionType>(typeStr, out var actionType))
                    actionType = SmartScreenDock.ActionType.Web;
                string selectedKey = ((ComboBoxItem)CmbKey.SelectedItem)?.Tag?.ToString() ?? "None";
                if (actionType != SmartScreenDock.ActionType.Hotkey && (string.IsNullOrWhiteSpace(TxtName.Text) || string.IsNullOrWhiteSpace(TxtActionValue.Text))) {
                    new DarkDialog("Заполните поля!") { Owner = this }.ShowDialog();
                    return;
                }

                if (actionType == SmartScreenDock.ActionType.ScriptFile)
                {
                    if (!File.Exists(TxtActionValue.Text))
                    {
                        new DarkDialog("Файл скрипта не найден.") { Owner = this }.ShowDialog();
                        return;
                    }

                    if (!IsAllowedScriptFile(TxtActionValue.Text))
                    {
                        new DarkDialog("Поддерживаются только .bat, .cmd, .ps1 и .py.") { Owner = this }.ShowDialog();
                        return;
                    }
                }

                var newElement = new CustomElement {
                    Id = _editingElement?.Id ?? Guid.NewGuid().ToString(),
                    Name = TxtName.Text,
                    BlockId = int.Parse(((ComboBoxItem)CmbBlock.SelectedItem).Tag?.ToString() ?? "4"),
                    ActionType = typeStr, ActionValue = actionType == SmartScreenDock.ActionType.Hotkey ? "" : TxtActionValue.Text,
                    Icon = _selectedIcon, IconFont = _selectedFont, Color = _selectedColor, ChromeProfile = ((ComboBoxItem)CmbChromeProfile.SelectedItem)?.Tag?.ToString() ?? "",
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
                Logger.Log(ex);
                new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog();
            }
            finally
            {
                if (saveButton != null) saveButton.IsEnabled = true;
            }
        }
    }
}
