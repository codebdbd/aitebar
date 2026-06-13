using System.Windows;

namespace AiteBar
{
    public partial class TextPromptDialog : DarkWindow
    {
        private readonly string? _titleResourceKey;
        private readonly string? _labelResourceKey;

        public string Value => TxtValue.Text;

        public TextPromptDialog(string title, string label, string initialValue = "")
        {
            InitializeComponent();
            Title = title;
            TxtLabel.Text = label;
            TxtValue.Text = initialValue ?? string.Empty;
            Loaded += (_, _) =>
            {
                TxtValue.SelectAll();
                TxtValue.Focus();
            };
            UpdateSaveState();
        }

        public TextPromptDialog(string titleResourceKey, string labelResourceKey, string initialValue, bool treatAsResourceKeys)
            : this(
                treatAsResourceKeys ? LocalizationService.Get(titleResourceKey) : titleResourceKey,
                treatAsResourceKeys ? LocalizationService.Get(labelResourceKey) : labelResourceKey,
                initialValue)
        {
            if (treatAsResourceKeys)
            {
                _titleResourceKey = titleResourceKey;
                _labelResourceKey = labelResourceKey;
            }
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

        protected override void OnLocalizationChanged()
        {
            if (!string.IsNullOrWhiteSpace(_titleResourceKey))
            {
                Title = LocalizationService.Get(_titleResourceKey);
            }

            if (!string.IsNullOrWhiteSpace(_labelResourceKey))
            {
                TxtLabel.Text = LocalizationService.Get(_labelResourceKey);
            }
        }
    }
}
