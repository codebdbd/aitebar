using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AiteBar;

public sealed class InstalledAppItemViewModel : INotifyPropertyChanged
{
    public InstalledAppInfo AppInfo { get; }
    public string Name => AppInfo.Name;
    public string Path => AppInfo.Path;
    public string Description => AppInfo.Description;
    public bool IsUwp => AppInfo.IsUwp;

    private ImageSource? _icon;
    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (!ReferenceEquals(_icon, value))
            {
                _icon = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public InstalledAppItemViewModel(InstalledAppInfo appInfo)
    {
        AppInfo = appInfo;
    }
}

[SupportedOSPlatform("windows")]
public partial class InstalledAppsWindow : DarkWindow
{
    private readonly ObservableCollection<InstalledAppItemViewModel> _items = [];
    private ICollectionView? _collectionView;
    private CancellationTokenSource? _cts;

    public InstalledAppInfo? SelectedApp { get; private set; }

    public InstalledAppsWindow()
    {
        InitializeComponent();
        _collectionView = CollectionViewSource.GetDefaultView(_items);
        _collectionView.Filter = FilterItem;
        LstApps.ItemsSource = _collectionView;

        Loaded += InstalledAppsWindow_Loaded;
        Closing += InstalledAppsWindow_Closing;
    }

    private async void InstalledAppsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        TxtSearch.Focus();
        await LoadAppsAsync();
    }

    private void InstalledAppsWindow_Closing(object? sender, CancelEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task LoadAppsAsync(bool forceRefresh = false)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        PrgLoading.Visibility = Visibility.Visible;
        LblStatus.Text = LocalizationService.Get("InstalledApps_Loading");

        try
        {
            var apps = await InstalledAppsService.GetInstalledAppsAsync(forceRefresh, token);
            if (token.IsCancellationRequested) return;

            _items.Clear();
            foreach (var app in apps)
            {
                _items.Add(new InstalledAppItemViewModel(app));
            }

            PrgLoading.Visibility = Visibility.Collapsed;
            UpdateCountStatus();

            // Фоновая подгрузка иконок
            _ = LoadIconsInBackgroundAsync(_items.ToList(), token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.Log(ex);
            PrgLoading.Visibility = Visibility.Collapsed;
            LblStatus.Text = "";
        }
    }

    private async Task LoadIconsInBackgroundAsync(List<InstalledAppItemViewModel> viewModels, CancellationToken token)
    {
        await Task.Run(() =>
        {
            foreach (var vm in viewModels)
            {
                if (token.IsCancellationRequested) break;

                string? iconPath = ShellIconHelper.ExtractAndSaveShellIcon(vm.Path, targetSize: 32);
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                            bitmap.EndInit();
                            bitmap.Freeze();
                            vm.Icon = bitmap;
                        }
                        catch { }
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }, token);
    }

    private bool FilterItem(object obj)
    {
        if (obj is not InstalledAppItemViewModel item) return false;
        string query = TxtSearch.Text.Trim();
        if (string.IsNullOrEmpty(query)) return true;

        return item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               item.Path.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               item.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        _collectionView?.Refresh();
        UpdateCountStatus();
    }

    private void UpdateCountStatus()
    {
        int total = _items.Count;
        int shown = _collectionView?.Cast<object>().Count() ?? 0;
        if (total == 0)
        {
            LblStatus.Text = "";
            return;
        }

        LblStatus.Text = shown == total
            ? $"{total}"
            : $"{shown} / {total}";
    }

    private void LstApps_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BtnSelect.IsEnabled = LstApps.SelectedItem != null;
    }

    private void LstApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstApps.SelectedItem is InstalledAppItemViewModel vm)
        {
            ConfirmSelection(vm.AppInfo);
        }
    }

    private void BtnSelect_Click(object sender, RoutedEventArgs e)
    {
        if (LstApps.SelectedItem is InstalledAppItemViewModel vm)
        {
            ConfirmSelection(vm.AppInfo);
        }
    }

    private void ConfirmSelection(InstalledAppInfo app)
    {
        SelectedApp = app;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnBrowseDisk_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = LocalizationService.Get("SettingsWindow_ProgramFilter")
        };

        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (Directory.Exists(progFiles))
        {
            dlg.InitialDirectory = progFiles;
        }

        if (dlg.ShowDialog(this) == true)
        {
            string path = dlg.FileName;
            SelectedApp = new InstalledAppInfo(
                Name: System.IO.Path.GetFileNameWithoutExtension(path),
                Path: path,
                AppType: InstalledAppInfo.TypeDesktop,
                Description: path);
            DialogResult = true;
            Close();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && LstApps.SelectedItem is InstalledAppItemViewModel vm)
        {
            ConfirmSelection(vm.AppInfo);
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            _ = LoadAppsAsync(forceRefresh: true);
            e.Handled = true;
        }
    }
}
