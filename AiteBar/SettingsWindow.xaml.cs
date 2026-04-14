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
        private static readonly string[] AllowedScriptExtensions = [".bat", ".cmd", ".ps1", ".py"];
        private string _selectedIcon = "\uef0d";
        private string _selectedFont = FontHelper.FluentKey;
        private string _selectedColor = "#E3E3E3";
        private string _selectedImagePath = "";
        private static readonly BrushConverter _brushConverter = new();
        private static readonly Brush _defaultInputBorderBrush = _brushConverter.ConvertFromString("#555555") as Brush ?? Brushes.Gray;
        private static readonly Brush _requiredErrorBorderBrush = _brushConverter.ConvertFromString("#E85A5A") as Brush ?? Brushes.OrangeRed;
        private readonly MainWindow _mainWindow;
        private readonly CustomElement? _editingElement = null;
        private List<CustomElement> _orderElements = [];
        private readonly Dictionary<int, string> _orderBaselineByBlock = [];
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
                UpdateActionUI();
                UpdateNamePlaceholderVisibility();
                CmbBlock.SelectedIndex = 0; 
            }

            InitializeOrderEditor();
            UpdateSaveButtonState();
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
            SetComboValue(CmbBlock, _editingElement.BlockId.ToString());

            SetComboValue(CmbBrowser, _editingElement.Browser.ToString());
            SetComboValue(CmbActionType, _editingElement.ActionType);
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
                "#3ABEFF",
                "#60A5FA",
                "#6366F1",
                "#8B5CF6",
                "#A855F7",
                "#22D3EE",
                "#34D399",
                "#A3E635",
                "#F59E0B",
                "#FB7185"
            ];
            GridColors.Children.Clear();
            foreach (var hex in colors)
            {
                var btn = new Button
                {
                    Background = _brushConverter.ConvertFromString(hex) as Brush ?? Brushes.White,
                    Width = 30,
                    Height = 30,
                    Margin = new Thickness(4),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btn.Click += (s, e) => { _selectedColor = hex; TxtHexColor.Text = hex; UpdatePreview(); };
                GridColors.Children.Add(btn);
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

        private void UpdatePreview()
        {
            if (PreviewIcon == null) return;

            if (!string.IsNullOrEmpty(_selectedImagePath) && File.Exists(_selectedImagePath))
            { 
                PreviewIcon.Visibility = Visibility.Collapsed;
                if (PreviewIcon.Parent is Grid parent)
                { 
                    var existingImage = parent.Children.OfType<Image>().FirstOrDefault();
                    if (existingImage == null)
                    { 
                        existingImage = new Image { Width = 32, Height = 32, Stretch = Stretch.Uniform };
                        parent.Children.Add(existingImage);
                    }
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_selectedImagePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    existingImage.Source = bitmap;
                    existingImage.Visibility = Visibility.Visible;
                }
            }
            else
            {
                PreviewIcon.Visibility = Visibility.Visible;
                PreviewIcon.Text = _selectedIcon;
                PreviewIcon.FontFamily = FontHelper.Resolve(_selectedFont);
                PreviewIcon.Foreground = _brushConverter.ConvertFromString(_selectedColor) as Brush ?? Brushes.White;
                if (PreviewIcon.Parent is Grid parent)
                { 
                    var existingImage = parent.Children.OfType<Image>().FirstOrDefault();
                    if (existingImage != null) existingImage.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void InitializeOrderEditor()
        {
            _orderElements = [.. _mainWindow.GetElementsSnapshot()];
            CaptureOrderBaseline(_orderElements);
            if (CmbOrderBlock.SelectedIndex < 0)
                CmbOrderBlock.SelectedIndex = 0;
            RefreshOrderList();
        }

        private void CmbOrderBlock_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshOrderList();
        }

        private void RefreshOrderList()
        {
            if (CmbOrderBlock.SelectedItem is not ComboBoxItem blockItem)
                return;

            int blockId = int.Parse(blockItem.Tag?.ToString() ?? "4");
            List<CustomElement> blockElements = [.. _orderElements.Where(x => x.BlockId == blockId)];

            LstOrderButtons.Items.Clear();
            foreach (var el in blockElements)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                
                if (!string.IsNullOrEmpty(el.ImagePath) && File.Exists(el.ImagePath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(el.ImagePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    row.Children.Add(new Image
                    {
                        Source = bitmap,
                        Width = 16, Height = 16,
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                else
                {
                    row.Children.Add(new TextBlock
                    {
                        Text = el.Icon,
                        FontFamily = FontHelper.Resolve(el.IconFont),
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                row.Children.Add(new TextBlock
                {
                    Text = el.Name,
                    VerticalAlignment = VerticalAlignment.Center
                });

                LstOrderButtons.Items.Add(new ListBoxItem { Content = row, Tag = el.Id });
            }

            if (LstOrderButtons.Items.Count > 0)
                LstOrderButtons.SelectedIndex = 0;

            UpdateOrderMoveButtonsState();
        }

        private void LstOrderButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateOrderMoveButtonsState();
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            MoveSelectedOrderItem(-1);
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            MoveSelectedOrderItem(1);
        }

        private void MoveSelectedOrderItem(int direction)
        {
            if (CmbOrderBlock.SelectedItem is not ComboBoxItem blockItem || LstOrderButtons.SelectedItem is not ListBoxItem selectedItem)
                return;

            int blockId = int.Parse(blockItem.Tag?.ToString() ?? "4");
            string selectedId = selectedItem.Tag?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selectedId))
                return;

            List<string> blockIds = [.. _orderElements.Where(x => x.BlockId == blockId).Select(x => x.Id)];
            int index = blockIds.FindIndex(x => x == selectedId);
            if (index < 0)
                return;

            int targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= blockIds.Count)
                return;

            (blockIds[index], blockIds[targetIndex]) = (blockIds[targetIndex], blockIds[index]);

            var byId = _orderElements
                .Where(x => x.BlockId == blockId)
                .ToDictionary(x => x.Id, x => x, StringComparer.Ordinal);

            List<CustomElement> reorderedBlock = [.. blockIds.Select(id => byId[id])];
            var result = new List<CustomElement>(_orderElements.Count);
            bool inserted = false;

            foreach (var item in _orderElements)
            {
                if (item.BlockId == blockId)
                {
                    if (!inserted)
                    {
                        result.AddRange(reorderedBlock);
                        inserted = true;
                    }
                    continue;
                }
                result.Add(item);
            }

            _orderElements = result;
            RefreshOrderList();
            LstOrderButtons.SelectedIndex = targetIndex;
            UpdateOrderMoveButtonsState();
        }

        private async void BtnSaveOrder_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
                button.IsEnabled = false;

            try
            {
                if (CmbOrderBlock.SelectedItem is not ComboBoxItem blockItem)
                    return;

                int blockId = int.Parse(blockItem.Tag?.ToString() ?? "4");
                List<CustomElement> latestSnapshot = [.. _mainWindow.GetElementsSnapshot()];
                string currentSignature = BuildBlockSignature(latestSnapshot, blockId);
                if (_orderBaselineByBlock.TryGetValue(blockId, out string? baselineSignature) &&
                    !string.Equals(baselineSignature, currentSignature, StringComparison.Ordinal))
                {
                    _orderElements = latestSnapshot;
                    CaptureOrderBaseline(_orderElements);
                    RefreshOrderList();
                    SetOrderStatus("Список изменился в другом окне. Обновлено, повторите действие.", isError: true);
                    return;
                }

                List<string> orderedIds = [.. _orderElements.Where(x => x.BlockId == blockId).Select(x => x.Id)];
                await _mainWindow.SaveBlockOrder((DockBlock)blockId, orderedIds);
                _orderElements = [.. _mainWindow.GetElementsSnapshot()];
                CaptureOrderBaseline(_orderElements);
                RefreshOrderList();
                SetOrderStatus("Порядок кнопок сохранен.");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                SetOrderStatus("Ошибка сохранения порядка.", isError: true);
                new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog();
            }
            finally
            {
                if (button != null)
                    button.IsEnabled = true;
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
                        BtnBrowse.Visibility = (actionType == AiteBar.ActionType.Exe || actionType == AiteBar.ActionType.ScriptFile)
                            ? Visibility.Visible : Visibility.Collapsed;
                        LblActionValue.Text = actionType switch
                        {
                            AiteBar.ActionType.Web => "Вставьте URL сайта (обязательно):",
                            AiteBar.ActionType.Command => "Введите консольную команду (обязательно):",
                            AiteBar.ActionType.Exe => "Укажите путь к файлу программы (обязательно):",
                            AiteBar.ActionType.ScriptFile => "Укажите путь к файлу скрипта (обязательно):",
                            _ => "Введите значение (обязательно):"
                        };
                        switch (actionType)
                        {
                            case AiteBar.ActionType.Web:
                                TxtActionPlaceholder.Text = "Вставьте URL сайта";
                                ActionHelpBox.Visibility = Visibility.Collapsed;
                                break;
                            case AiteBar.ActionType.Exe:
                                TxtActionPlaceholder.Text = @"Укажите путь к файлу программы";
                                ActionHelpBox.Visibility = Visibility.Collapsed;
                                break;
                            case AiteBar.ActionType.Command:
                                TxtActionPlaceholder.Text = "Введите консольную команду";
                                TxtActionHelp.Text = "Примеры:\ncmd, powershell, explorer, control, appwiz.cpl, ncpa.cpl, services.msc, taskmgr, regedit, msconfig\n\nPython-модуль (для систем, где установлен py):\ncd /d \"B:\\имя_проекта\" && py -m app.main";
                                ActionHelpBox.Visibility = Visibility.Visible;
                                break;
                            case AiteBar.ActionType.ScriptFile:
                                TxtActionPlaceholder.Text = "Укажите путь к файлу скрипта";
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
                    nameof(AiteBar.ActionType.Exe) => "Программы (*.exe;*.lnk)|*.exe;*.lnk|Все файлы (*.*)|*.*",
                    nameof(AiteBar.ActionType.ScriptFile) => "Скрипты (*.bat;*.cmd;*.ps1;*.py)|*.bat;*.cmd;*.ps1;*.py",
                    _ => "Все файлы (*.*)|*.*"
                }
            };

            if (typeStr == nameof(AiteBar.ActionType.Exe))
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
                if (string.IsNullOrEmpty(_selectedImagePath) && (_selectedIcon == "\uEF0D" || string.IsNullOrEmpty(_selectedIcon)))
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

        private void UpdateOrderMoveButtonsState()
        {
            if (BtnMoveUp == null || BtnMoveDown == null || LstOrderButtons == null)
                return;

            int count = LstOrderButtons.Items.Count;
            int index = LstOrderButtons.SelectedIndex;
            bool hasSelection = index >= 0 && index < count;

            BtnMoveUp.IsEnabled = hasSelection && index > 0;
            BtnMoveDown.IsEnabled = hasSelection && index < count - 1;
        }

        private static string BuildBlockSignature(IEnumerable<CustomElement> source, int blockId)
        {
            return string.Join("|", source.Where(x => x.BlockId == blockId).Select(x => x.Id));
        }

        private void CaptureOrderBaseline(IEnumerable<CustomElement> source)
        {
            _orderBaselineByBlock.Clear();
            _orderBaselineByBlock[(int)DockBlock.Utils] = BuildBlockSignature(source, (int)DockBlock.Utils);
            _orderBaselineByBlock[(int)DockBlock.AI] = BuildBlockSignature(source, (int)DockBlock.AI);
            _orderBaselineByBlock[(int)DockBlock.Web] = BuildBlockSignature(source, (int)DockBlock.Web);
            _orderBaselineByBlock[(int)DockBlock.Scripts] = BuildBlockSignature(source, (int)DockBlock.Scripts);
            _orderBaselineByBlock[(int)DockBlock.Other] = BuildBlockSignature(source, (int)DockBlock.Other);
        }

        private void SetOrderStatus(string text, bool isError = false)
        {
            if (TxtOrderStatus == null)
                return;

            TxtOrderStatus.Text = text;
            TxtOrderStatus.Visibility = string.IsNullOrWhiteSpace(text)
                ? Visibility.Collapsed
                : Visibility.Visible;
            TxtOrderStatus.Foreground = isError
                ? (_brushConverter.ConvertFromString("#E85A5A") as Brush ?? Brushes.OrangeRed)
                : (_brushConverter.ConvertFromString("#9FA7B3") as Brush ?? Brushes.Gray);
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
                            _ => "Путь к файлу"
                        });
                    }
                    new DarkDialog($"Заполните обязательные поля: {string.Join(", ", requiredFields)}.") { Owner = this }.ShowDialog();
                    return;
                }
                _showRequiredValidation = false;
                UpdateRequiredFieldsVisuals();

                if (actionType == AiteBar.ActionType.Exe && !File.Exists(TxtActionValue.Text))
                {
                    new DarkDialog("Файл программы или ярлыка не найден.") { Owner = this }.ShowDialog();
                    return;
                }

                if (actionType == AiteBar.ActionType.ScriptFile)
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
                    BlockId = int.Parse(((ComboBoxItem)CmbBlock.SelectedItem).Tag?.ToString() ?? "4"),
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
    }
}
