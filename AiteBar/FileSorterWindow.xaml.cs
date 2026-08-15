using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class FileSorterWindow : DarkWindow
{
    private static readonly FontFamily RowActionIconFont = new FontFamily(
        new Uri("pack://application:,,,/"),
        "./Resources/#FluentSystemIcons-Regular");
    private readonly AppSettingsService _settingsService;
    private readonly FileSorterService _fileSorterService = new();
    private readonly List<FolderListEntry> _folderEntries = [];
    private bool _isBusy;
    private string _overallStatusKey = "FileSorter_StatusReady";
    private object[] _overallStatusArguments = [];

    public FileSorterWindow(AppSettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        PopulateFolderList();
        UpdateSortButtonContent();
        ApplyOverallStatus();
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

    private void PopulateFolderList(
        IReadOnlySet<string>? selectedPaths = null,
        IReadOnlyDictionary<string, FolderVisualState>? visualStates = null)
    {
        _folderEntries.Clear();
        FolderListPanel.Children.Clear();

        string downloadsPath = GetDownloadsFolderPath();
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            downloadsPath,
            desktopPath
        };

        AddFolderRow(
            path: desktopPath,
            title: LocalizationService.Get("FileSorter_LocationDesktop"),
            subtitle: desktopPath,
            isSystem: true,
            isSelected: selectedPaths?.Contains(desktopPath) ?? false,
            visualState: visualStates?.GetValueOrDefault(desktopPath));

        AddFolderRow(
            path: downloadsPath,
            title: LocalizationService.Get("FileSorter_LocationDownloads"),
            subtitle: downloadsPath,
            isSystem: true,
            isSelected: selectedPaths?.Contains(downloadsPath) ?? true,
            visualState: visualStates?.GetValueOrDefault(downloadsPath));

        List<string> savedFolders = _settingsService.Settings.SavedFileSortFolders ?? [];
        foreach (string folder in savedFolders)
        {
            if (!TryNormalizeFolderPath(folder, out string normalized) || !knownPaths.Add(normalized))
            {
                continue;
            }

            string title = Path.GetFileName(normalized).Trim();
            if (string.IsNullOrWhiteSpace(title)) title = normalized;

            AddFolderRow(
                path: normalized,
                title: title,
                subtitle: normalized,
                isSystem: false,
                isSelected: selectedPaths?.Contains(normalized) ?? false,
                visualState: visualStates?.GetValueOrDefault(normalized));
        }

        RefreshUndoButtons();
    }

    private void AddFolderRow(
        string path,
        string title,
        string subtitle,
        bool isSystem,
        bool isSelected,
        FolderVisualState? visualState = null)
    {
        var grid = new Grid { Margin = new Thickness(12, 10, 12, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var checkBox = new CheckBox
        {
            Style = (Style)FindResource("ModernSwitchStyle"),
            IsChecked = isSelected,
            Margin = new Thickness(0, 0, 12, 0)
        };
        checkBox.Checked += FolderCheckBox_CheckedChanged;
        checkBox.Unchecked += FolderCheckBox_CheckedChanged;
        Grid.SetColumn(checkBox, 0);
        grid.Children.Add(checkBox);

        var textPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        var titleText = new TextBlock
        {
            Text = title,
            Style = (Style)FindResource("ModernSettingTitleStyle"),
            ToolTip = subtitle
        };
        var subtitleText = new TextBlock
        {
            Text = subtitle,
            Style = (Style)FindResource("ModernSettingDescriptionStyle"),
            ToolTip = subtitle
        };
        textPanel.Children.Add(titleText);
        textPanel.Children.Add(subtitleText);

        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(textPanel);

        var statusText = new TextBlock
        {
            Foreground = GetStatusBrush(visualState?.StatusTone ?? FolderStatusTone.Success),
            FontSize = 11,
            Margin = new Thickness(8, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Width = 96,
            TextAlignment = TextAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = FormatVisualStatus(visualState)
        };
        Grid.SetColumn(statusText, 2);
        grid.Children.Add(statusText);

        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        string undoTooltip = LocalizationService.Get("FileSorter_UndoFolderTooltip");
        var undoButton = new Button
        {
            Content = "\uF19A",
            Style = (Style)FindResource("FolderActionButtonStyle"),
            FontFamily = RowActionIconFont,
            ToolTip = undoTooltip
        };
        System.Windows.Automation.AutomationProperties.SetName(undoButton, undoTooltip);
        undoButton.Click += async (_, _) => await UndoFolderAsync(path);

        string openTooltip = LocalizationService.Get("FileSorter_OpenFolderTooltip");
        var openButton = new Button
        {
            Content = "\uF42F",
            Style = (Style)FindResource("FolderActionButtonStyle"),
            FontFamily = RowActionIconFont,
            ToolTip = openTooltip
        };
        System.Windows.Automation.AutomationProperties.SetName(openButton, openTooltip);
        openButton.Click += (_, _) => OpenFolder(path);

        string removeTooltip = LocalizationService.Get("FileSorter_RemoveFolderContextMenu");
        var removeButton = new Button
        {
            Content = "\uF34D",
            Style = (Style)FindResource("FolderActionButtonStyle"),
            FontFamily = RowActionIconFont,
            ToolTip = removeTooltip,
            Visibility = isSystem ? Visibility.Collapsed : Visibility.Visible
        };
        System.Windows.Automation.AutomationProperties.SetName(removeButton, removeTooltip);
        removeButton.Click += async (_, _) => await RemoveSavedFolderAsync(path);

        actionsPanel.Children.Add(undoButton);
        actionsPanel.Children.Add(openButton);
        actionsPanel.Children.Add(removeButton);
        Grid.SetColumn(actionsPanel, 3);
        grid.Children.Add(actionsPanel);

        var entry = new FolderListEntry(
            path,
            checkBox,
            statusText,
            undoButton,
            openButton)
        {
            StatusKey = visualState?.StatusKey,
            StatusArguments = visualState?.StatusArguments ?? [],
            StatusTone = visualState?.StatusTone ?? FolderStatusTone.Success
        };

        if (!isSystem)
        {
            ContextMenu menu = AppContextMenuFactory.CreateMenu(this);
            menu.Items.Add(AppContextMenuFactory.CreateItem(
                this,
                "\uF34D",
                LocalizationService.Get("FileSorter_RemoveFolderContextMenu"),
                async (_, _) => await RemoveSavedFolderAsync(path),
                isDanger: true));
            grid.ContextMenu = menu;
        }

        _folderEntries.Add(entry);
        FolderListPanel.Children.Add(grid);

        if (_folderEntries.Count > 1)
        {
            var divider = new Border { Style = (Style)FindResource("ModernDividerStyle") };
            FolderListPanel.Children.Insert(FolderListPanel.Children.Count - 1, divider);
        }
    }

    private void FolderCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        UpdateSortButtonContent();
    }

    private void UpdateSortButtonContent()
    {
        int selected = GetSelectedPaths().Count;
        BtnSort.Content = selected switch
        {
            0 => LocalizationService.Get("FileSorter_SortButton"),
            1 => LocalizationService.Get("FileSorter_SortButton"),
            _ => LocalizationService.Format("FileSorter_SortMultipleFormat", selected)
        };
        TxtSelectionCount.Text = LocalizationService.Format("FileSorter_SelectedCountFormat", selected);
        RefreshInteractionState();
    }

    private void RefreshInteractionState()
    {
        bool hasSelection = _folderEntries.Any(entry => entry.CheckBox.IsChecked == true);
        bool acceptsInput = !_isBusy;

        // Keep the visual state stable while work is running. Switching IsEnabled on the
        // whole window makes WPF apply disabled opacity to every row at once, which looks
        // like a flash. Hit testing blocks interaction without changing appearance.
        BtnSort.IsEnabled = hasSelection;
        BtnSort.IsHitTestVisible = acceptsInput;
        BtnAddFolder.IsEnabled = true;
        BtnAddFolder.IsHitTestVisible = acceptsInput;
        foreach (FolderListEntry entry in _folderEntries)
        {
            entry.CheckBox.IsEnabled = true;
            entry.CheckBox.IsHitTestVisible = acceptsInput;
            entry.OpenButton.IsEnabled = Directory.Exists(entry.Path);
            entry.OpenButton.IsHitTestVisible = acceptsInput;
            Grid? rowGrid = entry.CheckBox.Parent as Grid;
            if (rowGrid != null)
            {
                Button? deleteGlyphButton = rowGrid.Children
                    .OfType<StackPanel>()
                    .Where(panel => Grid.GetColumn(panel) == 3)
                    .SelectMany(panel => panel.Children.OfType<Button>())
                    .FirstOrDefault(btn => string.Equals("\uF34D", btn.Content as string, StringComparison.Ordinal));
                if (deleteGlyphButton != null)
                {
                    deleteGlyphButton.IsEnabled = acceptsInput;
                    deleteGlyphButton.IsHitTestVisible = acceptsInput;
                }
            }
            entry.UndoButton.IsEnabled = FindUndoState(entry.Path) != null;
            entry.UndoButton.IsHitTestVisible = acceptsInput;
        }
    }

    private List<string> GetSelectedPaths()
    {
        var list = new List<string>(_folderEntries.Count);
        foreach (var entry in _folderEntries)
        {
            if (entry.CheckBox.IsChecked == true)
            {
                list.Add(entry.Path);
            }
        }
        return list;
    }

    private async Task RemoveSavedFolderAsync(string path)
    {
        if (_isBusy)
        {
            return;
        }

        HashSet<string> selectedPaths = GetSelectedPaths().ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, FolderVisualState> visualStates = CaptureVisualStates();
        selectedPaths.Remove(path);
        visualStates.Remove(path);

        var updated = new List<string>();
        foreach (string savedPath in _settingsService.Settings.SavedFileSortFolders ?? [])
        {
            if (TryNormalizeFolderPath(savedPath, out string normalized) &&
                !string.Equals(normalized, path, StringComparison.OrdinalIgnoreCase) &&
                !updated.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                updated.Add(normalized);
            }
        }

        _settingsService.UpdateSettings(s => s.SavedFileSortFolders = updated);
        await _settingsService.SaveAsync();
        PopulateFolderList(selectedPaths, visualStates);
        UpdateSortButtonContent();
    }

    private void RefreshLocalizedUi()
    {
        HashSet<string> selectedPaths = GetSelectedPaths().ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, FolderVisualState> visualStates = CaptureVisualStates();
        PopulateFolderList(selectedPaths, visualStates);
        Title = LocalizationService.Get("FileSorter_Title");
        UpdateSortButtonContent();
        ApplyOverallStatus();
    }

    private Dictionary<string, FolderVisualState> CaptureVisualStates() =>
        _folderEntries.ToDictionary(
            entry => entry.Path,
            entry => new FolderVisualState(
                entry.StatusKey,
                entry.StatusArguments,
                entry.StatusTone),
            StringComparer.OrdinalIgnoreCase);

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        RefreshInteractionState();
    }

    private void SetOverallStatus(string resourceKey, params object[] arguments)
    {
        _overallStatusKey = resourceKey;
        _overallStatusArguments = arguments;
        ApplyOverallStatus();
    }

    private void ApplyOverallStatus()
    {
        TxtOverallStatus.Text = _overallStatusArguments.Length == 0
            ? LocalizationService.Get(_overallStatusKey)
            : LocalizationService.Format(_overallStatusKey, _overallStatusArguments);
    }

    private static string FormatVisualStatus(FolderVisualState? state)
    {
        if (state?.StatusKey == null)
        {
            return string.Empty;
        }

        return state.StatusArguments.Length == 0
            ? LocalizationService.Get(state.StatusKey)
            : LocalizationService.Format(state.StatusKey, state.StatusArguments);
    }

    private Brush GetStatusBrush(FolderStatusTone tone) => tone switch
    {
        FolderStatusTone.Error => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF0, 0x7A, 0x7A)),
        FolderStatusTone.Muted => (Brush)FindResource("MutedText"),
        _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7B, 0xC9, 0x9B))
    };

    private void SetRowStatus(
        FolderListEntry entry,
        string? resourceKey,
        FolderStatusTone tone = FolderStatusTone.Success,
        params object[] arguments)
    {
        entry.StatusKey = resourceKey;
        entry.StatusArguments = arguments;
        entry.StatusTone = tone;
        entry.StatusText.Foreground = GetStatusBrush(tone);
        entry.StatusText.Text = resourceKey == null
            ? string.Empty
            : arguments.Length == 0
                ? LocalizationService.Get(resourceKey)
                : LocalizationService.Format(resourceKey, arguments);
    }

    private FolderListEntry? FindEntry(string path) =>
        _folderEntries.FirstOrDefault(entry => PathsEqual(entry.Path, path));

    private void ApplyProgress(MultiFileSortProgress progress)
    {
        FolderListEntry? entry = FindEntry(progress.RootPath);
        if (entry == null)
        {
            return;
        }

        SetRowStatus(
            entry,
            "FileSorter_RowProgressFormat",
            FolderStatusTone.Muted,
            progress.ProcessedFiles,
            progress.TotalFiles);
        SetOverallStatus(
            "FileSorter_StatusSortingFormat",
            progress.FolderIndex + 1,
            progress.FolderCount);
    }

    private void ApplySortResults(MultiFileSortResult result)
    {
        foreach (FileSortResult folderResult in result.PerFolder)
        {
            FolderListEntry? entry = FindEntry(folderResult.RootPath);
            if (entry == null)
            {
                continue;
            }

            SetRowStatus(entry, "FileSorter_RowSortedFormat", FolderStatusTone.Success, folderResult.SortedCount);
        }
    }

    private async void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = LocalizationService.Get("FileSorter_SelectFolderDialogTitle"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        if (!TryNormalizeFolderPath(dialog.SelectedPath, out string selected))
        {
            return;
        }

        if (_folderEntries.Any(x => string.Equals(x.Path, selected, StringComparison.OrdinalIgnoreCase)))
        {
            new DarkDialog(LocalizationService.Get("FileSorter_FolderAlreadyAdded")) { Owner = this }.ShowDialog();
            return;
        }

        var saved = new List<string>();
        foreach (string savedPath in _settingsService.Settings.SavedFileSortFolders ?? [])
        {
            if (TryNormalizeFolderPath(savedPath, out string normalized) &&
                !saved.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                saved.Add(normalized);
            }
        }

        saved.Add(selected);
        _settingsService.UpdateSettings(s => s.SavedFileSortFolders = saved);
        await _settingsService.SaveAsync();

        string title = Path.GetFileName(selected).Trim();
        if (string.IsNullOrWhiteSpace(title)) title = selected;

        AddFolderRow(
            path: selected,
            title: title,
            subtitle: selected,
            isSystem: false,
            isSelected: true);

        UpdateSortButtonContent();
    }

    private async void BtnSort_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        List<string> selected = GetSelectedPaths();
        if (selected.Count == 0)
        {
            new DarkDialog(LocalizationService.Get("FileSorter_NoFoldersSelected")) { Owner = this }.ShowDialog();
            return;
        }

        SetBusy(true);
        SetOverallStatus("FileSorter_StatusSortingFormat", 0, selected.Count);

        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);

        try
        {
            var progress = new FileSorterUiProgress(Dispatcher, ApplyProgress);
            MultiFileSortResult result = await Task.Run(
                () => _fileSorterService.SortMultipleFoldersAsync(selected, progress));
            ApplySortResults(result);
            await MergeUndoStatesAsync(result.PerFolder);
            if (result.PerFolder.Count == 1)
            {
                SetOverallStatus("FileSorter_ResultFormat", result.TotalSorted);
            }
            else
            {
                SetOverallStatus("FileSorter_CompletedMultiFormat", result.TotalSorted, result.PerFolder.Count);
            }
        }
        catch (MultiFileSortException ex)
        {
            Logger.Log(ex);
            ApplySortResults(ex.PartialResult);
            await MergeUndoStatesAsync(ex.PartialResult.PerFolder);
            FolderListEntry? failedEntry = FindEntry(ex.FailedRootPath);
            if (failedEntry != null)
            {
                SetRowStatus(failedEntry, "FileSorter_RowError", FolderStatusTone.Error);
            }

            SetOverallStatus("FileSorter_StatusError");
            new DarkDialog(LocalizationService.Format("FileSorter_ErrorFormat", ex.Message)) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            SetOverallStatus("FileSorter_StatusError");
            new DarkDialog(LocalizationService.Format("FileSorter_ErrorFormat", ex.Message)) { Owner = this }.ShowDialog();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task UndoFolderAsync(string path)
    {
        FileSortUndoState? undoState = FindUndoState(path);
        FolderListEntry? entry = FindEntry(path);
        if (_isBusy || undoState == null || entry == null)
        {
            return;
        }

        SetBusy(true);
        SetRowStatus(entry, "FileSorter_RowUndoing", FolderStatusTone.Muted);
        SetOverallStatus("FileSorter_StatusUndoing");
        try
        {
            FileSortUndoResult result = await Task.Run(
                () => _fileSorterService.UndoLastSortAsync(undoState));
            await ReplaceUndoStateAsync(path, result.RemainingUndoState);
            string statusKey = result.SkippedCount == 0
                ? "FileSorter_UndoCompleted"
                : "FileSorter_UndoPartial";
            object[] statusArguments = result.SkippedCount == 0
                ? [result.RestoredCount]
                : [result.RestoredCount, result.SkippedCount];
            SetRowStatus(entry, statusKey, FolderStatusTone.Success, statusArguments);
            SetOverallStatus(statusKey, statusArguments);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            SetRowStatus(entry, "FileSorter_RowError", FolderStatusTone.Error);
            SetOverallStatus("FileSorter_StatusError");
            new DarkDialog(LocalizationService.Format("FileSorter_ErrorFormat", ex.Message)) { Owner = this }.ShowDialog();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OpenFolder(string path)
    {
        if (_isBusy || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            SetOverallStatus("FileSorter_StatusError");
            new DarkDialog(LocalizationService.Format("FileSorter_ErrorFormat", ex.Message)) { Owner = this }.ShowDialog();
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_isBusy)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isBusy)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
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

    internal static bool TryNormalizeFolderPath(string? path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            normalized = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or System.Security.SecurityException)
        {
            Logger.Log(new IOException($"File sorter ignored invalid saved folder path '{path}'.", ex));
            return false;
        }
    }

    private static bool PathsEqual(string left, string right) =>
        FileSorterUndoStateHelper.PathsEqual(left, right);

    private List<FileSortUndoState> GetUndoStatesSnapshot()
    {
        MultiFileSortUndoState? multiState = GetLastMultiFileSortOperation(_settingsService);
        if (multiState?.PerFolder is { Count: > 0 })
        {
            return [.. multiState.PerFolder];
        }

        FileSortUndoState? singleState = GetLastFileSortOperation(_settingsService);
        return singleState == null ? [] : [singleState];
    }

    private FileSortUndoState? FindUndoState(string path) =>
        FileSorterUndoStateHelper.Find(GetUndoStatesSnapshot(), path);

    private async Task MergeUndoStatesAsync(IEnumerable<FileSortResult> results)
    {
        List<FileSortUndoState> states = FileSorterUndoStateHelper.Merge(GetUndoStatesSnapshot(), results);
        await PersistUndoStatesAsync(states);
    }

    private async Task ReplaceUndoStateAsync(string path, FileSortUndoState? replacement)
    {
        List<FileSortUndoState> states = FileSorterUndoStateHelper.Replace(GetUndoStatesSnapshot(), path, replacement);
        await PersistUndoStatesAsync(states);
    }

    private async Task PersistUndoStatesAsync(List<FileSortUndoState> states)
    {
        MultiFileSortUndoState? multiState = states.Count == 0
            ? null
            : new MultiFileSortUndoState { PerFolder = [.. states] };
        SetLastMultiFileSortOperation(_settingsService, multiState);
        SetLastFileSortOperation(_settingsService, states.Count == 1 ? states[0] : null);
        await _settingsService.SaveAsync();
        RefreshUndoButtons();
    }

    private void RefreshUndoButtons()
    {
        RefreshInteractionState();
    }

    private static readonly Guid KnownFolderDownloads = new("374DE290-123F-4565-9164-39C4925E467B");

    internal static FileSortUndoState? GetLastFileSortOperation(AppSettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        return settingsService.Settings.LastFileSortOperation;
    }

    internal static void SetLastFileSortOperation(AppSettingsService settingsService, FileSortUndoState? undoState)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        settingsService.UpdateSettings(settings => settings.LastFileSortOperation = undoState);
    }

    internal static MultiFileSortUndoState? GetLastMultiFileSortOperation(AppSettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        return settingsService.Settings.LastMultiFileSortOperation;
    }

    internal static void SetLastMultiFileSortOperation(AppSettingsService settingsService, MultiFileSortUndoState? undoState)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        settingsService.UpdateSettings(settings => settings.LastMultiFileSortOperation = undoState);
    }

    protected override void OnLocalizationChanged()
    {
        RefreshLocalizedUi();
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr ppszPath);

    private sealed class FolderListEntry
    {
        public string Path { get; }
        public CheckBox CheckBox { get; }
        public TextBlock StatusText { get; }
        public Button UndoButton { get; }
        public Button OpenButton { get; }
        public string? StatusKey { get; set; }
        public object[] StatusArguments { get; set; } = [];
        public FolderStatusTone StatusTone { get; set; }

        public FolderListEntry(
            string path,
            CheckBox checkBox,
            TextBlock statusText,
            Button undoButton,
            Button openButton)
        {
            Path = path;
            CheckBox = checkBox;
            StatusText = statusText;
            UndoButton = undoButton;
            OpenButton = openButton;
        }
    }

    private sealed record FolderVisualState(
        string? StatusKey,
        object[] StatusArguments,
        FolderStatusTone StatusTone);

    private enum FolderStatusTone
    {
        Success,
        Error,
        Muted
    }

    private sealed class FileSorterUiProgress(
        System.Windows.Threading.Dispatcher dispatcher,
        Action<MultiFileSortProgress> callback) : IProgress<MultiFileSortProgress>
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private int _lastFolderIndex = -1;

        public void Report(MultiFileSortProgress value)
        {
            bool folderChanged = value.FolderIndex != _lastFolderIndex;
            bool folderCompleted = value.ProcessedFiles == value.TotalFiles;
            if (!folderChanged && !folderCompleted && _stopwatch.ElapsedMilliseconds < 50)
            {
                return;
            }

            _lastFolderIndex = value.FolderIndex;
            _stopwatch.Restart();
            if (dispatcher.CheckAccess())
            {
                callback(value);
                return;
            }

            dispatcher.Invoke(() => callback(value));
        }
    }
}
