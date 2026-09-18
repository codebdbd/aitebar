using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using System.Runtime.Versioning;

namespace AiteBar
{
    public partial class DarkDialog : DarkWindow
    {
        private readonly bool _isConfirmDialog;

        public DarkDialog(string message, bool isConfirm = false)
        {
            InitializeComponent();
            _isConfirmDialog = isConfirm;
            TxtMessage.Text = message;

            if (isConfirm)
            {
                this.Title = LocalizationService.Get("Common_Confirmation");
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
            }
            else
            {
                BtnOk.Visibility = Visibility.Visible;
            }

            Loaded += (_, _) =>
            {
                EnsurePositionOnScreen();
                FocusDefaultButton();
            };
        }

        public DarkDialog(string message, List<DialogButton> buttons, string? title = null)
        {
            InitializeComponent();
            TxtMessage.Text = message;

            if (!string.IsNullOrEmpty(title))
            {
                this.Title = title;
            }

            // Hide default buttons
            BtnYes.Visibility = Visibility.Collapsed;
            BtnNo.Visibility = Visibility.Collapsed;
            BtnOk.Visibility = Visibility.Collapsed;

            // Add custom buttons
            foreach (var button in buttons)
            {
                var btn = new System.Windows.Controls.Button
                {
                    Content = button.Text,
                    Background = new SolidColorBrush(button.IsPrimary ? System.Windows.Media.Color.FromRgb(66, 133, 244) : System.Windows.Media.Color.FromRgb(51, 51, 51)),
                    Style = (Style)FindResource("DialogBtnStyle"),
                    IsDefault = button.IsPrimary,
                    IsCancel = !button.IsPrimary
                };

                var btnCopy = button;
                btn.Click += (s, e) =>
                {
                    this.Tag = btnCopy.Value;
                    this.DialogResult = btnCopy.IsPrimary;
                };

                ButtonsPanel.Children.Insert(0, btn);
            }

            Loaded += (_, _) =>
            {
                EnsurePositionOnScreen();
                FocusDefaultButton();
            };
        }

        private void EnsurePositionOnScreen()
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                return;
            }

            try
            {
                var cursorPoint = System.Windows.Forms.Cursor.Position;
                var screen = System.Windows.Forms.Screen.FromPoint(cursorPoint)
                    ?? System.Windows.Forms.Screen.PrimaryScreen;

                if (screen == null)
                {
                    return;
                }

                var dpiScale = VisualTreeHelper.GetDpi(this);
                double dpiX = dpiScale.DpiScaleX > 0 ? dpiScale.DpiScaleX : 1.0;
                double dpiY = dpiScale.DpiScaleY > 0 ? dpiScale.DpiScaleY : 1.0;

                Rect workArea = new Rect(
                    screen.WorkingArea.Left / dpiX,
                    screen.WorkingArea.Top / dpiY,
                    screen.WorkingArea.Width / dpiX,
                    screen.WorkingArea.Height / dpiY);

                Rect? ownerBounds = null;
                bool isOwnerVisible = false;
                if (Owner != null)
                {
                    isOwnerVisible = Owner.IsVisible;
                    ownerBounds = new Rect(Owner.Left, Owner.Top, Owner.ActualWidth, Owner.ActualHeight);
                }

                double dialogWidth = ActualWidth > 0 ? ActualWidth : Width;
                double dialogHeight = ActualHeight > 0 ? ActualHeight : 160;

                var (newLeft, newTop) = DialogPositionHelper.CalculatePosition(
                    ownerBounds,
                    isOwnerVisible,
                    workArea,
                    dialogWidth,
                    dialogHeight);

                Left = newLeft;
                Top = newTop;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void FocusDefaultButton()
        {
            foreach (var child in ButtonsPanel.Children)
            {
                if (child is System.Windows.Controls.Button { IsDefault: true, Visibility: Visibility.Visible } button)
                {
                    button.Focus();
                    return;
                }
            }

            if (BtnOk.Visibility == Visibility.Visible)
            {
                BtnOk.Focus();
            }
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnLocalizationChanged()
        {
            if (_isConfirmDialog)
            {
                Title = LocalizationService.Get("Common_Confirmation");
            }
        }
    }

    public class DialogButton
    {
        public string Text { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
    }
}
