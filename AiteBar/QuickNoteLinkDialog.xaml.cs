using System;
using System.Windows;

namespace AiteBar
{
    public partial class QuickNoteLinkDialog : DarkWindow
    {
        public string LinkText => PreserveLinkText(TxtLinkText.Text);

        public string Url => TxtUrl.Text.Trim();

        public QuickNoteLinkDialog(string initialText, string initialUrl)
        {
            InitializeComponent();
            TxtLinkText.Text = initialText ?? string.Empty;
            TxtUrl.Text = initialUrl ?? string.Empty;
            Loaded += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(TxtLinkText.Text))
                {
                    TxtLinkText.Focus();
                }
                else
                {
                    TxtUrl.Focus();
                    TxtUrl.SelectAll();
                }
            };
            UpdateSaveState();
        }

        private void Input_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateSaveState();
        }

        private void UpdateSaveState()
        {
            BtnSave.IsEnabled = IsValidHttpUrl(Url) && !string.IsNullOrWhiteSpace(LinkText);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (BtnSave.IsEnabled)
            {
                DialogResult = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private static bool IsValidHttpUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        internal static string PreserveLinkText(string? value) => value ?? string.Empty;
    }
}
