using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace AiteBar;

public partial class AiConnectionDialog : DarkWindow
{
    private sealed record ProviderOption(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    public AiConnectionDialog()
    {
        InitializeComponent();
        foreach (AiProviderDefinition provider in AiProviderCatalog.All)
        {
            CmbProvider.Items.Add(new ProviderOption(provider.Id, provider.DisplayName));
        }
        CmbProvider.SelectedIndex = 0;
        UpdateAddState();
    }

    public string ProviderId => (CmbProvider.SelectedItem as ProviderOption)?.Id ?? string.Empty;
    public string ConnectionName => TxtDisplayName.Text.Trim();
    public string QuotaScopeId => TxtQuotaScope.Text.Trim();
    public string ApiKey => PwdApiKey.Password.Trim();

    private void CmbProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProvider.SelectedItem is ProviderOption provider && string.IsNullOrWhiteSpace(TxtDisplayName.Text))
        {
            TxtDisplayName.Text = provider.Name;
        }
        UpdateAddState();
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => UpdateAddState();
    private void Input_PasswordChanged(object sender, RoutedEventArgs e) => UpdateAddState();

    private void LinkGetApiKey_Click(object sender, RoutedEventArgs e)
    {
        if (CmbProvider.SelectedItem is not ProviderOption selected ||
            !AiProviderCatalog.TryGet(selected.Id, out AiProviderDefinition provider))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(provider.DocumentationUri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private void UpdateAddState()
    {
        if (BtnAdd == null)
        {
            return;
        }
        BtnAdd.IsEnabled = CmbProvider.SelectedItem != null &&
                           !string.IsNullOrWhiteSpace(TxtDisplayName.Text) &&
                           !string.IsNullOrWhiteSpace(TxtQuotaScope.Text) &&
                           !string.IsNullOrWhiteSpace(PwdApiKey.Password);
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (BtnAdd.IsEnabled)
        {
            DialogResult = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
