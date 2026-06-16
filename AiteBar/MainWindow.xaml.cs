using System.Runtime.Versioning;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class MainWindow : Window, ISettingsWindowContext
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private DateTime? _hoverStartTime;
    private bool _shown = false, _isAnimating = false;
    private double _panelLeft, _panelTop, _panelRight, _panelBottom, _cachedDpi = 1.0;
    private static readonly BrushConverter _brushConverter = new();
    private static FontFamily? _menuIconFont;
    private static FontFamily MenuIconFont => _menuIconFont ??= FontHelper.Resolve(FontHelper.FluentKey);

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
    }

    private readonly AppSettingsService _settingsService;
    private readonly ActionService _actionService;
    private readonly HotkeyService _hotkeyService = new();
    private readonly PanelPackageService _panelPackageService;
    private NativeIntegrationService? _nativeService;

    private AppSettings AppSettings => _settingsService.Settings;
    private IReadOnlyList<CustomElement> Elements => _settingsService.Elements;

    private System.Windows.Forms.NotifyIcon _notifyIcon = null!;

    private const string DonatePageUrl = "https://suvorov.pp.ua/donate/";
    private const double TopPanelVisibleOffset = 12;
    private bool _isPanelDragging = false;
    private bool _panelDragChanged = false;
    private DockEdge _dragStartEdge;
    private int _dragStartMonitorIndex;
    private bool _isElementContextMenuOpen;
    private bool _isBlockingPanelInteraction;
    private PanelInputMode _panelInputMode = PanelInputMode.Pointer;
    private readonly List<Button> _unifiedButtons = [];
    private List<UnifiedButton> _currentUnifiedButtons = [];
    private int _pendingContextAnimationDirection;
    private readonly UnifiedButtonService _unifiedButtonService;
    private bool _startupInfrastructureInitialized;
    private bool _deferredStartupCompleted;
    private readonly bool _settingsPreloaded;
    private int _panelRefreshVersion;
    private int _panelFocusRequestVersion;
    private int _contextWheelDelta;
    private readonly CancellationTokenSource _startupCts = new();
    private DateTime _lastContextWheelSwitchUtc = DateTime.MinValue;
    private readonly Dictionary<string, CachedButtonImage> _buttonImageCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Brush> _brushCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _isLocalizationSubscribed;

    private const double PanelScreenPadding = 20;
    private const double ButtonPitch = PanelLayoutHelper.ButtonOuterSize;
    private const double DragHandleSpan = 18;
    private const int WheelDeltaPerContextSwitch = 120;
    private static readonly TimeSpan ContextWheelSwitchCooldown = TimeSpan.FromMilliseconds(220);

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
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        UnregisterGlobalHotkey();
        RegisterGlobalHotkey();
    }

    public AppSettings GetAppSettings() => _settingsService.Settings;
    public AppSettingsService GetSettingsService() => _settingsService;
    public ActionService GetActionService() => _actionService;

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
            Dispatcher.Invoke(() => HandleCultureChanged(sender, e));
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
        BuildPanelContextMenu();
    }

    private ContextMenu BuildSystemUtilityContextMenu(Action detachAction)
    {
        ContextMenu menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
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
    {
        Brush accentBrush = isDanger
            ? new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FF5252"))
            : isActive
                ? (Brush)FindResource("AccentColor")
                : new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#E3E3E3"));

        System.Windows.Controls.TextBlock icon = new System.Windows.Controls.TextBlock
        {
            Text = glyph,
            FontFamily = MenuIconFont,
            Foreground = accentBrush,
            Style = (Style)FindResource("ContextMenuIconTextStyle")
        };

        MenuItem item = new MenuItem
        {
            Header = text,
            Style = (Style)FindResource("DarkMenuItem"),
            Padding = new Thickness(0),
            Icon = icon
        };

        if (isDanger)
        {
            item.Foreground = accentBrush;
        }

        if (onClick != null)
        {
            item.Click += onClick;
        }

        return item;
    }

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
                new DarkDialog(LocalizationService.Format("Settings_SaveFailed", ex.Message)).ShowDialog();
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
        if (string.IsNullOrWhiteSpace(contextId) || string.Equals(AppSettings.ActiveContextId, contextId, StringComparison.Ordinal))
        {
            return (false, 0);
        }

        IReadOnlyList<PanelContext> enabledContexts = ContextStateHelper.GetEnabledContexts(AppSettings.Contexts);
        int targetIndex = enabledContexts.ToList().FindIndex(context => string.Equals(context.Id, contextId, StringComparison.Ordinal));
        if (targetIndex < 0)
        {
            return (false, 0);
        }

        int currentIndex = enabledContexts.ToList().FindIndex(context => string.Equals(context.Id, AppSettings.ActiveContextId, StringComparison.Ordinal));
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        // Get settings from service, modify, update service!
        var settings = _settingsService.Settings;
        settings.ActiveContextId = contextId;
        _settingsService.Settings = settings;
        
        int animationDirection = targetIndex >= currentIndex ? 1 : -1;
        _pendingContextAnimationDirection = animationDirection;
        RefreshPanel();
        return (true, animationDirection);
    }

    private string? GetNextContextId(int direction)
    {
        IReadOnlyList<PanelContext> enabledContexts = ContextStateHelper.GetEnabledContexts(AppSettings.Contexts);
        if (enabledContexts.Count == 0)
        {
            return null;
        }

        int currentIndex = enabledContexts.ToList().FindIndex(context => string.Equals(context.Id, AppSettings.ActiveContextId, StringComparison.Ordinal));
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        int nextIndex = ContextStateHelper.WrapIndex(currentIndex + direction, enabledContexts.Count);
        return enabledContexts[nextIndex].Id;
    }

    private string? GetContextIdByIndex(int index)
    {
        IReadOnlyList<PanelContext> enabledContexts = ContextStateHelper.GetEnabledContexts(AppSettings.Contexts);
        if (index < 0 || index >= enabledContexts.Count)
        {
            return null;
        }

        return enabledContexts[index].Id;
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

    private async void ActivateContextByIndex(int index)
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

    private void BuildPanelContextMenu()
    {
        ContextMenu menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
        menu.Opened += (s, e) => _isElementContextMenuOpen = true;
        menu.Closed += (s, e) => _isElementContextMenuOpen = false;

        MenuItem panelsMenu = CreateMenuItem(FluentGlyph(MenuIcons.Panels), LocalizationService.Get("Menu_Panels"));

        foreach (PanelContext context in GetContextsSnapshot())
        {
            bool isActive = string.Equals(context.Id, AppSettings.ActiveContextId, StringComparison.Ordinal);
            string targetContextId = context.Id;

            MenuItem item = CreateMenuItem(
                glyph: string.IsNullOrEmpty(context.IconGlyph) ? "\uE8B7" : context.IconGlyph,
                text: context.Name,
                onClick: (s, e) => ActivateContextById(targetContextId),
                isActive: isActive
            );

            panelsMenu.Items.Add(item);
        }

        menu.Items.Add(panelsMenu);
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
        ContextMenu menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
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
            .Select(context => CreateMenuItem(FluentGlyph(MenuIcons.Move), context.Name, async (s, e) =>
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

    private static async Task OpenElementLocationAsync(CustomElement element)
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
                        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = true });
                    }
                    break;

                case ActionType.Program:
                case ActionType.File:
                case ActionType.ScriptFile:
                    if (File.Exists(target))
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{target}\"") { UseShellExecute = true });
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            await Task.Yield();
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
        var result = TryActivateContext(contextId);
        if (result.changed)
        {
            await SaveSettingsWithNotificationAsync();
        }
    }

    private Screen? GetTargetScreen()
    {
        Screen[] screens = Screen.AllScreens;
        return (AppSettings.MonitorIndex >= 0 && AppSettings.MonitorIndex < screens.Length)
            ? screens[AppSettings.MonitorIndex]
            : Screen.PrimaryScreen;
    }

    private (double AvailableWidth, double AvailableHeight) CalculateAvailableSize()
    {
        Screen? screen = GetTargetScreen();
        Rectangle? workArea = screen?.WorkingArea;
        double availableWidth = workArea.HasValue
            ? Math.Max(150, (workArea.Value.Width / _cachedDpi) - PanelScreenPadding)
            : 150;
        double availableHeight = workArea.HasValue
            ? Math.Max(150, (workArea.Value.Height / _cachedDpi) - PanelScreenPadding)
            : 150;

        return (availableWidth, availableHeight);
    }

    private PanelLayoutHelper.PanelLayoutMetrics ComputePanelMetrics(
        bool isVertical,
        double availableWidth,
        double availableHeight)
    {
        int totalButtonCount = _unifiedButtons.Count;

        return PanelLayoutHelper.Calculate(
            isVertical: isVertical,
            availablePrimary: isVertical ? availableHeight : availableWidth,
            panelPercent: AppSettings.PanelSizePercent,
            totalButtonCount: totalButtonCount,
            controlButtonCount: 1,
            trailingControlButtonCount: 1);
    }

    private void ApplyPanelSizeConstraints(PanelLayoutHelper.PanelLayoutMetrics metrics)
    {
        bool isVertical = AppSettings.Edge == DockEdge.Left || AppSettings.Edge == DockEdge.Right;

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

        // Apply layout rounding to avoid sub‑pixel values (prevents flicker & phantom scroll)
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
        FixedPanel.Height = Math.Round(metrics.FixedHeight);
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
        int count = 0;
        if (AppSettings.ShowPresetSearch) count++;
        if (AppSettings.ShowPresetScreenshot) count++;
        if (AppSettings.ShowPresetVideo) count++;
        if (AppSettings.ShowPresetCalc) count++;
        if (AppSettings.ShowPresetExplorer) count++;
        if (AppSettings.ShowPresetDownloads) count++;
        if (AppSettings.ShowPresetFileSorter) count++;
        if (AppSettings.ShowPresetIconConverter) count++;
        if (AppSettings.ShowPresetTimerStopwatch) count++;
        if (AppSettings.ShowPresetColorPicker) count++;
        if (AppSettings.ShowPresetQuickNote) count++;
        return count;
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
            case HotkeyCommand.QuickNote:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("QuickNote", HideDock));
                break;
            case HotkeyCommand.ColorPicker:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("ColorPicker", HideDock));
                break;
            case HotkeyCommand.TimerStopwatch:
                _ = RunPresetActionAsync(() => _actionService.LaunchUtilityAsync("TimerStopwatch", HideDock));
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
    }

    private Button CreatePanelButton(string content, string tooltip, RoutedEventHandler onClick, Brush? foreground = null)
    {
        var btn = new Button
        {
            Content = content,
            ToolTip = tooltip,
            Style = (Style)FindResource("PanelButtonStyle"),
            Focusable = true,
            FocusVisualStyle = _panelInputMode == PanelInputMode.Keyboard
                ? (Style)FindResource("ButtonFocusVisual")
                : null
        };

        if (foreground != null)
        {
            btn.Foreground = foreground;
        }

        btn.Click += onClick;
        return btn;
    }

    private bool IsPanelInteractionActive => _isElementContextMenuOpen || _isBlockingPanelInteraction || _isPanelDragging;

    private void BeginBlockingPanelInteraction()
    {
        _isBlockingPanelInteraction = true;
        _hoverStartTime = null;
    }

    private void EndBlockingPanelInteraction()
    {
        _isBlockingPanelInteraction = false;
        _hoverStartTime = null;
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
        var screen = GetTargetScreen();

        // Если экран не найден, используем PrimaryScreen. Если и его нет, используем системные параметры.
        var primary = Screen.PrimaryScreen;
        var drawingWorkArea = screen?.WorkingArea ?? primary?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
        var drawingBounds = screen?.Bounds ?? primary?.Bounds ?? new System.Drawing.Rectangle(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);

        // Если мы упали в fallback через SystemParameters, то значения уже в DIP-ах, и делить на DPI не нужно.
        // Если же мы взяли значения из Screen (System.Drawing), то они в пикселях и требуют деления.
        bool isFromSystemParameters = (screen == null && primary == null);
        double dpi = (isFromSystemParameters || _cachedDpi <= 0) ? 1.0 : _cachedDpi;

        return (
            new Rect(drawingWorkArea.Left / dpi, drawingWorkArea.Top / dpi, drawingWorkArea.Width / dpi, drawingWorkArea.Height / dpi),
            new Rect(drawingBounds.Left / dpi, drawingBounds.Top / dpi, drawingBounds.Width / dpi, drawingBounds.Height / dpi)
        );
    }

    private (double X, double Y) GetDockCoordinates(bool hide)
    {
        var metrics = GetTargetScreenMetrics();
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
            AppSettings.Edge,
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
        if (_isPositioning) return;
        _isPositioning = true;
        try
        {
            this.UpdateLayout();
            var coordinates = GetDockCoordinates(hide: !shown);
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
            UpdateAllButtonsFocusVisualStyle();
            PositionWindowImmediately(_shown);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            if (_settingsPreloaded)
            {
                _deferredStartupCompleted = true;
                RegisterGlobalHotkey();
                return;
            }
            _ = CompleteDeferredStartupAsync().ContinueWith(
                task => _ = Logger.LogAsync(task.Exception!.GetBaseException()),
                TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex) { Logger.Log(ex); }
    }

    private void EnsureStartupInfrastructure()
    {
        if (_startupInfrastructureInitialized)
        {
            return;
        }

        IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        _nativeService = new NativeIntegrationService(hwnd);
        _nativeService.MouseDownOutside += (x, y) =>
        {
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
            if (_isAnimating || _isPanelDragging) return;
            NativeMethods.Win32Point pt = new();
            if (NativeMethods.GetCursorPos(ref pt))
            {
                var screens = Screen.AllScreens;
                var screen = (AppSettings.MonitorIndex >= 0 && AppSettings.MonitorIndex < screens.Length)
                    ? screens[AppSettings.MonitorIndex]
                    : Screen.PrimaryScreen;

                if (screen == null) return;

                var bounds = screen.Bounds;
                double screenLeft = bounds.Left;
                double screenTop = bounds.Top;
                double screenWidth = bounds.Width;
                double screenHeight = bounds.Height;

                int delayMs = AppSettings.ActivationDelayMs;
                bool inActivationZone = ActivationZoneHelper.IsInActivationZone(
                    AppSettings.Edge,
                    screenLeft,
                    screenTop,
                    screenWidth,
                    screenHeight,
                    AppSettings.ActivationZoneSizePercent,
                    pt.X,
                    pt.Y);

                if (inActivationZone && !_shown)
                {
                    if (_hoverStartTime == null) _hoverStartTime = DateTime.Now;
                    else if ((DateTime.Now - _hoverStartTime.Value).TotalMilliseconds >= delayMs)
                    {
                        ShowDock();
                    }
                }
                else _hoverStartTime = null;
            }
        };

        _timer.Start();
        _nativeService.InstallMouseHook();
        _startupInfrastructureInitialized = true;
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
            _deferredStartupCompleted = true;
            ApplyLocalizedText();
            RegisterGlobalHotkey();
            RefreshPanel();
            PositionWindowImmediately(_shown);
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

    private void UpdateOrientation(bool reposition = true, bool applySizeConstraints = true)
    {
        bool isVertical = AppSettings.Edge == DockEdge.Left || AppSettings.Edge == DockEdge.Right;
        var orientation = System.Windows.Controls.Orientation.Horizontal;
        if (isVertical) orientation = System.Windows.Controls.Orientation.Vertical;

        if (isVertical) { this.MinWidth = 0; this.MinHeight = 150; }
        else { this.MinWidth = 150; this.MinHeight = 0; }

        System.Windows.Controls.DockPanel.SetDock(DragHandle, isVertical ? System.Windows.Controls.Dock.Top : System.Windows.Controls.Dock.Left);
        FixedPanel.Orientation = orientation;
        AppSettingsBlock.Orientation = orientation;
        UnifiedButtonsPanel.Orientation = isVertical
            ? System.Windows.Controls.Orientation.Vertical
            : System.Windows.Controls.Orientation.Horizontal;
        ControlBlock.Orientation = orientation;
        System.Windows.Controls.DockPanel.SetDock(FixedPanel, isVertical ? System.Windows.Controls.Dock.Top : System.Windows.Controls.Dock.Left);
        System.Windows.Controls.DockPanel.SetDock(UnifiedButtonsPanel, isVertical ? System.Windows.Controls.Dock.Top : System.Windows.Controls.Dock.Left);
        System.Windows.Controls.DockPanel.SetDock(AppSettingsBlock, isVertical ? System.Windows.Controls.Dock.Bottom : System.Windows.Controls.Dock.Right);
        FixedPanel.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
        UnifiedButtonsPanel.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
        AppSettingsBlock.VerticalAlignment = isVertical ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        FixedPanel.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Left;
        ControlBlock.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Stretch : System.Windows.HorizontalAlignment.Left;
        BtnAdd.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Stretch;
        UnifiedButtonsPanel.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Left;
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
            var (availableWidth, availableHeight) = CalculateAvailableSize();
            var metrics = ComputePanelMetrics(isVertical, availableWidth, availableHeight);
            ApplyPanelSizeConstraints(metrics);
        }

        ApplyPanelToolTipPlacement();
        if (reposition)
        {
            PositionWindowImmediately(_shown);
        }
    }

    public void RefreshPanel()
    {
        int panelVersion = unchecked(++_panelRefreshVersion);
        _buttonImageCache.Clear();
        _settingsService.NormalizeAppState();
        BuildPanelContextMenu();
        string activeContextId = AppSettings.ActiveContextId;

        UpdateOrientation(reposition: false, applySizeConstraints: false);
        UnifiedButtonsPanel.Children.Clear();
        _unifiedButtons.Clear();

        _currentUnifiedButtons = _unifiedButtonService.BuildUnifiedList(activeContextId);

        foreach (var item in _currentUnifiedButtons)
        {
            var btn = CreateUnifiedButton(item, panelVersion);
            UnifiedButtonsPanel.Children.Add(btn);
            _unifiedButtons.Add(btn);
        }

        bool isVertical = AppSettings.Edge == DockEdge.Left || AppSettings.Edge == DockEdge.Right;
        var (availableWidth, availableHeight) = CalculateAvailableSize();
        var metrics = ComputePanelMetrics(isVertical, availableWidth, availableHeight);
        bool hasUnifiedButtons = UnifiedButtonsPanel.Children.Count > 0;

        // Разделители
        SepSystem.Visibility = hasUnifiedButtons ? Visibility.Visible : Visibility.Collapsed;
        SepAppSettings.Visibility = hasUnifiedButtons ? Visibility.Visible : Visibility.Collapsed;

        ApplyPanelSizeConstraints(metrics);
        AnimateContextTransitionIfNeeded();
        ApplyPanelToolTipPlacement();

        PositionWindowImmediately(_shown);
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
                    if (item.SettingsKey != null)
                    {
                        // 1. Get the full settings from the service (which returns a clone
                        var settings = _settingsService.Settings;
                        
                        switch (item.SettingsKey)
                        {
                            case "ShowPresetSearch": settings.ShowPresetSearch = false; break;
                            case "ShowPresetScreenshot": settings.ShowPresetScreenshot = false; break;
                            case "ShowPresetVideo": settings.ShowPresetVideo = false; break;
                            case "ShowPresetCalc": settings.ShowPresetCalc = false; break;
                            case "ShowPresetExplorer": settings.ShowPresetExplorer = false; break;
                            case "ShowPresetDownloads": settings.ShowPresetDownloads = false; break;
                            case "ShowPresetFileSorter": settings.ShowPresetFileSorter = false; break;
                            case "ShowPresetIconConverter": settings.ShowPresetIconConverter = false; break;
                            case "ShowPresetTimerStopwatch": settings.ShowPresetTimerStopwatch = false; break;
                            case "ShowPresetColorPicker": settings.ShowPresetColorPicker = false; break;
                            case "ShowPresetQuickNote": settings.ShowPresetQuickNote = false; break;
                        }

                        // 2. Update the service with the modified settings in the service!
                        _settingsService.Settings = settings;
                        
                        await SaveSettingsWithNotificationAsync();
                        RefreshPanel();
                    }
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

    private void ApplyPanelToolTipPlacement()
    {
        var placement = GetPanelToolTipPlacement(AppSettings.Edge);
        var horizontalOffset = AppSettings.Edge switch
        {
            DockEdge.Left => 8,
            DockEdge.Right => -8,
            _ => 0
        };
        var verticalOffset = AppSettings.Edge switch
        {
            DockEdge.Top => 8,
            DockEdge.Bottom => -8,
            _ => 0
        };

        foreach (var button in EnumeratePanelButtons())
        {
            ToolTipService.SetPlacement(button, placement);
            ToolTipService.SetHorizontalOffset(button, horizontalOffset);
            ToolTipService.SetVerticalOffset(button, verticalOffset);
        }
    }

    private IEnumerable<Button> EnumeratePanelButtons()
    {
        yield return BtnAdd;

        foreach (var button in _unifiedButtons)
        {
            yield return button;
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

    private static System.Windows.Controls.Image CreateButtonImage(System.Windows.Media.Imaging.BitmapSource source) => new()
    {
        Source = source,
        Width = 24,
        Height = 24,
        Stretch = Stretch.Uniform
    };

    private sealed record CachedButtonImage(System.Windows.Media.Imaging.BitmapSource Source, DateTime LastWriteUtc);



    private void AnimateContextTransitionIfNeeded()
    {
        if (_pendingContextAnimationDirection == 0 || UnifiedButtonsPanel.Children.Count == 0)
        {
            _pendingContextAnimationDirection = 0;
            return;
        }

        int direction = _pendingContextAnimationDirection;
        _pendingContextAnimationDirection = 0;
        bool isVertical = AppSettings.Edge == DockEdge.Left || AppSettings.Edge == DockEdge.Right;

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
        return RegisterGlobalHotkey();
    }

    public IReadOnlyList<CustomElement> GetElementsSnapshot() => _settingsService.Elements.Select(_settingsService.CloneElement).ToList();

    private void ShowDock(bool fromKeyboard = false)
    {
        if (_shown || _isAnimating)
        {
            return;
        }

        SetPanelInputMode(PanelInputMode.Pointer, clearFocus: true);
        _shown = true;
        _hoverStartTime = null;
        Toggle(false);
    }



    private void ToggleDock(bool fromKeyboard = false)
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
            EnablePanelKeyboardMode();
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
                _timer.Start();
                UpdatePanelBounds();
                if (!hide)
                {
                    // Активируем окно при любом открытии, чтобы обрабатывать клавиши
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    ForceForegroundWindow(hwnd);
                    Activate();

                    if (IsPanelKeyboardMode)
                    {
                        FocusPanelForKeyboard();
                    }
                }
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
        _hoverStartTime = null;
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
        _timer.Start();
    }

    private int _mouseWheelCaptureToken = 0;

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
        await HideDock();
        new SettingsWindow(this).ShowDialog();
    }

    private async void BtnAdd_Click(object sender, RoutedEventArgs e) { await OpenAddButtonWindowAsync(); }
    private async void BtnAppSettings_Click(object sender, RoutedEventArgs e) { await HideDock(); new AppSettingsWindow(this).ShowDialog(); }

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
                _notifyIcon?.Dispose();
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


