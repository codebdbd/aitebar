using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AiteBar.AiteProfilesUtility;

[SupportedOSPlatform("windows6.1")]
public partial class AiteProfilesWindow : DarkWindow
{
    private readonly AiteProfilesViewModel _viewModel;
    private int _suppressAutoHideRefCount;
    private bool _initialized;

    private void PushSuppressAutoHide() => _suppressAutoHideRefCount++;
    private void PopSuppressAutoHide()
    {
        if (_suppressAutoHideRefCount > 0)
        {
            _suppressAutoHideRefCount--;
        }
    }

    public AiteProfilesWindow(AppSettingsService settingsService)
    {
        InitializeComponent();
        string root = Path.Combine(PathHelper.AppDataFolder, "AiteProfiles");
        var scanner = new AiteProfilesChromeScanner();
        var launcher = new AiteProfilesChromeLauncher();
        var store = new AiteProfilesStore(scanner, root);
        var quickLinks = new AiteProfilesQuickLinkService(root);
        var rotation = new AiteProfilesRotationStateService(root);
        _viewModel = new AiteProfilesViewModel(store, launcher, quickLinks, rotation);
        _viewModel.HideWindowRequested += Hide;
        _viewModel.MessageRequested += ShowMessage;
        _viewModel.EditTagsRequested += EditTagsAsync;
        _viewModel.EditQuickLinkRequested += EditQuickLinkAsync;
        _viewModel.ImportQuickLinksRequested += ImportQuickLinksAsync;
        _viewModel.ExportQuickLinksRequested += ExportQuickLinksAsync;
        DataContext = _viewModel;
        _ = settingsService;
        Loaded += OnLoaded;
    }

    public void ShowNearPanel(AppSettingsService settingsService)
    {
        AppSettings settings = settingsService.Settings;
        var screens = System.Windows.Forms.Screen.AllScreens;
        var screen = settings.MonitorIndex >= 0 && settings.MonitorIndex < screens.Length
            ? screens[settings.MonitorIndex]
            : System.Windows.Forms.Screen.PrimaryScreen;
        var work = screen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
        Left = work.Left + Math.Max(0, (work.Width - Width) / 2.0);
        Top = work.Bottom - Height - 10;
        Show();
        Activate();
        SearchBox.Focus();
    }

    internal void RestoreFromAiteBar()
    {
        WindowState = WindowState.Normal;
        if (!IsVisible)
        {
            Show();
        }

        Activate();
        SearchBox.Focus();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync().ConfigureAwait(true);
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (_suppressAutoHideRefCount > 0)
        {
            return;
        }

        Hide();
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        PushSuppressAutoHide();
        UpdateContextMenuState();
    }

    private void ContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        PopSuppressAutoHide();
        if (_suppressAutoHideRefCount == 0 && !IsActive)
        {
            Hide();
        }
    }

    private void UpdateContextMenuState()
    {
        AiteProfileListItemViewModel? profile = _viewModel.SelectedProfiles.FirstOrDefault() ?? _viewModel.CurrentProfile;
        bool multipleSelected = _viewModel.SelectedProfiles.Count > 1;

        OpenSelectedMenuItem.Visibility = multipleSelected ? Visibility.Visible : Visibility.Collapsed;

        bool isFavorite = profile?.IsFavorite ?? false;
        ToggleFavoriteMenuItem.Header = isFavorite ? "Удалить из избранного" : "Добавить в избранное";

        bool isFarm = profile?.IsFarm ?? false;
        ToggleFarmMenuItem.Header = isFarm ? "Удалить из \"Ферма\"" : "Добавить в \"Ферма\"";
    }

    private void ProfilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[^1] is AiteProfileListItemViewModel lastAdded)
        {
            _viewModel.CurrentProfile = lastAdded;
        }
    }

    private void ProfileCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: AiteProfileListItemViewModel item })
        {
            _viewModel.ToggleItemSelection(item, item.IsSelected);
            e.Handled = true;
        }
    }

    private void ProfilesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProfilesGrid.SelectedItem is AiteProfileListItemViewModel item)
        {
            _viewModel.CurrentProfile = item;
            if (_viewModel.OpenProfileCommand.CanExecute(null))
            {
                _viewModel.OpenProfileCommand.Execute(null);
            }
        }
    }

    private void ProfilesGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null && source is not System.Windows.Controls.ListViewItem)
        {
            source = VisualTreeHelper.GetParent(source);
        }

        if (source is System.Windows.Controls.ListViewItem { DataContext: AiteProfileListItemViewModel item } row)
        {
            row.IsSelected = true;
            _viewModel.CurrentProfile = item;
        }
    }

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e) =>
        _viewModel.SelectAllVisible(sender is CheckBox { IsChecked: true });

    private void ProfileHeader_Click(object sender, RoutedEventArgs e) => _viewModel.SetSortColumn(4);

    private void TimeHeader_Click(object sender, RoutedEventArgs e) => _viewModel.SetSortColumn(2);

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleQuickLinkSuggestionKey(e))
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A && ProfilesGrid.IsKeyboardFocusWithin)
        {
            _viewModel.SelectAllVisible(selected: true);
            ProfilesGrid.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space && !e.IsRepeat && ProfilesGrid.IsKeyboardFocusWithin &&
            ProfilesGrid.SelectedItem is AiteProfileListItemViewModel item)
        {
            item.IsSelected = !item.IsSelected;
            _viewModel.ToggleItemSelection(item, item.IsSelected);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && _viewModel.LaunchCommand.CanExecute(null))
        {
            _viewModel.LaunchCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrWhiteSpace(_viewModel.SearchText))
            {
                _viewModel.SearchText = string.Empty;
                e.Handled = true;
                return;
            }

            Hide();
            e.Handled = true;
        }
    }

    private void QuickLinkBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => UpdateQuickLinkSuggestionsPopup();

    private void QuickLinkBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        Dispatcher.BeginInvoke((Action)(() =>
        {
            if (!QuickLinkBox.IsKeyboardFocusWithin && !QuickLinkSuggestionsList.IsKeyboardFocusWithin)
            {
                QuickLinkSuggestionsPopup.IsOpen = false;
            }
        }));

    private void QuickLinkBox_TextChanged(object sender, TextChangedEventArgs e) =>
        Dispatcher.BeginInvoke((Action)UpdateQuickLinkSuggestionsPopup);

    private void QuickLinkSuggestionsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (QuickLinkSuggestionsList.SelectedItem is AiteProfileSnippet snippet)
        {
            ApplyQuickLinkSuggestion(snippet);
            e.Handled = true;
        }
    }

    private bool HandleQuickLinkSuggestionKey(KeyEventArgs e)
    {
        if (!QuickLinkBox.IsKeyboardFocusWithin)
        {
            return false;
        }

        if (e.Key == Key.Escape && QuickLinkSuggestionsPopup.IsOpen)
        {
            QuickLinkSuggestionsPopup.IsOpen = false;
            e.Handled = true;
            return true;
        }

        if (!QuickLinkSuggestionsPopup.IsOpen || _viewModel.QuickLinkSuggestions.Count == 0)
        {
            return false;
        }

        if (e.Key == Key.Down)
        {
            MoveQuickLinkSuggestionSelection(1);
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Up)
        {
            MoveQuickLinkSuggestionSelection(-1);
            e.Handled = true;
            return true;
        }

        if (e.Key is Key.Enter or Key.Tab)
        {
            if (QuickLinkSuggestionsList.SelectedItem is AiteProfileSnippet snippet)
            {
                ApplyQuickLinkSuggestion(snippet);
                e.Handled = true;
                return true;
            }
        }

        return false;
    }

    private void MoveQuickLinkSuggestionSelection(int delta)
    {
        int count = _viewModel.QuickLinkSuggestions.Count;
        if (count == 0)
        {
            return;
        }

        int current = QuickLinkSuggestionsList.SelectedIndex;
        int next = current < 0
            ? (delta > 0 ? 0 : count - 1)
            : Math.Clamp(current + delta, 0, count - 1);
        QuickLinkSuggestionsList.SelectedIndex = next;
        QuickLinkSuggestionsList.ScrollIntoView(QuickLinkSuggestionsList.SelectedItem);
    }

    private void ApplyQuickLinkSuggestion(AiteProfileSnippet snippet)
    {
        _viewModel.SelectedQuickLink = snippet;
        QuickLinkSuggestionsPopup.IsOpen = false;
        QuickLinkBox.Focus();
        QuickLinkBox.CaretIndex = QuickLinkBox.Text.Length;
    }

    private void UpdateQuickLinkSuggestionsPopup()
    {
        if (!QuickLinkBox.IsKeyboardFocusWithin)
        {
            QuickLinkSuggestionsPopup.IsOpen = false;
            return;
        }

        bool hasSuggestions = _viewModel.QuickLinkSuggestions.Count > 0;
        bool hasText = !string.IsNullOrWhiteSpace(QuickLinkBox.Text);
        QuickLinkSuggestionsPopup.IsOpen = hasSuggestions && hasText;
        if (QuickLinkSuggestionsPopup.IsOpen && QuickLinkSuggestionsList.SelectedIndex < 0)
        {
            QuickLinkSuggestionsList.SelectedIndex = 0;
        }
    }

    private async Task EditTagsAsync(AiteProfileListItemViewModel? profile)
    {
        if (profile is null)
        {
            return;
        }

        PushSuppressAutoHide();
        try
        {
            var dialog = new AiteProfilesTagsDialog(profile.TagsText)
            {
                Owner = this
            };
            if (dialog.ShowDialog() == true)
            {
                await _viewModel.SetTagsAsync(profile, dialog.TagsText).ConfigureAwait(true);
            }
        }
        finally
        {
            PopSuppressAutoHide();
        }
    }

    private async Task EditQuickLinkAsync(AiteProfileSnippet? original)
    {
        PushSuppressAutoHide();
        try
        {
            var dialog = new AiteProfilesQuickLinkDialog(_viewModel.GetSnippets(), original)
            {
                Owner = this
            };
            if (dialog.ShowDialog() == true && dialog.ResultSnippet is not null)
            {
                await _viewModel.SaveQuickLinkAsync(original, dialog.ResultSnippet).ConfigureAwait(true);
            }
        }
        finally
        {
            PopSuppressAutoHide();
        }
    }

    private async Task ImportQuickLinksAsync()
    {
        PushSuppressAutoHide();
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = LocalizationService.Get("AiteProfiles_LinkImportFilter")
            };
            if (dialog.ShowDialog(this) == true)
            {
                string content = await File.ReadAllTextAsync(dialog.FileName).ConfigureAwait(true);
                await _viewModel.ImportQuickLinksAsync(content).ConfigureAwait(true);
            }
        }
        finally
        {
            PopSuppressAutoHide();
        }
    }

    private async Task ExportQuickLinksAsync()
    {
        PushSuppressAutoHide();
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = LocalizationService.Get("AiteProfiles_LinkExportFilter"),
                FileName = "aiteprofiles-links.txt"
            };
            if (dialog.ShowDialog(this) == true)
            {
                await File.WriteAllTextAsync(dialog.FileName, _viewModel.ExportQuickLinksText()).ConfigureAwait(true);
            }
        }
        finally
        {
            PopSuppressAutoHide();
        }
    }

    private void ShowMessage(string title, string message)
    {
        PushSuppressAutoHide();
        try
        {
            new DarkDialog(message) { Owner = this, Title = title }.ShowDialog();
        }
        finally
        {
            PopSuppressAutoHide();
        }
    }
}
