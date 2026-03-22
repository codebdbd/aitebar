using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SmartScreenDock {
    public partial class DarkDialog : Window {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public DarkDialog(string message, bool isConfirm = false) {
            InitializeComponent();
            TxtMessage.Text = message;

            if (isConfirm) {
                this.Title = "Подтверждение";
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
            } else {
                BtnOk.Visibility = Visibility.Visible;
            }
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int darkTheme = 1;
            DwmSetWindowAttribute(hwnd, 20, ref darkTheme, sizeof(int));
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }
    }
}