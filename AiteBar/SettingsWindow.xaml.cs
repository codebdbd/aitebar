using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Псевдонимы для устранения неоднозначности
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    public partial class SettingsWindow : DarkWindow
    {
        private string _selectedIcon = "\uE8B9";
        private string _selectedFont = "Segoe MDL2 Assets";
        private string _selectedColor = "#FFFFFF";
        private string _selectedImagePath = "";
        private static readonly BrushConverter _brushConverter = new();
        private static readonly Brush _defaultInputBorderBrush = _brushConverter.ConvertFromString("#555555") as Brush ?? Brushes.Gray;
        private static readonly Brush _requiredErrorBorderBrush = _brushConverter.ConvertFromString("#E85A5A") as Brush ?? Brushes.OrangeRed;
        private readonly MainWindow _mainWindow;
        private readonly CustomElement? _editingElement = null;
        private bool _showRequiredValidation;

        public SettingsWindow(MainWindow main, CustomElement? el = null)
        {
            InitializeComponent();
            _mainWindow = main;
            _editingElement = el;

            LoadColors();

            _ = LoadProfilesAsync().ContinueWith(
                t => Logger.Log(t.Exception!.GetBaseException()),
                TaskContinuationOptions.OnlyOnFaulted);
            LoadKeyList();

            if (_editingElement != null) {
                this.Title = "Редактировать кнопку";
                LoadElementData();
            } else {
                SetComboValue(CmbActionType, nameof(ActionType.Web));
                var defaultBrowser = BrowserHelper.GetSystemDefaultBrowser();
                SetComboValue(CmbBrowser, defaultBrowser.ToString());
                UpdateActionUI();
                UpdateNamePlaceholderVisibility();
            }

            UpdateSaveButtonState();
            UpdatePreview();
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
            _selectedImagePath = _editingElement.ImagePath;
            TxtHexColor.Text = _selectedColor;
            ChkAppMode.IsChecked = _editingElement.IsAppMode;
            ChkIncognito.IsChecked = _editingElement.IsIncognito;
            ChkTopmost.IsChecked = _editingElement.IsTopmost;
            ChkRotation.IsChecked = _editingElement.UseRotation;
            ChkCtrl.IsChecked = _editingElement.Ctrl;
            ChkShift.IsChecked = _editingElement.Shift;
            ChkAlt.IsChecked = _editingElement.Alt;
            ChkWin.IsChecked = _editingElement.Win;

            SetComboValue(CmbBrowser, _editingElement.Browser.ToString());
            SetComboValue(CmbActionType, ActionTargetHelper.NormalizeActionType(_editingElement.ActionType, _editingElement.ActionValue));
            SetComboValue(CmbChromeProfile, _editingElement.ChromeProfile);
            SetComboValue(CmbKey, _editingElement.Key);
            UpdatePreview();
            UpdateActionUI();
            UpdateNamePlaceholderVisibility();
        }

        private static void SetComboValue(ComboBox combo, string value)
        {
            foreach (ComboBoxItem item in combo.Items) {
                if (item.Tag?.ToString() == value) { combo.SelectedItem = item; return; }
            }
        }

        private void LoadColors()
        {
            string[] colors =
            [
                "#3ABEFF", "#60A5FA", "#6366F1", "#8B5CF6", "#A855F7",
                "#22D3EE", "#34D399", "#A3E635", "#F59E0B", "#FB7185"
            ];
            GridColors.Children.Clear();
            foreach (var hex in colors)
            {
                var border = new Border
                {
                    Background = _brushConverter.ConvertFromString(hex) as Brush ?? Brushes.White,
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(5),
                    CornerRadius = new CornerRadius(4),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = hex
                };
                border.MouseDown += (s, e) => { 
                    _selectedColor = hex; 
                    TxtHexColor.Text = hex; 
                    UpdatePreview(); 
                };
                GridColors.Children.Add(border);
            }
        }

        private void TxtHexColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            try {
                var colorStr = TxtHexColor.Text.Trim();
                if (string.IsNullOrEmpty(colorStr)) return;
                if (!colorStr.StartsWith('#')) colorStr = "#" + colorStr;
                if (colorStr.Length is 7 or 9) { 
                    if (_brushConverter.ConvertFromString(colorStr) is Brush brush) { _selectedColor = colorStr; UpdatePreview(); }
                }
            } catch (Exception ex) { Logger.Log(ex); }
        }

        private void BorderPreview_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            BtnOpenCatalog_Click(this, new RoutedEventArgs());
        }

        private void BtnOpenCatalog_Click(object sender, RoutedEventArgs e)
        {
            var picker = new IconPickerWindow { Owner = this };
            if (picker.ShowDialog() == true)
            {
                _selectedIcon = picker.SelectedIcon;
                _selectedFont = picker.SelectedFont;
                _selectedImagePath = picker.SelectedImagePath;
                UpdatePreview();
            }
        }

        private void BtnSelectCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico|Все файлы (*.*)|*.*",
                Title = "Выберите иконку"
            };

            if (dlg.ShowDialog() == true)
            {
                string? savedPath = IconHelper.SaveCustomIcon(dlg.FileName);
                if (!string.IsNullOrEmpty(savedPath))
                {
                    _selectedImagePath = savedPath;
                    _selectedIcon = ""; // Сбрасываем шрифтовую иконку
                    UpdatePreview();
                }
            }
        }

        private void UpdatePreview()
        {
            if (!string.IsNullOrEmpty(_selectedImagePath) && File.Exists(_selectedImagePath))
            { 
                PreviewIcon.Visibility = Visibility.Collapsed;
                PreviewImage.Visibility = Visibility.Visible;
                try
                { 
                    PreviewImage.Source = new BitmapImage(new Uri(_selectedImagePath));
                }
                catch { }
            }
            else
            { 
                PreviewIcon.Visibility = Visibility.Visible;
                 PreviewImage.Visibility = Visibility.Collapsed;
                 PreviewIcon.Text = _selectedIcon;
                 PreviewIcon.FontFamily = FontHelper.Resolve(_selectedFont);
                 PreviewIcon.Foreground = _brushConverter.ConvertFromString(_selectedColor) as Brush ?? Brushes.White;
            }
        }

        private async void CmbBrowser_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadProfilesAsync();
        }

        private async Task LoadProfilesAsync()
        {
            if (CmbBrowser == null || CmbChromeProfile == null) return;

            string browserStr = ((ComboBoxItem)CmbBrowser.SelectedItem)?.Tag?.ToString() ?? "Chrome";
            if (!Enum.TryParse<BrowserType>(browserStr, out var browserType)) browserType = BrowserType.Chrome;

            CmbChromeProfile.Items.Clear();
            CmbChromeProfile.Items.Add(new ComboBoxItem { Content = "Без профиля", Tag = "" });
            
            var profileItems = await Task.Run(() => BrowserHelper.GetProfiles(browserType));

            foreach (var profile in profileItems)
                CmbChromeProfile.Items.Add(new ComboBoxItem { Content = profile.DisplayName, Tag = profile.ProfilePath });
            
            CmbChromeProfile.SelectedIndex = 0;
            if (_editingElement != null && _editingElement.Browser == browserType)
                SetComboValue(CmbChromeProfile, _editingElement.ChromeProfile);
        }

        private void UpdateActionUI()
        {
            if (PanelHotkeyAction == null || PanelStandardAction == null || CmbActionType.SelectedItem == null || ActionHelpBox == null || TxtActionHelp == null || TxtActionPlaceholder == null || TxtActionValue == null) return;
            string typeStr = ((ComboBoxItem)CmbActionType.SelectedItem).Tag?.ToString() ?? "Web";
            if (Enum.TryParse<AiteBar.ActionType>(typeStr, out var actionType))
            {
                switch (actionType)
                {
                    case AiteBar.ActionType.Hotkey:
                        PanelStandardAction.Visibility = Visibility.Collapsed;
                        PanelHotkeyAction.Visibility = Visibility.Visible;
                        ActionHelpBox.Visibility = Visibility.Collapsed;
                        TxtActionPlaceholder.Visibility = Visibility.Collapsed;
                        break;
                    default:
                        PanelStandardAction.Visibility = Visibility.Visible;
                        PanelHotkeyAction.Visibility = Visibility.Collapsed;
                        PanelWebSettings.Visibility = actionType == AiteBar.ActionType.Web ? Visibility.Visible : Visibility.Collapsed;
                        BtnBrowse.Visibility = (actionType == AiteBar.ActionType.Program ||
                                                actionType == AiteBar.ActionType.File ||
                                                actionType == AiteBar.ActionType.Folder ||
                                                actionType == AiteBar.ActionType.ScriptFile)
                            ? Visibility.Visible : Visibility.Collapsed;
                        LblActionValue.Text = actionType switch
                        {
                            AiteBar.ActionType.Web => "Вставьте URL сайта (обязательно):",
                            AiteBar.ActionType.Program => "Укажите путь к программе или ярлыку (обязательно):",
                            AiteBar.ActionType.File => "Укажите путь к файлу (обязательно):",
                            AiteBar.ActionType.Folder => "Укажите путь к папке (обязательно):",
                            AiteBar.ActionType.Command => "Введите консольную команду (обязательно):",
                            AiteBar.ActionType.ScriptFile => "Укажите путь к файлу скрипта (обязательно):",
                            _ => "Введите значение (обязательно):"
                        };
                        switch (actionType)
                        {
                            case AiteBar.ActionType.Web:
                                TxtActionPlaceholder.Text = "Вставьте URL сайта";
                                ActionHelpBox.Visibility = Visibility.Collapsed;
                                break;
                            case AiteBar.ActionType.Program:
                                TxtActionPlaceholder.Text = @"Укажите путь к .exe, .lnk или .appref-ms";
                                TxtActionHelp.Text = "Для программ и ярлыков. Поддерживаются .exe, .lnk и .appref-ms.";
                                ActionHelpBox.Visibility = Visibility.Visible;
                                break;
                            case AiteBar.ActionType.File:
                                TxtActionPlaceholder.Text = @"Укажите путь к обычному файлу";
                                TxtActionHelp.Text = "Для документов и других файлов, кроме скриптов. Скрипты используйте через тип \"Скрипт\".";
                                ActionHelpBox.Visibility = Visibility.Visible;
                                break;
                            case AiteBar.ActionType.Folder:
                                TxtActionPlaceholder.Text = @"Укажите путь к папке";
                                TxtActionHelp.Text = "Открывает выбранную папку в Проводнике Windows.";
                                ActionHelpBox.Visibility = Visibility.Visible;
                                break;
                            case AiteBar.ActionType.Command:
                                TxtActionPlaceholder.Text = "Введите консольную команду";
                                TxtActionHelp.Text = "Для продвинутых пользователей.\nПримеры:\ncmd, powershell, explorer, control, appwiz.cpl, ncpa.cpl, services.msc, taskmgr, regedit, msconfig\n\nPython-модуль (для систем, где установлен py):\ncd /d \"B:\\имя_проекта\" && py -m app.main";
                                ActionHelpBox.Visibility = Visibility.Visible;
                                break;
                            case AiteBar.ActionType.ScriptFile:
                                TxtActionPlaceholder.Text = "Укажите путь к файлу скрипта";
                                TxtActionHelp.Text = "Для продвинутых пользователей.\nПоддерживаются .bat, .cmd, .ps1 и standalone .py.\nДля модульных Python-проектов используйте тип \"Команда\".";
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
            UpdateRequiredFieldsVisuals();
            UpdateSaveButtonState();
        }

        private void CmbActionType_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActionUI();
        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateNamePlaceholderVisibility();
            UpdateRequiredFieldsVisuals();
            UpdateSaveButtonState();
        }
        private void TxtActionValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateActionPlaceholderVisibility();
            UpdateRequiredFieldsVisuals();
            UpdateSaveButtonState();
        }

        private static string? FindExecutableOnPath(string fileName)
        {
            string? pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
                return null;

            foreach (string dir in pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), fileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                }
            }

            return null;
        }
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            string typeStr = ((ComboBoxItem)CmbActionType.SelectedItem).Tag?.ToString() ?? "Web";
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = typeStr switch
                {
                    nameof(AiteBar.ActionType.Program) => "Программы (*.exe;*.lnk;*.appref-ms)|*.exe;*.lnk;*.appref-ms|Все файлы (*.*)|*.*",
                    nameof(AiteBar.ActionType.File) => "Файлы|*.*",
                    nameof(AiteBar.ActionType.ScriptFile) => "Скрипты (*.bat;*.cmd;*.ps1;*.py)|*.bat;*.cmd;*.ps1;*.py",
                    _ => "Все файлы (*.*)|*.*"
                }
            };

            if (typeStr == nameof(AiteBar.ActionType.Folder))
            {
                using var dlgFolder = new System.Windows.Forms.FolderBrowserDialog();
                if (!string.IsNullOrWhiteSpace(TxtActionValue.Text) && Directory.Exists(TxtActionValue.Text))
                    dlgFolder.SelectedPath = TxtActionValue.Text;

                if (dlgFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtActionValue.Text = dlgFolder.SelectedPath;
                    if (string.IsNullOrWhiteSpace(TxtName.Text))
                        TxtName.Text = Path.GetFileName(dlgFolder.SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                }
                return;
            }

            if (typeStr == nameof(AiteBar.ActionType.Program))
            {
                if (!string.IsNullOrWhiteSpace(TxtActionValue.Text))
                {
                    string? existingDir = Path.GetDirectoryName(TxtActionValue.Text);
                    if (!string.IsNullOrWhiteSpace(existingDir) && Directory.Exists(existingDir))
                        dlg.InitialDirectory = existingDir;
                }

                if (string.IsNullOrWhiteSpace(dlg.InitialDirectory))
                {
                    string programFilesX64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    if (Directory.Exists(programFilesX64))
                        dlg.InitialDirectory = programFilesX64;
                    else if (Directory.Exists(programFilesX86))
                        dlg.InitialDirectory = programFilesX86;
                }
            }

            if (dlg.ShowDialog() == true)
            {
                TxtActionValue.Text = dlg.FileName;
                
                // Если имя пустое - подставляем имя файла
                if (string.IsNullOrWhiteSpace(TxtName.Text))
                {
                    TxtName.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
                }

                // Если иконка еще не выбрана (пустое изображение и дефолтный шрифт) - пытаемся извлечь
                if ((typeStr == nameof(AiteBar.ActionType.Program) || typeStr == nameof(AiteBar.ActionType.ScriptFile)) &&
                    string.IsNullOrEmpty(_selectedImagePath) &&
                    (_selectedIcon == "\uEF0D" || string.IsNullOrEmpty(_selectedIcon)))
                {
                    string? extracted = IconHelper.ExtractAndSaveIcon(dlg.FileName);
                    if (!string.IsNullOrEmpty(extracted))
                    {
                        _selectedImagePath = extracted;
                        _selectedIcon = "";
                        UpdatePreview();
                    }
                }
            }
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

        private AiteBar.ActionType GetSelectedActionType()
        {
            string typeStr = ((ComboBoxItem)CmbActionType.SelectedItem).Tag?.ToString() ?? "Web";
            if (!Enum.TryParse<AiteBar.ActionType>(typeStr, out var actionType))
                actionType = AiteBar.ActionType.Web;
            return actionType;
        }

        private void UpdateRequiredFieldsVisuals()
        {
            if (TxtName == null || TxtActionValue == null || CmbActionType?.SelectedItem == null)
                return;

            if (!_showRequiredValidation)
            {
                TxtName.BorderBrush = _defaultInputBorderBrush;
                TxtActionValue.BorderBrush = _defaultInputBorderBrush;
                return;
            }

            var actionType = GetSelectedActionType();
            bool missingName = string.IsNullOrWhiteSpace(TxtName.Text);
            bool missingActionValue = actionType != AiteBar.ActionType.Hotkey && string.IsNullOrWhiteSpace(TxtActionValue.Text);

            TxtName.BorderBrush = missingName ? _requiredErrorBorderBrush : _defaultInputBorderBrush;
            TxtActionValue.BorderBrush = missingActionValue ? _requiredErrorBorderBrush : _defaultInputBorderBrush;
        }

        private bool HasRequiredFieldsFilled()
        {
            if (TxtName == null || CmbActionType?.SelectedItem == null)
                return false;

            var actionType = GetSelectedActionType();
            bool hasName = !string.IsNullOrWhiteSpace(TxtName.Text);
            bool hasActionValue = actionType == AiteBar.ActionType.Hotkey || !string.IsNullOrWhiteSpace(TxtActionValue.Text);
            return hasName && hasActionValue;
        }

        private void UpdateSaveButtonState()
        {
            if (BtnSave == null)
                return;

            BtnSave.IsEnabled = HasRequiredFieldsFilled();
        }

        private void UpdateNamePlaceholderVisibility()
        {
            if (TxtNamePlaceholder == null || TxtName == null)
                return;

            TxtNamePlaceholder.Visibility = string.IsNullOrEmpty(TxtName.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button saveButton) saveButton.IsEnabled = false;
            try
            {
                var actionType = GetSelectedActionType();
                string typeStr = actionType.ToString();
                string selectedKey = ((ComboBoxItem)CmbKey.SelectedItem)?.Tag?.ToString() ?? "None";
                string browserStr = ((ComboBoxItem)CmbBrowser.SelectedItem)?.Tag?.ToString() ?? "Chrome";
                if (!Enum.TryParse<BrowserType>(browserStr, out var browserType)) browserType = BrowserType.Chrome;

                bool missingName = string.IsNullOrWhiteSpace(TxtName.Text);
                bool missingActionValue = actionType != AiteBar.ActionType.Hotkey && string.IsNullOrWhiteSpace(TxtActionValue.Text);
                if (missingName || missingActionValue)
                {
                    _showRequiredValidation = true;
                    UpdateRequiredFieldsVisuals();
                    var requiredFields = new List<string>();
                    if (missingName) requiredFields.Add("Имя");
                    if (missingActionValue)
                    {
                        requiredFields.Add(actionType switch
                        {
                            AiteBar.ActionType.Web => "URL",
                            AiteBar.ActionType.Command => "Команда",
                            AiteBar.ActionType.Folder => "Путь к папке",
                            _ => "Путь к файлу"
                        });
                    }
                    new DarkDialog($"Заполните обязательные поля: {string.Join(", ", requiredFields)}.") { Owner = this }.ShowDialog();
                    return;
                }
                _showRequiredValidation = false;
                UpdateRequiredFieldsVisuals();

                if (actionType == AiteBar.ActionType.Program)
                {
                    if (!File.Exists(TxtActionValue.Text))
                    {
                        new DarkDialog("Файл программы или ярлыка не найден.") { Owner = this }.ShowDialog();
                        return;
                    }

                    if (!ActionTargetHelper.IsProgramPath(TxtActionValue.Text))
                    {
                        new DarkDialog("Для типа \"Программа\" поддерживаются только .exe, .lnk и .appref-ms.") { Owner = this }.ShowDialog();
                        return;
                    }
                }

                if (actionType == AiteBar.ActionType.File)
                {
                    if (!File.Exists(TxtActionValue.Text))
                    {
                        new DarkDialog("Файл не найден.") { Owner = this }.ShowDialog();
                        return;
                    }

                    if (!ActionTargetHelper.IsRegularFilePath(TxtActionValue.Text))
                    {
                        new DarkDialog("Для типа \"Файл\" подходят обычные файлы, кроме программ и скриптов.") { Owner = this }.ShowDialog();
                        return;
                    }
                }

                if (actionType == AiteBar.ActionType.Folder)
                {
                    if (!Directory.Exists(TxtActionValue.Text))
                    {
                        new DarkDialog("Папка не найдена.") { Owner = this }.ShowDialog();
                        return;
                    }
                }

                if (actionType == AiteBar.ActionType.ScriptFile)
                {
                    if (!File.Exists(TxtActionValue.Text))
                    {
                        new DarkDialog("Файл скрипта не найден.") { Owner = this }.ShowDialog();
                        return;
                    }

                    if (!ActionTargetHelper.IsScriptPath(TxtActionValue.Text))
                    {
                        new DarkDialog("Поддерживаются только .bat, .cmd, .ps1 и .py.") { Owner = this }.ShowDialog();
                        return;
                    }

                    if (string.Equals(Path.GetExtension(TxtActionValue.Text), ".py", StringComparison.OrdinalIgnoreCase) &&
                        FindExecutableOnPath("python.exe") == null)
                    {
                        new DarkDialog("Для запуска .py требуется установленный python.exe в PATH.") { Owner = this }.ShowDialog();
                        return;
                    }
                }

                var newElement = new CustomElement {
                    Id = _editingElement?.Id ?? Guid.NewGuid().ToString(),
                    Name = TxtName.Text,
                    Browser = browserType,
                    ActionType = typeStr, ActionValue = actionType == AiteBar.ActionType.Hotkey ? "" : TxtActionValue.Text,
                    Icon = _selectedIcon, IconFont = _selectedFont, Color = _selectedColor, 
                    ImagePath = _selectedImagePath,
                    ChromeProfile = ((ComboBoxItem)CmbChromeProfile.SelectedItem)?.Tag?.ToString() ?? "",
                    IsAppMode = ChkAppMode.IsChecked ?? false, IsIncognito = ChkIncognito.IsChecked ?? false,
                    UseRotation = ChkRotation.IsChecked ?? false, IsTopmost = ChkTopmost.IsChecked ?? false,
                    LastUsedProfile = _editingElement?.LastUsedProfile ?? "",
                    Ctrl = ChkCtrl.IsChecked ?? false, Shift = ChkShift.IsChecked ?? false, Alt = ChkAlt.IsChecked ?? false, Win = ChkWin.IsChecked ?? false, Key = selectedKey
                };

                await _mainWindow.SaveElement(newElement, _editingElement?.Id);
                this.DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog();
            }
            finally
            {
                UpdateSaveButtonState();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }
    }
}
