using System.Windows;

namespace AiteBar {
    public partial class DarkDialog : DarkWindow {
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
