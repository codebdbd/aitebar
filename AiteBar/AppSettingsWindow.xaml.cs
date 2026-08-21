using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows.Automation;
using System.Windows.Data;

namespace AiteBar;

public class FontNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => FontHelper.Resolve(value?.ToString() ?? FontHelper.FluentKey);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ColorBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.BrushConverter _bc = new();
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => _bc.ConvertFromString(value?.ToString() ?? "#FFD700") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Gold;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

internal sealed record AppSettingsContextRowState(string ContextId, string Name, bool IsEnabled);

[SupportedOSPlatform("windows6.1")]
public partial class AppSettingsWindow : DarkWindow
{
    private const string SupportUrl = "https://codebdbd.github.io/";
    private const string RepositoryUrl = "https://github.com/codebdbd/aitebar";
    private sealed record ContextRowDraft(string ContextId, string Name, bool IsEnabled, bool IsNameCustomized);
    private sealed record ContextRow(
        string ContextId,
        Grid RowGrid,
        CheckBox EnabledCheckBox,
        TextBox NameTextBox,
        Border BadgeBorder,
        Button ClearButton,
        Border DragHandle,
        Border RowSurface,
        FrameworkElement InsertBeforeIndicator,
        FrameworkElement InsertAfterIndicator);

    private readonly MainWindow _mainWindow;
    private readonly AppSettings _settings;
    private readonly string _dataDirectory;
    private readonly List<ContextRow> _contextRows = new();
    private readonly IAiCredentialStore _aiCredentialStore = new WindowsAiCredentialStore();
    private readonly AiProviderClient _aiProviderClient;
    private readonly List<AiConnectionSettings> _aiConnections = [];
    private readonly HashSet<string> _pendingAiCredentialTargets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _removedAiCredentialTargets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AiConnectionCheckResult> _aiConnectionChecks = new(StringComparer.Ordinal);
    private bool _aiSettingsCommitted;
    private bool _isLoadingSettings;
    private bool _panelSizeSelectionChanged;
    private bool _activationZoneSelectionChanged;
    private bool _activationDelaySelectionChanged;
    private readonly string _originalUiCulture;
    private string _selectedUiCulture = LocalizationService.AutoCulture;
    private bool _isSynchronizingNavigation;
    private bool _isWindowLoaded;
    private AppSettingsSection _requestedSection;
    private string? _draggedContextId;
    private Point _contextDragStartPoint;
    private string? _dragOverContextId;
    private bool _dragOverAfter;

    public AppSettingsWindow(MainWindow mainWindow, AppSettingsSection initialSection = AppSettingsSection.General)
    {
        _aiProviderClient = new AiProviderClient(_aiCredentialStore);
        InitializeComponent();
        LocalizationService.EnsureAppliedCulture();
        LocalizationService.RefreshLocalizedBindings(this);
        _mainWindow = mainWindow;
        _settings = _mainWindow.GetAppSettings();
        _dataDirectory = PathHelper.AppDataFolder;
        _requestedSection = initialSection;
        _originalUiCulture = LocalizationService.NormalizeCultureName(_settings.UiCulture);

        _isLoadingSettings = true;
        LoadLanguageList();
        LoadSettings();
        NormalizeDiscreteSettings();
        _panelSizeSelectionChanged = false;
        _activationZoneSelectionChanged = false;
        _activationDelaySelectionChanged = false;
        _isLoadingSettings = false;
        RefreshLocalizedUi();
        Closed += (_, _) => CleanupPendingAiCredentials();
    }

    private FrameworkElement[] GetSettingsSections() =>
    [
        GeneralSettingsSection,
        ContextSettingsSection,
        HotkeySettingsSection,
        QuickToolsSettingsSection,
        AiProvidersSettingsSection,
        AboutSettingsSection
    ];

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _isWindowLoaded = true;
        NavigateToSection(_requestedSection);
    }

    private void BtnKeepOnTop_Click(object sender, RoutedEventArgs e)
    {
        Topmost = BtnKeepOnTop.IsChecked == true;
    }

    private void SettingsNavigationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isWindowLoaded || _isSynchronizingNavigation || SettingsNavigationList.SelectedIndex < 0)
        {
            return;
        }

        ScrollToSection(SettingsNavigationList.SelectedIndex);
    }

    private void SettingsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!_isWindowLoaded || _isSynchronizingNavigation)
        {
            return;
        }

        FrameworkElement[] sections = GetSettingsSections();
        double[] sectionTops = sections
            .Select(GetSectionTop)
            .ToArray();
        int activeIndex = AppSettingsSectionNavigationHelper.GetActiveSectionIndex(
            sectionTops,
            SettingsScrollViewer.VerticalOffset,
            SettingsScrollViewer.ViewportHeight,
            SettingsScrollViewer.ExtentHeight);

        SetNavigationSelection(activeIndex);
    }

    private void ScrollToSection(int sectionIndex)
    {
        FrameworkElement[] sections = GetSettingsSections();
        if (sectionIndex < 0 || sectionIndex >= sections.Length)
        {
            return;
        }

        double targetOffset = AppSettingsSectionNavigationHelper.GetTargetOffset(
            GetSectionTop(sections[sectionIndex]),
            SettingsScrollViewer.ScrollableHeight);
        SettingsScrollViewer.ScrollToVerticalOffset(targetOffset);
    }

    private static double GetSectionTop(FrameworkElement section) =>
        LayoutInformation.GetLayoutSlot(section).Top;

    private void SetNavigationSelection(int sectionIndex)
    {
        if (sectionIndex < 0 || SettingsNavigationList.SelectedIndex == sectionIndex)
        {
            return;
        }

        _isSynchronizingNavigation = true;
        try
        {
            SettingsNavigationList.SelectedIndex = sectionIndex;
        }
        finally
        {
            _isSynchronizingNavigation = false;
        }
    }

    private void QueueScrollToSection(int sectionIndex)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!_isWindowLoaded)
            {
                return;
            }

            SetNavigationSelection(sectionIndex);
            ScrollToSection(sectionIndex);
        });
    }

    public void NavigateToSection(AppSettingsSection section)
    {
        _requestedSection = section;
        int sectionIndex = (int)section;
        if (!_isWindowLoaded)
        {
            return;
        }

        if (section == AppSettingsSection.General)
        {
            SetNavigationSelection(sectionIndex);
            SettingsScrollViewer.ScrollToTop();
            return;
        }

        QueueScrollToSection(sectionIndex);
    }

    private void LoadLanguageList()
    {
        CmbLanguage.Items.Clear();
        CmbLanguage.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("AppSettingsWindow_LanguageAuto"), Tag = LocalizationService.AutoCulture });
        CmbLanguage.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Language_English"), Tag = "en" });
        CmbLanguage.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Language_Deutsch"), Tag = "de" });
        CmbLanguage.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Language_Ukrainian"), Tag = "uk" });
        CmbLanguage.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Language_Russian"), Tag = "ru" });
        CmbLanguage.SelectedIndex = 0;
    }

    private (CheckBox CheckBox, UtilityButtonDefinition Definition)[] GetUtilityVisibilityBindings() =>
    [
        (ChkShowPresetSearch, UtilityButtonCatalog.Search),
        (ChkShowPresetScreenshot, UtilityButtonCatalog.Screenshot),
        (ChkShowPresetVideo, UtilityButtonCatalog.Record),
        (ChkShowPresetCalc, UtilityButtonCatalog.Calculator),
        (ChkShowPresetExplorer, UtilityButtonCatalog.Explorer),
        (ChkShowPresetDownloads, UtilityButtonCatalog.Downloads),
        (ChkShowPresetFileSorter, UtilityButtonCatalog.FileSorter),
        (ChkShowPresetIconConverter, UtilityButtonCatalog.IconConverter),
        (ChkShowPresetTimerStopwatch, UtilityButtonCatalog.TimerStopwatch),
        (ChkShowPresetColorPicker, UtilityButtonCatalog.ColorPicker),
        (ChkShowPresetQuickNote, UtilityButtonCatalog.QuickNote),
        (ChkShowPresetQRCodeGenerator, UtilityButtonCatalog.QRCodeGenerator),
        (ChkShowPresetClipboardManager, UtilityButtonCatalog.ClipboardManager),
        (ChkShowPresetShowDesktop, UtilityButtonCatalog.ShowDesktop),
        (ChkShowPresetAppsFolder, UtilityButtonCatalog.AppsFolder),
        (ChkShowPresetCopilot, UtilityButtonCatalog.Copilot),
        (ChkShowPresetTextProcessing, UtilityButtonCatalog.TextProcessing),
        (ChkShowPresetPromptBuilder, UtilityButtonCatalog.PromptBuilder),
        (ChkShowPresetZenEditor, UtilityButtonCatalog.ZenEditor),
        (ChkShowPresetAiteProfiles, UtilityButtonCatalog.AiteProfiles)
    ];

    private static void SetComboValue(ComboBox combo, string? value)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (string.Equals(item.Tag?.ToString(), value, StringComparison.Ordinal))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        if (combo.SelectedIndex < 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private void ReloadLocalizedChoiceLists()
    {
        string language = _selectedUiCulture;
        string edge = GetSelectedSegmentTag(SegEdgeTop, SegEdgeBottom, SegEdgeLeft, SegEdgeRight);
        bool isSecondaryMonitor = ChkSecondaryMonitor.IsChecked == true;

        _isLoadingSettings = true;
        try
        {
            LoadLanguageList();
            SetComboValue(CmbLanguage, language);
            SelectSegmentByTag(edge, SegEdgeTop, SegEdgeBottom, SegEdgeLeft, SegEdgeRight);
            ChkSecondaryMonitor.IsChecked = isSecondaryMonitor;
            UpdateMonitorCheckbox();
            foreach (HotkeyCaptureBox captureBox in GetHotkeyCaptureBoxes())
            {
                captureBox.RefreshDisplay();
            }
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void RefreshLocalizedUi()
    {
        int selectedSectionIndex = Math.Max(0, SettingsNavigationList.SelectedIndex);
        List<ContextRowDraft> drafts = CaptureContextRowDrafts();
        ReloadLocalizedChoiceLists();
        BuildContextRows(BuildContextDisplaySnapshot(_mainWindow.GetAllContextsSnapshot(), drafts));
        RefreshContextRowTooltips();
        RefreshAutomationNames();
        SortQuickToolRows();
        BuildAiConnectionRows();
        UpdateAiDependentQuickToolAvailability();
        UpdateAboutVersionText();

        if (_isWindowLoaded)
        {
            QueueScrollToSection(selectedSectionIndex);
        }
    }

    private (Grid Row, string TitleResourceKey)[] GetQuickToolRows() =>
    [
        (QuickToolRowQuickNote, "QuickTool_QuickNote_Title"),
        (QuickToolRowQRCodeGenerator, "QuickTool_QRCodeGenerator_Title"),
        (QuickToolRowDownloads, "QuickTool_Downloads_Title"),
        (QuickToolRowVideo, "QuickTool_Video_Title"),
        (QuickToolRowCopilot, "QuickTool_Copilot_Title"),
        (QuickToolRowCalculator, "QuickTool_Calculator_Title"),
        (QuickToolRowIconConverter, "QuickTool_IconConverter_Title"),
        (QuickToolRowClipboardManager, "QuickTool_ClipboardManager_Title"),
        (QuickToolRowColorPicker, "QuickTool_ColorPicker_Title"),
        (QuickToolRowSearch, "QuickTool_Search_Title"),
        (QuickToolRowShowDesktop, "QuickTool_ShowDesktop_Title"),
        (QuickToolRowAppsFolder, "QuickTool_AppsFolder_Title"),
        (QuickToolRowExplorer, "QuickTool_Explorer_Title"),
        (QuickToolRowScreenshot, "QuickTool_Screenshot_Title"),
        (QuickToolRowFileSorter, "QuickTool_FileSorter_Title"),
        (QuickToolRowTimerStopwatch, "QuickTool_TimerStopwatch_Title"),
        (QuickToolRowTextProcessing, "QuickTool_TextProcessing_Title"),
        (QuickToolRowPromptBuilder, "QuickTool_PromptBuilder_Title"),
        (QuickToolRowZenEditor, "QuickTool_ZenEditor_Title"),
        (QuickToolRowAiteProfiles, "QuickTool_AiteProfiles_Title")
    ];

    private void SortQuickToolRows()
    {
        (Grid Row, string TitleResourceKey)[] rows = GetQuickToolRows()
            .OrderBy(
                item => LocalizationService.Get(item.TitleResourceKey),
                StringComparer.Create(LocalizationService.ResolvedCulture, ignoreCase: true))
            .ToArray();

        QuickToolsList.Children.Clear();
        for (int index = 0; index < rows.Length; index++)
        {
            rows[index].Row.Margin = index switch
            {
                0 => new Thickness(0, 0, 0, 12),
                _ when index == rows.Length - 1 => new Thickness(0, 12, 0, 0),
                _ => new Thickness(0, 12, 0, 12)
            };
            QuickToolsList.Children.Add(rows[index].Row);

            if (index < rows.Length - 1)
            {
                QuickToolsList.Children.Add(new Border
                {
                    Style = (Style)FindResource("ModernDividerStyle")
                });
            }
        }
    }

    private void UpdateAboutVersionText()
    {
        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        TxtAboutVersion.Text = LocalizationService.Format("About_VersionFormat", version?.Major, version?.Minor, version?.Build);
    }

    private void BuildAiConnectionRows()
    {
        AiConnectionsList.Children.Clear();
        TxtAiConnectionsEmpty.Visibility = _aiConnections.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AiConnectionSettings[] ordered = _aiConnections
            .OrderBy(connection => GetAiProviderRank(connection.ProviderId))
            .ThenBy(connection => connection.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        for (int index = 0; index < ordered.Length; index++)
        {
            AiConnectionSettings connection = ordered[index];
            AiProviderCatalog.TryGet(connection.ProviderId, out AiProviderDefinition provider);
            var row = new Grid { Margin = new Thickness(0, 12, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var details = new StackPanel { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
            details.Children.Add(new TextBlock
            {
                Text = connection.DisplayName,
                Style = (Style)FindResource("ModernSettingTitleStyle"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            details.Children.Add(new TextBlock
            {
                Text = LocalizationService.Format("AiSettings_ConnectionDetails", provider.DisplayName),
                Style = (Style)FindResource("ModernSettingDescriptionStyle"),
                TextWrapping = TextWrapping.Wrap
            });
            details.Children.Add(new TextBlock
            {
                Text = GetAiConnectionStatusText(connection.Id),
                Foreground = (System.Windows.Media.Brush)FindResource("AccentColor"),
                FontSize = 11,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            row.Children.Add(details);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            actions.Children.Add(CreateAiRowButton(LocalizationService.Get("AiSettings_Test"), "AiSettings_Test", connection.Id, BtnAiTest_Click, 90));
            actions.Children.Add(CreateAiRowButton(LocalizationService.Get("Common_Delete"), "Common_Delete", connection.Id, BtnAiRemove_Click, 90));
            Grid.SetColumn(actions, 1);
            row.Children.Add(actions);
            AiConnectionsList.Children.Add(row);

            if (index < ordered.Length - 1)
            {
                AiConnectionsList.Children.Add(new Border { Style = (Style)FindResource("ModernDividerStyle") });
            }
        }
    }

    private Button CreateAiRowButton(
        string content,
        string tooltipResourceKey,
        string connectionId,
        RoutedEventHandler handler,
        double width,
        bool isEnabled = true)
    {
        var button = new Button
        {
            Content = content,
            ToolTip = LocalizationService.Get(tooltipResourceKey),
            Tag = connectionId,
            Style = (Style)FindResource("CommandButtonStyle"),
            Width = width,
            Height = 36,
            Padding = new Thickness(15, 0, 15, 0),
            FontSize = 12,
            IsEnabled = isEnabled,
            Margin = new Thickness(6, 0, 0, 0)
        };
        button.Click += handler;
        return button;
    }

    private string GetAiConnectionStatusText(string connectionId)
    {
        if (!_aiConnectionChecks.TryGetValue(connectionId, out AiConnectionCheckResult? result))
        {
            return LocalizationService.Get("AiSettings_StatusNotChecked");
        }
        return result.IsSuccess
            ? LocalizationService.Format("AiSettings_StatusAvailable", result.ModelCount)
            : LocalizationService.Format("AiSettings_StatusError", result.ErrorMessage ?? string.Empty);
    }

    private static int GetAiProviderRank(string providerId)
    {
        for (int index = 0; index < AiProviderCatalog.DefaultProviderOrder.Count; index++)
        {
            if (string.Equals(AiProviderCatalog.DefaultProviderOrder[index], providerId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }
        return int.MaxValue;
    }

    private void BtnAiAddConnection_Click(object sender, RoutedEventArgs e)
    {
        var existingNames = _aiConnections.Select(c => c.DisplayName);
        var dialog = new AiConnectionDialog(existingNames) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string id = Guid.NewGuid().ToString("N");
        string target = AiProviderCatalog.CreateCredentialTarget(id);
        try
        {
            _aiCredentialStore.Write(target, dialog.ApiKey);
            _aiConnections.Add(new AiConnectionSettings
            {
                Id = id,
                ProviderId = dialog.ProviderId,
                DisplayName = dialog.ConnectionName,
                CredentialTarget = target,
                IsEnabled = true
            });
            _pendingAiCredentialTargets.Add(target);
            BuildAiConnectionRows();
            UpdateAiDependentQuickToolAvailability();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Get("AiSettings_CredentialSaveFailed")) { Owner = this }.ShowDialog();
        }
    }

    private async void BtnAiTest_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string connectionId)
        {
            return;
        }
        AiConnectionSettings? connection = _aiConnections.FirstOrDefault(item => item.Id == connectionId);
        if (connection == null)
        {
            return;
        }

        button.IsEnabled = false;
        button.Content = LocalizationService.Get("AiSettings_Testing");
        try
        {
            _aiConnectionChecks[connection.Id] = await _aiProviderClient.CheckConnectionAsync(connection, CancellationToken.None);
        }
        finally
        {
            BuildAiConnectionRows();
        }
    }

    private void BtnAiRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string connectionId)
        {
            return;
        }
        AiConnectionSettings? connection = _aiConnections.FirstOrDefault(item => item.Id == connectionId);
        if (connection == null)
        {
            return;
        }

        _aiConnections.Remove(connection);
        _aiConnectionChecks.Remove(connection.Id);
        if (_pendingAiCredentialTargets.Remove(connection.CredentialTarget))
        {
            TryDeleteAiCredential(connection.CredentialTarget);
        }
        else
        {
            _removedAiCredentialTargets.Add(connection.CredentialTarget);
        }
        BuildAiConnectionRows();
        UpdateAiDependentQuickToolAvailability();
    }

    private void CleanupPendingAiCredentials()
    {
        if (_aiSettingsCommitted)
        {
            return;
        }
        foreach (string target in _pendingAiCredentialTargets.ToArray())
        {
            TryDeleteAiCredential(target);
        }
        _pendingAiCredentialTargets.Clear();
    }

    private void CommitAiCredentialChanges()
    {
        foreach (string target in _removedAiCredentialTargets)
        {
            TryDeleteAiCredential(target);
        }
        _removedAiCredentialTargets.Clear();
        _pendingAiCredentialTargets.Clear();
        _aiSettingsCommitted = true;
    }

    private void TryDeleteAiCredential(string target)
    {
        try
        {
            _aiCredentialStore.Delete(target);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private static void OpenAboutTarget(string target)
    {
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    private async void BtnAboutCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        await UpdateCheckUi.CheckForUpdatesAsync(this);
    }

    private void BtnAboutWebsite_Click(object sender, RoutedEventArgs e)
    {
        OpenAboutTarget(SupportUrl);
    }

    private void BtnAboutRepository_Click(object sender, RoutedEventArgs e)
    {
        OpenAboutTarget(RepositoryUrl);
    }

    private void BtnAboutLicenses_Click(object sender, RoutedEventArgs e)
    {
        string noticesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "THIRD_PARTY_NOTICES.txt");
        if (!File.Exists(noticesPath))
        {
            noticesPath = Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.txt");
        }

        if (!File.Exists(noticesPath))
        {
            noticesPath = Path.Combine(Directory.GetCurrentDirectory(), "THIRD_PARTY_NOTICES.txt");
        }

        if (!File.Exists(noticesPath))
        {
            new DarkDialog(LocalizationService.Get("About_NoticesMissing")) { Owner = this }.ShowDialog();
            return;
        }

        OpenAboutTarget(noticesPath);
    }

    private void BtnAboutOpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            OpenAboutTarget(_dataDirectory);
        }
        catch (Exception ex)
        {
            new DarkDialog(LocalizationService.Format("About_OpenDataFolderFailed", ex.Message)) { Owner = this }.ShowDialog();
        }
    }

    private async void BtnCreateBackup_Click(object sender, RoutedEventArgs e)
    {
        var optionsDialog = new BackupPasswordDialog(isRestore: false) { Owner = this };
        if (optionsDialog.ShowDialog() != true) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "AiteBar backup (*.aitebarbackup)|*.aitebarbackup",
            FileName = $"AiteBar-backup-{DateTime.Now:yyyy-MM-dd-HH-mm}.aitebarbackup"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            await new BackupService(_aiCredentialStore).CreateAsync(
                dialog.FileName,
                _mainWindow.GetAppSettings(),
                new BackupCreateOptions(optionsDialog.IncludeSecrets, optionsDialog.IncludeClipboard, optionsDialog.Password));
            new DarkDialog(LocalizationService.Get("Backup_CreateSuccess")) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Settings_SaveFailed", ex.Message)) { Owner = this }.ShowDialog();
        }
    }

    private async void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "AiteBar backup (*.aitebarbackup)|*.aitebarbackup" };
        if (dialog.ShowDialog(this) != true) return;

        var backupService = new BackupService(_aiCredentialStore);
        string? password = null;
        try
        {
            _ = await backupService.ReadAsync(dialog.FileName, new BackupRestoreOptions(null));
        }
        catch (InvalidOperationException)
        {
            var passwordDialog = new BackupPasswordDialog(isRestore: true) { Owner = this };
            if (passwordDialog.ShowDialog() != true) return;
            password = passwordDialog.Password;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Backup_RestoreFailed", ex.Message)) { Owner = this }.ShowDialog();
            return;
        }

        if (new DarkDialog(LocalizationService.Get("Backup_RestoreConfirm"), isConfirm: true) { Owner = this }.ShowDialog() != true) return;
        try
        {
            string safetyDirectory = Path.Combine(_dataDirectory, "backups");
            string safetyBackup = Path.Combine(safetyDirectory, $"before-restore-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.aitebarbackup");
            await backupService.CreateAsync(safetyBackup, _mainWindow.GetAppSettings(), new BackupCreateOptions(false, false, null));
            await backupService.RestoreAsync(dialog.FileName, new BackupRestoreOptions(password), _mainWindow.GetSettingsService());
            IReadOnlyList<string> failedHotkeys = await _mainWindow.SaveAppSettings();
            _mainWindow.RefreshPanel();
            new DarkDialog(LocalizationService.Get("Backup_RestoreSuccess")) { Owner = this }.ShowDialog();
            if (failedHotkeys.Count > 0)
                new DarkDialog(LocalizationService.Format("HotkeyRegistrationFailed", string.Join("\n", failedHotkeys))) { Owner = this }.ShowDialog();
            Close();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Backup_RestoreFailed", ex.Message)) { Owner = this }.ShowDialog();
        }
    }

    private void BtnAboutOpenProgramFolder_Click(object sender, RoutedEventArgs e)
    {
        string? exeDirectory = Path.GetDirectoryName(Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName);
        if (string.IsNullOrWhiteSpace(exeDirectory) || !Directory.Exists(exeDirectory))
        {
            new DarkDialog(LocalizationService.Get("About_ProgramFolderUnknown")) { Owner = this }.ShowDialog();
            return;
        }

        OpenAboutTarget(exeDirectory);
    }

    private void RefreshAutomationNames()
    {
        AutomationProperties.SetName(CmbLanguage, LocalizationService.Get("AppSettingsWindow_Language"));
        AutomationProperties.SetName(ChkShowPanelOnMouseHover, LocalizationService.Get("AppSettingsWindow_ShowPanelOnMouseHover"));
        AutomationProperties.SetName(ChkShowTaskbarPositionIndicator, LocalizationService.Get("AppSettingsWindow_ShowTaskbarPositionIndicator"));
        AutomationProperties.SetName(ChkSecondaryMonitor, LocalizationService.Get("AppSettingsWindow_SecondaryMonitor"));
        AutomationProperties.SetName(ChkCheckForUpdatesEnabled, LocalizationService.Get("AppSettingsWindow_CheckForUpdates"));

        (CheckBox CheckBox, string ResourceKey)[] utilitySwitches =
        [
            (ChkShowPresetSearch, "Tool_Search"),
            (ChkShowPresetScreenshot, "Tool_Screenshot"),
            (ChkShowPresetVideo, "Tool_Video"),
            (ChkShowPresetCalc, "Tool_Calculator"),
            (ChkShowPresetExplorer, "Tool_Explorer"),
            (ChkShowPresetDownloads, "Tool_Downloads"),
            (ChkShowPresetFileSorter, "Tool_FileSorter"),
            (ChkShowPresetIconConverter, "Tool_IconConverter"),
            (ChkShowPresetTimerStopwatch, "Tool_TimerStopwatch"),
            (ChkShowPresetColorPicker, "Tool_ColorPicker"),
            (ChkShowPresetQuickNote, "Tool_QuickNote"),
            (ChkShowPresetQRCodeGenerator, "Tool_QRCodeGenerator"),
            (ChkShowPresetClipboardManager, "Tool_ClipboardManager"),
            (ChkShowPresetShowDesktop, "Tool_ShowDesktop"),
            (ChkShowPresetAppsFolder, "Tool_AppsFolder"),
            (ChkShowPresetCopilot, "Tool_Copilot"),
            (ChkShowPresetTextProcessing, "Tool_TextProcessing"),
            (ChkShowPresetPromptBuilder, "Tool_PromptBuilder"),
            (ChkShowPresetZenEditor, "Tool_ZenEditor"),
            (ChkShowPresetAiteProfiles, "Tool_AiteProfiles"),
            (ChkClipboardManagerPersistHistory, "ClipboardManager_PersistHistorySetting")
        ];
        foreach ((CheckBox checkBox, string resourceKey) in utilitySwitches)
        {
            AutomationProperties.SetName(checkBox, LocalizationService.Get(resourceKey));
        }

        (HotkeyCaptureBox CaptureBox, string ResourceKey)[] hotkeyFields =
        [
            (HotkeyNextContext, "AppSettingsWindow_NextPanel"),
            (HotkeyPreviousContext, "AppSettingsWindow_PreviousPanel"),
            (HotkeyAddButton, "AppSettingsWindow_AddButton"),
            (HotkeyFileSorter, "Tool_FileSorter"),
            (HotkeyIconConverter, "Tool_IconConverter"),
            (HotkeyQuickNote, "Tool_QuickNote"),
            (HotkeyColorPicker, "Tool_ColorPicker"),
            (HotkeyTimerStopwatch, "Tool_TimerStopwatch"),
            (HotkeyQRCodeGenerator, "Tool_QRCodeGenerator"),
            (HotkeyClipboardManager, "Tool_ClipboardManager"),
            (HotkeyTextProcessing, "Tool_TextProcessing"),
            (HotkeyPromptBuilder, "Tool_PromptBuilder"),
            (HotkeyZenEditor, "Tool_ZenEditor"),
            (HotkeyAiteProfiles, "Tool_AiteProfiles")
        ];
        foreach ((HotkeyCaptureBox captureBox, string resourceKey) in hotkeyFields)
        {
            AutomationProperties.SetName(captureBox, LocalizationService.Get(resourceKey));
        }
    }

    private static string? GetComboTag(ComboBox combo) =>
        (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString();

    private IEnumerable<HotkeyCaptureBox> GetHotkeyCaptureBoxes()
    {
        yield return HotkeyNextContext;
        yield return HotkeyPreviousContext;
        yield return HotkeyAddButton;
        yield return HotkeyFileSorter;
        yield return HotkeyIconConverter;
        yield return HotkeyQuickNote;
        yield return HotkeyColorPicker;
        yield return HotkeyTimerStopwatch;
        yield return HotkeyQRCodeGenerator;
        yield return HotkeyClipboardManager;
        yield return HotkeyTextProcessing;
        yield return HotkeyPromptBuilder;
        yield return HotkeyZenEditor;
        yield return HotkeyAiteProfiles;
    }

    private List<ContextRowDraft> CaptureContextRowDrafts()
    {
        var drafts = new List<ContextRowDraft>(_contextRows.Count);
        for (int i = 0; i < _contextRows.Count; i++)
        {
            string draftName = _contextRows[i].NameTextBox.Text;
            drafts.Add(new ContextRowDraft(
                _contextRows[i].ContextId,
                draftName,
                _contextRows[i].EnabledCheckBox.IsChecked ?? false,
                ContextStateHelper.IsCustomizedContextNameInput(draftName, i)));
        }

        return drafts;
    }

    private void ApplyContextRowDrafts(IReadOnlyList<ContextRowDraft> drafts)
    {
        for (int i = 0; i < _contextRows.Count && i < drafts.Count; i++)
        {
            if (!string.Equals(_contextRows[i].ContextId, drafts[i].ContextId, StringComparison.Ordinal))
            {
                continue;
            }

            if (drafts[i].IsNameCustomized)
            {
                _contextRows[i].NameTextBox.Text = drafts[i].Name.Trim();
            }

            _contextRows[i].EnabledCheckBox.IsChecked = i == 0 || drafts[i].IsEnabled;

            // Цвет всегда фиксированный, обновляем бейдж
            SetBadgeColor(_contextRows[i].BadgeBorder, ContextStateHelper.GetContextColor(i));
        }
    }

    private static IReadOnlyList<PanelContext> BuildContextDisplaySnapshot(
        IReadOnlyList<PanelContext> source,
        IReadOnlyList<ContextRowDraft> drafts)
    {
        if (drafts.Count == 0)
        {
            return source;
        }

        var sourceById = source.ToDictionary(context => context.Id, StringComparer.Ordinal);
        var ordered = new List<PanelContext>(source.Count);
        var usedIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < drafts.Count; i++)
        {
            ContextRowDraft draft = drafts[i];
            if (!sourceById.TryGetValue(draft.ContextId, out PanelContext? sourceContext) || !usedIds.Add(draft.ContextId))
            {
                continue;
            }

            bool isNameCustomized = ContextStateHelper.IsCustomizedContextNameInput(draft.Name, i);
            ordered.Add(new PanelContext
            {
                Id = sourceContext.Id,
                Name = isNameCustomized ? draft.Name.Trim() : ContextStateHelper.GetDefaultContextName(i),
                IsNameCustomized = isNameCustomized,
                IconGlyph = sourceContext.IconGlyph,
                IsEnabled = i == 0 || draft.IsEnabled,
                Color = ContextStateHelper.GetContextColor(i)
            });
        }

        foreach (PanelContext context in source)
        {
            if (usedIds.Add(context.Id))
            {
                ordered.Add(context);
            }
        }

        return ordered;
    }

    private void RefreshContextRowTooltips()
    {
        for (int i = 0; i < _contextRows.Count; i++)
        {
            ContextRow row = _contextRows[i];
            row.EnabledCheckBox.ToolTip = i == 0
                ? LocalizationService.Get("AppSettingsWindow_PrimaryPanelAlwaysEnabled")
                : LocalizationService.Get("AppSettingsWindow_PanelEnabled");
            AutomationProperties.SetName(
                row.EnabledCheckBox,
                $"{row.NameTextBox.Text}: {LocalizationService.Get("AppSettingsWindow_PanelEnabled")}");

            RefreshContextClearButton(row);
        }
    }

    private void RefreshContextClearButton(ContextRow row)
    {
        int buttonCount = CountContextButtons(row.ContextId);
        row.ClearButton.IsEnabled = buttonCount > 0;
        row.ClearButton.ToolTip = buttonCount > 0
            ? LocalizationService.Format("AppSettingsWindow_ClearPanelTooltip", row.NameTextBox.Text, buttonCount)
            : LocalizationService.Get("AppSettingsWindow_ClearPanelEmpty");
        AutomationProperties.SetName(row.ClearButton, LocalizationService.Format("AppSettingsWindow_ClearPanelAutomation", row.NameTextBox.Text));
        ToolTipService.SetShowOnDisabled(row.ClearButton, true);
    }

    private int CountContextButtons(string contextId)
    {
        return _settings.Elements.Count(element => string.Equals(element.ContextId, contextId, StringComparison.Ordinal));
    }

    private static void SetBadgeColor(Border badgeBorder, string colorString)
    {
        var converter = new System.Windows.Media.BrushConverter();
        badgeBorder.Background = (System.Windows.Media.Brush)(converter.ConvertFromString(colorString) ?? System.Windows.Media.Brushes.DimGray);
    }

    private static string GetSelectedSegmentTag(System.Windows.Controls.RadioButton a, System.Windows.Controls.RadioButton b, System.Windows.Controls.RadioButton c, System.Windows.Controls.RadioButton d)
    {
        if (a.IsChecked == true) return a.Tag!.ToString()!;
        if (b.IsChecked == true) return b.Tag!.ToString()!;
        if (c.IsChecked == true) return c.Tag!.ToString()!;
        return d.Tag!.ToString()!;
    }

    private static void SelectSegmentByTag(string tag, System.Windows.Controls.RadioButton a, System.Windows.Controls.RadioButton b, System.Windows.Controls.RadioButton c, System.Windows.Controls.RadioButton d)
    {
        if (string.Equals(a.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) a.IsChecked = true;
        else if (string.Equals(b.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) b.IsChecked = true;
        else if (string.Equals(c.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) c.IsChecked = true;
        else d.IsChecked = true;
    }

    private void UpdateMonitorCheckbox()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        bool hasSecondary = screens.Length > 1;
        ChkSecondaryMonitor.IsEnabled = hasSecondary;
        if (!hasSecondary) ChkSecondaryMonitor.IsChecked = false;
    }

    private static string? GetHotkeyToken(HotkeyBinding binding)
    {
        if (binding == null || string.IsNullOrWhiteSpace(binding.Key) || string.Equals(binding.Key, "None", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"{(binding.Ctrl ? "C" : "-")}{(binding.Shift ? "S" : "-")}{(binding.Alt ? "A" : "-")}{(binding.Win ? "W" : "-")}:{binding.Key.ToUpperInvariant()}";
    }

    private static bool HasAssignedKey(HotkeyBinding binding)
    {
        return HotkeyValidationHelper.HasAssignedKey(binding);
    }

    private static bool HasModifier(HotkeyBinding binding)
    {
        return HotkeyValidationHelper.HasModifier(binding);
    }

    private bool ValidateHotkeyBindings(
        HotkeyBinding nextBinding,
        HotkeyBinding previousBinding,
        HotkeyBinding addButtonBinding,
        HotkeyBinding fileSorterBinding,
        HotkeyBinding iconConverterBinding,
        HotkeyBinding quickNoteBinding,
        HotkeyBinding colorPickerBinding,
        HotkeyBinding timerStopwatchBinding,
        HotkeyBinding qrCodeGeneratorBinding,
        HotkeyBinding clipboardManagerBinding,
        HotkeyBinding textProcessingBinding,
        HotkeyBinding promptBuilderBinding,
        HotkeyBinding zenEditorBinding,
        HotkeyBinding aiteProfilesBinding)
    {
        var registrations = new (string Name, HotkeyBinding Binding)[]
        {
            (LocalizationService.Get("AppSettingsWindow_NextPanel"), nextBinding),
            (LocalizationService.Get("AppSettingsWindow_PreviousPanel"), previousBinding),
            (LocalizationService.Get("AppSettingsWindow_AddButton"), addButtonBinding),
            (LocalizationService.Get("Tool_FileSorter"), fileSorterBinding),
            (LocalizationService.Get("Tool_IconConverter"), iconConverterBinding),
            (LocalizationService.Get("Tool_QuickNote"), quickNoteBinding),
            (LocalizationService.Get("Tool_ColorPicker"), colorPickerBinding),
            (LocalizationService.Get("Tool_TimerStopwatch"), timerStopwatchBinding),
            (LocalizationService.Get("Tool_QRCodeGenerator"), qrCodeGeneratorBinding),
            (LocalizationService.Get("Tool_ClipboardManager"), clipboardManagerBinding),
            (LocalizationService.Get("Tool_TextProcessing"), textProcessingBinding),
            (LocalizationService.Get("Tool_PromptBuilder"), promptBuilderBinding),
            (LocalizationService.Get("Tool_ZenEditor"), zenEditorBinding),
            (LocalizationService.Get("Tool_AiteProfiles"), aiteProfilesBinding)
        };

        var missingModifiers = registrations
            .Where(item => HasAssignedKey(item.Binding) && !HasModifier(item.Binding))
            .Select(item => item.Name)
            .ToList();

        if (missingModifiers.Count > 0)
        {
            new DarkDialog(
                LocalizationService.Format("HotkeyModifierRequiredMessage", string.Join("\n", missingModifiers)))
            {
                Owner = this
            }.ShowDialog();
            return false;
        }

        var duplicates = registrations
            .Select(item => new { item.Name, Token = GetHotkeyToken(item.Binding) })
            .Where(item => item.Token != null)
            .GroupBy(item => item.Token!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(", ", group.Select(item => item.Name)))
            .ToList();

        if (duplicates.Count > 0)
        {
            new DarkDialog(
                LocalizationService.Format("HotkeyConflictMessage", string.Join("\n", duplicates)))
            {
                Owner = this
            }.ShowDialog();
            return false;
        }

        var reserved = registrations
            .Where(item => HasAssignedKey(item.Binding) && HotkeyValidationHelper.IsReservedHotkey(item.Binding))
            .Select(item => item.Name)
            .ToList();

        if (reserved.Count > 0)
        {
            new DarkDialog(
                LocalizationService.Format("HotkeyGlobalReservedMessage", string.Join("\n", reserved)))
            {
                Owner = this
            }.ShowDialog();
            return false;
        }

        return true;
    }

    private void LoadSettings()
    {
        // Load all settings from config
        foreach (var (checkBox, definition) in GetUtilityVisibilityBindings())
        {
            checkBox.IsChecked = definition.IsVisible(_settings);
        }
        ChkClipboardManagerPersistHistory.IsChecked = _settings.ClipboardManagerPersistHistory;
        ChkSaveTextProcessingDraft.IsChecked = _settings.SaveTextProcessingDraft;
        ChkSavePromptBuilderDrafts.IsChecked = _settings.SavePromptBuilderDrafts;
        ChkShowPanelOnMouseHover.IsChecked = _settings.ShowPanelOnMouseHover;
        ChkShowTaskbarPositionIndicator.IsChecked = _settings.ShowTaskbarPositionIndicator.GetValueOrDefault(true);
        ChkCheckForUpdatesEnabled.IsChecked = _settings.CheckForUpdatesEnabled;
        _selectedUiCulture = LocalizationService.NormalizeCultureName(_settings.UiCulture);
        SetComboValue(CmbLanguage, _selectedUiCulture);

        HotkeyNextContext.SetBinding(_settings.NextContextHotkey);
        HotkeyPreviousContext.SetBinding(_settings.PreviousContextHotkey);
        HotkeyAddButton.SetBinding(_settings.AddButtonHotkey);
        HotkeyFileSorter.SetBinding(_settings.FileSorterHotkey);
        HotkeyIconConverter.SetBinding(_settings.IconConverterHotkey);
        HotkeyQuickNote.SetBinding(_settings.QuickNoteHotkey);
        HotkeyColorPicker.SetBinding(_settings.ColorPickerHotkey);
        HotkeyTimerStopwatch.SetBinding(_settings.TimerStopwatchHotkey);
        HotkeyQRCodeGenerator.SetBinding(_settings.QRCodeGeneratorHotkey);
        HotkeyClipboardManager.SetBinding(_settings.ClipboardManagerHotkey);
        HotkeyTextProcessing.SetBinding(_settings.TextProcessingHotkey);
        HotkeyPromptBuilder.SetBinding(_settings.PromptBuilderHotkey);
        HotkeyZenEditor.SetBinding(_settings.ZenEditorHotkey);
        HotkeyAiteProfiles.SetBinding(_settings.AiteProfilesHotkey);

        SelectSegmentByTag(_settings.Edge.ToString(), SegEdgeTop, SegEdgeBottom, SegEdgeLeft, SegEdgeRight);
        ChkSecondaryMonitor.IsChecked = _settings.MonitorIndex > 0;
        UpdateMonitorCheckbox();

        SliderPanelSize.Value = AppSettingsDiscreteChoiceHelper.GetNearestIndex(
            AppSettingsDiscreteChoiceHelper.PanelSizeValues,
            _settings.PanelSizePercent);
        SliderActivationZone.Value = AppSettingsDiscreteChoiceHelper.GetNearestIndex(
            AppSettingsDiscreteChoiceHelper.ActivationZoneValues,
            _settings.ActivationZoneSizePercent);
        SliderActivationDelay.Value = AppSettingsDiscreteChoiceHelper.GetNearestIndex(
            AppSettingsDiscreteChoiceHelper.ActivationDelayValues,
            _settings.ActivationDelayMs);
        UpdateSliderValueLabels();

        BuildContextRows(_mainWindow.GetAllContextsSnapshot());
        _aiConnections.Clear();
        _aiConnections.AddRange((_settings.Ai?.Connections ?? []).Select(CloneAiConnection));
        BuildAiConnectionRows();
        UpdateAiDependentQuickToolAvailability();
    }

    private void UpdateAiDependentQuickToolAvailability()
    {
        bool hasAiConnection = _aiConnections.Any(IsUsableAiConnection);
        string? tooltip = hasAiConnection
            ? null
            : LocalizationService.Get("QuickTool_AiProviderRequiredTooltip");

        SetAiDependentQuickToolAvailability(QuickToolRowTextProcessing, ChkShowPresetTextProcessing, hasAiConnection, tooltip);
        SetAiDependentQuickToolAvailability(QuickToolRowPromptBuilder, ChkShowPresetPromptBuilder, hasAiConnection, tooltip);
    }

    private static bool IsUsableAiConnection(AiConnectionSettings connection) =>
        connection.IsEnabled &&
        !string.IsNullOrWhiteSpace(connection.ProviderId) &&
        !string.IsNullOrWhiteSpace(connection.CredentialTarget) &&
        AiProviderCatalog.TryGet(connection.ProviderId, out _);

    private static void SetAiDependentQuickToolAvailability(
        FrameworkElement row,
        CheckBox checkBox,
        bool isEnabled,
        string? tooltip)
    {
        row.IsEnabled = isEnabled;
        row.ToolTip = tooltip;
        ToolTipService.SetShowOnDisabled(row, true);
        checkBox.ToolTip = tooltip;
        ToolTipService.SetShowOnDisabled(checkBox, true);
        if (!isEnabled)
        {
            checkBox.IsChecked = false;
        }
    }

    private static AiConnectionSettings CloneAiConnection(AiConnectionSettings connection) => new()
    {
        Id = connection.Id,
        ProviderId = connection.ProviderId,
        DisplayName = connection.DisplayName,
        CredentialTarget = connection.CredentialTarget,
        IsEnabled = connection.IsEnabled,
        PreferredModelId = connection.PreferredModelId
    };

    private void NormalizeDiscreteSettings()
    {
        _settings.PanelSizePercent = AppSettingsDiscreteChoiceHelper.GetValue(
            AppSettingsDiscreteChoiceHelper.PanelSizeValues,
            SliderPanelSize.Value);
        _settings.ActivationZoneSizePercent = AppSettingsDiscreteChoiceHelper.GetValue(
            AppSettingsDiscreteChoiceHelper.ActivationZoneValues,
            SliderActivationZone.Value);
        _settings.ActivationDelayMs = AppSettingsDiscreteChoiceHelper.GetValue(
            AppSettingsDiscreteChoiceHelper.ActivationDelayValues,
            SliderActivationDelay.Value);
    }

    private void BuildContextRows(IReadOnlyList<PanelContext> contexts)
    {
        PanelContextsList.Children.Clear();
        _contextRows.Clear();
        double formControlHeight = (double)FindResource("FormControlHeight");
        for (int i = 0; i < contexts.Count; i++)
        {
            PanelContext context = contexts[i];
            int contextNumber = i;
            var row = new Grid
            {
                Height = formControlHeight + 20,
                Background = System.Windows.Media.Brushes.Transparent
            };
            row.Tag = context.Id;
            row.AllowDrop = true;
            row.DragOver += ContextRow_DragOver;
            row.DragLeave += ContextRow_DragLeave;
            row.Drop += ContextRow_Drop;
            row.GiveFeedback += ContextRow_GiveFeedback;
            var rowSurface = new Border
            {
                Margin = new Thickness(0, 4, 0, 4),
                CornerRadius = new CornerRadius(6),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false
            };
            Grid.SetColumnSpan(rowSurface, 5);
            row.Children.Add(rowSurface);

            var insertBeforeIndicator = CreateContextDropIndicator(VerticalAlignment.Top);
            var insertAfterIndicator = CreateContextDropIndicator(VerticalAlignment.Bottom);
            Grid.SetColumnSpan(insertBeforeIndicator, 5);
            Grid.SetColumnSpan(insertAfterIndicator, 5);

            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dragHandle = new Border
            {
                Width = 24,
                Height = formControlHeight,
                Background = BrushFromHex("#242A30"),
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "\uE945",
                    FontFamily = FontHelper.Resolve(FontHelper.MaterialKey),
                    Foreground = BrushFromHex("#8A95A3"),
                    FontSize = 18,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    TextAlignment = System.Windows.TextAlignment.Center
                }
            };
            AutomationProperties.SetName(dragHandle, LocalizationService.Get("AppSettingsWindow_ReorderPanelTooltip"));
            dragHandle.PreviewMouseLeftButtonDown += ContextDragHandle_PreviewMouseLeftButtonDown;
            dragHandle.PreviewMouseMove += ContextDragHandle_PreviewMouseMove;
            dragHandle.MouseEnter += ContextDragHandle_MouseEnter;
            dragHandle.MouseLeave += ContextDragHandle_MouseLeave;
            dragHandle.Tag = context.Id;
            Grid.SetColumn(dragHandle, 0);
            row.Children.Add(dragHandle);

            var badge = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(4),
                Background = GetPanelBadgeBrush(ContextStateHelper.GetContextColor(contextNumber)),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = contextNumber.ToString(CultureInfo.InvariantCulture),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                TextAlignment = System.Windows.TextAlignment.Center
            };
            Grid.SetColumn(badge, 1);
            row.Children.Add(badge);

            var nameTextBox = new TextBox
            {
                Text = context.Name,
                Height = formControlHeight,
                Margin = new Thickness(0, 0, 16, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Style = (Style)FindResource("BaseTextBoxStyle")
            };
            Grid.SetColumn(nameTextBox, 2);
            row.Children.Add(nameTextBox);

            var enabledCheckBox = new CheckBox
            {
                IsChecked = context.IsEnabled,
                IsEnabled = contextNumber != 0,
                Style = (Style)FindResource("ModernSwitchStyle"),
                ToolTip = contextNumber == 0
                    ? LocalizationService.Get("AppSettingsWindow_PrimaryPanelAlwaysEnabled")
                    : LocalizationService.Get("AppSettingsWindow_PanelEnabled")
            };
            AutomationProperties.SetName(
                enabledCheckBox,
                $"{context.Name}: {LocalizationService.Get("AppSettingsWindow_PanelEnabled")}");
            Grid.SetColumn(enabledCheckBox, 3);
            row.Children.Add(enabledCheckBox);

            var clearButton = new Button
            {
                Content = "\uF202",
                Tag = context.Id,
                Width = formControlHeight,
                Height = formControlHeight,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(0),
                FontFamily = FontHelper.Resolve(FontHelper.FluentKey),
                FontSize = 17,
                MinWidth = 0,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Style = (Style)FindResource("ContextIconButtonStyle")
            };
            clearButton.Click += BtnClearContext_Click;
            Grid.SetColumn(clearButton, 4);
            row.Children.Add(clearButton);
            row.Children.Add(insertBeforeIndicator);
            row.Children.Add(insertAfterIndicator);

            var contextRow = new ContextRow(
                context.Id,
                row,
                enabledCheckBox,
                nameTextBox,
                badge,
                clearButton,
                dragHandle,
                rowSurface,
                insertBeforeIndicator,
                insertAfterIndicator);
            RefreshContextClearButton(contextRow);

            PanelContextsList.Children.Add(row);
            if (i < contexts.Count - 1)
            {
                PanelContextsList.Children.Add(new Border
                {
                    Height = 1,
                    Background = (System.Windows.Media.Brush)FindResource("ModernRowDivider")
                });
            }
            _contextRows.Add(contextRow);
        }
    }

    private FrameworkElement CreateContextDropIndicator(VerticalAlignment alignment)
    {
        var grid = new Grid
        {
            Height = 6,
            VerticalAlignment = alignment,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        grid.Children.Add(new Border
        {
            Height = 2,
            Margin = new Thickness(36, 0, 8, 0),
            Background = (System.Windows.Media.Brush)FindResource("AccentColor"),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            CornerRadius = new CornerRadius(1)
        });
        grid.Children.Add(new Border
        {
            Width = 6,
            Height = 6,
            Margin = new Thickness(30, 0, 0, 0),
            Background = (System.Windows.Media.Brush)FindResource("AccentColor"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            CornerRadius = new CornerRadius(3)
        });
        return grid;
    }

    private async void BtnClearContext_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string contextId })
        {
            return;
        }

        ContextRow? row = _contextRows.FirstOrDefault(entry => string.Equals(entry.ContextId, contextId, StringComparison.Ordinal));
        if (row == null)
        {
            return;
        }

        int buttonCount = CountContextButtons(contextId);
        if (buttonCount <= 0)
        {
            RefreshContextClearButton(row);
            return;
        }

        string panelName = row.NameTextBox.Text.TrimOrDefault(_mainWindow.GetSettingsService().GetContextDisplayName(contextId));
        string message = LocalizationService.Format("AppSettingsWindow_ClearPanelConfirm", panelName, buttonCount);
        if (new DarkDialog(message, isConfirm: true) { Owner = this }.ShowDialog() != true)
        {
            return;
        }

        row.ClearButton.IsEnabled = false;
        List<CustomElement> originalElements = _settings.Elements.ToList();
        var contextIds = new HashSet<string>(StringComparer.Ordinal) { contextId };
        AppSettingsService settingsService = _mainWindow.GetSettingsService();
        AppSettings originalServiceSettings = settingsService.Settings;
        settingsService.RemoveElementsForContexts(contextIds);
        ClearElementsForContexts(_settings, contextIds);

        IReadOnlyList<string> failedHotkeys;
        try
        {
            failedHotkeys = await _mainWindow.SaveAppSettings();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            settingsService.Settings = originalServiceSettings;
            _settings.Elements = originalElements;
            RefreshContextRowTooltips();
            new DarkDialog(LocalizationService.Format("Settings_SaveFailed", ex.Message))
            {
                Owner = this
            }.ShowDialog();
            return;
        }

        _mainWindow.RefreshPanel();
        RefreshContextRowTooltips();

        if (failedHotkeys.Count > 0)
        {
            new DarkDialog(LocalizationService.Format("HotkeyRegistrationFailed", string.Join("\n", failedHotkeys)))
            {
                Owner = this
            }.ShowDialog();
        }
    }

    private void ContextDragHandle_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _draggedContextId = (sender as FrameworkElement)?.Tag as string;
        _contextDragStartPoint = e.GetPosition(this);
    }

    private void ContextDragHandle_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = BrushFromHex("#2E363D");
            if (border.Child is TextBlock glyph)
            {
                glyph.Foreground = BrushFromHex("#E8EEF6");
            }
        }
    }

    private void ContextDragHandle_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = BrushFromHex("#242A30");
            if (border.Child is TextBlock glyph)
            {
                glyph.Foreground = BrushFromHex("#8A95A3");
            }
        }
    }

    private void ContextRow_GiveFeedback(object sender, System.Windows.GiveFeedbackEventArgs e)
    {
        e.UseDefaultCursors = false;
        if ((e.Effects & DragDropEffects.Move) != 0)
        {
            System.Windows.Input.Mouse.SetCursor(System.Windows.Input.Cursors.Hand);
        }
        else
        {
            System.Windows.Input.Mouse.SetCursor(System.Windows.Input.Cursors.No);
        }

        e.Handled = true;
    }

    private void ContextDragHandle_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_draggedContextId == null || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }

        Point current = e.GetPosition(this);
        if (Math.Abs(current.X - _contextDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _contextDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        SetContextDragVisuals(_draggedContextId, null, false);
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, _draggedContextId, DragDropEffects.Move);
        }
        finally
        {
            _draggedContextId = null;
            ClearContextDragVisuals();
        }
    }

    private void ContextRow_DragOver(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string targetContextId } row ||
            e.Data.GetData(DataFormats.StringFormat) is not string sourceContextId ||
            string.Equals(sourceContextId, targetContextId, StringComparison.Ordinal))
        {
            e.Effects = DragDropEffects.None;
            ClearContextDropTargetVisuals();
            e.Handled = true;
            return;
        }

        bool insertAfter = e.GetPosition(row).Y > row.ActualHeight / 2.0;
        e.Effects = DragDropEffects.Move;
        if (!string.Equals(_dragOverContextId, targetContextId, StringComparison.Ordinal) ||
            _dragOverAfter != insertAfter)
        {
            SetContextDragVisuals(sourceContextId, targetContextId, insertAfter);
        }
        e.Handled = true;
    }

    private void ContextRow_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string contextId } &&
            string.Equals(_dragOverContextId, contextId, StringComparison.Ordinal))
        {
            ClearContextDropTargetVisuals();
        }
    }

    private void ContextRow_Drop(object sender, DragEventArgs e)
    {
        bool insertAfter = sender is FrameworkElement row && e.GetPosition(row).Y > row.ActualHeight / 2.0;
        if (sender is not FrameworkElement { Tag: string targetContextId } ||
            e.Data.GetData(DataFormats.StringFormat) is not string sourceContextId ||
            string.Equals(sourceContextId, targetContextId, StringComparison.Ordinal))
        {
            ClearContextDragVisuals();
            return;
        }

        ReorderContextRows(sourceContextId, targetContextId, insertAfter);
        ClearContextDragVisuals();
    }

    private void ReorderContextRows(string sourceContextId, string targetContextId, bool insertAfter)
    {
        List<ContextRowDraft> drafts = CaptureContextRowDrafts();
        int sourceIndex = drafts.FindIndex(draft => string.Equals(draft.ContextId, sourceContextId, StringComparison.Ordinal));
        if (sourceIndex < 0)
        {
            return;
        }

        ContextRowDraft moved = drafts[sourceIndex];
        drafts.RemoveAt(sourceIndex);
        int targetIndex = drafts.FindIndex(draft => string.Equals(draft.ContextId, targetContextId, StringComparison.Ordinal));
        if (targetIndex < 0)
        {
            return;
        }

        if (insertAfter)
        {
            targetIndex++;
        }

        drafts.Insert(targetIndex, moved);
        BuildContextRows(BuildContextDisplaySnapshot(_mainWindow.GetAllContextsSnapshot(), drafts));
        ApplyContextRowDrafts(drafts);
        RefreshContextRowTooltips();
    }

    private void SetContextDragVisuals(string? sourceContextId, string? targetContextId, bool insertAfter)
    {
        _dragOverContextId = targetContextId;
        _dragOverAfter = insertAfter;
        foreach (ContextRow row in _contextRows)
        {
            bool isSource = string.Equals(row.ContextId, sourceContextId, StringComparison.Ordinal);
            bool isTarget = string.Equals(row.ContextId, targetContextId, StringComparison.Ordinal);

            if (isSource)
            {
                row.RowGrid.Opacity = 0.35;
                row.RowSurface.Background = System.Windows.Media.Brushes.Transparent;
                row.RowSurface.BorderBrush = System.Windows.Media.Brushes.Transparent;
                row.DragHandle.Background = BrushFromHex("#242A30");
                if (row.DragHandle.Child is TextBlock sourceGlyph)
                {
                    sourceGlyph.Foreground = BrushFromHex("#8A95A3");
                }
            }
            else
            {
                row.RowGrid.Opacity = 1.0;
                row.RowSurface.Background = System.Windows.Media.Brushes.Transparent;
                row.RowSurface.BorderBrush = System.Windows.Media.Brushes.Transparent;
                row.DragHandle.Background = BrushFromHex("#242A30");
                if (row.DragHandle.Child is TextBlock otherGlyph)
                {
                    otherGlyph.Foreground = BrushFromHex("#8A95A3");
                }
            }

            row.InsertBeforeIndicator.Visibility = isTarget && !insertAfter ? Visibility.Visible : Visibility.Collapsed;
            row.InsertAfterIndicator.Visibility = isTarget && insertAfter ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ClearContextDropTargetVisuals()
    {
        _dragOverContextId = null;
        foreach (ContextRow row in _contextRows)
        {
            row.InsertBeforeIndicator.Visibility = Visibility.Collapsed;
            row.InsertAfterIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearContextDragVisuals()
    {
        _dragOverContextId = null;
        _dragOverAfter = false;
        foreach (ContextRow row in _contextRows)
        {
            row.RowGrid.Opacity = 1.0;
            row.RowSurface.Background = System.Windows.Media.Brushes.Transparent;
            row.RowSurface.BorderBrush = System.Windows.Media.Brushes.Transparent;
            row.DragHandle.Background = BrushFromHex("#242A30");
            if (row.DragHandle.Child is TextBlock glyph)
            {
                glyph.Foreground = BrushFromHex("#8A95A3");
            }
            row.InsertBeforeIndicator.Visibility = Visibility.Collapsed;
            row.InsertAfterIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private System.Windows.Media.Brush GetPanelBadgeBrush(string colorString)
    {
        var converter = new System.Windows.Media.BrushConverter();
        return (System.Windows.Media.Brush)(converter.ConvertFromString(colorString) ?? System.Windows.Media.Brushes.DimGray);
    }

    private static System.Windows.Media.Brush BrushFromHex(string colorString)
    {
        var converter = new System.Windows.Media.BrushConverter();
        return (System.Windows.Media.Brush)(converter.ConvertFromString(colorString) ?? System.Windows.Media.Brushes.Transparent);
    }

    private void SliderPanelSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoadingSettings)
        {
            _panelSizeSelectionChanged = true;
        }

        UpdateSliderValueLabels();
    }

    private void SliderActivationZone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoadingSettings)
        {
            _activationZoneSelectionChanged = true;
        }

        UpdateSliderValueLabels();
    }

    private void SliderActivationDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoadingSettings)
        {
            _activationDelaySelectionChanged = true;
        }

        UpdateSliderValueLabels();
    }

    private void UpdateSliderValueLabels()
    {
        if (SliderPanelSize == null || SliderActivationZone == null || SliderActivationDelay == null ||
            LblPanelSize50 == null || LblPanelSize70 == null || LblPanelSize90 == null || LblPanelSize100 == null ||
            LblActivationZone10 == null || LblActivationZone30 == null || LblActivationZone50 == null || LblActivationZone100 == null ||
            LblActivationDelay100 == null || LblActivationDelay200 == null || LblActivationDelay300 == null || LblActivationDelay500 == null)
        {
            return;
        }

        UpdateSliderScaleSelection(SliderPanelSize.Value, LblPanelSize50, LblPanelSize70, LblPanelSize90, LblPanelSize100);
        UpdateSliderScaleSelection(SliderActivationZone.Value, LblActivationZone10, LblActivationZone30, LblActivationZone50, LblActivationZone100);
        UpdateSliderScaleSelection(SliderActivationDelay.Value, LblActivationDelay100, LblActivationDelay200, LblActivationDelay300, LblActivationDelay500);
    }

    private void UpdateSliderScaleSelection(double sliderValue, params TextBlock[] labels)
    {
        int selectedIndex = Math.Clamp((int)Math.Round(sliderValue), 0, labels.Length - 1);
        var accentBrush = (System.Windows.Media.Brush)FindResource("AccentColor");
        var mutedBrush = (System.Windows.Media.Brush)FindResource("MutedText");

        for (int index = 0; index < labels.Length; index++)
        {
            labels[index].Foreground = index == selectedIndex ? accentBrush : mutedBrush;
            labels[index].FontWeight = index == selectedIndex ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private async void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        string selectedCulture = GetComboTag(CmbLanguage) ?? LocalizationService.AutoCulture;
        _selectedUiCulture = LocalizationService.NormalizeCultureName(selectedCulture);
        _settings.UiCulture = _selectedUiCulture;
        LocalizationService.ApplyCulture(_selectedUiCulture);
        _mainWindow.GetSettingsService().NormalizeAppState();
        try
        {
            await _mainWindow.GetSettingsService().SaveAsync();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Settings_SaveFailed", ex.Message))
            {
                Owner = this
            }.ShowDialog();
            return;
        }

        LocalizationService.EnsureAppliedCulture();
        LocalizationService.RefreshLocalizedBindings(this);
        RefreshLocalizedUi();
    }

    private void SegEdge_Click(object sender, RoutedEventArgs e) { }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        bool clearSensitiveDraftBackups =
            ChkSaveTextProcessingDraft.IsChecked != true ||
            ChkSavePromptBuilderDrafts.IsChecked != true;
        HotkeyBinding nextBinding = HotkeyNextContext.GetBinding();
        HotkeyBinding previousBinding = HotkeyPreviousContext.GetBinding();
        HotkeyBinding addButtonBinding = HotkeyAddButton.GetBinding();
        HotkeyBinding fileSorterBinding = HotkeyFileSorter.GetBinding();
        HotkeyBinding iconConverterBinding = HotkeyIconConverter.GetBinding();
        HotkeyBinding quickNoteBinding = HotkeyQuickNote.GetBinding();
        HotkeyBinding colorPickerBinding = HotkeyColorPicker.GetBinding();
        HotkeyBinding timerStopwatchBinding = HotkeyTimerStopwatch.GetBinding();
        HotkeyBinding qrCodeGeneratorBinding = HotkeyQRCodeGenerator.GetBinding();
        HotkeyBinding clipboardManagerBinding = HotkeyClipboardManager.GetBinding();
        HotkeyBinding textProcessingBinding = HotkeyTextProcessing.GetBinding();
        HotkeyBinding promptBuilderBinding = HotkeyPromptBuilder.GetBinding();
        HotkeyBinding zenEditorBinding = HotkeyZenEditor.GetBinding();
        HotkeyBinding aiteProfilesBinding = HotkeyAiteProfiles.GetBinding();

        if (!ValidateHotkeyBindings(nextBinding, previousBinding, addButtonBinding, fileSorterBinding, iconConverterBinding, quickNoteBinding, colorPickerBinding, timerStopwatchBinding, qrCodeGeneratorBinding, clipboardManagerBinding, textProcessingBinding, promptBuilderBinding, zenEditorBinding, aiteProfilesBinding))
        {
            return;
        }

        UpdateAiDependentQuickToolAvailability();

        _mainWindow.GetSettingsService().UpdateSettings(settings =>
        {
            settings.NextContextHotkey = nextBinding;
            settings.PreviousContextHotkey = previousBinding;
            settings.AddButtonHotkey = addButtonBinding;
            settings.FileSorterHotkey = fileSorterBinding;
            settings.IconConverterHotkey = iconConverterBinding;
            settings.QuickNoteHotkey = quickNoteBinding;
            settings.ColorPickerHotkey = colorPickerBinding;
            settings.TimerStopwatchHotkey = timerStopwatchBinding;
            settings.QRCodeGeneratorHotkey = qrCodeGeneratorBinding;
            settings.ClipboardManagerHotkey = clipboardManagerBinding;
            settings.TextProcessingHotkey = textProcessingBinding;
            settings.PromptBuilderHotkey = promptBuilderBinding;
            settings.ZenEditorHotkey = zenEditorBinding;
            settings.AiteProfilesHotkey = aiteProfilesBinding;

            foreach (var (checkBox, definition) in GetUtilityVisibilityBindings())
            {
                definition.SetVisible(settings, checkBox.IsChecked ?? false);
            }
            settings.ClipboardManagerPersistHistory = ChkClipboardManagerPersistHistory.IsChecked ?? true;
            settings.SaveTextProcessingDraft = ChkSaveTextProcessingDraft.IsChecked ?? false;
            if (!settings.SaveTextProcessingDraft)
            {
                settings.TextProcessingLastText = null;
            }
            settings.SavePromptBuilderDrafts = ChkSavePromptBuilderDrafts.IsChecked ?? false;
            if (!settings.SavePromptBuilderDrafts)
            {
                settings.PromptBuilderDrafts = [];
                settings.PromptBuilderLastText = null;
            }
            settings.ShowPanelOnMouseHover = ChkShowPanelOnMouseHover.IsChecked ?? true;
            settings.ShowTaskbarPositionIndicator = ChkShowTaskbarPositionIndicator.IsChecked ?? true;
            settings.CheckForUpdatesEnabled = ChkCheckForUpdatesEnabled.IsChecked ?? true;
            settings.UiCulture = _selectedUiCulture;
            settings.Ai = new AiSettings
            {
                FreeTierOnly = true,
                ProviderOrder = [.. (_settings.Ai?.ProviderOrder ?? AiProviderCatalog.DefaultProviderOrder)],
                Connections = _aiConnections.Select(CloneAiConnection).ToList()
            };

            string edgeStr = GetSelectedSegmentTag(SegEdgeTop, SegEdgeBottom, SegEdgeLeft, SegEdgeRight);
            if (Enum.TryParse<DockEdge>(edgeStr, out var edge))
            {
                settings.Edge = edge;
            }

            settings.MonitorIndex = AppSettingsSelectionHelper.ResolveMonitorIndex(
                settings.MonitorIndex,
                ChkSecondaryMonitor.IsChecked == true);
            System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;
            settings.MonitorDeviceName = settings.MonitorIndex >= 0 && settings.MonitorIndex < screens.Length
                ? screens[settings.MonitorIndex].DeviceName
                : System.Windows.Forms.Screen.PrimaryScreen?.DeviceName ?? string.Empty;

            settings.ActivationZoneSizePercent = AppSettingsSelectionHelper.ResolveSegmentedValue(
                settings.ActivationZoneSizePercent,
                AppSettingsDiscreteChoiceHelper.GetValue(
                    AppSettingsDiscreteChoiceHelper.ActivationZoneValues,
                    SliderActivationZone.Value),
                _activationZoneSelectionChanged);
            settings.PanelSizePercent = AppSettingsSelectionHelper.ResolveSegmentedValue(
                settings.PanelSizePercent,
                AppSettingsDiscreteChoiceHelper.GetValue(
                    AppSettingsDiscreteChoiceHelper.PanelSizeValues,
                    SliderPanelSize.Value),
                _panelSizeSelectionChanged);
            settings.ActivationDelayMs = AppSettingsSelectionHelper.ResolveSegmentedValue(
                settings.ActivationDelayMs,
                AppSettingsDiscreteChoiceHelper.GetValue(
                    AppSettingsDiscreteChoiceHelper.ActivationDelayValues,
                    SliderActivationDelay.Value),
                _activationDelaySelectionChanged);

            settings.Contexts = BuildReorderedContexts(settings, _contextRows
                .Select(row => new AppSettingsContextRowState(
                    row.ContextId,
                    row.NameTextBox.Text,
                    row.EnabledCheckBox.IsChecked ?? false))
                .ToList());
        });
        IReadOnlyList<string> failedHotkeys;
        try
        {
            failedHotkeys = await _mainWindow.SaveAppSettings();
            if (clearSensitiveDraftBackups)
            {
                _mainWindow.GetSettingsService().ClearSettingsBackups();
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Settings_SaveFailed", ex.Message))
            {
                Owner = this
            }.ShowDialog();
            return;
        }

        LocalizationService.ApplyCulture(_selectedUiCulture);
        CommitAiCredentialChanges();
        _mainWindow.RefreshPanel();

        if (failedHotkeys.Count > 0)
        {
            new DarkDialog(LocalizationService.Format("HotkeyRegistrationFailed", string.Join("\n", failedHotkeys)))
            {
                Owner = this
            }.ShowDialog();
        }

        this.Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        LocalizationService.ApplyCulture(_originalUiCulture);
        this.Close();
    }

    protected override void OnLocalizationChanged()
    {
        RefreshLocalizedUi();
    }

    internal static int ClearElementsForContexts(AppSettings settings, IReadOnlySet<string> contextIds)
    {
        if (contextIds.Count == 0 || settings.Elements.Count == 0)
        {
            return 0;
        }

        int before = settings.Elements.Count;
        settings.Elements = settings.Elements
            .Where(element => !contextIds.Contains(element.ContextId))
            .ToList();
        return before - settings.Elements.Count;
    }

    internal static List<PanelContext> BuildReorderedContexts(AppSettings settings, IReadOnlyList<AppSettingsContextRowState> rows)
    {
        var existingById = settings.Contexts.ToDictionary(context => context.Id, StringComparer.Ordinal);
        var result = new List<PanelContext>(rows.Count);
        var usedIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < rows.Count; i++)
        {
            AppSettingsContextRowState row = rows[i];
            if (!usedIds.Add(row.ContextId))
            {
                continue;
            }

            existingById.TryGetValue(row.ContextId, out PanelContext? existing);
            bool isNameCustomized = ContextStateHelper.IsCustomizedContextNameInput(row.Name, i);
            result.Add(new PanelContext
            {
                Id = row.ContextId,
                Name = isNameCustomized ? row.Name.Trim() : ContextStateHelper.GetDefaultContextName(i),
                IsNameCustomized = isNameCustomized,
                IconGlyph = string.IsNullOrWhiteSpace(existing?.IconGlyph) ? "\uE8B7" : existing.IconGlyph,
                IsEnabled = i == 0 || row.IsEnabled,
                Color = ContextStateHelper.GetContextColor(i)
            });
        }

        foreach (PanelContext existing in settings.Contexts)
        {
            if (usedIds.Add(existing.Id))
            {
                result.Add(existing);
            }
        }

        return ContextStateHelper.NormalizeContexts(result);
    }
}

internal static class StringExtensions
{
    public static string TrimOrDefault(this string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
