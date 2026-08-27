using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class DiskCleanerWindow : DarkWindow
{
    private sealed class CategoryUiItem
    {
        public required DiskCleanCategory Category { get; set; }
        public required CheckBox Switch { get; set; }
        public required TextBlock SizeText { get; set; }
    }

    private readonly AppSettingsService _settingsService;
    private readonly DiskCleanerService _cleanerService = new();
    private readonly List<CategoryUiItem> _categoryItems = [];
    private bool _isBusy;
    private CancellationTokenSource? _cts;

    public DiskCleanerWindow(AppSettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        Loaded += DiskCleanerWindow_Loaded;
    }

    public void ShowNearPanel(AppSettingsService settingsService)
    {
        var settings = settingsService.Settings;
        var screens = Forms.Screen.AllScreens;
        var screen = settings.MonitorIndex >= 0 && settings.MonitorIndex < screens.Length
            ? screens[settings.MonitorIndex]
            : Forms.Screen.PrimaryScreen;
        var work = screen?.WorkingArea ?? Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);

        var (shownX, shownY) = UtilityWindowLayoutHelper.GetCenteredCoordinates(settings.Edge, work, Width, Height);
        Left = shownX;
        Top = shownY;
        Show();
        Activate();
    }

    private async void DiskCleanerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RunScanAsync();
    }

    private async Task RunScanAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        UpdateUiState();

        TxtOverallStatus.Text = LocalizationService.Get("DiskCleaner_Status_Scanning");
        ProgressBarStatus.Visibility = Visibility.Visible;
        ProgressBarStatus.Value = 0;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var progress = new Progress<DiskCleanProgress>(p =>
        {
            ProgressBarStatus.Value = p.Percentage;
        });

        try
        {
            var result = await _cleanerService.ScanAsync(progress, _cts.Token);
            PopulateCategories(result.Categories);
            TxtOverallStatus.Text = LocalizationService.Get("DiskCleaner_Status_Ready");
        }
        catch (OperationCanceledException)
        {
            TxtOverallStatus.Text = LocalizationService.Get("DiskCleaner_Status_Ready");
        }
        catch (Exception ex)
        {
            TxtOverallStatus.Text = ex.Message;
        }
        finally
        {
            _isBusy = false;
            ProgressBarStatus.Visibility = Visibility.Collapsed;
            UpdateUiState();
            UpdateTotalReclaimable();
        }
    }

    private void PopulateCategories(IReadOnlyList<DiskCleanCategory> categories)
    {
        CategoriesPanel.Children.Clear();
        _categoryItems.Clear();

        for (int i = 0; i < categories.Count; i++)
        {
            var cat = categories[i];
            if (i > 0)
            {
                CategoriesPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = (Brush)FindResource("ModernRowDivider")
                });
            }

            var rowGrid = new Grid { Margin = new Thickness(12, 10, 12, 10) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel { Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            var titleBlock = new TextBlock
            {
                Text = LocalizationService.Get(cat.TitleKey),
                Style = (Style)FindResource("ModernSettingTitleStyle")
            };
            var descBlock = new TextBlock
            {
                Text = LocalizationService.Get(cat.DescriptionKey),
                Style = (Style)FindResource("ModernSettingDescriptionStyle")
            };
            textStack.Children.Add(titleBlock);
            textStack.Children.Add(descBlock);
            Grid.SetColumn(textStack, 0);
            rowGrid.Children.Add(textStack);

            // Size Badge
            var badgeBorder = new Border { Style = (Style)FindResource("SizeBadgeStyle") };
            var sizeText = new TextBlock
            {
                Text = DiskCleanerService.FormatByteSize(cat.SizeBytes),
                FontSize = 11,
                Foreground = cat.SizeBytes > 0 ? (Brush)FindResource("SafeGreenColor") : (Brush)FindResource("MutedText"),
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder.Child = sizeText;
            Grid.SetColumn(badgeBorder, 1);
            rowGrid.Children.Add(badgeBorder);

            // Toggle Switch
            var toggleSwitch = new CheckBox
            {
                IsChecked = cat.IsSelected,
                Style = (Style)FindResource("ModernSwitchStyle")
            };
            toggleSwitch.Checked += (_, _) => UpdateTotalReclaimable();
            toggleSwitch.Unchecked += (_, _) => UpdateTotalReclaimable();
            Grid.SetColumn(toggleSwitch, 2);
            rowGrid.Children.Add(toggleSwitch);

            CategoriesPanel.Children.Add(rowGrid);
            _categoryItems.Add(new CategoryUiItem
            {
                Category = cat,
                Switch = toggleSwitch,
                SizeText = sizeText
            });
        }
    }

    private void UpdateTotalReclaimable()
    {
        long selectedTotal = _categoryItems
            .Where(item => item.Switch.IsChecked == true)
            .Sum(item => item.Category.SizeBytes);

        string formatted = DiskCleanerService.FormatByteSize(selectedTotal);
        TxtTotalFound.Text = string.Format(LocalizationService.Get("DiskCleaner_ReclaimableFormat"), formatted);

        string cleanTitle = LocalizationService.Get("DiskCleaner_Clean");
        BtnClean.Content = selectedTotal > 0
            ? $"{cleanTitle} ({formatted})"
            : cleanTitle;

        BtnClean.IsEnabled = !_isBusy && selectedTotal >= 0 && _categoryItems.Any(c => c.Switch.IsChecked == true);
    }

    private void UpdateUiState()
    {
        BtnScan.IsEnabled = !_isBusy;
        BtnSelectAll.IsEnabled = !_isBusy;
        BtnDeselectAll.IsEnabled = !_isBusy;
        foreach (var item in _categoryItems)
        {
            item.Switch.IsEnabled = !_isBusy;
        }
    }

    private async void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        await RunScanAsync();
    }

    private async void BtnClean_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        _isBusy = true;
        UpdateUiState();

        var selectedIds = _categoryItems
            .Where(item => item.Switch.IsChecked == true)
            .Select(item => item.Category.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (selectedIds.Count == 0)
        {
            _isBusy = false;
            UpdateUiState();
            return;
        }

        TxtOverallStatus.Text = LocalizationService.Get("DiskCleaner_Status_Cleaning");
        ProgressBarStatus.Visibility = Visibility.Visible;
        ProgressBarStatus.Value = 0;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var progress = new Progress<DiskCleanProgress>(p =>
        {
            ProgressBarStatus.Value = p.Percentage;
        });

        try
        {
            var result = await _cleanerService.CleanAsync(selectedIds, progress, _cts.Token);
            string formattedFreed = DiskCleanerService.FormatByteSize(result.TotalFreedBytes);
            TxtOverallStatus.Text = string.Format(LocalizationService.Get("DiskCleaner_Status_CompletedFormat"), formattedFreed);

            // Re-run quick scan to refresh remaining sizes
            await Task.Delay(500);
            var scanRes = await _cleanerService.ScanAsync(null, _cts.Token);
            PopulateCategories(scanRes.Categories);
        }
        catch (OperationCanceledException)
        {
            TxtOverallStatus.Text = LocalizationService.Get("DiskCleaner_Status_Ready");
        }
        catch (Exception ex)
        {
            TxtOverallStatus.Text = ex.Message;
        }
        finally
        {
            _isBusy = false;
            ProgressBarStatus.Visibility = Visibility.Collapsed;
            UpdateUiState();
            UpdateTotalReclaimable();
        }
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _categoryItems)
        {
            item.Switch.IsChecked = true;
        }
        UpdateTotalReclaimable();
    }

    private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _categoryItems)
        {
            item.Switch.IsChecked = false;
        }
        UpdateTotalReclaimable();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
