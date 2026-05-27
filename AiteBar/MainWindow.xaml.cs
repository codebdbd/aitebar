using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Drawing;
using System.Windows.Forms;

// Псевдонимы для устранения неоднозначности (WPF vs WinForms)
using Button = System.Windows.Controls.Button;
using Panel = System.Windows.Controls.Panel;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Separator = System.Windows.Controls.Separator;
using ToolTipService = System.Windows.Controls.ToolTipService;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using Point = System.Windows.Point;
using FontFamily = System.Windows.Media.FontFamily;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using PlacementMode = System.Windows.Controls.Primitives.PlacementMode;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    public partial class MainWindow : Window
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
        }

        private readonly AppSettingsService _settingsService = new();
        private readonly ActionService _actionService;
        private readonly PanelPackageService _panelPackageService;
        private NativeIntegrationService? _nativeService;

        private AppSettings _appSettings => _settingsService.Settings;
        private IReadOnlyList<CustomElement> _elements => _settingsService.Elements;

        private System.Windows.Forms.NotifyIcon _notifyIcon = null!;

        private Button? _draggedButton = null;
        private CustomElement? _draggedElement = null;
        private const string ProductPageUrl = "https://suvorov.pp.ua/aitebar/";
        private const string DonatePageUrl = "https://suvorov.pp.ua/donate/";
        private const double TopPanelVisibleOffset = 12;
        private Point _dragStartPos;
        private bool _isReordering = false;
        private int _draggedOriginalIndex;
        private bool _isPanelDragging = false;
        private bool _panelDragChanged = false;
        private DockEdge _dragStartEdge;
        private int _dragStartMonitorIndex;
        private bool _isElementContextMenuOpen;
        private bool _isBlockingPanelInteraction;
        private List<Button> _userButtons = [];
        private List<CustomElement> _activeContextElements = [];
        private int _pendingContextAnimationDirection;
        private bool _startupInfrastructureInitialized;
        private bool _deferredStartupCompleted;
        private int _panelRefreshVersion;
        private int _contextWheelDelta;
        private DateTime _lastContextWheelSwitchUtc = DateTime.MinValue;
        private readonly Dictionary<string, CachedButtonImage> _buttonImageCache = new(StringComparer.OrdinalIgnoreCase);

        private const int HOTKEY_ID = 9000;
        private const int HOTKEY_CONTEXT_NEXT_ID = 9001;
        private const int HOTKEY_CONTEXT_PREVIOUS_ID = 9002;
        private const int HOTKEY_ADD_BUTTON_ID = 9003;
        private const int WM_HOTKEY = 0x0312;
        private const double PanelScreenPadding = 20;
        private const double ButtonPitch = PanelLayoutHelper.ButtonOuterSize;
        private const double DragHandleSpan = 18;
        private const int WheelDeltaPerContextSwitch = 120;
        private const int PanelShowAnimationMs = 175;
        private const int PanelHideAnimationMs = 140;
        private static readonly TimeSpan ContextWheelSwitchCooldown = TimeSpan.FromMilliseconds(220);

        public MainWindow()
        {
            InitializeComponent();
            _actionService = new ActionService(_settingsService);
            _panelPackageService = new PanelPackageService(_settingsService);
            this.Top = -2000; 

            this.SizeChanged += (s, e) => {
                if (!IsLoaded || _isAnimating)
                {
                    return;
                }

                PositionWindowImmediately(_shown);
            };
            
            PathHelper.EnsureDirectories();
            InitTrayIcon();

            Application.Current.Exit += (_, _) => {
                _nativeService?.Dispose();
                UnregisterGlobalHotkey();
            };
        }

        public AppSettings GetAppSettings() => _settingsService.Settings;
        public AppSettingsService GetSettingsService() => _settingsService;
        public ActionService GetActionService() => _actionService;

        public void ApplyLocalizedText()
        {
            BtnAdd.ToolTip = LocalizationService.Get("Main_AddButtonTooltip");
            BtnAppSettings.ToolTip = LocalizationService.Get("Menu_ProgramSettings");

            BtnSearch.ToolTip = LocalizationService.Get("Main_SearchTooltip");
            BtnScreenshot.ToolTip = LocalizationService.Get("Main_ScreenshotTooltip");
            BtnRecord.ToolTip = LocalizationService.Get("Main_RecordTooltip");
            BtnCalc.ToolTip = LocalizationService.Get("Main_CalcTooltip");
            BtnExplorer.ToolTip = LocalizationService.Get("Main_ExplorerTooltip");
            BtnDownloads.ToolTip = LocalizationService.Get("Main_DownloadsTooltip");
            BtnColorPicker.ToolTip = LocalizationService.Get("Main_ColorPickerTooltip");
            BtnQuickNote.ToolTip = LocalizationService.Get("Main_QuickNoteTooltip");
            AttachSystemUtilityContextMenus();
            BuildPanelContextMenu();
        }

        private void AttachSystemUtilityContextMenus()
        {
            BtnSearch.ContextMenu = BuildSystemUtilityContextMenu(LocalizationService.Get("Tool_Search"), () => _appSettings.ShowPresetSearch = false);
            BtnScreenshot.ContextMenu = BuildSystemUtilityContextMenu(LocalizationService.Get("Tool_Screenshot"), () => _appSettings.ShowPresetScreenshot = false);
            BtnRecord.ContextMenu = BuildSystemUtilityContextMenu(LocalizationService.Get("Tool_Video"), () => _appSettings.ShowPresetVideo = false);
            BtnCalc.ContextMenu = BuildSystemUtilityContextMenu(LocalizationService.Get("Tool_Calculator"), () => _appSettings.ShowPresetCalc = false);
            BtnExplorer.ContextMenu = BuildSystemUtilityContextMenu(LocalizationService.Get("Tool_Explorer"), () => _appSettings.ShowPresetExplorer = false);
            BtnDownloads.ContextMenu = BuildSystemUtilityContextMenu(LocalizationService.Get("Tool_Downloads"), () => _appSettings.ShowPresetDownloads = false);
            BtnColorPicker.ContextMenu = BuildSystemUtilityContextMenu(LocalizationService.Get("Tool_ColorPicker"), () => _appSettings.ShowPresetColorPicker = false);
            BtnQuickNote.ContextMenu = BuildSystemUtilityContextMenu(LocalizationService.Get("Tool_QuickNote"), () => _appSettings.ShowPresetQuickNote = false);
        }

        private ContextMenu BuildSystemUtilityContextMenu(string title, Action detachAction)
        {
            var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
            menu.Opened += (s, e) => _isElementContextMenuOpen = true;
            menu.Closed += (s, e) => _isElementContextMenuOpen = false;

            menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Unpin), LocalizationService.Get("Menu_Unpin"), async (s, e) =>
            {
                await RunPanelInteractionAsync(async () =>
                {
                    detachAction();
                    await _settingsService.SaveAsync();
                    RefreshPanel();
                });
            }));

            return menu;
        }

        private MenuItem CreateMenuItem(string glyph, string text, RoutedEventHandler? onClick = null, bool isDanger = false, bool isActive = false)
        {
            var accentBrush = isDanger 
                ? new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FF5252"))
                : isActive
                    ? (Brush)FindResource("AccentColor")
                    : new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#E3E3E3"));

            var icon = new System.Windows.Controls.TextBlock
            {
                Text = glyph,
                FontFamily = MenuIconFont,
                FontSize = 16,
                Foreground = accentBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Width = 16,
                Height = 16,
                LineHeight = 16,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            TextOptions.SetTextFormattingMode(icon, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(icon, TextRenderingMode.ClearType);

            var item = new MenuItem
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

        public async Task<IReadOnlyList<string>> SaveAppSettings(AppSettings settings)
        {
            _settingsService.NormalizeAppState();
            await _settingsService.SaveAsync();
            return RegisterGlobalHotkey();
        }

        public IReadOnlyList<PanelContext> GetContextsSnapshot() => _settingsService.GetContextsSnapshot();

        public IReadOnlyList<PanelContext> GetAllContextsSnapshot() => _settingsService.GetAllContextsSnapshot();

        public string GetContextDisplayName(string contextId) => _settingsService.GetContextDisplayName(contextId);

        private string GetPrimaryContextId() => _settingsService.GetPrimaryContextId();

        private bool ShouldShowSystemUtilsForContext(string? contextId = null)
        {
            string targetContextId = string.IsNullOrWhiteSpace(contextId) ? _appSettings.ActiveContextId : contextId;
            return string.Equals(targetContextId, GetPrimaryContextId(), System.StringComparison.Ordinal);
        }

        private async Task SwitchActiveContextAsync(int direction)
        {
            var enabledContexts = ContextStateHelper.GetEnabledContexts(_appSettings.Contexts);
            if (enabledContexts.Count == 0) return;

            int currentIndex = enabledContexts.ToList().FindIndex(context => string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal));
            if (currentIndex < 0) currentIndex = 0;

            int nextIndex = ContextStateHelper.WrapIndex(currentIndex + direction, enabledContexts.Count);
            string nextContextId = enabledContexts[nextIndex].Id;
            if (string.Equals(_appSettings.ActiveContextId, nextContextId, StringComparison.Ordinal)) return;

            _appSettings.ActiveContextId = nextContextId;
            _pendingContextAnimationDirection = Math.Sign(direction);
            RefreshPanel();
            await _settingsService.SaveAsync();
        }

        private void ActivateContextRelative(int direction)
        {
            var enabledContexts = ContextStateHelper.GetEnabledContexts(_appSettings.Contexts);
            if (enabledContexts.Count == 0) return;

            int currentIndex = enabledContexts.ToList().FindIndex(context => string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal));
            if (currentIndex < 0) currentIndex = 0;

            int nextIndex = ContextStateHelper.WrapIndex(currentIndex + direction, enabledContexts.Count);
            string nextContextId = enabledContexts[nextIndex].Id;
            if (string.Equals(_appSettings.ActiveContextId, nextContextId, StringComparison.Ordinal)) return;

            _appSettings.ActiveContextId = nextContextId;
            _pendingContextAnimationDirection = Math.Sign(direction);
            RefreshPanel();
            _ = _settingsService.SaveAsync();
        }

        private void ActivateContextByIndex(int index)
        {
            var enabledContexts = ContextStateHelper.GetEnabledContexts(_appSettings.Contexts);
            if (index < 0 || index >= enabledContexts.Count) return;

            int currentIndex = enabledContexts.ToList().FindIndex(context => string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal));
            if (currentIndex < 0) currentIndex = 0;

            string nextContextId = enabledContexts[index].Id;
            if (string.Equals(_appSettings.ActiveContextId, nextContextId, StringComparison.Ordinal)) return;

            _appSettings.ActiveContextId = nextContextId;
            _pendingContextAnimationDirection = index >= currentIndex ? 1 : -1;
            RefreshPanel();
            _ = _settingsService.SaveAsync();
        }

        private void BuildPanelContextMenu()
        {
            var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
            var panelsMenu = CreateMenuItem(FluentGlyph(MenuIcons.Panels), LocalizationService.Get("Menu_Panels"));

            foreach (var context in ContextStateHelper.GetEnabledContexts(_appSettings.Contexts))
            {
                bool isActive = string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal);
                string targetContextId = context.Id;
                
                var item = CreateMenuItem(
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
            var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
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

            var moveTargets = _appSettings.Contexts
                .Where(context => context.IsEnabled)
                .Where(context => !string.Equals(context.Id, element.ContextId, StringComparison.Ordinal))
                .Select(context => CreateMenuItem(FluentGlyph(MenuIcons.Move), context.Name, async (s, e) =>
                {
                    await RunPanelInteractionAsync(() => MoveElementToContextAsync(element.Id, context.Id));
                }))
                .ToList();

            if (moveTargets.Count > 0)
            {
                var moveMenu = CreateMenuItem(FluentGlyph(MenuIcons.Move), LocalizationService.Get("Menu_Move"));
                foreach (var moveTarget in moveTargets)
                {
                    moveMenu.Items.Add(moveTarget);
                }
                menu.Items.Add(moveMenu);
            }

            if (TryCreateCopyActionMenuItem(element, out var copyItem))
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
            var duplicate = _settingsService.CloneElement(source);
            duplicate.Id = Guid.NewGuid().ToString();
            duplicate.Name = BuildDuplicateElementName(source.Name);
            duplicate.LastUsedProfile = "";

            await _settingsService.InsertElementAfterAsync(source.Id, duplicate);
            RefreshPanel();
            new SettingsWindow(this, duplicate) { Owner = this }.ShowDialog();
        }

        private string BuildDuplicateElementName(string sourceName)
        {
            string baseName = string.IsNullOrWhiteSpace(sourceName) ? LocalizationService.Get("Element_NewButton") : sourceName.Trim();
            string firstCandidate = $"{baseName} ({LocalizationService.Get("Element_CopySuffix")})";
            if (_elements.All(x => !string.Equals(x.Name, firstCandidate, StringComparison.OrdinalIgnoreCase))) return firstCandidate;

            for (int index = 2; ; index++)
            {
                string candidate = $"{baseName} ({LocalizationService.Format("Element_CopySuffixFormat", index)})";
                if (_elements.All(x => !string.Equals(x.Name, candidate, StringComparison.OrdinalIgnoreCase))) return candidate;
            }
        }

        private async Task RenameElementAsync(CustomElement source)
        {
            var elementToRename = _elements.FirstOrDefault(x => string.Equals(x.Id, source.Id, StringComparison.Ordinal));
            if (elementToRename == null) return;

            var dialog = new TextPromptDialog(LocalizationService.Get("Prompt_RenameButtonTitle"), LocalizationService.Get("Prompt_NewName"), elementToRename.Name) { Owner = this };
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
            var elementToMove = _elements.FirstOrDefault(x => string.Equals(x.Id, elementId, StringComparison.Ordinal));
            if (elementToMove == null || string.Equals(elementToMove.ContextId, targetContextId, StringComparison.Ordinal))
            {
                return;
            }

            await _settingsService.UpdateElementAsync(elementToMove.Id, element => element.ContextId = targetContextId);
            RefreshPanel();
        }

        private async Task DeleteElementAsync(CustomElement source)
        {
            var elementToDelete = _elements.FirstOrDefault(x => string.Equals(x.Id, source.Id, StringComparison.Ordinal));
            if (elementToDelete == null)
            {
                return;
            }

            var dialog = new DarkDialog(LocalizationService.Format("DeleteButtonConfirm", elementToDelete.Name), isConfirm: true) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            await _settingsService.DeleteElementAsync(elementToDelete.Id);
            RefreshPanel();
        }

        private bool TryCreateCopyActionMenuItem(CustomElement element, out MenuItem menuItem)
        {
            menuItem = null!;

            if (!TryGetCopyValue(element, out var caption, out var value))
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

        private async Task OpenElementLocationAsync(CustomElement element)
        {
            try
            {
                if (!Enum.TryParse<ActionType>(element.ActionType, out var actionType))
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

        private void ActivateContextById(string contextId)
        {
            if (string.IsNullOrWhiteSpace(contextId) || string.Equals(_appSettings.ActiveContextId, contextId, StringComparison.Ordinal))
            {
                return;
            }

            var enabledContexts = ContextStateHelper.GetEnabledContexts(_appSettings.Contexts);
            if (!enabledContexts.Any(context => string.Equals(context.Id, contextId, StringComparison.Ordinal)))
            {
                return;
            }

            int currentIndex = enabledContexts.ToList().FindIndex(context => string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal));
            if (currentIndex < 0) currentIndex = 0;

            int index = enabledContexts.ToList().FindIndex(context => string.Equals(context.Id, contextId, StringComparison.Ordinal));
            if (index < 0) return;

            _appSettings.ActiveContextId = contextId;
            _pendingContextAnimationDirection = index >= currentIndex ? 1 : -1;
            RefreshPanel();
            _ = _settingsService.SaveAsync();
        }

        private Screen? GetTargetScreen()
        {
            var screens = Screen.AllScreens;
            return (_appSettings.MonitorIndex >= 0 && _appSettings.MonitorIndex < screens.Length)
                ? screens[_appSettings.MonitorIndex]
                : Screen.PrimaryScreen;
        }

        private void ApplyPanelSizeConstraints()
        {
            bool isVertical = _appSettings.Edge == DockEdge.Left || _appSettings.Edge == DockEdge.Right;
            var screen = GetTargetScreen();
            var workArea = screen?.WorkingArea;

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
            SystemUtilsPanel.Width = double.NaN;
            SystemUtilsPanel.Height = double.NaN;
            AppSettingsBlock.MaxWidth = double.PositiveInfinity;
            AppSettingsBlock.MaxHeight = double.PositiveInfinity;
            AppSettingsBlock.Width = double.NaN;
            AppSettingsBlock.Height = double.NaN;
            UserButtonsPanel.MaxWidth = double.PositiveInfinity;
            UserButtonsPanel.MaxHeight = double.PositiveInfinity;
            UserButtonsPanel.MinWidth = 0;
            UserButtonsPanel.MinHeight = 0;
            UserButtonsPanel.Width = double.NaN;
            UserButtonsPanel.Height = double.NaN;

            if (workArea == null)
            {
                return;
            }

            double availableWidth = Math.Max(150, (workArea.Value.Width / _cachedDpi) - PanelScreenPadding);
            double availableHeight = Math.Max(150, (workArea.Value.Height / _cachedDpi) - PanelScreenPadding);
            int visibleSystemButtonCount = GetVisibleSystemButtonCount();
            var metrics = PanelLayoutHelper.Calculate(
                isVertical: isVertical,
                availablePrimary: isVertical ? availableHeight : availableWidth,
                panelPercent: _appSettings.PanelSizePercent,
                visibleSystemButtonCount: visibleSystemButtonCount,
                controlButtonCount: 1,
                contextCounts: ContextStateHelper.GetEnabledContexts(_appSettings.Contexts)
                    .Select(context => _elements.Count(element => string.Equals(element.ContextId, context.Id, StringComparison.Ordinal))).ToList(),
                activeContextIndex: Math.Max(0, ContextStateHelper.GetEnabledContexts(_appSettings.Contexts).ToList().FindIndex(context => string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal))),
                systemContextIndex: 0,
                trailingControlButtonCount: 1);

            RootBorder.MinWidth = metrics.PanelWidth;
            RootBorder.MaxWidth = metrics.PanelWidth;
            RootBorder.MinHeight = metrics.PanelHeight;
            RootBorder.MaxHeight = metrics.PanelHeight;

            double contentWidth = Math.Max(0, metrics.PanelWidth - PanelLayoutHelper.PanelChrome);
            double contentHeight = Math.Max(0, metrics.PanelHeight - PanelLayoutHelper.PanelChrome);

            if (isVertical)
            {
                RootBorder.MinHeight += DragHandleSpan;
                RootBorder.MaxHeight += DragHandleSpan;
                contentHeight += DragHandleSpan;
            }
            else
            {
                RootBorder.MinWidth += DragHandleSpan;
                RootBorder.MaxWidth += DragHandleSpan;
                contentWidth += DragHandleSpan;
            }

            MainPanel.Width = contentWidth;
            MainPanel.Height = contentHeight;
            FixedPanel.Width = isVertical ? contentWidth : metrics.FixedWidth;
            FixedPanel.Height = metrics.FixedHeight;
            ControlBlock.Width = isVertical ? contentWidth : double.NaN;
            AppSettingsBlock.Width = metrics.TrailingWidth;
            AppSettingsBlock.Height = metrics.TrailingHeight;
            if (isVertical && visibleSystemButtonCount > 1)
            {
                SystemUtilsPanel.Width = metrics.SystemWidth;
                SystemUtilsPanel.Height = metrics.SystemHeight;
            }

            UserButtonsPanel.Width = metrics.UserWidth;
            UserButtonsPanel.Height = metrics.UserHeight;
            UserButtonsPanel.MaxWidth = metrics.UserWidth;
            UserButtonsPanel.MaxHeight = metrics.UserHeight;
            UserButtonsPanel.MinWidth = metrics.UserWidth;
            UserButtonsPanel.MinHeight = metrics.UserHeight;
            UserButtonsPanel.LeadingPrimaryReserve = isVertical ? metrics.UserLeadingReserve : 0;
            UserButtonsPanel.OverflowPrimaryReserve = isVertical ? metrics.UserOverflowReserve : 0;
            UserButtonsPanel.Margin = isVertical && metrics.UserLeadingReserve > 0
                ? new Thickness(0, -metrics.UserLeadingReserve, 0, 0)
                : new Thickness(0);
        }

        private int GetVisibleSystemButtonCount()
        {
            int count = 0;
            if (_appSettings.ShowPresetSearch) count++;
            if (_appSettings.ShowPresetScreenshot) count++;
            if (_appSettings.ShowPresetVideo) count++;
            if (_appSettings.ShowPresetCalc) count++;
            if (_appSettings.ShowPresetExplorer) count++;
            if (_appSettings.ShowPresetDownloads) count++;
            if (_appSettings.ShowPresetColorPicker) count++;
            if (_appSettings.ShowPresetQuickNote) count++;
            return count;
        }

        private IReadOnlyList<string> RegisterGlobalHotkey()
        {
            var failedHotkeys = new List<string>();
            UnregisterGlobalHotkey();
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return failedHotkeys;

            var showPanelBinding = new HotkeyBinding
            {
                Ctrl = _appSettings.GlobalHotkeyCtrl,
                Alt = _appSettings.GlobalHotkeyAlt,
                Shift = _appSettings.GlobalHotkeyShift,
                Win = _appSettings.GlobalHotkeyWin,
                Key = _appSettings.GlobalHotkeyKey
            };

            AddFailedHotkey(
                failedHotkeys,
                LocalizationService.Get("AppSettingsWindow_ShowPanel"),
                RegisterHotkeyBinding(hwnd, HOTKEY_ID, showPanelBinding));
            AddFailedHotkey(
                failedHotkeys,
                LocalizationService.Get("AppSettingsWindow_NextPanel"),
                RegisterHotkeyBinding(hwnd, HOTKEY_CONTEXT_NEXT_ID, _appSettings.NextContextHotkey));
            AddFailedHotkey(
                failedHotkeys,
                LocalizationService.Get("AppSettingsWindow_PreviousPanel"),
                RegisterHotkeyBinding(hwnd, HOTKEY_CONTEXT_PREVIOUS_ID, _appSettings.PreviousContextHotkey));
            AddFailedHotkey(
                failedHotkeys,
                LocalizationService.Get("AppSettingsWindow_AddButton"),
                RegisterHotkeyBinding(hwnd, HOTKEY_ADD_BUTTON_ID, _appSettings.AddButtonHotkey));

            return failedHotkeys;
        }

        private static void AddFailedHotkey(List<string> failedHotkeys, string name, bool success)
        {
            if (!success)
            {
                failedHotkeys.Add(name);
            }
        }

        private void UnregisterGlobalHotkey()
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            int[] hotkeyIds =
            [
                HOTKEY_ID,
                HOTKEY_CONTEXT_NEXT_ID,
                HOTKEY_CONTEXT_PREVIOUS_ID,
                HOTKEY_ADD_BUTTON_ID,
                9011,
                9012,
                9013,
                9014
            ];

            foreach (int hotkeyId in hotkeyIds)
            {
                NativeMethods.UnregisterHotKey(hwnd, hotkeyId);
            }
        }

        private static bool RegisterHotkeyBinding(IntPtr hwnd, int hotkeyId, HotkeyBinding binding)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.Key) || string.Equals(binding.Key, "None", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!Enum.TryParse(typeof(Key), binding.Key, out var key))
            {
                return false;
            }

            uint modifiers = 0;
            if (binding.Ctrl) modifiers |= 0x0002;
            if (binding.Alt) modifiers |= 0x0001;
            if (binding.Shift) modifiers |= 0x0004;
            if (binding.Win) modifiers |= 0x0008;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey((Key)key!);
            return NativeMethods.RegisterHotKey(hwnd, hotkeyId, modifiers, vk);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                ToggleDock();
                handled = true;
            }
            else if (msg == NativeMethods.WM_HOTKEY)
            {
                switch (wParam.ToInt32())
                {
                    case HOTKEY_CONTEXT_NEXT_ID:
                        ActivateContextRelative(1);
                        handled = true;
                        break;
                    case HOTKEY_CONTEXT_PREVIOUS_ID:
                        ActivateContextRelative(-1);
                        handled = true;
                        break;
                    case HOTKEY_ADD_BUTTON_ID:
                        _ = OpenAddButtonWindowAsync();
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            System.Windows.Interop.HwndSource.FromHwnd(hwnd).AddHook(WndProc);
            RegisterGlobalHotkey();
        }

        private void InitTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "AiteBar",
                Visible = true
            };
            try {
                var iconUri = new Uri("pack://application:,,,/Resources/app.ico");
                var streamInfo = Application.GetResourceStream(iconUri);
                if (streamInfo != null) {
                    using var stream = streamInfo.Stream; _notifyIcon.Icon = new Icon(stream);
                } else _notifyIcon.Icon = SystemIcons.Application;
            } catch (Exception ex) { Logger.Log(ex); _notifyIcon.Icon = SystemIcons.Application; }

            _notifyIcon.MouseClick += (s, e) => {
                if (e.Button == System.Windows.Forms.MouseButtons.Left) {
                    ToggleDock();
                }
                else if (e.Button == System.Windows.Forms.MouseButtons.Right) {
                    ShowTrayContextMenu();
                }
            };
        }

        private Button CreatePanelButton(string content, string tooltip, RoutedEventHandler onClick, Brush? foreground = null)
        {
            var btn = new Button
            {
                Content = content,
                ToolTip = tooltip,
                Style = (Style)FindResource("PanelButtonStyle")
            };
            
            if (foreground != null)
            {
                btn.Foreground = foreground;
            }
            
            btn.Click += onClick;
            return btn;
        }

        private void ShowTrayContextMenu()
        {
            var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };

            menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Open), LocalizationService.Get("Menu_Open"), (s, e) => ShowDock()));
            menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Settings), LocalizationService.Get("Menu_ProgramSettings"), (s, e) => new AppSettingsWindow(this) { Owner = this }.ShowDialog()));
            menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Info), LocalizationService.Get("Menu_About"), (s, e) => OpenUrl(ProductPageUrl)));
            menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Donate), LocalizationService.Get("Menu_Donate"), (s, e) => OpenUrl(DonatePageUrl)));
            menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Exit), LocalizationService.Get("Menu_Exit"), (s, e) => { _notifyIcon.Dispose(); Application.Current.Shutdown(); }));

            // Для того чтобы ContextMenu закрывалось при клике мимо, 
            // его нужно привязать к невидимому элементу или использовать Placement.
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;

            // Важный хак для WPF ContextMenu в трее: фокус окна
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            NativeMethods.SetForegroundWindow(hwnd);
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

        private static void OpenUrl(string url) {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Logger.Log(ex); }
        }

        private async Task ExportCurrentPanelAsync()
        {
            try
            {
                var activeContext = _appSettings.Contexts.FirstOrDefault(context =>
                    string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal));

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = LocalizationService.Get("Export_Title"),
                    Filter = LocalizationService.Get("Export_Filter"),
                    DefaultExt = PanelPackageService.PackageExtension,
                    AddExtension = true,
                    OverwritePrompt = true,
                    FileName = BuildPanelPackageFileName(activeContext?.Name ?? LocalizationService.Format("Panel_DefaultNameFormat", 1))
                };

                if (dialog.ShowDialog(this) != true)
                {
                    return;
                }

                PanelExportResult result = await _panelPackageService.ExportCurrentPanelAsync(dialog.FileName);
                new DarkDialog(LocalizationService.Format("Export_Success", result.ExportedCount), false) { Owner = this }.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog(LocalizationService.Format("Export_Failed", ex.Message), false) { Owner = this }.ShowDialog();
            }
        }

        private async Task ImportIntoCurrentPanelAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = LocalizationService.Get("Import_Title"),
                    Filter = LocalizationService.Get("Export_Filter"),
                    DefaultExt = PanelPackageService.PackageExtension,
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (dialog.ShowDialog(this) != true)
                {
                    return;
                }

                PanelImportPreview preview = await _panelPackageService.ReadImportPreviewAsync(dialog.FileName);
                string targetPanelName = GetContextDisplayName(_appSettings.ActiveContextId);
                var confirm = new DarkDialog(
                    LocalizationService.Format("Import_Confirm", preview.ElementCount, targetPanelName),
                    isConfirm: true)
                {
                    Owner = this
                };

                if (confirm.ShowDialog() != true)
                {
                    return;
                }

                PanelImportResult result = await _panelPackageService.ImportIntoCurrentPanelAsync(dialog.FileName);
                RefreshPanel();
                new DarkDialog(LocalizationService.Format("Import_Success", result.ImportedCount), false) { Owner = this }.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog(LocalizationService.Format("Import_Failed", ex.Message), false) { Owner = this }.ShowDialog();
            }
        }

        private static string BuildPanelPackageFileName(string panelName)
        {
            string sanitized = string.Concat(panelName.Select(ch =>
                Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "Panel";
            }

            return sanitized + PanelPackageService.PackageExtension;
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

            double centeredX = workArea.Left + Math.Max(0, (workArea.Width - width) / 2);
            double centeredY = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);

            return _appSettings.Edge switch
            {
                DockEdge.Top => (centeredX, hide ? bounds.Top - height : workArea.Top + TopPanelVisibleOffset),
                DockEdge.Bottom => (centeredX, hide ? bounds.Bottom : workArea.Bottom - height),
                DockEdge.Left => (hide ? bounds.Left - width : workArea.Left, centeredY),
                DockEdge.Right => (hide ? bounds.Right : workArea.Right - width, centeredY),
                _ => (workArea.Left, workArea.Top)
            };
        }

        private bool _isPositioning = false;
        private void PositionWindowImmediately(bool shown)
        {
            if (_isPositioning) return;
            _isPositioning = true;
            try
            {
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

        private async void Window_Loaded(object sender, RoutedEventArgs e) {
            try
            {
                ApplyLocalizedText();
                EnsureStartupInfrastructure();
                RefreshPanel();
                PositionWindowImmediately(_shown);
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                _ = CompleteDeferredStartupAsync();
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
            _nativeService.MouseDownOutside += (x, y) => {
                if (_shown && !_isAnimating && !IsPanelInteractionActive)
                {
                    if (x < _panelLeft || x > _panelRight || y < _panelTop || y > _panelBottom)
                    {
                        this.Dispatcher.InvokeAsync(async () => await HideDock());
                    }
                }
            };

            _timer.Tick += (s, ev) => {
                if (_isAnimating || _isPanelDragging) return;
                NativeMethods.Win32Point pt = new();
                if (NativeMethods.GetCursorPos(ref pt)) {
                    var screens = Screen.AllScreens;
                    var screen = (_appSettings.MonitorIndex >= 0 && _appSettings.MonitorIndex < screens.Length) 
                        ? screens[_appSettings.MonitorIndex] 
                        : Screen.PrimaryScreen;
                    
                    if (screen == null) return;

                    var bounds = screen.Bounds;
                    double screenLeft = bounds.Left;
                    double screenTop = bounds.Top;
                    double screenWidth = bounds.Width;
                    double screenHeight = bounds.Height;
                    
                    int delayMs = _appSettings.ActivationDelayMs;
                    bool inActivationZone = ActivationZoneHelper.IsInActivationZone(
                        _appSettings.Edge,
                        screenLeft,
                        screenTop,
                        screenWidth,
                        screenHeight,
                        _appSettings.ActivationZoneSizePercent,
                        pt.X,
                        pt.Y);

                    if (inActivationZone && !_shown) {
                        if (_hoverStartTime == null) _hoverStartTime = DateTime.Now;
                        else if ((DateTime.Now - _hoverStartTime.Value).TotalMilliseconds >= delayMs) {
                            _shown = true; _hoverStartTime = null; Toggle(false);
                        }
                    } else _hoverStartTime = null;
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

            try
            {
                await Task.Run(async () => await _settingsService.LoadAsync());
                LocalizationService.ApplyCulture(_appSettings.UiCulture);
                _deferredStartupCompleted = true;
                ApplyLocalizedText();
                RegisterGlobalHotkey();
                RefreshPanel();
                PositionWindowImmediately(_shown);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void UpdateOrientation()
        {
            bool isVertical = _appSettings.Edge == DockEdge.Left || _appSettings.Edge == DockEdge.Right;
            var orientation = System.Windows.Controls.Orientation.Horizontal;
            if (isVertical) orientation = System.Windows.Controls.Orientation.Vertical;

            if (isVertical) { this.MinWidth = 0; this.MinHeight = 150; }
            else { this.MinWidth = 150; this.MinHeight = 0; }

            System.Windows.Controls.DockPanel.SetDock(DragHandle, isVertical ? System.Windows.Controls.Dock.Top : System.Windows.Controls.Dock.Left);
            FixedPanel.Orientation = orientation;
            AppSettingsBlock.Orientation = orientation;
            UserButtonsPanel.Orientation = isVertical
                ? System.Windows.Controls.Orientation.Vertical
                : System.Windows.Controls.Orientation.Horizontal;
            SystemUtilsPanel.Orientation = isVertical
                ? System.Windows.Controls.Orientation.Vertical
                : System.Windows.Controls.Orientation.Horizontal;
            ControlBlock.Orientation = orientation;
            System.Windows.Controls.DockPanel.SetDock(FixedPanel, isVertical ? System.Windows.Controls.Dock.Top : System.Windows.Controls.Dock.Left);
            System.Windows.Controls.DockPanel.SetDock(UserButtonsPanel, isVertical ? System.Windows.Controls.Dock.Top : System.Windows.Controls.Dock.Left);
            System.Windows.Controls.DockPanel.SetDock(AppSettingsBlock, isVertical ? System.Windows.Controls.Dock.Bottom : System.Windows.Controls.Dock.Right);
            FixedPanel.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
            UserButtonsPanel.VerticalAlignment = isVertical ? VerticalAlignment.Top : VerticalAlignment.Center;
            AppSettingsBlock.VerticalAlignment = isVertical ? VerticalAlignment.Bottom : VerticalAlignment.Center;
            FixedPanel.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Left;
            ControlBlock.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Stretch : System.Windows.HorizontalAlignment.Left;
            BtnAdd.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Center : System.Windows.HorizontalAlignment.Stretch;
            SystemUtilsPanel.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Left;
            UserButtonsPanel.HorizontalAlignment = isVertical ? System.Windows.HorizontalAlignment.Left : System.Windows.HorizontalAlignment.Left;
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

            var separators = new[] { SepSystem, SepControl, SepAppSettings };
            foreach (var sep in separators)
            {
                if (isVertical) { sep.Width = 20; sep.Height = 1; sep.Margin = new Thickness(0, 4, 0, 4); }
                else { sep.Width = 1; sep.Height = 20; sep.Margin = new Thickness(4, 0, 4, 0); }
            }

            ApplyPanelSizeConstraints();
            ApplyPanelToolTipPlacement();
            PositionWindowImmediately(_shown);
        }

        public void RefreshPanel() {
            int panelVersion = unchecked(++_panelRefreshVersion);
            _settingsService.NormalizeAppState();
            BuildPanelContextMenu();
            string activeContextId = _appSettings.ActiveContextId;
            bool hasSystemUtils = ApplySystemUtilityVisibility(activeContextId);

            UpdateOrientation();
            UserButtonsPanel.Children.Clear();
            _userButtons.Clear();

            _activeContextElements = _elements
                .Where(element => string.Equals(element.ContextId, activeContextId, StringComparison.Ordinal))
                .ToList();

            foreach (var el in _activeContextElements) {
                var btn = CreatePanelButton(string.Empty, el.Name, (s, e) => {
                    // Обработка клика перенесена в PreviewMouseUp для поддержки реордеринга
                }, (Brush)_brushConverter.ConvertFromString(el.Color ?? "#E3E3E3")!);
                
                btn.RenderTransform = new TranslateTransform();
                btn.Tag = el.Id;

                ApplyButtonIcon(btn, el, panelVersion);
                 
                var capturedElement = el;
                btn.PreviewMouseDown += (s, e) => {
                    if (e.ChangedButton != MouseButton.Left) return;
                    _draggedButton = s as Button; 
                    _draggedElement = capturedElement;
                    _dragStartPos = e.GetPosition(this); 
                    _isReordering = false;
                    _draggedOriginalIndex = _userButtons.IndexOf(_draggedButton!);
                    _draggedButton!.CaptureMouse();
                };
                
                btn.PreviewMouseMove += (s, e) => {
                    if (_draggedButton == null || e.LeftButton != MouseButtonState.Pressed) return;
                    
                    Point currentPos = e.GetPosition(this);
                    double deltaX = currentPos.X - _dragStartPos.X;
                    double deltaY = currentPos.Y - _dragStartPos.Y;

                    if (!_isReordering && (Math.Abs(deltaX) > 10 || Math.Abs(deltaY) > 10)) {
                        _isReordering = true;
                        _draggedButton.Opacity = 0.7;
                        Panel.SetZIndex(_draggedButton, 100);
                    }

                    if (_isReordering) {
                        bool isVertical = _appSettings.Edge == DockEdge.Left || _appSettings.Edge == DockEdge.Right;
                        var tt = (TranslateTransform)_draggedButton.RenderTransform;
                        if (isVertical) tt.Y = deltaY; else tt.X = deltaX;

                        UpdateReorderPositions(currentPos);
                    }
                };

                btn.PreviewMouseUp += async (s, e) => {
                    if (_draggedButton == null) return;
                    _draggedButton.ReleaseMouseCapture();

                    if (_isReordering) {
                        _draggedButton.Opacity = 1.0;
                        int newIndex = CalculateTargetIndex(e.GetPosition(this));
                        if (newIndex >= 0 && newIndex < _activeContextElements.Count && newIndex != _draggedOriginalIndex) {
                            _settingsService.ReorderElements(_draggedOriginalIndex, newIndex, _appSettings.ActiveContextId);
                            await _settingsService.SaveAsync();
                        }
                        RefreshPanel();
                    } else {
                        // Если это был просто клик (не реордеринг), выполняем действие
                        var result = await _actionService.ExecuteCustomActionAsync(capturedElement, HideDock);
                        if (!result.Success)
                        {
                            new DarkDialog(LocalizationService.Format("Action_Failed", result.ErrorMessage)) { Owner = this }.ShowDialog();
                        }
                    }
                    _draggedButton = null; _draggedElement = null; _isReordering = false;
                };

                btn.MouseRightButtonUp += (s, e) =>
                {
                    btn.ContextMenu ??= BuildElementContextMenu(capturedElement);
                };
                UserButtonsPanel.Children.Add(btn);
                _userButtons.Add(btn);
            }
            
            // Разделители
            SepSystem.Visibility = hasSystemUtils ? Visibility.Visible : Visibility.Collapsed;
            SepControl.Visibility = UserButtonsPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            SepAppSettings.Visibility = UserButtonsPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            AnimateContextTransitionIfNeeded();
            ApplyPanelToolTipPlacement();
            
            UpdatePanelBounds();
        }

        private void ApplyPanelToolTipPlacement()
        {
            var placement = GetPanelToolTipPlacement(_appSettings.Edge);
            var horizontalOffset = _appSettings.Edge switch
            {
                DockEdge.Left => 8,
                DockEdge.Right => -8,
                _ => 0
            };
            var verticalOffset = _appSettings.Edge switch
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
            yield return BtnSearch;
            yield return BtnScreenshot;
            yield return BtnRecord;
            yield return BtnCalc;
            yield return BtnExplorer;
            yield return BtnDownloads;
            yield return BtnColorPicker;
            yield return BtnQuickNote;
            yield return BtnAppSettings;

            foreach (var button in _userButtons)
            {
                yield return button;
            }
        }

        private static PlacementMode GetPanelToolTipPlacement(DockEdge edge) => edge switch
        {
            DockEdge.Bottom => PlacementMode.Top,
            DockEdge.Left => PlacementMode.Right,
            DockEdge.Right => PlacementMode.Left,
            _ => PlacementMode.Bottom
        };

        private bool ApplySystemUtilityVisibility(string activeContextId)
        {
            bool showSystemUtils = ShouldShowSystemUtilsForContext(activeContextId);
            BtnSearch.Visibility = showSystemUtils && _appSettings.ShowPresetSearch ? Visibility.Visible : Visibility.Collapsed;
            BtnScreenshot.Visibility = showSystemUtils && _appSettings.ShowPresetScreenshot ? Visibility.Visible : Visibility.Collapsed;
            BtnRecord.Visibility = showSystemUtils && _appSettings.ShowPresetVideo ? Visibility.Visible : Visibility.Collapsed;
            BtnCalc.Visibility = showSystemUtils && _appSettings.ShowPresetCalc ? Visibility.Visible : Visibility.Collapsed;
            BtnExplorer.Visibility = showSystemUtils && _appSettings.ShowPresetExplorer ? Visibility.Visible : Visibility.Collapsed;
            BtnDownloads.Visibility = showSystemUtils && _appSettings.ShowPresetDownloads ? Visibility.Visible : Visibility.Collapsed;
            BtnColorPicker.Visibility = showSystemUtils && _appSettings.ShowPresetColorPicker ? Visibility.Visible : Visibility.Collapsed;
            BtnQuickNote.Visibility = showSystemUtils && _appSettings.ShowPresetQuickNote ? Visibility.Visible : Visibility.Collapsed;

            bool hasSystemUtils = BtnSearch.Visibility == Visibility.Visible ||
                                  BtnScreenshot.Visibility == Visibility.Visible ||
                                  BtnRecord.Visibility == Visibility.Visible ||
                                  BtnCalc.Visibility == Visibility.Visible ||
                                  BtnExplorer.Visibility == Visibility.Visible ||
                                  BtnDownloads.Visibility == Visibility.Visible ||
                                  BtnColorPicker.Visibility == Visibility.Visible ||
                                  BtnQuickNote.Visibility == Visibility.Visible;
            SystemUtilsPanel.Visibility = hasSystemUtils ? Visibility.Visible : Visibility.Collapsed;
            return hasSystemUtils;
        }

        private void ApplyButtonIcon(Button button, CustomElement element, int panelVersion)
        {
            if (!string.IsNullOrWhiteSpace(element.ImagePath) && System.IO.File.Exists(element.ImagePath))
            {
                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(element.ImagePath);
                if (_buttonImageCache.TryGetValue(element.ImagePath, out var cached) &&
                    cached.LastWriteUtc == lastWriteUtc)
                {
                    button.Content = CreateButtonImage(cached.Source);
                    return;
                }

                // Avoid flashing the fallback glyph while custom icons are being decoded.
                button.Content = null;
                _ = LoadButtonImageAsync(button, element.Id, element.ImagePath, lastWriteUtc, element.Icon, element.IconFont, panelVersion);
                return;
            }

            button.Content = element.Icon;
            button.FontFamily = FontHelper.Resolve(element.IconFont);
        }

        private async Task LoadButtonImageAsync(
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

        private static System.Windows.Controls.Image CreateButtonImage(System.Windows.Media.Imaging.BitmapSource source) => new()
        {
            Source = source,
            Width = 24,
            Height = 24,
            Stretch = Stretch.Uniform
        };

        private sealed record CachedButtonImage(System.Windows.Media.Imaging.BitmapSource Source, DateTime LastWriteUtc);

        private static DockEdge GetClosestDockEdge(System.Drawing.Rectangle workArea, int cursorX, int cursorY, DockEdge currentEdge)
        {
            var distances = new Dictionary<DockEdge, int>
            {
                [DockEdge.Top] = Math.Abs(cursorY - workArea.Top),
                [DockEdge.Bottom] = Math.Abs(workArea.Bottom - cursorY),
                [DockEdge.Left] = Math.Abs(cursorX - workArea.Left),
                [DockEdge.Right] = Math.Abs(workArea.Right - cursorX)
            };

            // Применяем небольшой гистерезис (смещение), чтобы панель не "прыгала" 
            // слишком легко между краями при движении рядом с углами.
            distances[currentEdge] -= 60;

            return distances.OrderBy(pair => pair.Value).First().Key;
        }

        private static int FindScreenIndex(Screen targetScreen)
        {
            var screens = Screen.AllScreens;
            for (int index = 0; index < screens.Length; index++)
            {
                if (string.Equals(screens[index].DeviceName, targetScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return 0;
        }

        private void SetDragHandleActive(bool isActive)
        {
            DragHandleGrip.Background = isActive
                ? (Brush)_brushConverter.ConvertFromString("#64C7FF")!
                : (Brush)_brushConverter.ConvertFromString("#2A9CFF")!;
        }

        private void SetPanelDragRenderingActive(bool isActive)
        {
            if (isActive)
            {
                BeginAnimation(LeftProperty, null);
                BeginAnimation(TopProperty, null);
                _isAnimating = false;
                RootBorder.CacheMode ??= new BitmapCache();
                return;
            }

            RootBorder.CacheMode = null;
        }

        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isPanelDragging = true;
            _panelDragChanged = false;
            _dragStartEdge = _appSettings.Edge;
            _dragStartMonitorIndex = _appSettings.MonitorIndex;
            SetPanelDragRenderingActive(true);
            DragHandle.CaptureMouse();
            SetDragHandleActive(true);
            e.Handled = true;
        }

        private void DragHandle_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isPanelDragging)
            {
                return;
            }

            _isPanelDragging = false;
            SetDragHandleActive(false);
            SetPanelDragRenderingActive(false);

            if (_panelDragChanged)
            {
                _ = _settingsService.SaveAsync();
            }
            else
            {
                _appSettings.Edge = _dragStartEdge;
                _appSettings.MonitorIndex = _dragStartMonitorIndex;
                UpdateOrientation();
            }
        }

        private void DragHandle_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isPanelDragging || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            NativeMethods.Win32Point pt = new();
            if (!NativeMethods.GetCursorPos(ref pt))
            {
                return;
            }

            var targetScreen = Screen.FromPoint(new System.Drawing.Point(pt.X, pt.Y));
            int nextMonitorIndex = FindScreenIndex(targetScreen);
            DockEdge nextEdge = GetClosestDockEdge(targetScreen.WorkingArea, pt.X, pt.Y, _appSettings.Edge);

            if (_appSettings.MonitorIndex == nextMonitorIndex && _appSettings.Edge == nextEdge)
            {
                return;
            }

            _appSettings.MonitorIndex = nextMonitorIndex;
            _appSettings.Edge = nextEdge;
            _panelDragChanged = true;
            UpdateOrientation();
            // PositionWindowImmediately(shown: true); // Удалено, так как UpdateOrientation уже вызывает PositionWindowImmediately
        }

        private async void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanelDragging)
            {
                return;
            }

            DragHandle.ReleaseMouseCapture();
            _isPanelDragging = false;
            SetDragHandleActive(false);
            SetPanelDragRenderingActive(false);

            if (_panelDragChanged)
            {
                await _settingsService.SaveAsync();
            }
            else
            {
                _appSettings.Edge = _dragStartEdge;
                _appSettings.MonitorIndex = _dragStartMonitorIndex;
            }

            e.Handled = true;
        }

        private void AnimateContextTransitionIfNeeded()
        {
            if (_pendingContextAnimationDirection == 0 || UserButtonsPanel.Children.Count == 0)
            {
                _pendingContextAnimationDirection = 0;
                return;
            }

            int direction = _pendingContextAnimationDirection;
            _pendingContextAnimationDirection = 0;
            bool isVertical = _appSettings.Edge == DockEdge.Left || _appSettings.Edge == DockEdge.Right;

            if (UserButtonsPanel.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                UserButtonsPanel.RenderTransform = transform;
            }

            double initialOffset = direction * 8;
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

            UserButtonsPanel.Opacity = 0.55;

            var fadeAnimation = new DoubleAnimation(1, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var slideAnimation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            UserButtonsPanel.BeginAnimation(OpacityProperty, fadeAnimation);
            transform.BeginAnimation(isVertical ? TranslateTransform.YProperty : TranslateTransform.XProperty, slideAnimation);
        }

        private int CalculateTargetIndex(Point currentPos)
        {
            bool isVertical = _appSettings.Edge == DockEdge.Left || _appSettings.Edge == DockEdge.Right;
            for (int i = 0; i < _userButtons.Count; i++) {
                if (_userButtons[i] == _draggedButton) continue;
                var pos = _userButtons[i].TransformToAncestor(this).Transform(new Point(0, 0));
                var size = new System.Windows.Size(_userButtons[i].ActualWidth, _userButtons[i].ActualHeight);
                if (isVertical) {
                    if (currentPos.Y < pos.Y + size.Height / 2) return i > _draggedOriginalIndex ? i - 1 : i;
                } else {
                    if (currentPos.X < pos.X + size.Width / 2) return i > _draggedOriginalIndex ? i - 1 : i;
                }
            }
            return _userButtons.Count - 1;
        }

        private void UpdateReorderPositions(Point currentPos)
         {
             if (_userButtons.Count < 2) return;
             int targetIndex = CalculateTargetIndex(currentPos);
             bool isVertical = _appSettings.Edge == DockEdge.Left || _appSettings.Edge == DockEdge.Right;
             
             var buttonMargin = _userButtons[0].Margin;
             double offset = isVertical
                 ? _userButtons[0].ActualHeight + buttonMargin.Top + buttonMargin.Bottom
                 : _userButtons[0].ActualWidth + buttonMargin.Left + buttonMargin.Right;
 
             for (int i = 0; i < _userButtons.Count; i++) {
                if (_userButtons[i] == _draggedButton) continue;
                
                double targetOffset = 0;
                if (_draggedOriginalIndex < targetIndex) {
                    if (i > _draggedOriginalIndex && i <= targetIndex) targetOffset = -offset;
                } else if (_draggedOriginalIndex > targetIndex) {
                    if (i >= targetIndex && i < _draggedOriginalIndex) targetOffset = offset;
                }

                var tt = (TranslateTransform)_userButtons[i].RenderTransform;
                double currentOffset = isVertical ? tt.Y : tt.X;

                if (Math.Abs(currentOffset - targetOffset) > 0.1) {
                    var anim = new DoubleAnimation(targetOffset, TimeSpan.FromMilliseconds(150)) {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    tt.BeginAnimation(isVertical ? TranslateTransform.YProperty : TranslateTransform.XProperty, anim);
                }
            }
        }

        public async Task SaveElement(CustomElement updated, string? removeId = null)
        {
            await _settingsService.SaveElementAsync(updated, removeId);
            RefreshPanel();
        }

        public IReadOnlyList<CustomElement> GetElementsSnapshot() => _settingsService.Elements.Select(_settingsService.CloneElement).ToList();

        private void ShowDock()
        {
            if (_shown || _isAnimating)
            {
                return;
            }

            _shown = true;
            _hoverStartTime = null;
            Toggle(false);
        }

        private void ToggleDock()
        {
            if (_isAnimating)
            {
                return;
            }

            if (_shown)
            {
                _ = HideDock();
            }
            else
            {
                ShowDock();
            }
        }

        private void Toggle(bool hide) {
            _isAnimating = true; _timer.Stop();

            if (!hide) { 
                this.Topmost = false; 
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE); 
                this.Topmost = true; 
            }

            var start = GetDockCoordinates(hide: !hide);
            var finish = GetDockCoordinates(hide: hide);
            this.Left = start.X;
            this.Top = start.Y;

            double finalX = finish.X;
            double finalY = finish.Y;

            var duration = TimeSpan.FromMilliseconds(hide ? PanelHideAnimationMs : PanelShowAnimationMs);
            var easing = new CubicEase { EasingMode = hide ? EasingMode.EaseIn : EasingMode.EaseOut };
            var animX = new DoubleAnimation(finalX, duration) { EasingFunction = easing };
            var animY = new DoubleAnimation(finalY, duration) { EasingFunction = easing };
            
            int completedCount = 0;
            void onCompleted(object? s, EventArgs ev) {
                completedCount++;
                if (completedCount == 2) {
                    this.BeginAnimation(LeftProperty, null);
                    this.BeginAnimation(TopProperty, null);
                    this.Left = finalX;
                    this.Top = finalY;
                    _isAnimating = false; 
                    _timer.Start(); 
                    UpdatePanelBounds();
                }
            }

            animX.Completed += onCompleted;
            animY.Completed += onCompleted;

            this.BeginAnimation(LeftProperty, animX);
            this.BeginAnimation(TopProperty, animY);
        }

        private async Task HideDock()
        {
            if (!_shown || _isAnimating)
            {
                return;
            }

            _shown = false;
            _hoverStartTime = null;
            Toggle(true);
            await Task.Delay(PanelHideAnimationMs);
        }

        private async void RootBorder_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_isAnimating || !_shown) return;
            if (e.Delta == 0) return;

            e.Handled = true;
            DateTime now = DateTime.UtcNow;
            if (now - _lastContextWheelSwitchUtc < ContextWheelSwitchCooldown)
            {
                return;
            }

            if (_contextWheelDelta != 0 && Math.Sign(_contextWheelDelta) != Math.Sign(e.Delta))
            {
                _contextWheelDelta = 0;
            }

            _contextWheelDelta += e.Delta;
            if (Math.Abs(_contextWheelDelta) < WheelDeltaPerContextSwitch)
            {
                return;
            }

            int direction = _contextWheelDelta > 0 ? -1 : 1;
            _contextWheelDelta = 0;
            _lastContextWheelSwitchUtc = now;
            await SwitchActiveContextAsync(direction);
        }

        private async Task RunPresetActionAsync(Func<Task> action)
        {
            try
            {
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
        private async void BtnColorPicker_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.StartColorPickerAsync(HideDock)); }
        private async void BtnQuickNote_Click(object sender, RoutedEventArgs e) { await RunPresetActionAsync(() => _actionService.StartQuickNoteAsync(HideDock)); }
        private async Task OpenAddButtonWindowAsync()
        {
            await HideDock();
            new SettingsWindow(this).ShowDialog();
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e) { await OpenAddButtonWindowAsync(); }
        private async void BtnAppSettings_Click(object sender, RoutedEventArgs e) { await HideDock(); new AppSettingsWindow(this).ShowDialog(); }
        
        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            return parent is T p ? p : FindParent<T>(parent);
        }

        private void Border_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = CanAcceptDropData(e.Data) ? DragDropEffects.Link : DragDropEffects.None;
            e.Handled = true;
        }

        private static bool CanAcceptDropData(System.Windows.IDataObject data)
        {
            return TryGetDropTarget(data, out _, out _, out _);
        }

        private static bool TryGetDropTarget(System.Windows.IDataObject data, out string? value, out ActionType type, out string? errorMessage)
        {
            value = null;
            type = ActionType.Web;
            errorMessage = null;

            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                var droppedItems = data.GetData(DataFormats.FileDrop) as string[];
                if (droppedItems == null || droppedItems.Length == 0)
                {
                    errorMessage = LocalizationService.Get("Drop_ReadFailed");
                    return false;
                }

                if (droppedItems.Length > 1)
                {
                    errorMessage = LocalizationService.Get("Drop_OnlyOne");
                    return false;
                }

                string candidate = droppedItems[0];
                if (Directory.Exists(candidate))
                {
                    value = candidate;
                    type = ActionType.Folder;
                    return true;
                }

                if (!File.Exists(candidate))
                {
                    errorMessage = LocalizationService.Get("Drop_ExistingFilesFoldersOnly");
                    return false;
                }

                string extension = Path.GetExtension(candidate).ToLowerInvariant();
                if (extension == ".url")
                {
                    try
                    {
                        var lines = File.ReadAllLines(candidate);
                        var urlLine = lines.FirstOrDefault(line => line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrWhiteSpace(urlLine))
                        {
                            string url = urlLine[4..].Trim();
                            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                            {
                                value = url;
                                type = ActionType.Web;
                                return true;
                            }
                        }
                    }
                    catch (Exception ex) { Logger.Log(ex); }

                    errorMessage = LocalizationService.Get("Drop_UrlShortcutOnly");
                    return false;
                }

                if (ActionTargetHelper.IsScriptPath(candidate))
                {
                    value = candidate;
                    type = ActionType.ScriptFile;
                    return true;
                }

                if (ActionTargetHelper.IsProgramPath(candidate))
                {
                    value = candidate;
                    type = ActionType.Program;
                    return true;
                }

                value = candidate;
                type = ActionType.File;
                return true;
            }

            string? text = null;
            if (data.GetDataPresent(DataFormats.UnicodeText))
                text = (data.GetData(DataFormats.UnicodeText) as string)?.Trim();
            else if (data.GetDataPresent(DataFormats.Text))
                text = (data.GetData(DataFormats.Text) as string)?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                errorMessage = LocalizationService.Get("Drop_FileFolderOrUrlOnly");
                return false;
            }

            if (Uri.TryCreate(text, UriKind.Absolute, out var textUri) &&
                (textUri.Scheme == Uri.UriSchemeHttp || textUri.Scheme == Uri.UriSchemeHttps))
            {
                value = text;
                type = ActionType.Web;
                return true;
            }

            errorMessage = LocalizationService.Get("Drop_SupportedTargets");
            return false;
        }

        private async void Border_Drop(object sender, DragEventArgs e) {
            try {
                if (!TryGetDropTarget(e.Data, out string? val, out ActionType type, out string? errorMessage))
                {
                    new DarkDialog(errorMessage ?? LocalizationService.Get("Drop_NotSupported")) { Owner = this }.ShowDialog();
                    return;
                }

                if (!string.IsNullOrEmpty(val)) { 
                    string? iconPath = null;
                    bool isWeb = val.StartsWith("http", StringComparison.OrdinalIgnoreCase) || val.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
                    
                    if (isWeb && !val.StartsWith("http", StringComparison.OrdinalIgnoreCase)) val = "https://" + val;

                    if (type == ActionType.Program || type == ActionType.ScriptFile || type == ActionType.File)
                        iconPath = IconHelper.ExtractAndSaveIcon(val);

                    var newElement = new CustomElement { 
                        Id = Guid.NewGuid().ToString(),
                        Name = isWeb ? (Uri.TryCreate(val, UriKind.Absolute, out var uri) ? uri.Host : val) : Path.GetFileNameWithoutExtension(val), 
                        ActionValue = val, 
                        ActionType = type.ToString(),
                        ImagePath = iconPath ?? "",
                        Browser = isWeb ? BrowserHelper.GetSystemDefaultBrowser() : BrowserType.Chrome,
                        ContextId = _appSettings.ActiveContextId
                    };
                    
                    await _settingsService.SaveElementAsync(newElement);
                    RefreshPanel();

                    if (isWeb && string.IsNullOrEmpty(iconPath))
                    {
                        _ = Task.Run(async () => {
                            try
                            {
                                string? webIcon = await IconHelper.DownloadFaviconAsync(val);
                                if (!string.IsNullOrEmpty(webIcon))
                                {
                                    await Dispatcher.InvokeAsync(async () => {
                                        var el = _elements.FirstOrDefault(x => x.Id == newElement.Id);
                                        if (el != null)
                                        {
                                            await _settingsService.UpdateElementAsync(el.Id, element => element.ImagePath = webIcon);
                                            RefreshPanel();
                                        }
                                    });
                                }
                            }
                            catch (Exception ex) { Logger.Log(ex); }
                        });
                    }
                }
            } catch (Exception ex) { Logger.Log(ex); }
        }
        protected override void OnClosed(EventArgs e) { _nativeService?.Dispose(); _notifyIcon?.Dispose(); base.OnClosed(e); }
    }
}


