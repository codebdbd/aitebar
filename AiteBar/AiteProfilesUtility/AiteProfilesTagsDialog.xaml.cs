using System.Runtime.Versioning;
using System.Windows;

namespace AiteBar.AiteProfilesUtility;

[SupportedOSPlatform("windows6.1")]
public partial class AiteProfilesTagsDialog : DarkWindow
{
    public AiteProfilesTagsDialog(string initialTags)
    {
        InitializeComponent();
        TagsBox.Text = initialTags ?? string.Empty;
        Loaded += (_, _) =>
        {
            TagsBox.Focus();
            TagsBox.SelectAll();
        };
    }

    internal string TagsText { get; private set; } = string.Empty;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        TagsText = TagsBox.Text ?? string.Empty;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
