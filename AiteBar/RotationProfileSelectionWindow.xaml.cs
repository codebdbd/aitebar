using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using CheckBox = System.Windows.Controls.CheckBox;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class RotationProfileSelectionWindow : DarkWindow
{
    private readonly IReadOnlyList<BrowserProfileInfo> _profiles;
    private readonly HashSet<string> _selectedProfilePaths = new(StringComparer.OrdinalIgnoreCase);

    public RotationProfileSelectionWindow(
        IReadOnlyList<BrowserProfileInfo> profiles,
        IReadOnlyList<string> selectedProfilePaths)
    {
        InitializeComponent();
        _profiles = profiles;
        LoadProfiles(selectedProfilePaths);
        UpdateSaveButtonState();
        Loaded += (_, _) => TxtSearch.Focus();
    }

    public List<string> SelectedProfilePaths { get; private set; } = [];

    private void LoadProfiles(IReadOnlyList<string> selectedProfilePaths)
    {
        var selected = new HashSet<string>(selectedProfilePaths, StringComparer.OrdinalIgnoreCase);
        bool selectAll = selected.Count == 0;
        _selectedProfilePaths.Clear();
        foreach (var profile in _profiles)
        {
            if (selectAll || selected.Contains(profile.ProfilePath))
            {
                _selectedProfilePaths.Add(profile.ProfilePath);
            }
        }

        RenderProfiles();
    }

    private void RenderProfiles()
    {
        string filter = TxtSearch?.Text?.Trim() ?? "";

        PanelProfiles.Children.Clear();
        foreach (var profile in _profiles.Where(profile => MatchesFilter(profile, filter)))
        {
            var checkBox = new CheckBox
            {
                Content = profile.DisplayName,
                Tag = profile.ProfilePath,
                IsChecked = _selectedProfilePaths.Contains(profile.ProfilePath),
                ToolTip = profile.ProfilePath,
                Style = (Style)FindResource("SelectionListCheckBoxStyle")
            };
            checkBox.Checked += ProfileCheckBox_Changed;
            checkBox.Unchecked += ProfileCheckBox_Changed;
            PanelProfiles.Children.Add(checkBox);
        }
    }

    private static bool MatchesFilter(BrowserProfileInfo profile, string filter) =>
        string.IsNullOrWhiteSpace(filter) ||
        profile.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        profile.ProfilePath.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private void ProfileCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is string profilePath)
        {
            if (checkBox.IsChecked == true)
            {
                _selectedProfilePaths.Add(profilePath);
            }
            else
            {
                _selectedProfilePaths.Remove(profilePath);
            }
        }

        UpdateSaveButtonState();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtSearchPlaceholder != null)
        {
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        RenderProfiles();
        UpdateSaveButtonState();
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        _selectedProfilePaths.Clear();
        foreach (var profile in _profiles)
        {
            _selectedProfilePaths.Add(profile.ProfilePath);
        }
        RenderProfiles();
        UpdateSaveButtonState();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _selectedProfilePaths.Clear();
        RenderProfiles();
        UpdateSaveButtonState();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SelectedProfilePaths = _selectedProfilePaths.Count == _profiles.Count ? [] : [.. _selectedProfilePaths];
        DialogResult = true;
        Close();
    }

    private void UpdateSaveButtonState()
    {
        if (BtnSave == null)
        {
            return;
        }

        int selectedCount = _selectedProfilePaths.Count;
        BtnSave.IsEnabled = _profiles.Count == 0 || selectedCount > 0;
        if (TxtSelectionCount != null)
        {
            TxtSelectionCount.Text = LocalizationService.Format("RotationProfiles_SelectedFormat", selectedCount, _profiles.Count);
        }
    }

    protected override void OnLocalizationChanged()
    {
        RenderProfiles();
        UpdateSaveButtonState();
    }
}
