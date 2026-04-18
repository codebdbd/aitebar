using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AiteBar
{
    public partial class ProfileEditWindow : DarkWindow
    {
        public string ProfileName => TxtName.Text;
        public string ProfileIcon { get; private set; } = "\uF36A";
        public string ProfileFont { get; private set; } = FontHelper.FluentKey;
        public string ProfileColor { get; private set; } = "#FFD700";
        public string ProfileImagePath { get; private set; } = "";

        public ProfileEditWindow(string name = "Новый профиль", string icon = "\uF36A", string font = "FluentSystemIcons-Regular", string color = "#FFD700", string imagePath = "")
        {
            InitializeComponent();
            TxtName.Text = name;
            ProfileIcon = icon;
            ProfileFont = font;
            ProfileColor = color;
            ProfileImagePath = imagePath;

            LoadColors();
            UpdatePreview();
        }

        private void LoadColors()
        {
            string[] colors = { "#FFD700", "#FF4500", "#FF69B4", "#00FF7F", "#00BFFF", "#1E90FF", "#9370DB", "#FFFFFF" };
            foreach (var col in colors)
            {
                var btn = new System.Windows.Controls.Button { 
                    Background = new System.Windows.Media.BrushConverter().ConvertFromString(col) as System.Windows.Media.Brush,
                    Width = 24, Height = 24, Margin = new Thickness(2),
                    Tag = col
                };
                btn.Click += (s, e) => { ProfileColor = (string)((System.Windows.Controls.Button)s).Tag; UpdatePreview(); };
                GridColors.Children.Add(btn);
            }
        }

        private void UpdatePreview()
        {
            if (!string.IsNullOrEmpty(ProfileImagePath) && File.Exists(ProfileImagePath))
            {
                ImgPreview.Source = new BitmapImage(new Uri(ProfileImagePath));
                ImgPreview.Visibility = Visibility.Visible;
                TxtPreviewIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtPreviewIcon.Text = ProfileIcon;
                TxtPreviewIcon.FontFamily = FontHelper.Resolve(ProfileFont);
                TxtPreviewIcon.Foreground = new System.Windows.Media.BrushConverter().ConvertFromString(ProfileColor) as System.Windows.Media.Brush;
                ImgPreview.Visibility = Visibility.Collapsed;
                TxtPreviewIcon.Visibility = Visibility.Visible;
            }
        }

        private void BtnSelectIcon_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new IconPickerWindow { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                ProfileIcon = dlg.SelectedIcon;
                ProfileFont = dlg.SelectedFont;
                ProfileImagePath = dlg.SelectedImagePath;
                UpdatePreview();
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text)) { new DarkDialog("Введите название профиля.") { Owner = this }.ShowDialog(); return; }
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
