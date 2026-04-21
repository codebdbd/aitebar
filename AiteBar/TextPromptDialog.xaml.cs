using System.Windows;

namespace AiteBar
{
    public partial class TextPromptDialog : DarkWindow
    {
        public string Value => TxtValue.Text;

        public TextPromptDialog(string title, string label, string initialValue = "")
        {
            InitializeComponent();
            Title = title;
            TxtLabel.Text = label;
            TxtValue.Text = initialValue ?? string.Empty;
            TxtValue.SelectAll();
            TxtValue.Focus();
            UpdateSaveState();
        }

        private void TxtValue_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateSaveState();
        }

        private void UpdateSaveState()
        {
            BtnSave.IsEnabled = !string.IsNullOrWhiteSpace(TxtValue.Text);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TxtValue.Text))
            {
                DialogResult = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
