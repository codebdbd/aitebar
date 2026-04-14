using System;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    public partial class ProfileEditWindow : DarkWindow
    {
        public string ProfileName { get; private set; } = "Новый профиль";
        public string ProfileIcon { get; private set; } = "\uF36A";
        public string ProfileFont { get; private set; } = FontHelper.FluentKey;
        public string ProfileColor { get; private set; } = "#FFD700";
        public string ProfileImagePath { get; private set; } = "";

        private static readonly BrushConverter _brushConverter = new();

        public ProfileEditWindow(string currentName = "", string currentIcon = "", string currentFont = "", string currentColor = "", string currentImagePath = "")
        {
            InitializeComponent();
            
            if (!string.IsNullOrEmpty(currentName)) TxtName.Text = currentName;
            if (!string.IsNullOrEmpty(currentIcon)) ProfileIcon = currentIcon;
            if (!string.IsNullOrEmpty(currentFont)) ProfileFont = currentFont;
            if (!string.IsNullOrEmpty(currentColor)) ProfileColor = currentColor;
            if (!string.IsNullOrEmpty(currentImagePath)) ProfileImagePath = currentImagePath;

            LoadColors();
            UpdatePreview();
        }

        private void LoadColors()
        {
            string[] colors = { "#FFD700", "#3ABEFF", "#60A5FA", "#6366F1", "#8B5CF6", "#A855F7", "#22D3EE", "#34D399", "#A3E635", "#FB7185" };
            UniformColors.Children.Clear();
            foreach (var hex in colors)
            {
                var btn = new System.Windows.Controls.Button
                {
                    Background = _brushConverter.ConvertFromString(hex) as System.Windows.Media.Brush,
                    Width = 30, Height = 30, Margin = new Thickness(4),
                    BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
                };
                btn.Click += (s, e) => { ProfileColor = hex; UpdatePreview(); };
                UniformColors.Children.Add(btn);
            }
        }

        private void UpdatePreview()
        {
            if (!string.IsNullOrEmpty(ProfileImagePath) && System.IO.File.Exists(ProfileImagePath))
            {
                PreviewIcon.Visibility = Visibility.Collapsed;
                if (PreviewIcon.Parent is System.Windows.Controls.Grid parent)
                {
                    var existingImage = System.Linq.Enumerable.OfType<System.Windows.Controls.Image>(parent.Children).FirstOrDefault();
                    if (existingImage == null)
                    {
                        existingImage = new System.Windows.Controls.Image { Width = 32, Height = 32, Stretch = Stretch.Uniform };
                        parent.Children.Add(existingImage);
                    }
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(ProfileImagePath);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    existingImage.Source = bitmap;
                    existingImage.Visibility = Visibility.Visible;
                }
            }
            else
            {
                PreviewIcon.Visibility = Visibility.Visible;
                PreviewIcon.Text = ProfileIcon;
                PreviewIcon.FontFamily = FontHelper.Resolve(ProfileFont);
                PreviewIcon.Foreground = _brushConverter.ConvertFromString(ProfileColor) as System.Windows.Media.Brush;
                if (PreviewIcon.Parent is System.Windows.Controls.Grid parent)
                {
                    var existingImage = System.Linq.Enumerable.OfType<System.Windows.Controls.Image>(parent.Children).FirstOrDefault();
                    if (existingImage != null) existingImage.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void BtnPickIcon_Click(object sender, RoutedEventArgs e)
        {
            var picker = new IconPickerWindow { Owner = this };
            if (picker.ShowDialog() == true)
            {
                ProfileIcon = picker.SelectedIcon;
                ProfileFont = picker.SelectedFont;
                ProfileImagePath = picker.SelectedImagePath;
                UpdatePreview();
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                new DarkDialog("Имя профиля не может быть пустым.") { Owner = this }.ShowDialog();
                return;
            }
            ProfileName = TxtName.Text.Trim();
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
