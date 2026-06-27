using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class FileSorterWindow : DarkWindow
{
    private readonly AppSettingsService _settingsService;
    private readonly FileSorterService _fileSorterService = new();
    private string? _selectedCustomPath;
    private string? _lastRootPath;
    private FileSortResult? _lastCompletedResult;
    private FileSorterUndoStatus? _lastUndoStatus;

    public FileSorterWindow(AppSettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        LoadLocationOptions();
        SetIdleState();
        StartSpinnerAnimation();
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

    private void LoadLocationOptions()
    {
        CmbLocation.Items.Clear();
        CmbLocation.Items.Add(new ComboBoxItem
        {
            Content = LocalizationService.Get("FileSorter_LocationDownloads"),
            Tag = FileSortLocationKind.Downloads
        });
        CmbLocation.Items.Add(new ComboBoxItem
        {
            Content = LocalizationService.Get("FileSorter_LocationDesktop"),
            Tag = FileSortLocationKind.Desktop
        });
        CmbLocation.Items.Add(new ComboBoxItem
        {
            Content = LocalizationService.Get("FileSorter_SelectFolder"),
            Tag = FileSortLocationKind.Custom
        });
        CmbLocation.SelectedIndex = 0;
    }

    private void SetIdleState()
    {
        _lastCompletedResult = null;
        _lastUndoStatus = null;
        Title = LocalizationService.Get("FileSorter_Title");
        IdleStatePanel.Visibility = Visibility.Visible;
        SortingStatePanel.Visibility = Visibility.Collapsed;
        CompletedStatePanel.Visibility = Visibility.Collapsed;
        TxtUndoStatus.Visibility = Visibility.Collapsed;
        BtnSort.IsEnabled = true;
    }

    private void SetSortingState()
    {
        _lastUndoStatus = null;
        IdleStatePanel.Visibility = Visibility.Collapsed;
        SortingStatePanel.Visibility = Visibility.Visible;
        CompletedStatePanel.Visibility = Visibility.Collapsed;
        TxtUndoStatus.Visibility = Visibility.Collapsed;
        BtnSort.IsEnabled = false;
    }

    private void SetCompletedState(FileSortResult result)
    {
        _lastCompletedResult = result;
        _lastRootPath = result.RootPath;
        _lastUndoStatus = null;
        Title = LocalizationService.Get("FileSorter_Title");
        TxtResultSummary.Text = LocalizationService.Format("FileSorter_ResultFormat", result.SortedCount);
        IdleStatePanel.Visibility = Visibility.Collapsed;
        SortingStatePanel.Visibility = Visibility.Collapsed;
        CompletedStatePanel.Visibility = Visibility.Visible;
        TxtUndoStatus.Visibility = Visibility.Collapsed;
        BtnUndo.IsEnabled = result.UndoState != null;
    }

    private void RefreshLocalizedUi()
    {
        FileSortLocationKind? selectedKind = CmbLocation.SelectedItem is ComboBoxItem { Tag: FileSortLocationKind kind }
            ? kind
            : null;

        LoadLocationOptions();

        if (selectedKind != null)
        {
            foreach (ComboBoxItem item in CmbLocation.Items)
            {
                if (item.Tag is FileSortLocationKind itemKind && itemKind == selectedKind.Value)
                {
                    CmbLocation.SelectedItem = item;
                    break;
                }
            }
        }

        UpdateCustomPathText();

        if (CompletedStatePanel.Visibility == Visibility.Visible && _lastCompletedResult != null)
        {
            SetCompletedState(_lastCompletedResult);
            ApplyUndoStatus();
            return;
        }

        if (SortingStatePanel.Visibility == Visibility.Visible)
        {
            Title = LocalizationService.Get("FileSorter_Title");
            return;
        }

        SetIdleState();
    }

    private void CmbLocation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbLocation.SelectedItem is not ComboBoxItem { Tag: FileSortLocationKind kind })
        {
            return;
        }

        if (kind == FileSortLocationKind.Custom)
        {
            PickCustomFolder();
        }

        UpdateCustomPathText();
    }

    private void UpdateCustomPathText()
    {
        if (CmbLocation.SelectedItem is ComboBoxItem { Tag: FileSortLocationKind.Custom } && !string.IsNullOrWhiteSpace(_selectedCustomPath))
        {
            TxtCustomPath.Text = _selectedCustomPath;
            TxtCustomPath.Visibility = Visibility.Visible;
            return;
        }

        TxtCustomPath.Visibility = Visibility.Collapsed;
    }

    private void PickCustomFolder()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = LocalizationService.Get("FileSorter_SelectFolderDialogTitle"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = string.IsNullOrWhiteSpace(_selectedCustomPath) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : _selectedCustomPath
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            _selectedCustomPath = dialog.SelectedPath;
            return;
        }

        if (CmbLocation.Items.Count > 0)
        {
            CmbLocation.SelectedIndex = 0;
        }
    }

    private async void BtnSort_Click(object sender, RoutedEventArgs e)
    {
        string? rootPath = ResolveSelectedRootPath();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return;
        }

        SetSortingState();

        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);

        try
        {
            FileSortResult result = await _fileSorterService.SortFilesAsync(rootPath);
            _settingsService.Settings.LastFileSortOperation = result.UndoState;
            await _settingsService.SaveAsync();
            SetCompletedState(result);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            SetIdleState();
            new DarkDialog(LocalizationService.Format("FileSorter_ErrorFormat", ex.Message)) { Owner = this }.ShowDialog();
        }
    }

    private string? ResolveSelectedRootPath()
    {
        if (CmbLocation.SelectedItem is not ComboBoxItem { Tag: FileSortLocationKind kind })
        {
            return null;
        }

        return kind switch
        {
            FileSortLocationKind.Downloads => GetDownloadsFolderPath(),
            FileSortLocationKind.Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            FileSortLocationKind.Custom => _selectedCustomPath,
            _ => null
        };
    }

    private async void BtnUndo_Click(object sender, RoutedEventArgs e)
    {
        FileSortUndoState? undoState = _settingsService.Settings.LastFileSortOperation;
        if (undoState == null)
        {
            return;
        }

        try
        {
            FileSortUndoResult result = await _fileSorterService.UndoLastSortAsync(undoState);
            _settingsService.Settings.LastFileSortOperation = result.RemainingUndoState;
            await _settingsService.SaveAsync();

            _lastUndoStatus = new FileSorterUndoStatus(result.RestoredCount, result.SkippedCount);
            ApplyUndoStatus();
            BtnUndo.IsEnabled = result.RemainingUndoState != null;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("FileSorter_ErrorFormat", ex.Message)) { Owner = this }.ShowDialog();
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastRootPath) || !Directory.Exists(_lastRootPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(_lastRootPath) { UseShellExecute = true });
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void StartSpinnerAnimation()
    {
        var animation = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1.1)))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        SpinnerRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, animation);
    }

    private static string GetDownloadsFolderPath()
    {
        IntPtr pathPtr = IntPtr.Zero;
        try
        {
            int hr = SHGetKnownFolderPath(KnownFolderDownloads, 0, IntPtr.Zero, out pathPtr);
            if (hr == 0 && pathPtr != IntPtr.Zero)
            {
                return Marshal.PtrToStringUni(pathPtr) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
        }
        finally
        {
            if (pathPtr != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pathPtr);
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    private static readonly Guid KnownFolderDownloads = new("374DE290-123F-4565-9164-39C4925E467B");

    protected override void OnLocalizationChanged()
    {
        RefreshLocalizedUi();
    }

    private void ApplyUndoStatus()
    {
        if (_lastUndoStatus == null)
        {
            TxtUndoStatus.Visibility = Visibility.Collapsed;
            return;
        }

        TxtUndoStatus.Text = _lastUndoStatus.SkippedCount == 0
            ? LocalizationService.Format("FileSorter_UndoCompleted", _lastUndoStatus.RestoredCount)
            : LocalizationService.Format("FileSorter_UndoPartial", _lastUndoStatus.RestoredCount, _lastUndoStatus.SkippedCount);
        TxtUndoStatus.Visibility = Visibility.Visible;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr ppszPath);
}

internal sealed record FileSorterUndoStatus(int RestoredCount, int SkippedCount);
