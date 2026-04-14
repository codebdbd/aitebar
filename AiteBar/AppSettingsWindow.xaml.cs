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

    public class ProfileIconConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string icon = values[0]?.ToString() ?? "";
            string imagePath = values[2]?.ToString() ?? "";
            
            if (!string.IsNullOrEmpty(imagePath) && System.IO.File.Exists(imagePath))
            {
                return new System.Windows.Media.Imaging.BitmapImage(new Uri(imagePath));
            }
            return icon;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
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
            CmbKey.Items.Clear();
            for (char c = 'A'; c <= 'Z'; c++) CmbKey.Items.Add(new ComboBoxItem { Content = c.ToString(), Tag = c.ToString() });
            for (int i = 0; i <= 9; i++) CmbKey.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = "D" + i });
            for (int i = 1; i <= 12; i++) CmbKey.Items.Add(new ComboBoxItem { Content = "F" + i, Tag = "F" + i });
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

            // Умная зона активации
            foreach (ComboBoxItem item in CmbEdge.Items)
            {
                if (item.Tag?.ToString() == _settings.Edge.ToString())
                {
                    CmbEdge.SelectedItem = item;
                    break;
                }
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
            SldDelay.Value = _settings.ActivationDelayMs;
            TxtDelay.Text = $"{(int)SldDelay.Value}";

            RefreshProfilesList();
        }

        private void SldZoneSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtZoneSize != null) TxtZoneSize.Text = $"{(int)e.NewValue}%";
        }

        private void SldDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtDelay != null) TxtDelay.Text = $"{(int)e.NewValue}";
        }

        private void RefreshProfilesList()
        {
            LstProfiles.ItemsSource = null;
            LstProfiles.ItemsSource = _settings.Profiles;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _settings.GlobalHotkeyCtrl = ChkCtrl.IsChecked ?? false;
            _settings.GlobalHotkeyAlt = ChkAlt.IsChecked ?? false;
            _settings.GlobalHotkeyShift = ChkShift.IsChecked ?? false;
            _settings.GlobalHotkeyWin = ChkWin.IsChecked ?? false;
            _settings.GlobalHotkeyKey = (CmbKey.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Z";

            _settings.ShowPresetSearch = ChkShowPresetSearch.IsChecked ?? false;
            _settings.ShowPresetScreenshot = ChkShowPresetScreenshot.IsChecked ?? false;
            _settings.ShowPresetVideo = ChkShowPresetVideo.IsChecked ?? false;
            _settings.ShowPresetCalc = ChkShowPresetCalc.IsChecked ?? false;

            // Сохранение настроек активации
            if (CmbEdge.SelectedItem is ComboBoxItem edgeItem && Enum.TryParse<DockEdge>(edgeItem.Tag?.ToString(), out var edge))
                _settings.Edge = edge;
            
            if (CmbMonitor.SelectedItem is ComboBoxItem monitorItem)
                _settings.MonitorIndex = (int)(monitorItem.Tag ?? 0);

            _settings.ActivationZoneSizePercent = SldZoneSize.Value;
            _settings.ActivationDelayMs = (int)SldDelay.Value;

            await _mainWindow.SaveAppSettings(_settings);
            _mainWindow.RefreshPanel();
            this.Close();
        }

        private async void BtnAddProfile_Click(object sender, RoutedEventArgs e)
        {
            var editWin = new ProfileEditWindow { Owner = this };
            if (editWin.ShowDialog() == true)
            {
                var profile = new Profile { 
                    Name = editWin.ProfileName, 
                    Icon = editWin.ProfileIcon, 
                    IconFont = editWin.ProfileFont,
                    IconColor = editWin.ProfileColor,
                    ImagePath = editWin.ProfileImagePath
                };
                _settings.Profiles.Add(profile);
                RefreshProfilesList();
                await _mainWindow.SaveAppSettings(_settings);
            }
        }

        private async void BtnEditProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Profile profile)
            {
                var editWin = new ProfileEditWindow(profile.Name, profile.Icon, profile.IconFont, profile.IconColor, profile.ImagePath) { Owner = this };
                if (editWin.ShowDialog() == true)
                {
                    profile.Name = editWin.ProfileName;
                    profile.Icon = editWin.ProfileIcon;
                    profile.IconFont = editWin.ProfileFont;
                    profile.IconColor = editWin.ProfileColor;
                    profile.ImagePath = editWin.ProfileImagePath;
                    RefreshProfilesList();
                    await _mainWindow.SaveAppSettings(_settings);
                    _mainWindow.RefreshPanel();
                }
            }
        }

        private async void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.Profiles.Count <= 1)
            {
                new DarkDialog("Нельзя удалить единственный профиль.") { Owner = this }.ShowDialog();
                return;
            }

            if (sender is Button btn && btn.Tag is Profile profile)
            {
                var confirm = new DarkDialog($"Удалить профиль '{profile.Name}'?", isConfirm: true) { Owner = this };
                if (confirm.ShowDialog() == true)
                {
                    _settings.Profiles.Remove(profile);
                    if (_settings.ActiveProfileId == profile.Id)
                    {
                        _settings.ActiveProfileId = _settings.Profiles[0].Id;
                    }
                    RefreshProfilesList();
                    await _mainWindow.SaveAppSettings(_settings);
                    _mainWindow.RefreshPanel();
                }
            }
        }
    }
}
