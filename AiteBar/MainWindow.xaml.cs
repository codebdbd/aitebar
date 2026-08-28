using System.Runtime.Versioning;
using Microsoft.Win32;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class MainWindow : Window, ISettingsWindowContext
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly ActivationDwellTracker _activationDwellTracker = new();
    private bool _shown = false, _isAnimating = false;
    private bool _activateWindowOnShow = true;
    private double _panelLeft, _panelTop, _panelRight, _panelBottom, _cachedDpi = 1.0;
    private static readonly BrushConverter _brushConverter = new();
    private static class MenuIcons
    {
        public const int Open = 62849; // ic_fluent_open_16_regular
        public const int Settings = 63144; // ic_fluent_settings_16_regular
        public const int Import = 58591; // ic_fluent_document_arrow_down_16_regular
        public const int Export = 62465; // ic_fluent_document_arrow_up_16_regular
        public const int Info = 62626; // ic_fluent_info_16_regular
        public const int Help = 63036; // ic_fluent_question_circle_16_regular
        public const int Donate = 59035; // ic_fluent_gift_16_regular
        public const int Exit = 985317; // ic_fluent_arrow_exit_16_regular
        public const int Unpin = 59781; // ic_fluent_pin_off_16_regular
        public const int Edit = 62428; // ic_fluent_edit_16_regular
        public const int Copy = 62250; // ic_fluent_copy_16_regular
        public const int Rename = 63080; // ic_fluent_rename_16_regular
        public const int Move = 57579; // ic_fluent_arrow_right_16_regular
        public const int Panels = 59567; // ic_fluent_panel_left_16_regular
        public const int OpenFolder = 59536; // ic_fluent_open_folder_16_regular
        public const int Clipboard = 58178; // ic_fluent_clipboard_16_regular
        public const int Delete = 58491; // ic_fluent_delete_16_regular
        public const int Update = 59548; // ic_fluent_arrow_sync_16_regular
        public const int Add = 61706; // ic_fluent_add_16_filled
    }

    private readonly AppSettingsService _settingsService;
    private readonly ActionService _actionService;
    private readonly HotkeyService _hotkeyService = new();
    private readonly PanelPackageService _panelPackageService;
    private readonly AiGateway _aiGateway;
    private readonly TaskbarPositionIndicatorService _positionIndicatorService = new();
    private NativeIntegrationService? _nativeService;

    private AppSettings AppSettings => _settingsService.Settings;
    private IReadOnlyList<CustomElement> Elements => _settingsService.Elements;

    internal void SetUtilityFullscreenSuppressed(bool suppressed) =>
        _positionIndicatorService.SetUtilityFullscreenSuppressed(suppressed);

    private System.Windows.Forms.NotifyIcon _notifyIcon = null!;

    private const string DonatePageUrl = "https://codebdbd.github.io/";
    private const double TopPanelVisibleOffset = 5;
    private bool _isPanelDragging = false;
    private bool _panelDragChanged = false;
    private DockEdge _dragStartEdge;
    private int _dragStartMonitorIndex;
    private bool _isElementContextMenuOpen;
    private bool _isBlockingPanelInteraction;
    private PanelInputMode _panelInputMode = PanelInputMode.Pointer;
    private readonly List<Button> _unifiedButtons = [];
    private List<UnifiedButton> _currentUnifiedButtons = [];
    private Button? _overflowButton;
    private int _pendingContextAnimationDirection;
    private readonly UnifiedButtonService _unifiedButtonService;
    private bool _startupInfrastructureInitialized;
    private bool _deferredStartupCompleted;
    private readonly bool _settingsPreloaded;
    private int _panelRefreshVersion;
        private int _panelFocusRequestVersion;
        private int _contextWheelDelta;
        private int _lastElementsVersion = -1;
        private int _mouseWheelCaptureToken = 0;
    private readonly CancellationTokenSource _startupCts = new();
    private PanelLayoutHelper.PanelLayoutMetrics _lastMetrics;
    private DateTime _lastContextWheelSwitchUtc = DateTime.MinValue;
    private readonly Dictionary<string, CachedButtonImage> _buttonImageCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Brush> _brushCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLocalizationSubscribed;
    private bool _focusPanelButtonsOnShow = true;
    private AppSettingsWindow? _appSettingsWindow;
    private bool _isOpeningAppSettingsWindow;
    private bool _powerModeEventsSubscribed;
    private int _powerResumeGuard = 0;

    private const double PanelScreenPadding = Constants.PanelScreenPadding;
    private const double ButtonPitch = Constants.ButtonOuterSize;
    private const double DragHandleSpan = Constants.DragHandleSpan;
    private const int WheelDeltaPerContextSwitch = Constants.WheelDeltaPerContextSwitch;
    private static readonly TimeSpan ContextWheelSwitchCooldown = TimeSpan.FromMilliseconds(Constants.ContextWheelSwitchCooldownMs);

    public MainWindow()
        : this(new AppSettingsService(), settingsPreloaded: false)
    {
    }

    public MainWindow(AppSettingsService settingsService)
        : this(settingsService, settingsPreloaded: true)
    {
    }

    private MainWindow(AppSettingsService settingsService, bool settingsPreloaded)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settingsPreloaded = settingsPreloaded;
        _actionService = new ActionService(_settingsService);
        _aiGateway = new AiGateway(_settingsService);
        _panelPackageService = new PanelPackageService(_settingsService);
        _unifiedButtonService = new UnifiedButtonService(_settingsService);
        Top = -2000;

        SizeChanged += (s, e) =>
        {
            if (!IsLoaded || _isAnimating)
            {
                return;
            }

            PositionWindowImmediately(_shown);
        };

        PathHelper.EnsureDirectories();
        InitTrayIcon();
        SubscribeToLocalizationChanges();

        // Subscribe to settings changes for auto-re-registration
        _settingsService.SettingsChanged += OnSettingsChanged;
        if (_settingsPreloaded)
        {
            ClipboardHistoryService.Instance.ConfigurePersistence(AppSettings.ClipboardManagerPersistHistory);
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        UiDispatcher.Run(Dispatcher, ApplySettingsChanged);
    }

    private void ApplySettingsChanged()
    {
        AppSettings settings = AppSettings;
        ClipboardHistoryService.Instance.ConfigurePersistence(settings.ClipboardManagerPersistHistory);
        UnregisterGlobalHotkey();
        RegisterGlobalHotkey();
        UpdateHoverActivationTimer(settings);
    }

    public AppSettings GetAppSettings() => _settingsService.Settings;
    public AppSettingsService GetSettingsService() => _settingsService;
    public ActionService GetActionService() => _actionService;
    public AiGateway GetAiGateway() => _aiGateway;

    private void SubscribeToLocalizationChanges()
    {
        if (_isLocalizationSubscribed)
        {
            return;
        }

        LocalizationService.CultureChanged += HandleCultureChanged;
        _isLocalizationSubscribed = true;
    }

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => HandleCultureChanged(sender, e));
            return;
        }

        LocalizationService.RefreshLocalizedBindings(this);
        ApplyLocalizedText();
        RefreshPanel();
    }

    public void ApplyLocalizedText()
    {
        BtnAdd.ToolTip = LocalizationService.Get("Main_AddButtonTooltip");
        BtnAppSettings.ToolTip = LocalizationService.Get("Menu_ProgramSettings");
        string dragHandleLabel = LocalizationService.Get("Main_DragHandleTooltip");
        DragHandle.ToolTip = dragHandleLabel;
        System.Windows.Automation.AutomationProperties.SetName(DragHandle, dragHandleLabel);
        System.Windows.Automation.AutomationProperties.SetHelpText(DragHandle, dragHandleLabel);
        BuildPanelContextMenu();
    }

    private ContextMenu BuildSystemUtilityContextMenu(Action detachAction)
    {
        ContextMenu menu = AppContextMenuFactory.CreateMenu(this);
        menu.Opened += (s, e) => _isElementContextMenuOpen = true;
        menu.Closed += (s, e) => _isElementContextMenuOpen = false;

        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Unpin), LocalizationService.Get("Menu_Unpin"), async (s, e) =>
        {
            await RunPanelInteractionAsync(async () =>
            {
                detachAction();
                await SaveSettingsWithNotificationAsync();
                RefreshPanel();
            });
        }));

        return menu;
    }

    private MenuItem CreateMenuItem(string glyph, string text, RoutedEventHandler? onClick = null, bool isDanger = false, bool isActive = false)
        => AppContextMenuFactory.CreateItem(
            this,
            glyph,
            text,
            onClick,
            isDanger,
            isActive);

    public async Task<IReadOnlyList<string>> SaveAppSettings()
    {
        _settingsService.NormalizeAppState();
        await _settingsService.SaveAsync();
        return RegisterGlobalHotkey();
    }

    private async Task SaveSettingsWithNotificationAsync()
    {
        try
        {
            await _settingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            // Show error dialog on UI thread
            await Dispatcher.InvokeAsync(() =>
            {
                new DarkDialog(LocalizationService.Format("Settings_SaveFailed", ex.Message)) { Owner = this }.ShowDialog();
            });
        }
    }

    private async void SaveSettingsWithNotification()
    {
        await SaveSettingsWithNotificationAsync();
    }

    public IReadOnlyList<PanelContext> GetContextsSnapshot() => _settingsService.GetContextsSnapshot();

    public IReadOnlyList<PanelContext> GetAllContextsSnapshot() => _settingsService.GetAllContextsSnapshot();

    public string GetContextDisplayName(string contextId) => _settingsService.GetContextDisplayName(contextId);

    private string GetPrimaryContextId() => _settingsService.GetPrimaryContextId();

    private (bool changed, int animationDirection) TryActivateContext(string contextId)
    {
        AppSettings settings = AppSettings;
        if (string.IsNullOrWhiteSpace(contextId) || string.Equals(settings.ActiveContextId, contextId, StringComparison.Ordinal))
        {
            return (false, 0);
        }

        int targetIndex = ContextStateHelper.FindEnabledContextIndex(settings.Contexts, contextId);
        if (targetIndex < 0)
        {
            return (false, 0);
        }

        int currentIndex = ContextStateHelper.FindEnabledContextIndex(settings.Contexts, settings.ActiveContextId);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        // Get settings from service, modify, update service!
        _settingsService.UpdateSettings(s => s.ActiveContextId = contextId);
        
        int animationDirection = targetIndex >= currentIndex ? 1 : -1;
        _pendingContextAnimationDirection = animationDirection;
        RefreshPanel();
        return (true, animationDirection);
    }

    private string? GetNextContextId(int direction)
    {
        AppSettings settings = AppSettings;
        return ContextStateHelper.GetRelativeEnabledContextId(settings.ActiveContextId, settings.Contexts, direction);
    }

    private string? GetContextIdByIndex(int index)
    {
        PanelContext? context = ContextStateHelper.GetContextAt(AppSettings.Contexts, index);
        return context?.IsEnabled == true ? context.Id : null;
    }

    private Brush GetCachedBrush(string colorHex)
    {
        if (_brushCache.TryGetValue(colorHex, out var brush))
        {
            return brush;
        }

        brush = (Brush)_brushConverter.ConvertFromString(colorHex)!;
        _brushCache[colorHex] = brush;
        return brush;
    }

    private async Task SwitchActiveContextAsync(int direction)
    {
        string? nextContextId = GetNextContextId(direction);
        if (nextContextId == null)
        {
            return;
        }

        var result = TryActivateContext(nextContextId);
        if (result.changed)
        {
            await SaveSettingsWithNotificationAsync();
        }
    }

    private async void ActivateContextRelative(int direction)
        {
            try
            {
                string? nextContextId = GetNextContextId(direction);
                if (nextContextId == null)
                {
                    return;
                }

                var result = TryActivateContext(nextContextId);
                if (result.changed)
                {
                    await SaveSettingsWithNotificationAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog(LocalizationService.Format("Action_Failed", ex.Message)) { Owner = this }.ShowDialog();
            }
        }

        private async void ActivateContextByIndex(int index)
        {
            try
            {
                string? nextContextId = GetContextIdByIndex(index);
                if (nextContextId == null)
                {
                    return;
                }

                var result = TryActivateContext(nextContextId);
                if (result.changed)
                {
                    await SaveSettingsWithNotificationAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

    private void BuildPanelContextMenu()
    {
        BuildPanelContextMenu(AppSettings.ActiveContextId);
    }

    private void BuildPanelContextMenu(string activeContextId)
    {
        ContextMenu menu = AppContextMenuFactory.CreateMenu(this);
        menu.Opened += (s, e) => _isElementContextMenuOpen = true;
        menu.Closed += (s, e) => _isElementContextMenuOpen = false;

        MenuItem panelsMenu = CreateMenuItem(FluentGlyph(MenuIcons.Panels), LocalizationService.Get("Menu_Panels"));

        foreach (PanelContext context in GetContextsSnapshot())
        {
            bool isActive = string.Equals(context.Id, activeContextId, StringComparison.Ordinal);
            string targetContextId = context.Id;

            MenuItem item = CreateMenuItem(
                glyph: FluentGlyph(MenuIcons.Panels),
                text: ContextStateHelper.GetContextListDisplayName(context),
                onClick: (s, e) => ActivateContextById(targetContextId),
                isActive: isActive
            );

            panelsMenu.Items.Add(item);
        }

        menu.Items.Add(panelsMenu);
        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Add), LocalizationService.Get("Menu_AddButton"), async (s, e) =>
        {
            await OpenAddButtonWindowAsync();
        }));
        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Import), LocalizationService.Get("Menu_ImportCurrentPanel"), async (s, e) =>
        {
            await RunPanelInteractionAsync(ImportIntoCurrentPanelAsync);
        }));
        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Export), LocalizationService.Get("Menu_ExportCurrentPanel"), async (s, e) =>
        {
            await RunPanelInteractionAsync(ExportCurrentPanelAsync);
        }));

        RootBorder.ContextMenu = menu;
    }

    private ContextMenu BuildElementContextMenu(CustomElement element)
    {
        ContextMenu menu = AppContextMenuFactory.CreateMenu(this);
        menu.Opened += (s, e) => _isElementContextMenuOpen = true;
        menu.Closed += (s, e) => _isElementContextMenuOpen = false;

        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Edit), LocalizationService.Get("Menu_Edit"), (s, e) =>
        {
            RunPanelInteraction(() => new SettingsWindow(this, element) { Owner = this }.ShowDialog());
        }));

        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Copy), LocalizationService.Get("Menu_Duplicate"), async (s, e) =>
        {
            await RunPanelInteractionAsync(() => DuplicateElementAsync(element));
        }));

        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Rename), LocalizationService.Get("Menu_Rename"), async (s, e) =>
        {
            await RunPanelInteractionAsync(() => RenameElementAsync(element));
        }));

        List<MenuItem> moveTargets = GetContextsSnapshot()
            .Where(context => !string.Equals(context.Id, element.ContextId, StringComparison.Ordinal))
            .Select(context => CreateMenuItem(
                FluentGlyph(MenuIcons.Panels),
                ContextStateHelper.GetContextListDisplayName(context),
                async (s, e) =>
                {
                    await RunPanelInteractionAsync(() => MoveElementToContextAsync(element.Id, context.Id));
                }))
            .ToList();

        if (moveTargets.Count > 0)
        {
            MenuItem moveMenu = CreateMenuItem(FluentGlyph(MenuIcons.Move), LocalizationService.Get("Menu_Move"));
            foreach (MenuItem moveTarget in moveTargets)
            {
                moveMenu.Items.Add(moveTarget);
            }
            menu.Items.Add(moveMenu);
        }

        if (TryCreateCopyActionMenuItem(element, out MenuItem copyItem))
        {
            menu.Items.Add(copyItem);
        }

        if (CanOpenElementLocation(element))
        {
            menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.OpenFolder), LocalizationService.Get("Menu_OpenLocation"), async (s, e) =>
            {
                await RunPanelInteractionAsync(() => OpenElementLocationAsync(element));
            }));
        }

        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Delete), LocalizationService.Get("Menu_Delete"), async (s, e) =>
        {
            await RunPanelInteractionAsync(() => DeleteElementAsync(element));
        }, isDanger: true));

        return menu;
    }

    private async Task DuplicateElementAsync(CustomElement source)
    {
        CustomElement duplicate = _settingsService.CloneElement(source);
        duplicate.Id = Guid.NewGuid().ToString();
        duplicate.Name = BuildDuplicateElementName(source.Name);
        duplicate.LastUsedProfile = "";

        await _settingsService.InsertElementAfterAsync(source.Id, duplicate);
        RegisterGlobalHotkey();
        RefreshPanel();
        new SettingsWindow(this, duplicate) { Owner = this }.ShowDialog();
    }

    private string BuildDuplicateElementName(string sourceName)
    {
        string baseName = string.IsNullOrWhiteSpace(sourceName) ? LocalizationService.Get("Element_NewButton") : sourceName.Trim();
        string firstCandidate = $"{baseName} ({LocalizationService.Get("Element_CopySuffix")})";
        if (Elements.All(x => !string.Equals(x.Name, firstCandidate, StringComparison.OrdinalIgnoreCase))) return firstCandidate;

        for (int index = 2; index < 10000; index++)
        {
            string candidate = $"{baseName} ({LocalizationService.Format("Element_CopySuffixFormat", index)})";
            if (Elements.All(x => !string.Equals(x.Name, candidate, StringComparison.OrdinalIgnoreCase))) return candidate;
        }
        // Fallback if all 9999 names are taken (extremely unlikely)
        return $"{baseName} ({Guid.NewGuid()})";
    }

    private async Task RenameElementAsync(CustomElement source)
    {
        CustomElement? elementToRename = Elements.FirstOrDefault(x => string.Equals(x.Id, source.Id, StringComparison.Ordinal));
        if (elementToRename == null) return;

        TextPromptDialog dialog = new TextPromptDialog("Prompt_RenameButtonTitle", "Prompt_NewName", elementToRename.Name, treatAsResourceKeys: true) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string newName = dialog.Value.Trim();
        if (string.Equals(elementToRename.Name, newName, StringComparison.Ordinal))
        {
            return;
        }

        await _settingsService.UpdateElementAsync(elementToRename.Id, element => element.Name = newName);
        RefreshPanel();
    }

    private async Task MoveElementToContextAsync(string elementId, string targetContextId)
    {
        CustomElement? elementToMove = Elements.FirstOrDefault(x => string.Equals(x.Id, elementId, StringComparison.Ordinal));
        if (elementToMove == null || string.Equals(elementToMove.ContextId, targetContextId, StringComparison.Ordinal))
        {
            return;
        }

        await _settingsService.UpdateElementAsync(elementToMove.Id, element => element.ContextId = targetContextId);
        RefreshPanel();
    }

    private async Task DeleteElementAsync(CustomElement source)
    {
        CustomElement? elementToDelete = Elements.FirstOrDefault(x => string.Equals(x.Id, source.Id, StringComparison.Ordinal));
        if (elementToDelete == null)
        {
            return;
        }

        DarkDialog dialog = new DarkDialog(LocalizationService.Format("DeleteButtonConfirm", elementToDelete.Name), isConfirm: true) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await _settingsService.DeleteElementAsync(elementToDelete.Id);
        RegisterGlobalHotkey();
        RefreshPanel();
    }

    private bool TryCreateCopyActionMenuItem(CustomElement element, out MenuItem menuItem)
    {
        menuItem = null!;

        if (!TryGetCopyValue(element, out string caption, out string value))
        {
            return false;
        }

        menuItem = CreateMenuItem(FluentGlyph(MenuIcons.Clipboard), caption, (s, e) =>
        {
            try
            {
                Clipboard.SetText(value);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog(LocalizationService.Format("Action_Failed", ex.Message)) { Owner = this }.ShowDialog();
            }
        });
        return true;
    }

    private static bool TryGetCopyValue(CustomElement element, out string caption, out string value)
    {
        caption = string.Empty;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(element.ActionValue) ||
            !Enum.TryParse<ActionType>(element.ActionType, out var actionType))
        {
            return false;
        }

        switch (actionType)
        {
            case ActionType.Web:
                caption = LocalizationService.Get("Menu_CopyUrl");
                value = element.ActionValue;
                return true;
            case ActionType.Program:
            case ActionType.File:
            case ActionType.Folder:
            case ActionType.ScriptFile:
                caption = LocalizationService.Get("Menu_CopyPath");
                value = element.ActionValue;
                return true;
            case ActionType.Command:
                caption = LocalizationService.Get("Menu_CopyCommand");
                value = element.ActionValue;
                return true;
            default:
                return false;
        }
    }

    private static string FluentGlyph(int codePoint) => char.ConvertFromUtf32(codePoint);

    private static bool CanOpenElementLocation(CustomElement element)
    {
        if (!Enum.TryParse<ActionType>(element.ActionType, out var actionType))
        {
            return false;
        }

        return actionType is ActionType.Program or ActionType.File or ActionType.Folder or ActionType.ScriptFile;
    }

    private async Task OpenElementLocationAsync(CustomElement element)
    {
        try
        {
            if (!Enum.TryParse<ActionType>(element.ActionType, out ActionType actionType))
            {
                return;
            }

            string target = element.ActionValue;
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            switch (actionType)
            {
                case ActionType.Folder:
                    if (Directory.Exists(target))
                    {
                        var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                        psi.ArgumentList.Add(target);
                        Process.Start(psi);
                    }
                    else
                    {
                        new DarkDialog(LocalizationService.Format("Action_TargetNotFound", target)) { Owner = this }.ShowDialog();
                    }
                    break;

                case ActionType.Program:
                case ActionType.File:
                case ActionType.ScriptFile:
                    if (File.Exists(target))
                    {
                        var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                        psi.ArgumentList.Add("/select," + target);
                        Process.Start(psi);
                    }
                    else
                    {
                        new DarkDialog(LocalizationService.Format("Action_TargetNotFound", target)) { Owner = this }.ShowDialog();
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Action_Failed", ex.Message)) { Owner = this }.ShowDialog();
            await Task.CompletedTask;
        }
    }

    private void RunPanelInteraction(Action action)
    {
        BeginBlockingPanelInteraction();
        try
        {
            action();
        }
        finally
        {
            EndBlockingPanelInteraction();
        }
    }

    private async Task RunPanelInteractionAsync(Func<Task> action)
    {
        BeginBlockingPanelInteraction();
        try
        {
            await action();
        }
        finally
        {
            EndBlockingPanelInteraction();
        }
    }

    private async void ActivateContextById(string contextId)
        {
            try
            {
                var result = TryActivateContext(contextId);
                if (result.changed)
                {
                    await SaveSettingsWithNotificationAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

    private Screen? GetTargetScreen()
    {
        AppSettings settings = AppSettings;
        return GetTargetScreen(settings.MonitorIndex, settings.MonitorDeviceName);
    }

    private static Screen? GetTargetScreen(int monitorIndex, string? monitorDeviceName = null)
    {
        Screen[] screens = Screen.AllScreens;
        if (!string.IsNullOrWhiteSpace(monitorDeviceName))
        {
            Screen? byDeviceName = screens.FirstOrDefault(screen =>
                string.Equals(screen.DeviceName, monitorDeviceName, StringComparison.OrdinalIgnoreCase));
            if (byDeviceName is not null)
            {
                return byDeviceName;
            }
        }

        return (monitorIndex >= 0 && monitorIndex < screens.Length)
            ? screens[monitorIndex]
            : Screen.PrimaryScreen;
    }

    private (double AvailableWidth, double AvailableHeight) CalculateAvailableSize()
    {
        AppSettings settings = AppSettings;
        return CalculateAvailableSize(settings.MonitorIndex, settings.MonitorDeviceName);
    }

    private (double AvailableWidth, double AvailableHeight) CalculateAvailableSize(int monitorIndex, string? monitorDeviceName = null)
    {
        Screen? screen = GetTargetScreen(monitorIndex, monitorDeviceName);
        Rectangle? workArea = screen?.WorkingArea;
        double targetDpi = TaskbarGeometryHelper.GetMonitorDpiScale(screen, _cachedDpi);
        double availableWidth = workArea.HasValue
            ? Math.Max(150, TaskbarGeometryHelper.PixelsToDips(workArea.Value.Width, targetDpi) - PanelScreenPadding)
            : 150;
        double availableHeight = workArea.HasValue
            ? Math.Max(150, TaskbarGeometryHelper.PixelsToDips(workArea.Value.Height, targetDpi) - PanelScreenPadding)
            : 150;

        return (availableWidth, availableHeight);
    }

    private PanelLayoutHelper.PanelLayoutMetrics ComputePanelMetrics(
        bool isVertical,
        double availableWidth,
        double availableHeight,
        int totalButtonCount,
        double panelSizePercent)
    {
        return PanelLayoutHelper.Calculate(
            isVertical: isVertical,
            availablePrimary: isVertical ? availableHeight : availableWidth,
            panelPercent: panelSizePercent,
            totalButtonCount: totalButtonCount,
            controlButtonCount: 2,
            trailingControlButtonCount: 1);
    }

    private PanelLayoutHelper.PanelLayoutMetrics ComputeStablePrimaryPanelMetrics(
        bool isVertical,
        double availableWidth,
        double availableHeight,
        AppSettings settings,
        IReadOnlyList<CustomElement> elements)
    {
        int activeButtonCount = _unifiedButtonService
            .BuildUnifiedList(settings.ActiveContextId, settings, elements)
            .Count;
        PanelLayoutHelper.PanelLayoutMetrics activeMetrics = ComputePanelMetrics(
            isVertical,
            availableWidth,
            availableHeight,
            activeButtonCount,
            settings.PanelSizePercent);
        double maxPrimary = isVertical ? activeMetrics.PanelHeight : activeMetrics.PanelWidth;

        foreach (PanelContext context in settings.Contexts)
        {
            if (!context.IsEnabled)
            {
                continue;
            }

            int contextButtonCount = _unifiedButtonService.BuildUnifiedList(context.Id, settings, elements).Count;
            PanelLayoutHelper.PanelLayoutMetrics contextMetrics = ComputePanelMetrics(
                isVertical,
                availableWidth,
                availableHeight,
                contextButtonCount,
                settings.PanelSizePercent);

            double contextPrimary = isVertical ? contextMetrics.PanelHeight : contextMetrics.PanelWidth;
            if (contextPrimary > maxPrimary)
            {
                maxPrimary = contextPrimary;
            }
        }

        return isVertical
            ? activeMetrics with { PanelHeight = maxPrimary }
            : activeMetrics with { PanelWidth = maxPrimary };
    }

    private void ApplyPanelSizeConstraints(PanelLayoutHelper.PanelLayoutMetrics metrics, bool isVertical)
    {
        RootBorder.MaxWidth = double.PositiveInfinity;
        RootBorder.MaxHeight = double.PositiveInfinity;
        RootBorder.MinWidth = 0;
        RootBorder.MinHeight = 0;
        MainPanel.MaxWidth = double.PositiveInfinity;
        MainPanel.MaxHeight = double.PositiveInfinity;
        MainPanel.Width = double.NaN;
        MainPanel.Height = double.NaN;
        FixedPanel.MaxWidth = double.PositiveInfinity;
        FixedPanel.MaxHeight = double.PositiveInfinity;
        FixedPanel.Width = double.NaN;
        FixedPanel.Height = double.NaN;
        ControlBlock.Width = double.NaN;
        ControlBlock.Height = double.NaN;
        AppSettingsBlock.MaxWidth = double.PositiveInfinity;
        AppSettingsBlock.MaxHeight = double.PositiveInfinity;
        AppSettingsBlock.Width = double.NaN;
        AppSettingsBlock.Height = double.NaN;
        UnifiedButtonsPanel.MaxWidth = double.PositiveInfinity;
        UnifiedButtonsPanel.MaxHeight = double.PositiveInfinity;
        UnifiedButtonsPanel.MinWidth = 0;
        UnifiedButtonsPanel.MinHeight = 0;
        UnifiedButtonsPanel.Width = double.NaN;
        UnifiedButtonsPanel.Height = double.NaN;

        // Apply layout rounding to avoid sub-pixel values (prevents flicker & phantom scroll)
        RootBorder.MinWidth = Math.Round(metrics.PanelWidth);
        RootBorder.MaxWidth = Math.Round(metrics.PanelWidth);
        RootBorder.MinHeight = Math.Round(metrics.PanelHeight);
        RootBorder.MaxHeight = Math.Round(metrics.PanelHeight);

        double contentWidth = Math.Max(0, Math.Round(metrics.PanelWidth - PanelLayoutHelper.PanelChrome));
        double contentHeight = Math.Max(0, Math.Round(metrics.PanelHeight - PanelLayoutHelper.PanelChrome));

        if (isVertical)
        {
            RootBorder.MinHeight = Math.Round(RootBorder.MinHeight + DragHandleSpan);
            RootBorder.MaxHeight = Math.Round(RootBorder.MaxHeight + DragHandleSpan);
            contentHeight = Math.Round(contentHeight + DragHandleSpan);
        }
        else
        {
            RootBorder.MinWidth = Math.Round(RootBorder.MinWidth + DragHandleSpan);
            RootBorder.MaxWidth = Math.Round(RootBorder.MaxWidth + DragHandleSpan);
            contentWidth = Math.Round(contentWidth + DragHandleSpan);
        }

        MainPanel.Width = contentWidth;
        MainPanel.Height = contentHeight;
        FixedPanel.Width = isVertical ? contentWidth : Math.Round(metrics.FixedWidth);
        FixedPanel.Height = isVertical && !metrics.UseMultiColumnControls ? 2 * PanelLayoutHelper.ButtonOuterSize : Math.Round(metrics.FixedHeight);
        ControlBlock.Width = isVertical ? contentWidth : double.NaN;
        AppSettingsBlock.Width = Math.Round(metrics.TrailingWidth);
        AppSettingsBlock.Height = Math.Round(metrics.TrailingHeight);

        UnifiedButtonsPanel.Width = Math.Round(metrics.UserWidth);
        UnifiedButtonsPanel.Height = Math.Round(metrics.UserHeight);
        UnifiedButtonsPanel.MaxWidth = Math.Round(metrics.UserWidth);
        UnifiedButtonsPanel.MaxHeight = Math.Round(metrics.UserHeight);
        UnifiedButtonsPanel.MinWidth = Math.Round(metrics.UserWidth);
        UnifiedButtonsPanel.MinHeight = Math.Round(metrics.UserHeight);
        UnifiedButtonsPanel.LeadingPrimaryReserve = isVertical ? Math.Round(metrics.UserLeadingReserve) : 0;
        UnifiedButtonsPanel.OverflowPrimaryReserve = isVertical ? Math.Round(metrics.UserOverflowReserve) : 0;
        UnifiedButtonsPanel.Margin = isVertical && metrics.UserLeadingReserve > 0
            ? new Thickness(0, -Math.Round(metrics.UserLeadingReserve), 0, 0)
            : new Thickness(0);
    }

    private int GetVisibleSystemButtonCount()
    {
        return UtilityButtonCatalog.CountVisible(AppSettings);
    }

    private static readonly HashSet<HotkeyCommand> AllowedHotkeysWithOwnedWindows = new()
        {
            HotkeyCommand.QuickNote,
            HotkeyCommand.TimerStopwatch
        };

    private IReadOnlyList<string> RegisterGlobalHotkey()
    {
        IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        IReadOnlyList<HotkeyDefinition> commandDefinitions = _hotkeyService.CreateDefinitions(AppSettings, LocalizationService.Get);
        IReadOnlyList<HotkeyRegistrationResult> results = _hotkeyService.RegisterAll(hwnd, commandDefinitions);
        return _hotkeyService.GetFailedDisplayNames(results);
    }

    private void UnregisterGlobalHotkey()
    {
        IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        _hotkeyService.UnregisterAll(hwnd);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_POWERBROADCAST)
        {
            int powerEvent = wParam.ToInt32();
            if (powerEvent == NativeMethods.PBT_APMSUSPEND)
            {
                HandleSystemSuspend();
            }
            else if (powerEvent == NativeMethods.PBT_APMRESUMESUSPEND ||
                     powerEvent == NativeMethods.PBT_APMRESUMEAUTOMATIC)
            {
                HandleSystemResume();
            }
        }

        if (msg == NativeMethods.WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();

            // Check if we have a visible owned window
            if (HasVisibleOwnedWindow())
            {
                // Check if this is an allowed command hotkey
                if (_hotkeyService.TryGetCommand(hotkeyId, out HotkeyCommand command) &&
                    AllowedHotkeysWithOwnedWindows.Contains(command))
                {
                    // Allow it - fall through to normal handling
                }
                else
                {
                    handled = true;
                    return IntPtr.Zero;
                }
            }

            // Handle command hotkey
            if (_hotkeyService.TryGetCommand(hotkeyId, out HotkeyCommand cmd))
            {
                ExecuteHotkeyCommand(cmd);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private void ExecuteHotkeyCommand(HotkeyCommand command)
    {
        if (HotkeyService.TryGetContextNumber(command, out int contextNumber))
        {
            PanelContext? context = ContextStateHelper.GetContextAt(AppSettings.Contexts, contextNumber);
            if (context?.IsEnabled != true)
            {
                return;
            }
            string contextId = context.Id;
            var result = TryActivateContext(contextId);
            if (result.changed)
            {
                _ = SaveSettingsWithNotificationAsync();
            }
            ShowDock(fromKeyboard: true);
            return;
        }

        switch (command)
        {
            case HotkeyCommand.ShowPanel:
                ToggleDock(fromKeyboard: true);
                break;
            case HotkeyCommand.NextContext:
                ActivateContextRelative(1);
                break;
            case HotkeyCommand.PreviousContext:
                ActivateContextRelative(-1);
                break;
            case HotkeyCommand.AddButton:
                _ = OpenAddButtonWindowAsync();
                break;
            case HotkeyCommand.FileSorter:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("FileSorter", HideDock));
                break;
            case HotkeyCommand.IconConverter:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("IconConverter", HideDock));
                break;
            case HotkeyCommand.QuickNote:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("QuickNote", HideDock));
                break;
            case HotkeyCommand.ColorPicker:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("ColorPicker", HideDock));
                break;
            case HotkeyCommand.TimerStopwatch:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("TimerStopwatch", HideDock));
                break;
            case HotkeyCommand.QRCodeGenerator:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("QRCodeGenerator", HideDock));
                break;
            case HotkeyCommand.ClipboardManager:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("ClipboardManager", HideDock));
                break;
            case HotkeyCommand.TextProcessing:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("TextProcessing", HideDock));
                break;
            case HotkeyCommand.PromptBuilder:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("PromptBuilder", HideDock));
                break;
            case HotkeyCommand.ZenEditor:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("ZenEditor", HideDock));
                break;
            case HotkeyCommand.AiteProfiles:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("AiteProfiles", HideDock));
                break;
        }
    }

    private bool HasVisibleOwnedWindow()
    {
        foreach (Window window in OwnedWindows)
        {
            if (window.IsVisible)
            {
                return true;
            }
        }

        return false;
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        System.Windows.Interop.HwndSource.FromHwnd(hwnd).AddHook(WndProc);
        ClipboardHistoryService.Instance.Initialize(hwnd);
    }

    private Button CreatePanelButton(string content, string tooltip, RoutedEventHandler onClick, Brush? foreground = null)
    {
        var btn = new Button
        {
            Content = content,
            ToolTip = tooltip,
            Style = (Style)FindResource("PanelButtonStyle"),
            Focusable = true
        };

        if (foreground != null)
        {
            btn.Foreground = foreground;
        }

        if (!string.IsNullOrWhiteSpace(tooltip))
        {
            System.Windows.Automation.AutomationProperties.SetName(btn, tooltip);
            System.Windows.Automation.AutomationProperties.SetHelpText(btn, tooltip);
        }

        btn.Click += onClick;
        return btn;
    }

    private bool IsPanelInteractionActive => _isElementContextMenuOpen || _isBlockingPanelInteraction || _isPanelDragging;

    private void BeginBlockingPanelInteraction()
    {
        _isBlockingPanelInteraction = true;
        _activationDwellTracker.Reset();
    }

    private void EndBlockingPanelInteraction()
    {
        _isBlockingPanelInteraction = false;
        _activationDwellTracker.Reset();
    }

    private void UpdatePanelBounds()
    {
        if (!this.IsLoaded) return;
        _cachedDpi = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        _panelLeft = this.Left * _cachedDpi;
        _panelTop = this.Top * _cachedDpi;
        _panelRight = _panelLeft + this.ActualWidth * _cachedDpi;
        _panelBottom = _panelTop + this.ActualHeight * _cachedDpi;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            // Validate URL scheme to only allow http/https
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
        catch (Exception ex) { Logger.Log(ex); }
    }



    private (Rect WorkArea, Rect Bounds) GetTargetScreenMetrics()
    {
        AppSettings settings = AppSettings;
        return GetTargetScreenMetrics(settings.MonitorIndex, settings.MonitorDeviceName);
    }

    private (Rect WorkArea, Rect Bounds) GetTargetScreenMetrics(int monitorIndex, string? monitorDeviceName = null)
    {
        var screen = GetTargetScreen(monitorIndex, monitorDeviceName);

        // Если экран не найден, используем PrimaryScreen. Если и его нет, используем системные параметры.
        var primary = Screen.PrimaryScreen;
        var drawingWorkArea = screen?.WorkingArea ?? primary?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
        var drawingBounds = screen?.Bounds ?? primary?.Bounds ?? new System.Drawing.Rectangle(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);

        // Если мы упали в fallback через SystemParameters, то значения уже в DIP-ах, и делить на DPI не нужно.
        // Если же мы взяли значения из Screen (System.Drawing), то они в пикселях и требуют деления.
        bool isFromSystemParameters = (screen == null && primary == null);
        double dpi = isFromSystemParameters
            ? 1.0
            : TaskbarGeometryHelper.GetMonitorDpiScale(screen, _cachedDpi);

        return (
            new Rect(
                TaskbarGeometryHelper.PixelsToDips(drawingWorkArea.Left, dpi),
                TaskbarGeometryHelper.PixelsToDips(drawingWorkArea.Top, dpi),
                TaskbarGeometryHelper.PixelsToDips(drawingWorkArea.Width, dpi),
                TaskbarGeometryHelper.PixelsToDips(drawingWorkArea.Height, dpi)),
            new Rect(
                TaskbarGeometryHelper.PixelsToDips(drawingBounds.Left, dpi),
                TaskbarGeometryHelper.PixelsToDips(drawingBounds.Top, dpi),
                TaskbarGeometryHelper.PixelsToDips(drawingBounds.Width, dpi),
                TaskbarGeometryHelper.PixelsToDips(drawingBounds.Height, dpi))
        );
    }

    private (double X, double Y) GetDockCoordinates(bool hide)
    {
        return GetDockCoordinates(hide, AppSettings);
    }

    private (double X, double Y) GetDockCoordinates(bool hide, AppSettings settings)
    {
        var metrics = GetTargetScreenMetrics(settings.MonitorIndex, settings.MonitorDeviceName);
        var workArea = metrics.WorkArea;
        var bounds = metrics.Bounds;

        // Используем заданные ограничения RootBorder вместо ActualWidth/ActualHeight, 
        // так как Actual-свойства могут быть устаревшими во время смены ориентации (SizeToContent срабатывает не мгновенно).
        double width = (RootBorder != null && RootBorder.MinWidth > 0)
            ? RootBorder.MinWidth + RootBorder.Margin.Left + RootBorder.Margin.Right
            : ActualWidth;
        double height = (RootBorder != null && RootBorder.MinHeight > 0)
            ? RootBorder.MinHeight + RootBorder.Margin.Top + RootBorder.Margin.Bottom
            : ActualHeight;

        // Если все еще 0, пробуем запустить Measure
        if ((width <= 0 || height <= 0) && RootBorder != null)
        {
            RootBorder.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            width = RootBorder.DesiredSize.Width + RootBorder.Margin.Left + RootBorder.Margin.Right;
            height = RootBorder.DesiredSize.Height + RootBorder.Margin.Top + RootBorder.Margin.Bottom;
        }

        // Защита от нулевых размеров
        if (width <= 0) width = 200;
        if (height <= 0) height = 50;

        return PanelPositionHelper.GetDockCoordinates(
            settings.Edge,
            workArea,
            bounds,
            width,
            height,
            TopPanelVisibleOffset,
            hide);
    }

    private bool _isPositioning = false;
    private void PositionWindowImmediately(bool shown)
    {
        PositionWindowImmediately(shown, AppSettings);
    }

    private void PositionWindowImmediately(bool shown, AppSettings settings)
    {
        if (_isPositioning) return;
        _isPositioning = true;
        try
        {
            this.UpdateLayout();
            var coordinates = GetDockCoordinates(hide: !shown, settings);
            Left = coordinates.X;
            Top = coordinates.Y;
            UpdatePanelBounds();
        }
        finally
        {
            _isPositioning = false;
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LocalizationService.EnsureAppliedCulture();
            LocalizationService.RefreshLocalizedBindings(this);
            ApplyLocalizedText();
            EnsureStartupInfrastructure();
            RefreshPanel();
            PositionWindowImmediately(_shown);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            if (_settingsPreloaded)
            {
                _deferredStartupCompleted = true;
                RegisterGlobalHotkey();
                _positionIndicatorService.Initialize(_settingsService, this);
                return;
            }
            try
            {
                await CompleteDeferredStartupAsync();
            }
            catch (Exception ex)
            {
                _ = Logger.LogAsync(ex.GetBaseException());
            }
        }
        catch (Exception ex) { Logger.Log(ex); }
    }

    private void EnsureStartupInfrastructure()
    {
        if (_startupInfrastructureInitialized)
        {
            return;
        }

        SubscribeToPowerEvents();

        _nativeService = new NativeIntegrationService();
        _nativeService.MouseDownOutside += (x, y) =>
        {
            _positionIndicatorService.HandleGlobalMouseDown(x, y);

            if (_shown && !_isAnimating && !IsPanelInteractionActive)
            {
                if (x < _panelLeft || x > _panelRight || y < _panelTop || y > _panelBottom)
                {
                    this.Dispatcher.InvokeAsync(async () => await HideDock());
                }
            }
        };

        _timer.Tick += (s, ev) =>
        {
            if (_shown || _isAnimating || _isPanelDragging)
            {
                _activationDwellTracker.Reset();
                UpdateHoverActivationTimer();
                return;
            }

            AppSettings settings = AppSettings;
            if (!settings.ShowPanelOnMouseHover)
            {
                _activationDwellTracker.Reset();
                UpdateHoverActivationTimer(settings);
                return;
            }

            NativeMethods.Win32Point pt = new();
            if (NativeMethods.GetCursorPos(ref pt))
            {
                var screens = Screen.AllScreens;
                var screen = (settings.MonitorIndex >= 0 && settings.MonitorIndex < screens.Length)
                    ? screens[settings.MonitorIndex]
                    : Screen.PrimaryScreen;

                if (screen == null) return;

                var bounds = screen.Bounds;
                double screenLeft = bounds.Left;
                double screenTop = bounds.Top;
                double screenWidth = bounds.Width;
                double screenHeight = bounds.Height;

                bool inActivationZone = ActivationZoneHelper.IsInActivationZone(
                    settings.Edge,
                    screenLeft,
                    screenTop,
                    screenWidth,
                    screenHeight,
                    settings.ActivationZoneSizePercent,
                    pt.X,
                    pt.Y);

                if (_activationDwellTracker.Update(
                    inActivationZone,
                    pt.X,
                    pt.Y,
                    DateTime.UtcNow,
                    settings.ActivationDelayMs))
                {
                    ShowDock(activateWindow: false);
                }
            }
        };

        _nativeService.InstallMouseHook();
        _startupInfrastructureInitialized = true;
        UpdateHoverActivationTimer();
    }

    private void SubscribeToPowerEvents()
    {
        if (_powerModeEventsSubscribed)
        {
            return;
        }

        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            _powerModeEventsSubscribed = true;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private void UnsubscribeFromPowerEvents()
    {
        if (!_powerModeEventsSubscribed)
        {
            return;
        }

        try
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }

        _powerModeEventsSubscribed = false;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                HandleSystemSuspend();
                break;
            case PowerModes.Resume:
                HandleSystemResume();
                break;
        }
    }

    private void HandleSystemSuspend()
    {
        try
        {
            _timer.Stop();
            _activationDwellTracker.Reset();
            _nativeService?.UninstallMouseHook();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private void HandleSystemResume()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(HandleSystemResume), DispatcherPriority.Background);
            return;
        }

        int guard = System.Threading.Interlocked.CompareExchange(ref _powerResumeGuard, 1, 0);
        if (guard != 0)
        {
            return;
        }

        try
        {
            _nativeService?.InstallMouseHook();

            try
            {
                ClipboardHistoryService.Instance?.ReinitializeFormatListener();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            try
            {
                UnregisterGlobalHotkey();
                RegisterGlobalHotkey();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            _isAnimating = false;
            _isPanelDragging = false;
            _isBlockingPanelInteraction = false;
            _activationDwellTracker.Reset();

            RefreshPanel();
            PositionWindowImmediately(_shown);
            UpdateHoverActivationTimer();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _powerResumeGuard, 0);
        }
    }

    private void UpdateHoverActivationTimer(AppSettings? settings = null)
    {
        settings ??= AppSettings;
        bool shouldRun = _startupInfrastructureInitialized
            && !_shown
            && !_isAnimating
            && settings.ShowPanelOnMouseHover;

        if (shouldRun)
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
        }
        else
        {
            _timer.Stop();
            _activationDwellTracker.Reset();
        }
    }

    private async Task CompleteDeferredStartupAsync()
    {
        if (_deferredStartupCompleted)
        {
            return;
        }

        CancellationToken token = _startupCts.Token;
        try
        {
            await Task.Run(async () => await _settingsService.LoadAsync(), token);
            token.ThrowIfCancellationRequested();
            LocalizationService.ApplyCulture(AppSettings.UiCulture);
            ClipboardHistoryService.Instance.ConfigurePersistence(AppSettings.ClipboardManagerPersistHistory);
            _deferredStartupCompleted = true;
            ApplyLocalizedText();
            RegisterGlobalHotkey();
            RefreshPanel();
            PositionWindowImmediately(_shown);
            _positionIndicatorService.Initialize(_settingsService, this);
            _ = Dispatcher.BeginInvoke(IconPickerWindow.WarmupCatalogMetadata, DispatcherPriority.ApplicationIdle);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _ = Logger.LogAsync(ex);
        }
        finally
        {
            _startupCts.Dispose();
        }
    }

    internal void UpdateOrientation(bool reposition = true, bool applySizeConstraints = true)
    {
        UpdateOrientation(AppSettings, reposition, applySizeConstraints);
    }

    private void UpdateOrientation(AppSettings settings, bool reposition = true, bool applySizeConstraints = true)
    {
        bool isVertical = settings.Edge == DockEdge.Left || settings.Edge == DockEdge.Right;
        var orientation = System.Windows.Controls.Orientation.Horizontal;
        if (isVertical) orientation = System.Windows.Controls.Orientation.Vertical;

        if (applySizeConstraints)
        {
            var (availableWidth, availableHeight) = CalculateAvailableSize(settings.MonitorIndex, settings.MonitorDeviceName);
            _lastMetrics = ComputeStablePrimaryPanelMetrics(
                isVertical,
                availableWidth,
                availableHeight,
                settings,
                _settingsService.Elements);
        }

        if (isVertical) { this.MinWidth = 0; this.MinHeight = 150; }
        else { this.MinWidth = 150; this.MinHeight = 0; }

        System.Windows.Controls.DockPanel.SetDock(DragHandle, isVertical ? System.Windows.Controls.Dock.Top : System.Windows.Controls.Dock.Left);
        FixedPanel.Orientation = orientation;
        AppSettingsBlock.Orientation = orientation;
        UnifiedButtonsPanel.Orientation = isVertical
            ? System.Windows.Controls.Orientation.Vertical
            : System.Windows.Controls.Orientation.Horizontal;
        ControlBlock.Orientation = (isVertical && !_lastMetrics.UseMultiColumnControls) || (!isVertical && _lastMetrics.UseMultiColumnControls)
            ? System.Windows.Controls.Orientation.Vertical
            : System.Windows.Controls.Orientation.Horizontal;
        System.Windows.Controls.DockPanel.SetDock(FixedPanel, isVertical ? System.Windows.Controls.Dock.Top : System.Windows.Controls.Dock.Left);
        System.Windows.Controls.DockPanel.SetDock(UnifiedButtonsPanel, isVertical ? System.Windows.Controls.Dock.Top : System.Windows.Controls.Dock.Left);
        System.Windows.Controls.DockPanel.SetDock(AppSettingsBlock, isVertical ? System.Windows.Controls.Dock.Bottom : System.Windows.Controls.Dock.Right);
        FixedPanel.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
        UnifiedButtonsPanel.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
        AppSettingsBlock.VerticalAlignment = isVertical ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        FixedPanel.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Left;
        ControlBlock.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Left;
        BtnAdd.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Stretch;
        ContextIndicator.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Stretch;
        UnifiedButtonsPanel.HorizontalAlignment = isVertical && _lastMetrics.UserBands == 1 && _lastMetrics.UseMultiColumnControls
            ? System.Windows.HorizontalAlignment.Center
            : System.Windows.HorizontalAlignment.Left;
        AppSettingsBlock.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Right;

        if (isVertical)
        {
            DragHandle.Width = ButtonPitch;
            DragHandle.Height = 14;
            DragHandle.Margin = new Thickness(0, 0, 0, 4);
            DragHandleGrip.Width = 18;
            DragHandleGrip.Height = 4;
        }
        else
        {
            DragHandle.Width = 14;
            DragHandle.Height = ButtonPitch;
            DragHandle.Margin = new Thickness(0, 0, 4, 0);
            DragHandleGrip.Width = 4;
            DragHandleGrip.Height = 18;
        }

        var separators = new[] { SepSystem, SepAppSettings };
        foreach (var sep in separators)
        {
            if (isVertical) { sep.Width = 20; sep.Height = 1; sep.Margin = new Thickness(0, 4, 0, 4); }
            else { sep.Width = 1; sep.Height = 20; sep.Margin = new Thickness(4, 0, 4, 0); }
        }

        if (applySizeConstraints)
        {
            ApplyPanelSizeConstraints(_lastMetrics, isVertical);
        }

        ApplyPanelToolTipPlacement(settings.Edge);
        if (reposition)
        {
            PositionWindowImmediately(_shown, settings);
        }
    }

    public void RefreshPanel()
    {
        // Cancel any ongoing drag-and-drop operation first
        if (_isReordering)
        {
            if (_draggedButton != null && _draggedButton.IsMouseCaptured)
            {
                _draggedButton.ReleaseMouseCapture();
            }
            _draggedButton = null;
            _isReordering = false;
            _draggedOriginalIndex = -1;
        }

        int panelVersion = unchecked(++_panelRefreshVersion);

        _settingsService.NormalizeAppState();
        AppSettings settings = _settingsService.Settings;
        IReadOnlyList<CustomElement> elements = _settingsService.Elements;

        // Calculate a simple hash of current elements to detect changes
        int currentElementsVersion = 0;
        foreach (var element in elements)
        {
            currentElementsVersion = unchecked(currentElementsVersion * 397 + (element.Id?.GetHashCode() ?? 0));
        }
        
        if (currentElementsVersion != _lastElementsVersion)
        {
            _buttonImageCache.Clear();
            _lastElementsVersion = currentElementsVersion;
        }

        BuildPanelContextMenu(settings.ActiveContextId);
        string activeContextId = settings.ActiveContextId;

        UnifiedButtonsPanel.Children.Clear();
        _unifiedButtons.Clear();
        _overflowButton = null;

        List<UnifiedButton> allUnifiedButtons = _unifiedButtonService.BuildUnifiedList(activeContextId, settings, elements);

        bool isVertical = settings.Edge == DockEdge.Left || settings.Edge == DockEdge.Right;
        var (availableWidth, availableHeight) = CalculateAvailableSize(settings.MonitorIndex, settings.MonitorDeviceName);

        _lastMetrics = ComputeStablePrimaryPanelMetrics(
            isVertical,
            availableWidth,
            availableHeight,
            settings,
            elements);

        PanelOverflowHelper.OverflowPlan overflowPlan = PanelOverflowHelper.Calculate(
            _lastMetrics,
            allUnifiedButtons.Count);
        _currentUnifiedButtons = allUnifiedButtons
            .Take(overflowPlan.VisibleItemCount)
            .ToList();

        foreach (var item in _currentUnifiedButtons)
        {
            var btn = CreateUnifiedButton(item, panelVersion);
            UnifiedButtonsPanel.Children.Add(btn);
            _unifiedButtons.Add(btn);
        }

        if (overflowPlan.HasOverflow)
        {
            IReadOnlyList<UnifiedButton> hiddenButtons = allUnifiedButtons
                .Skip(overflowPlan.VisibleItemCount)
                .ToList();
            _overflowButton = CreateOverflowButton(hiddenButtons);
            UnifiedButtonsPanel.Children.Add(_overflowButton);
        }

        bool hasUnifiedButtons = UnifiedButtonsPanel.Children.Count > 0;

        // Разделители
        SepSystem.Visibility = hasUnifiedButtons ? Visibility.Visible : Visibility.Collapsed;
        SepAppSettings.Visibility = hasUnifiedButtons ? Visibility.Visible : Visibility.Collapsed;

        UpdateContextIndicator(settings);

        UpdateOrientation(settings, reposition: false, applySizeConstraints: false);
        ApplyPanelSizeConstraints(_lastMetrics, isVertical);
        AnimateContextTransitionIfNeeded(isVertical);
        ApplyPanelToolTipPlacement(settings.Edge);

        PositionWindowImmediately(_shown, settings);
    }

    private void UpdateContextIndicator(AppSettings settings)
    {
        int enabledCount = ContextStateHelper.CountEnabledContexts(settings.Contexts);
        int activeIndex = ContextStateHelper.FindEnabledContextIndex(settings.Contexts, settings.ActiveContextId);
        if (activeIndex < 0) activeIndex = 0;

        int displayNumber = ContextStateHelper.GetContextDisplayNumber(settings.Contexts, settings.ActiveContextId);
        ContextIndicatorText.Text = displayNumber.ToString();
        if (activeIndex < enabledCount)
        {
            PanelContext? activeContext = ContextStateHelper.GetEnabledContextAt(settings.Contexts, activeIndex);
            if (activeContext != null)
            {
                ContextIndicatorCircle.Background = GetCachedBrush(activeContext.Color);
                ContextIndicator.ToolTip = LocalizationService.Format(
                    "Main_ContextIndicatorTooltipFormat",
                    displayNumber,
                    activeContext.Name);
                string contextLabel = ContextIndicator.ToolTip?.ToString() ?? activeContext.Name;
                System.Windows.Automation.AutomationProperties.SetName(ContextIndicator, contextLabel);
                System.Windows.Automation.AutomationProperties.SetHelpText(ContextIndicator, contextLabel);
            }
        }
    }

    private void ContextIndicator_Click(object sender, RoutedEventArgs e)
    {
        ActivateContextRelative(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
    }

    private void ContextIndicator_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        ContextMenu? menu = RootBorder.ContextMenu;
        if (menu is null)
        {
            return;
        }

        menu.PlacementTarget = ContextIndicator;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void ApplyUnifiedButtonIcon(Button button, UnifiedButton item, int panelVersion)
    {
        if (item.Type == UnifiedButtonType.User && !string.IsNullOrWhiteSpace(item.ImagePath) && System.IO.File.Exists(item.ImagePath))
        {
            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(item.ImagePath);
            if (_buttonImageCache.TryGetValue(item.ImagePath, out var cached) &&
                cached.LastWriteUtc == lastWriteUtc)
            {
                button.Content = CreateButtonImage(cached.Source);
                return;
            }

            // Avoid flashing the fallback glyph while custom icons are being decoded.
            button.Content = null;
            _ = LoadUnifiedButtonImageAsync(button, item.Id, item.ImagePath, lastWriteUtc, item.Icon, item.IconFont, panelVersion);
            return;
        }

        button.Content = item.Icon;
        button.FontFamily = FontHelper.Resolve(item.IconFont);
    }

    private async Task LoadUnifiedButtonImageAsync(
        Button button,
        string elementId,
        string imagePath,
        DateTime lastWriteUtc,
        string fallbackIcon,
        string fallbackFont,
        int panelVersion)
    {
        try
        {
            byte[] imageBytes = await File.ReadAllBytesAsync(imagePath).ConfigureAwait(false);
            using var stream = new MemoryStream(imageBytes);
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            int pixelSize = (int)Math.Ceiling(24 * _cachedDpi);
            bitmap.DecodePixelWidth = pixelSize;
            bitmap.DecodePixelHeight = pixelSize;
            bitmap.EndInit();
            bitmap.Freeze();

            await Dispatcher.InvokeAsync(() =>
            {
                if (panelVersion != _panelRefreshVersion)
                {
                    return;
                }

                if (!string.Equals(button.Tag as string, elementId, StringComparison.Ordinal))
                {
                    return;
                }

                _buttonImageCache[imagePath] = new CachedButtonImage(bitmap, lastWriteUtc);
                button.Content = CreateButtonImage(bitmap);
            });
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            await Dispatcher.InvokeAsync(() =>
            {
                if (panelVersion != _panelRefreshVersion ||
                    !string.Equals(button.Tag as string, elementId, StringComparison.Ordinal))
                {
                    return;
                }

                button.Content = fallbackIcon;
                button.FontFamily = FontHelper.Resolve(fallbackFont);
            });
        }
    }

    private ContextMenu BuildUnifiedButtonContextMenu(UnifiedButton item)
    {
        if (item.Type == UnifiedButtonType.Utility)
        {
            return BuildSystemUtilityContextMenu(async () =>
            {
                await RunPanelInteractionAsync(async () =>
                {
                    _settingsService.SetUtilityVisibility(item.Id, false);
                    await SaveSettingsWithNotificationAsync();
                    RefreshPanel();
                });
            });
        }
        else
        {
            return BuildElementContextMenu(item.SourceElement!);
        }
    }

    private async Task ExecuteUnifiedButtonActionAsync(UnifiedButton item)
    {
        if (item.Type == UnifiedButtonType.Utility)
        {
            await RunPresetActionAsync(async () =>
            {
                switch (item.Id)
                {
                    case "Search":
                        string t = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";
                        if (!string.IsNullOrEmpty(t))
                            await _actionService.StartSearchAsync(t, HideDock);
                        break;
                    case "Screenshot":
                        await _actionService.StartScreenshotAsync(HideDock);
                        break;
                    case "Record":
                        await _actionService.StartRecordVideoAsync(HideDock);
                        break;
                    case "Calc":
                        await _actionService.StartCalculatorAsync(HideDock);
                        break;
                    case "Explorer":
                        await _actionService.StartExplorerAsync(HideDock);
                        break;
                    case "Downloads":
                        await _actionService.StartDownloadsAsync(HideDock);
                        break;
                    case "FileSorter":
                        await _actionService.LaunchUtilityAsync("FileSorter", HideDock);
                        break;
                    case "DiskCleaner":
                        await _actionService.LaunchUtilityAsync("DiskCleaner", HideDock);
                        break;
                    case "IconConverter":
                        await _actionService.LaunchUtilityAsync("IconConverter", HideDock);
                        break;
                    case "TimerStopwatch":
                        await _actionService.LaunchUtilityAsync("TimerStopwatch", HideDock);
                        break;
                    case "ColorPicker":
                        await _actionService.LaunchUtilityAsync("ColorPicker", HideDock);
                        break;
                    case "QuickNote":
                        await _actionService.LaunchUtilityAsync("QuickNote", HideDock);
                        break;
                    case "QRCodeGenerator":
                        await _actionService.LaunchUtilityAsync("QRCodeGenerator", HideDock);
                        break;
                    case "ClipboardManager":
                        await _actionService.LaunchUtilityAsync("ClipboardManager", HideDock);
                        break;
                    case "ShowDesktop":
                        await _actionService.StartShowDesktopAsync(HideDock);
                        break;
                    case "AppsFolder":
                        await _actionService.StartAppsFolderAsync(HideDock);
                        break;
                    case "Copilot":
                        await _actionService.StartCopilotAsync(HideDock);
                        break;
                    case "TextProcessing":
                        await _actionService.LaunchUtilityAsync("TextProcessing", HideDock);
                        break;
                    case "PromptBuilder":
                        await _actionService.LaunchUtilityAsync("PromptBuilder", HideDock);
                        break;
                    case "ZenEditor":
                        await _actionService.LaunchUtilityAsync("ZenEditor", HideDock);
                        break;
                    case "AiteProfiles":
                        await _actionService.LaunchUtilityAsync("AiteProfiles", HideDock);
                        break;
                }
            });
        }
        else
        {
            await ExecuteUserButtonActionAsync(item.SourceElement!);
        }
    }

    private async Task ExecuteUserButtonActionAsync(CustomElement element)
    {
        var result = await _actionService.ExecuteCustomActionAsync(element, HideDock);
        if (!result.Success)
        {
            new DarkDialog(LocalizationService.Format("Action_Failed", result.ErrorMessage)) { Owner = this }.ShowDialog();
        }
    }

    private Button CreateOverflowButton(IReadOnlyList<UnifiedButton> hiddenButtons)
    {
        string label = LocalizationService.Format("Main_MoreButtonsFormat", hiddenButtons.Count);
        return CreatePanelButton("\uE712", label, (sender, e) =>
        {
            if (sender is not Button button)
            {
                return;
            }

            ContextMenu menu = AppContextMenuFactory.CreateMenu(this);
            menu.Opened += (s, args) => _isElementContextMenuOpen = true;
            menu.Closed += (s, args) => _isElementContextMenuOpen = false;
            foreach (UnifiedButton item in hiddenButtons)
            {
                menu.Items.Add(AppContextMenuFactory.CreateItem(
                    this,
                    item.Icon,
                    item.Name,
                    async (s, args) => await ExecuteUnifiedButtonActionAsync(item),
                    iconFont: FontHelper.Resolve(item.IconFont)));
            }

            button.ContextMenu = menu;
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        });
    }

    private void ApplyPanelToolTipPlacement()
    {
        ApplyPanelToolTipPlacement(AppSettings.Edge);
    }

    private void ApplyPanelToolTipPlacement(DockEdge edge)
    {
        var placement = GetPanelToolTipPlacement(edge);
        const double tooltipGap = 4d;
        const double tooltipVerticalCenterOffset = 4d;

        var horizontalOffset = placement switch
        {
            PlacementMode.Right => tooltipGap,
            PlacementMode.Left => -tooltipGap,
            _ => 0
        };

        var verticalOffset = placement switch
        {
            PlacementMode.Bottom => tooltipGap,
            PlacementMode.Top => -tooltipGap,
            PlacementMode.Left or PlacementMode.Right => tooltipVerticalCenterOffset,
            _ => 0
        };

        foreach (var button in EnumeratePanelButtons())
        {
            ToolTipService.SetPlacement(button, placement);
            ToolTipService.SetHorizontalOffset(button, horizontalOffset);
            ToolTipService.SetVerticalOffset(button, verticalOffset);
        }

        ToolTipService.SetPlacement(ContextIndicator, placement);
        ToolTipService.SetHorizontalOffset(ContextIndicator, horizontalOffset);
        ToolTipService.SetVerticalOffset(ContextIndicator, verticalOffset);
    }

    private IEnumerable<Button> EnumeratePanelButtons()
    {
        yield return BtnAdd;

        foreach (var button in _unifiedButtons)
        {
            yield return button;
        }

        if (_overflowButton is not null)
        {
            yield return _overflowButton;
        }

        yield return BtnAppSettings;
    }

    private static PlacementMode GetPanelToolTipPlacement(DockEdge edge) => edge switch
    {
        DockEdge.Bottom => PlacementMode.Top,
        DockEdge.Left => PlacementMode.Right,
        DockEdge.Right => PlacementMode.Left,
        _ => PlacementMode.Bottom
    };

    private static System.Windows.Controls.Image CreateButtonImage(System.Windows.Media.Imaging.BitmapSource source)
    {
        var image = new System.Windows.Controls.Image
        {
            Source = source,
            Width = 24,
            Height = 24,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }

    private sealed record CachedButtonImage(System.Windows.Media.Imaging.BitmapSource Source, DateTime LastWriteUtc);



    private void AnimateContextTransitionIfNeeded(bool isVertical)
    {
        if (_pendingContextAnimationDirection == 0 || UnifiedButtonsPanel.Children.Count == 0)
        {
            _pendingContextAnimationDirection = 0;
            return;
        }

        int direction = _pendingContextAnimationDirection;
        _pendingContextAnimationDirection = 0;
        if (UnifiedButtonsPanel.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            UnifiedButtonsPanel.RenderTransform = transform;
        }

        double initialOffset = direction * 8d;
        if (isVertical)
        {
            transform.X = 0;
            transform.Y = initialOffset;
        }
        else
        {
            transform.X = initialOffset;
            transform.Y = 0;
        }

        UnifiedButtonsPanel.Opacity = 0.55;

        var fadeAnimation = new DoubleAnimation(1, TimeSpan.FromMilliseconds(Constants.AnimationFadeMs))
        {
            EasingFunction = EasingHelper.DefaultEasing
        };

        var slideAnimation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(Constants.AnimationFadeMs))
        {
            EasingFunction = EasingHelper.DefaultEasing
        };

        UnifiedButtonsPanel.BeginAnimation(OpacityProperty, fadeAnimation);
        transform.BeginAnimation(isVertical ? TranslateTransform.YProperty : TranslateTransform.XProperty, slideAnimation);
    }

    public async Task<IReadOnlyList<string>> SaveElement(CustomElement updated, string? removeId = null)
    {
        await _settingsService.SaveElementAsync(updated, removeId);
        RefreshPanel();
        
        // Если это веб-элемент и иконка не задана — пытаемся скачать favicon
        if (updated.ActionType == nameof(ActionType.Web) &&
            string.IsNullOrEmpty(updated.ImagePath) &&
            (string.IsNullOrEmpty(updated.Icon) || updated.Icon == "\uF45B"))
        {
            string elementId = updated.Id;
            string elementActionValue = updated.ActionValue;
            double currentDpi = _cachedDpi;
            _ = Task.Run(async () =>
            {
                try
                {
                    string? webIcon = await IconHelper.DownloadFaviconAsync(elementActionValue, currentDpi);
                    if (!string.IsNullOrEmpty(webIcon))
                    {
                        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                        {
                            return;
                        }

                        await Dispatcher.InvokeAsync(() =>
                        {
                            _ = UpdateDownloadedFaviconAsync(elementId, elementActionValue, webIcon);
                        });
                    }
                }
                catch (Exception ex) { Logger.Log(ex); }
            });
        }
        
        return RegisterGlobalHotkey();
    }

    public IReadOnlyList<CustomElement> GetElementsSnapshot() => _settingsService.Elements.Select(_settingsService.CloneElement).ToList();

    private void ShowDock(bool fromKeyboard = false, bool activateWindow = true)
    {
        if (_shown || _isAnimating)
        {
            return;
        }

        SetPanelInputMode(PanelInputMode.Pointer, clearFocus: true);
        _activateWindowOnShow = activateWindow;
        _shown = true;
        _activationDwellTracker.Reset();
        Toggle(false);
    }



    public void ToggleDock(bool fromKeyboard = false)
    {
        if (_isAnimating)
        {
            return;
        }

        if (_shown)
        {
            _ = HideDock();
            return;
        }

        ShowDock(fromKeyboard);
        if (fromKeyboard)
        {
            EnablePanelKeyboardMode(focusButtons: false);
        }
    }

    private void Toggle(bool hide, bool fromCurrentPosition = false)
    {
        _isAnimating = true; _timer.Stop();

        if (!hide)
        {
            this.Topmost = false;
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE);
            this.Topmost = true;
        }

        var start = fromCurrentPosition ? (X: Left, Y: Top) : GetDockCoordinates(hide: !hide);
        var finish = GetDockCoordinates(hide: hide);
        this.Left = start.X;
        this.Top = start.Y;

        double finalX = finish.X;
        double finalY = finish.Y;

        var duration = TimeSpan.FromMilliseconds(hide ? Constants.PanelHideAnimationMs : Constants.PanelShowAnimationMs);
        var easing = EasingHelper.ForToggle(hide);
        var animX = new DoubleAnimation(finalX, duration) { EasingFunction = easing };
        var animY = new DoubleAnimation(finalY, duration) { EasingFunction = easing };

        int completedCount = 0;
        void onCompleted(object? s, EventArgs ev)
        {
            if (Interlocked.Increment(ref completedCount) == 2)
            {
                this.BeginAnimation(LeftProperty, null);
                this.BeginAnimation(TopProperty, null);
                this.Left = finalX;
                this.Top = finalY;
                _isAnimating = false;
                UpdatePanelBounds();
                if (!hide)
                {
                    if (_activateWindowOnShow)
                    {
                        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                        ForceForegroundWindow(hwnd);
                        Activate();
                    }

                    if (IsPanelKeyboardMode)
                    {
                        if (_focusPanelButtonsOnShow)
                        {
                            FocusPanelForKeyboard();
                        }
                        else
                        {
                            Focus();
                        }
                    }
                }

                UpdateHoverActivationTimer();
            }
        }

        animX.Completed += onCompleted;
        animY.Completed += onCompleted;

        this.BeginAnimation(LeftProperty, animX);
        this.BeginAnimation(TopProperty, animY);
    }

    private async Task HideDock()
    {
        if (!_shown)
        {
            return;
        }

        if (_isAnimating)
        {
            StopPanelAnimationAtCurrentPosition();
        }

        _shown = false;
        SetPanelInputMode(PanelInputMode.Pointer, clearFocus: true);
        _activationDwellTracker.Reset();
        Toggle(true, fromCurrentPosition: true);
        await Task.Delay(Constants.PanelHideAnimationMs);
    }

    private void StopPanelAnimationAtCurrentPosition()
    {
        double currentLeft = Left;
        double currentTop = Top;
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        Left = currentLeft;
        Top = currentTop;
        _isAnimating = false;
        UpdateHoverActivationTimer();
    }

    private async Task RunPresetActionAsync(Func<Task> action)
    {
        try
        {
            unchecked { _panelFocusRequestVersion++; }
            await action();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Action_Failed", ex.Message)) { Owner = this }.ShowDialog();
        }
    }

    private async void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        await RunPresetActionAsync(async () =>
        {
            string t = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";
            if (string.IsNullOrEmpty(t)) return;
            await _actionService.StartSearchAsync(t, HideDock);
        });
    }
    private async void BtnScreenshotRegion_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.StartScreenshotAsync(HideDock)); }
    private async void BtnRecordVideo_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.StartRecordVideoAsync(HideDock)); }
    private async void BtnCalc_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.StartCalculatorAsync(HideDock)); }
    private async void BtnExplorer_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.StartExplorerAsync(HideDock)); }
    private async void BtnDownloads_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.StartDownloadsAsync(HideDock)); }
    private async void BtnFileSorter_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("FileSorter", HideDock)); }
    private async void BtnIconConverter_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("IconConverter", HideDock)); }
    private async void BtnTimerStopwatch_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("TimerStopwatch", HideDock)); }
    private async void BtnColorPicker_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("ColorPicker", HideDock)); }
    private async void BtnQuickNote_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("QuickNote", HideDock)); }
    private async Task OpenAddButtonWindowAsync()
    {
        try
        {
            await HideDock();
            new SettingsWindow(this).ShowDialog();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private async void BtnAdd_Click(object sender, RoutedEventArgs e) { await OpenAddButtonWindowAsync(); }
    public async Task ShowAppSettingsWindow(AppSettingsSection section = AppSettingsSection.General)
    {
        if (_appSettingsWindow != null)
        {
            if (_appSettingsWindow.WindowState == WindowState.Minimized)
            {
                _appSettingsWindow.WindowState = WindowState.Maximized;
            }

            _appSettingsWindow.NavigateToSection(section);
            _appSettingsWindow.Show();
            _appSettingsWindow.Activate();
            return;
        }

        if (_isOpeningAppSettingsWindow)
        {
            return;
        }

        _isOpeningAppSettingsWindow = true;
        try
        {
            await HideDock();
            var settingsWindow = new AppSettingsWindow(this, section);
            _appSettingsWindow = settingsWindow;
            settingsWindow.Closed += (_, _) => _appSettingsWindow = null;
            settingsWindow.Show();
            settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
        finally
        {
            _isOpeningAppSettingsWindow = false;
        }
    }

    private async void BtnAppSettings_Click(object sender, RoutedEventArgs e) => await ShowAppSettingsWindow();

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject parent = VisualTreeHelper.GetParent(child);
        if (parent == null) return null;
        return parent is T p ? p : FindParent<T>(parent);
    }


    protected override void OnClosed(EventArgs e)
    {
        try
        {
            UnsubscribeFromPowerEvents();
            _startupCts.Cancel();
            try
            {
                _nativeService?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            try
            {
                _positionIndicatorService?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            try
            {
                ClipboardHistoryService.Instance?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            _settingsService.SettingsChanged -= OnSettingsChanged;
            if (_isLocalizationSubscribed)
            {
                LocalizationService.CultureChanged -= HandleCultureChanged;
                _isLocalizationSubscribed = false;
            }

            UnregisterGlobalHotkey();
        }
        finally
        {
            base.OnClosed(e);
        }
    }
}


