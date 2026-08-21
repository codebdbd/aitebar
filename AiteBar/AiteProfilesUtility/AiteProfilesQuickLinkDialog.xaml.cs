using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;

namespace AiteBar.AiteProfilesUtility;

[SupportedOSPlatform("windows6.1")]
public partial class AiteProfilesQuickLinkDialog : DarkWindow
{
    internal AiteProfilesQuickLinkDialog(
        IReadOnlyList<AiteProfileSnippet> knownSnippets,
        AiteProfileSnippet? initialSnippet)
    {
        InitializeComponent();
        if (initialSnippet is not null)
        {
            NameBox.Text = initialSnippet.Name;
            TagsBox.Text = string.Join(", ", initialSnippet.Tags);
            UrlsBox.Text = string.Join(Environment.NewLine, initialSnippet.Urls);
        }
        else
        {
            TagsBox.Text = knownSnippets.SelectMany(static snippet => snippet.Tags).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static tag => tag, StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? "misc";
        }

        Loaded += (_, _) =>
        {
            UpdatePlaceholders();
            NameBox.Focus();
            NameBox.SelectAll();
        };
        UpdateState();
    }

    internal AiteProfileSnippet? ResultSnippet { get; private set; }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateState();
        UpdatePlaceholders();
    }

    private void UpdatePlaceholders()
    {
        if (NamePlaceholder is not null)
        {
            NamePlaceholder.Visibility = string.IsNullOrWhiteSpace(NameBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        if (TagsPlaceholder is not null)
        {
            TagsPlaceholder.Visibility = string.IsNullOrWhiteSpace(TagsBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        if (UrlsPlaceholder is not null)
        {
            UrlsPlaceholder.Visibility = string.IsNullOrWhiteSpace(UrlsBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildSnippet(out AiteProfileSnippet? snippet))
        {
            return;
        }

        ResultSnippet = snippet;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void UpdateState()
    {
        bool isValid = TryBuildSnippet(out _, out string validationError);
        SaveButton.IsEnabled = isValid;
        ValidationText.Text = validationError;
        ValidationText.Visibility = string.IsNullOrWhiteSpace(validationError) ? Visibility.Collapsed : Visibility.Visible;
    }

    private bool TryBuildSnippet(out AiteProfileSnippet? snippet)
    {
        return TryBuildSnippet(out snippet, out _);
    }

    private bool TryBuildSnippet(out AiteProfileSnippet? snippet, out string validationError)
    {
        snippet = null;
        validationError = string.Empty;
        string name = (NameBox.Text ?? string.Empty).Trim();
        List<string> tags = AiteProfilesQuickLinkService.ParseTags(TagsBox.Text ?? string.Empty);
        List<string> urls = [];
        var invalidUrls = new List<string>();
        foreach (string rawUrl in (UrlsBox.Text ?? string.Empty).Split(["\r\n", "\n", "|"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (AiteProfilesQuickLinkService.TryNormalizeUrlInput(rawUrl, out string normalizedUrl))
            {
                urls.Add(normalizedUrl);
            }
            else
            {
                invalidUrls.Add(rawUrl);
            }
        }

        if (string.IsNullOrWhiteSpace(name) || urls.Count == 0 || invalidUrls.Count > 0)
        {
            validationError = invalidUrls.Count > 0
                ? LocalizationService.Format("AiteProfiles_LinkInvalidUrls", string.Join(", ", invalidUrls))
                : LocalizationService.Get("AiteProfiles_LinkValidation");
            return false;
        }

        snippet = new AiteProfileSnippet
        {
            Name = name,
            Tags = tags.Count == 0 ? ["misc"] : tags,
            Urls = urls.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
        return true;
    }
}
