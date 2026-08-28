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

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnClosed(e);
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
        Loaded -= DiskCleanerWindow_Loaded;
        await RunScanAsync();
    }

    private async Task RunScanAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        UpdateUiState();

        TxtOverallStatus.Foreground = (Brush)FindResource("MutedText");
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
            TxtOverallStatus.Foreground = (Brush)FindResource("MutedText");
            TxtOverallStatus.Text = LocalizationService.Get("DiskCleaner_Status_Ready");
        }
        catch (OperationCanceledException)
        {
            TxtOverallStatus.Foreground = (Brush)FindResource("MutedText");
            TxtOverallStatus.Text = LocalizationService.Get("DiskCleaner_Status_Ready");
        }
        catch (Exception ex)
        {
            TxtOverallStatus.Foreground = (Brush)FindResource("CautionAmberColor");
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

            // Title line with Safety Badge
            var titleLine = new StackPanel { Orientation = Orientation.Horizontal };
            var titleBlock = new TextBlock
            {
                Text = LocalizationService.Get(cat.TitleKey),
                Style = (Style)FindResource("ModernSettingTitleStyle"),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleLine.Children.Add(titleBlock);

            var safetyBadge = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(8, 0, 0, 0),
                Background = cat.IsSafe ? (Brush)FindResource("SafeBadgeBg") : (Brush)FindResource("CautionBadgeBg"),
                VerticalAlignment = VerticalAlignment.Center
            };
            var safetyText = new TextBlock
            {
                Text = LocalizationService.Get(cat.IsSafe ? "DiskCleaner_Badge_Safe" : "DiskCleaner_Badge_Caution"),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = cat.IsSafe ? (Brush)FindResource("SafeGreenColor") : (Brush)FindResource("CautionAmberColor")
            };
            safetyBadge.Child = safetyText;
            titleLine.Children.Add(safetyBadge);

            textStack.Children.Add(titleLine);

            var descBlock = new TextBlock
            {
                Text = LocalizationService.Get(cat.DescriptionKey),
                Style = (Style)FindResource("ModernSettingDescriptionStyle")
            };
            textStack.Children.Add(descBlock);

            if (!string.IsNullOrEmpty(cat.WarningKey))
            {
                var warnBlock = new TextBlock
                {
                    Text = "⚠️ " + LocalizationService.Get(cat.WarningKey),
                    Style = (Style)FindResource("ModernSettingWarningStyle")
                };
                textStack.Children.Add(warnBlock);
            }

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
        BtnSelectSafeOnly.IsEnabled = !_isBusy;
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

        var selectedItems = _categoryItems
            .Where(item => item.Switch.IsChecked == true)
            .ToList();

        if (selectedItems.Count == 0) return;

        // Safety confirmation for Caution categories
        var cautionItems = selectedItems.Where(item => !item.Category.IsSafe).ToList();
        if (cautionItems.Count > 0)
        {
            string itemsList = string.Join("\n• ", cautionItems.Select(c => LocalizationService.Get(c.Category.TitleKey)));
            string message = string.Format(
                LocalizationService.Get("DiskCleaner_ConfirmCautionMessage"),
                cautionItems.Count,
                "• " + itemsList);
            string caption = LocalizationService.Get("DiskCleaner_ConfirmCautionTitle");

            var confirmResult = System.Windows.MessageBox.Show(this, message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmResult != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _isBusy = true;
        UpdateUiState();

        var selectedIds = selectedItems.Select(item => item.Category.Id).ToHashSet(StringComparer.Ordinal);

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
            TxtOverallStatus.Text = string.Format(
                LocalizationService.Get("DiskCleaner_Status_DetailedFormat"),
                formattedFreed,
                result.SucceededCount,
                result.PartialCount,
                result.FailedCount,
                result.SkippedCount);

            if (result.FailedCount > 0)
            {
                TxtOverallStatus.Foreground = (Brush)FindResource("CautionAmberColor");
            }
            else
            {
                TxtOverallStatus.Foreground = (Brush)FindResource("SafeGreenColor");
            }

            PopulateResultDetails(result);

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

    private void PopulateResultDetails(DiskCleanResult result)
    {
        ResultsDetailsPanel.Children.Clear();

        for (int i = 0; i < result.Reports.Count; i++)
        {
            var report = result.Reports[i];
            var matchingCat = _categoryItems.FirstOrDefault(c => c.Category.Id == report.CategoryId)?.Category;
            string title = matchingCat != null ? LocalizationService.Get(matchingCat.TitleKey) : report.CategoryId;

            if (i > 0)
            {
                ResultsDetailsPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = (Brush)FindResource("ModernRowDivider"),
                    Margin = new Thickness(0, 4, 0, 4)
                });
            }

            var rowGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: Title + Reason (if any)
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = (Brush)FindResource("TextPrimary")
            };
            textStack.Children.Add(titleBlock);

            if (!string.IsNullOrWhiteSpace(report.FailureReason))
            {
                var reasonBlock = new TextBlock
                {
                    Text = report.FailureReason,
                    FontSize = 9.5,
                    Foreground = report.Status == DiskCleanCategoryStatus.Failed
                        ? (Brush)FindResource("FailedRedColor")
                        : (Brush)FindResource("CautionAmberColor"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                textStack.Children.Add(reasonBlock);
            }
            Grid.SetColumn(textStack, 0);
            rowGrid.Children.Add(textStack);

            // Middle: Size + Locked info
            var sizeStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var sizeText = new TextBlock
            {
                Text = DiskCleanerService.FormatByteSize(report.FreedBytes),
                FontSize = 11,
                Foreground = report.FreedBytes > 0 ? (Brush)FindResource("SafeGreenColor") : (Brush)FindResource("MutedText"),
                VerticalAlignment = VerticalAlignment.Center
            };
            sizeStack.Children.Add(sizeText);

            if (report.LockedCount > 0)
            {
                var lockedText = new TextBlock
                {
                    Text = " " + string.Format(LocalizationService.Get("DiskCleaner_LockedFilesFormat"), report.LockedCount),
                    FontSize = 10,
                    Foreground = (Brush)FindResource("CautionAmberColor"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                sizeStack.Children.Add(lockedText);
            }
            Grid.SetColumn(sizeStack, 1);
            rowGrid.Children.Add(sizeStack);

            // Right: Status Badge
            var (badgeText, badgeFg, badgeBg) = report.Status switch
            {
                DiskCleanCategoryStatus.Succeeded => (LocalizationService.Get("DiskCleaner_Status_Succeeded"), (Brush)FindResource("SafeGreenColor"), (Brush)FindResource("SafeBadgeBg")),
                DiskCleanCategoryStatus.PartiallyCleaned => (LocalizationService.Get("DiskCleaner_Status_PartiallyCleaned"), (Brush)FindResource("CautionAmberColor"), (Brush)FindResource("CautionBadgeBg")),
                DiskCleanCategoryStatus.Failed => (LocalizationService.Get("DiskCleaner_Status_Failed"), (Brush)FindResource("FailedRedColor"), (Brush)FindResource("FailedBadgeBg")),
                _ => (LocalizationService.Get("DiskCleaner_Status_Skipped"), (Brush)FindResource("MutedText"), (Brush)FindResource("BadgeBackground"))
            };

            var badgeBorder = new Border
            {
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Background = badgeBg,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder.Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = badgeFg
            };
            Grid.SetColumn(badgeBorder, 2);
            rowGrid.Children.Add(badgeBorder);

            ResultsDetailsPanel.Children.Add(rowGrid);
        }

        ResultsCard.Visibility = result.Reports.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnSelectSafeOnly_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _categoryItems)
        {
            item.Switch.IsChecked = item.Category.IsSafe;
        }
        UpdateTotalReclaimable();
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
