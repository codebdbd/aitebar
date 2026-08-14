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
            TagsBox.Text = knownSnippets.SelectMany(static snippet => snippet.Tags).Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? "misc";
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
            UpdateState();
            return;
        }

        ResultSnippet = snippet;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void UpdateState()
    {
        SaveButton.IsEnabled = TryBuildSnippet(out _);
    }

    private bool TryBuildSnippet(out AiteProfileSnippet? snippet)
    {
        snippet = null;
        string name = (NameBox.Text ?? string.Empty).Trim();
        List<string> tags = (TagsBox.Text ?? string.Empty)
            .Split([',', ';', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static tag => tag.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        List<string> urls = [];
        foreach (string rawUrl in (UrlsBox.Text ?? string.Empty).Split(["\r\n", "\n", "|"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (AiteProfilesQuickLinkService.TryNormalizeUrlInput(rawUrl, out string normalizedUrl))
            {
                urls.Add(normalizedUrl);
            }
        }

        if (string.IsNullOrWhiteSpace(name) || urls.Count == 0)
        {
            StatusText.Text = LocalizationService.Get("AiteProfiles_LinkValidation");
            return false;
        }

        snippet = new AiteProfileSnippet
        {
            Name = name,
            Tags = tags.Count == 0 ? ["misc"] : tags,
            Urls = urls.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
        StatusText.Text = string.Empty;
        return true;
    }
}
