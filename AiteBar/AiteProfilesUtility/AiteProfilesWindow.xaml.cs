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
    private bool _suppressAutoHide;
    private bool _initialized;

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
        if (_suppressAutoHide)
        {
            return;
        }

        Hide();
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e) => _suppressAutoHide = true;

    private void ContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        _suppressAutoHide = false;
        if (!IsActive)
        {
            Hide();
        }
    }

    private void ProfilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (object item in e.RemovedItems)
        {
            if (item is AiteProfileListItemViewModel profile)
            {
                profile.IsSelected = false;
            }
        }

        foreach (object item in e.AddedItems)
        {
            if (item is AiteProfileListItemViewModel profile)
            {
                profile.IsSelected = true;
                _viewModel.CurrentProfile = profile;
            }
        }
    }

    private void ProfileCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: AiteProfileListItemViewModel item })
        {
            _viewModel.ToggleItemSelection(item, item.IsSelected);
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
        while (source is not null && source is not DataGridRow)
        {
            source = VisualTreeHelper.GetParent(source);
        }

        if (source is DataGridRow { DataContext: AiteProfileListItemViewModel item } row)
        {
            row.IsSelected = true;
            _viewModel.CurrentProfile = item;
        }
    }

    private void ProfilesGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        int column = e.Column.SortMemberPath switch
        {
            "LastTs" => 2,
            _ => e.Column.DisplayIndex == 1 ? 4 : 0
        };
        _viewModel.SetSortColumn(column);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
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

    private async Task EditTagsAsync(AiteProfileListItemViewModel? profile)
    {
        if (profile is null)
        {
            return;
        }

        _suppressAutoHide = true;
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
            _suppressAutoHide = false;
        }
    }

    private async Task EditQuickLinkAsync(AiteProfileSnippet? original)
    {
        _suppressAutoHide = true;
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
            _suppressAutoHide = false;
        }
    }

    private async Task ImportQuickLinksAsync()
    {
        _suppressAutoHide = true;
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
            _suppressAutoHide = false;
        }
    }

    private async Task ExportQuickLinksAsync()
    {
        _suppressAutoHide = true;
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
            _suppressAutoHide = false;
        }
    }

    private void ShowMessage(string title, string message)
    {
        _suppressAutoHide = true;
        try
        {
            new DarkDialog(message) { Owner = this, Title = title }.ShowDialog();
        }
        finally
        {
            _suppressAutoHide = false;
        }
    }
}
