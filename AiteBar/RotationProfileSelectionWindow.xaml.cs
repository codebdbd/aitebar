using System.Runtime.Versioning;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class RotationProfileSelectionWindow : DarkWindow
{
    private readonly IReadOnlyList<BrowserProfileInfo> _profiles;
    private readonly HashSet<string> _selectedProfilePaths = new(StringComparer.OrdinalIgnoreCase);
    private bool _useRotation;
    private string _selectedSingleProfilePath = "";

    public RotationProfileSelectionWindow(
        IReadOnlyList<BrowserProfileInfo> profiles,
        IReadOnlyList<string> selectedProfilePaths)
        : this(profiles, false, "", selectedProfilePaths)
    {
    }

    public RotationProfileSelectionWindow(
        IReadOnlyList<BrowserProfileInfo> profiles,
        bool useRotation,
        string selectedSingleProfilePath,
        IReadOnlyList<string> selectedProfilePaths)
    {
        InitializeComponent();
        _profiles = profiles;
        _useRotation = useRotation;
        _selectedSingleProfilePath = selectedSingleProfilePath ?? "";
        LoadProfiles(selectedProfilePaths);
        UpdateModeUi();
        UpdateSaveButtonState();
        Loaded += (_, _) => TxtSearch.Focus();
    }

    public bool UseRotation { get; private set; }

    public string SelectedSingleProfilePath { get; private set; } = "";

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

    private void ProfileMode_Changed(object sender, RoutedEventArgs e)
    {
        if (RbModeRotation == null || RbModeSingle == null)
        {
            return;
        }

        _useRotation = RbModeRotation.IsChecked == true;
        UpdateModeUi();
        RenderProfiles();
        UpdateSaveButtonState();
    }

    private void UpdateModeUi()
    {
        if (RbModeRotation == null || RbModeSingle == null)
        {
            return;
        }

        if (_useRotation)
        {
            RbModeRotation.IsChecked = true;
            RbModeSingle.IsChecked = false;
            if (TxtModeHint != null)
            {
                TxtModeHint.Text = LocalizationService.Get("RotationProfiles_DefaultHint");
            }
            if (BadgeSelectionCount != null)
            {
                BadgeSelectionCount.Visibility = Visibility.Visible;
            }
            if (BtnQuickAction1 != null)
            {
                BtnQuickAction1.Content = LocalizationService.Get("RotationProfiles_SelectAll");
                BtnQuickAction1.Visibility = Visibility.Visible;
            }
            if (BtnQuickAction2 != null)
            {
                BtnQuickAction2.Content = LocalizationService.Get("RotationProfiles_Clear");
                BtnQuickAction2.Visibility = Visibility.Visible;
            }
        }
        else
        {
            RbModeSingle.IsChecked = true;
            RbModeRotation.IsChecked = false;
            if (TxtModeHint != null)
            {
                TxtModeHint.Text = LocalizationService.Get("RotationProfiles_SingleHint");
            }
            if (BadgeSelectionCount != null)
            {
                BadgeSelectionCount.Visibility = Visibility.Collapsed;
            }
            if (BtnQuickAction1 != null)
            {
                BtnQuickAction1.Content = LocalizationService.Get("RotationProfiles_ResetDefault");
                BtnQuickAction1.Visibility = Visibility.Visible;
            }
            if (BtnQuickAction2 != null)
            {
                BtnQuickAction2.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void RenderProfiles()
    {
        string filter = TxtSearch?.Text?.Trim() ?? "";

        PanelProfiles.Children.Clear();

        if (_useRotation)
        {
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
        else
        {
            string defaultLabel = LocalizationService.Get("RotationProfiles_DefaultProfile");
            if (string.IsNullOrWhiteSpace(filter) || defaultLabel.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                var defaultRadio = new RadioButton
                {
                    Content = defaultLabel,
                    Tag = "",
                    GroupName = "SingleProfileListGroup",
                    IsChecked = string.IsNullOrWhiteSpace(_selectedSingleProfilePath),
                    Style = (Style)FindResource("SelectionListRadioButtonStyle")
                };
                defaultRadio.Checked += SingleRadio_Checked;
                PanelProfiles.Children.Add(defaultRadio);
            }

            foreach (var profile in _profiles.Where(profile => MatchesFilter(profile, filter)))
            {
                bool isSelected = !string.IsNullOrWhiteSpace(_selectedSingleProfilePath) &&
                    (string.Equals(profile.ProfilePath, _selectedSingleProfilePath, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(profile.LaunchName, _selectedSingleProfilePath, StringComparison.OrdinalIgnoreCase));

                var radio = new RadioButton
                {
                    Content = profile.DisplayName,
                    Tag = profile.ProfilePath,
                    GroupName = "SingleProfileListGroup",
                    IsChecked = isSelected,
                    ToolTip = profile.ProfilePath,
                    Style = (Style)FindResource("SelectionListRadioButtonStyle")
                };
                radio.Checked += SingleRadio_Checked;
                PanelProfiles.Children.Add(radio);
            }
        }
    }

    private static bool MatchesFilter(BrowserProfileInfo profile, string filter) =>
        string.IsNullOrWhiteSpace(filter) ||
        profile.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        profile.ProfilePath.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private void SingleRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radio && radio.Tag is string profilePath)
        {
            _selectedSingleProfilePath = profilePath;
            UpdateSaveButtonState();
        }
    }

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

    private void TxtSearch_GotFocus(object sender, RoutedEventArgs e) =>
        TxtSearchPlaceholder.Visibility = Visibility.Collapsed;

    private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TxtSearchPlaceholder != null)
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void BtnQuickAction1_Click(object sender, RoutedEventArgs e)
    {
        if (_useRotation)
        {
            _selectedProfilePaths.Clear();
            foreach (var profile in _profiles)
            {
                _selectedProfilePaths.Add(profile.ProfilePath);
            }
            RenderProfiles();
            UpdateSaveButtonState();
        }
        else
        {
            _selectedSingleProfilePath = "";
            RenderProfiles();
            UpdateSaveButtonState();
        }
    }

    private void BtnQuickAction2_Click(object sender, RoutedEventArgs e)
    {
        if (_useRotation)
        {
            _selectedProfilePaths.Clear();
            RenderProfiles();
            UpdateSaveButtonState();
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        UseRotation = _useRotation;
        SelectedSingleProfilePath = _selectedSingleProfilePath;
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

        if (_useRotation)
        {
            int selectedCount = _selectedProfilePaths.Count;
            BtnSave.IsEnabled = _profiles.Count == 0 || selectedCount > 0;
            if (TxtSelectionCount != null)
            {
                TxtSelectionCount.Text = LocalizationService.Format("RotationProfiles_SelectedFormat", selectedCount, _profiles.Count);
            }
        }
        else
        {
            BtnSave.IsEnabled = true;
        }
    }

    protected override void OnLocalizationChanged()
    {
        UpdateModeUi();
        RenderProfiles();
        UpdateSaveButtonState();
    }
}
