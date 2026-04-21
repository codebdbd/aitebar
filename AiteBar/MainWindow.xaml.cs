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
        private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
        private readonly string _configFile;
        private readonly string _settingsFile;
        private List<CustomElement> _elements = [];
        private AppSettings _appSettings = new();
        private System.Windows.Forms.NotifyIcon _notifyIcon = null!;
        private NativeMethods.LowLevelMouseProc? _mouseProc;
        private IntPtr _mouseHook = IntPtr.Zero;

        private Button? _draggedButton = null;
        private CustomElement? _draggedElement = null;
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

        private const int HOTKEY_ID = 9000;
        private const int HOTKEY_CONTEXT_NEXT_ID = 9001;
        private const int HOTKEY_CONTEXT_PREVIOUS_ID = 9002;
        private const int HOTKEY_CONTEXT1_ID = 9011;
        private const int HOTKEY_CONTEXT2_ID = 9012;
        private const int HOTKEY_CONTEXT3_ID = 9013;
        private const int HOTKEY_CONTEXT4_ID = 9014;
        private const int WM_HOTKEY = 0x0312;
        private const double PanelScreenPadding = 20;
        private const double ButtonPitch = PanelLayoutHelper.ButtonOuterSize;
        private const double DragHandleSpan = 18;
        public MainWindow()
        {
            InitializeComponent();
            this.Top = -2000; 

            this.SizeChanged += (s, e) => {
                if (!IsLoaded || _isAnimating)
                {
                    return;
                }

                PositionWindowImmediately(_shown);
            };
            
            PathHelper.EnsureDirectories();
            _configFile = PathHelper.ConfigFile;
            _settingsFile = PathHelper.SettingsFile;

            InitTrayIcon();

            Application.Current.Exit += (_, _) => {
                UninstallMouseHook();
                UnregisterGlobalHotkey();
            };
        }

        private void AttachSystemUtilityContextMenus()
        {
            BtnSearch.ContextMenu = BuildSystemUtilityContextMenu("Поиск", () => _appSettings.ShowPresetSearch = false);
            BtnScreenshot.ContextMenu = BuildSystemUtilityContextMenu("Скриншот", () => _appSettings.ShowPresetScreenshot = false);
            BtnRecord.ContextMenu = BuildSystemUtilityContextMenu("Видео", () => _appSettings.ShowPresetVideo = false);
            BtnCalc.ContextMenu = BuildSystemUtilityContextMenu("Калькулятор", () => _appSettings.ShowPresetCalc = false);
        }

        private ContextMenu BuildSystemUtilityContextMenu(string title, Action detachAction)
        {
            var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
            menu.Opened += (s, e) => _isElementContextMenuOpen = true;
            menu.Closed += (s, e) => _isElementContextMenuOpen = false;

            menu.Items.Add(CreateElementMenuItem(FluentGlyph(62979), "Открепить от панели", async (s, e) =>
            {
                await RunPanelInteractionAsync(async () =>
                {
                    detachAction();
                    await SaveConfig();
                    RefreshPanel();
                });
            }));

            return menu;
        }

        private async Task LoadSettings()
        {
            try
            {
                bool changed = false;
                if (File.Exists(_settingsFile))
                {
                    string json = await File.ReadAllTextAsync(_settingsFile);
                    _appSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
                    changed = NormalizeAppState();
                }
                else if (File.Exists(_configFile))
                {
                    string json = await File.ReadAllTextAsync(_configFile);
                    _appSettings.Elements = JsonSerializer.Deserialize<List<CustomElement>>(json) ?? [];
                    changed = NormalizeAppState();
                    await SaveAppSettings(_appSettings);
                }
                else
                {
                    changed = NormalizeAppState();
                }

                if (changed)
                {
                    await SaveAppSettings(_appSettings);
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public async Task SaveAppSettings(AppSettings settings)
        {
            await _saveSemaphore.WaitAsync();
            try {
                _appSettings = settings;
                _appSettings.Elements = [.. _elements];
                string json = JsonSerializer.Serialize(_appSettings, _jsonOptions);
                await File.WriteAllTextAsync(_settingsFile, json);
                RegisterGlobalHotkey();
            }
            catch (Exception ex) { Logger.Log(ex); }
            finally { _saveSemaphore.Release(); }
        }

        public AppSettings GetAppSettings() => _appSettings;

        public IReadOnlyList<PanelContext> GetContextsSnapshot() =>
            [.. _appSettings.Contexts.Select(context => new PanelContext { Id = context.Id, Name = context.Name })];

        public string GetContextDisplayName(string contextId)
        {
            return _appSettings.Contexts.FirstOrDefault(context => string.Equals(context.Id, contextId, StringComparison.Ordinal))?.Name
                ?? contextId;
        }

        private string GetPrimaryContextId()
        {
            return _appSettings.Contexts.FirstOrDefault()?.Id ?? ContextStateHelper.GetDefaultContextId(0);
        }

        private bool ShouldShowSystemUtilsForContext(string? contextId = null)
        {
            string targetContextId = string.IsNullOrWhiteSpace(contextId) ? _appSettings.ActiveContextId : contextId;
            return string.Equals(targetContextId, GetPrimaryContextId(), StringComparison.Ordinal);
        }

        private bool NormalizeAppState()
        {
            bool changed = false;

            var originalContexts = _appSettings.Contexts ?? [];
            var normalizedContexts = ContextStateHelper.NormalizeContexts(originalContexts);
            if (originalContexts.Count != normalizedContexts.Count ||
                originalContexts.Zip(normalizedContexts, (left, right) => left.Id != right.Id || left.Name != right.Name).Any(hasDifference => hasDifference))
            {
                changed = true;
            }
            _appSettings.Contexts = normalizedContexts;

            string normalizedActiveContextId = ContextStateHelper.NormalizeActiveContextId(_appSettings.ActiveContextId, _appSettings.Contexts);
            if (!string.Equals(_appSettings.ActiveContextId, normalizedActiveContextId, StringComparison.Ordinal))
            {
                _appSettings.ActiveContextId = normalizedActiveContextId;
                changed = true;
            }

            var normalizedElements = NormalizeElements(_appSettings.Elements, GetPrimaryContextId());
            if (_appSettings.Elements.Count != normalizedElements.Count)
            {
                changed = true;
            }
            else
            {
                for (int i = 0; i < normalizedElements.Count; i++)
                {
                    if (!AreElementsEquivalent(_appSettings.Elements[i], normalizedElements[i]))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            _elements = normalizedElements;
            _appSettings.Elements = [.. normalizedElements];

            return changed;
        }

        private static bool AreElementsEquivalent(CustomElement left, CustomElement right)
        {
            return left.Id == right.Id &&
                   left.Name == right.Name &&
                   left.Icon == right.Icon &&
                   left.IconFont == right.IconFont &&
                   left.Color == right.Color &&
                   left.ActionType == right.ActionType &&
                   left.ActionValue == right.ActionValue &&
                   left.Browser == right.Browser &&
                   left.ChromeProfile == right.ChromeProfile &&
                   left.IsAppMode == right.IsAppMode &&
                   left.IsIncognito == right.IsIncognito &&
                   left.UseRotation == right.UseRotation &&
                   left.OpenFullscreen == right.OpenFullscreen &&
                   left.LastUsedProfile == right.LastUsedProfile &&
                   left.Alt == right.Alt &&
                   left.Ctrl == right.Ctrl &&
                   left.Shift == right.Shift &&
                   left.Win == right.Win &&
                   left.Key == right.Key &&
                   left.ImagePath == right.ImagePath &&
                   left.ContextId == right.ContextId;
        }

        private static int WrapIndex(int index, int count)
        {
            if (count == 0)
            {
                return 0;
            }

            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        private async Task SwitchActiveContextAsync(int direction)
        {
            if (_appSettings.Contexts.Count == 0)
            {
                return;
            }

            int currentIndex = _appSettings.Contexts.FindIndex(context => string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = ContextStateHelper.WrapIndex(currentIndex + direction, _appSettings.Contexts.Count);
            string nextContextId = _appSettings.Contexts[nextIndex].Id;
            if (string.Equals(_appSettings.ActiveContextId, nextContextId, StringComparison.Ordinal))
            {
                return;
            }

            _appSettings.ActiveContextId = nextContextId;
            _pendingContextAnimationDirection = Math.Sign(direction);
            RefreshPanel();
            await SaveConfig();
        }

        private void ActivateContextRelative(int direction)
        {
            if (_appSettings.Contexts.Count == 0)
            {
                return;
            }

            int currentIndex = _appSettings.Contexts.FindIndex(context => string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = ContextStateHelper.WrapIndex(currentIndex + direction, _appSettings.Contexts.Count);
            string nextContextId = _appSettings.Contexts[nextIndex].Id;
            if (string.Equals(_appSettings.ActiveContextId, nextContextId, StringComparison.Ordinal))
            {
                return;
            }

            _appSettings.ActiveContextId = nextContextId;
            _pendingContextAnimationDirection = Math.Sign(direction);
            RefreshPanel();
            _ = SaveConfig();
        }

        private void ActivateContextByIndex(int index)
        {
            if (index < 0 || index >= _appSettings.Contexts.Count)
            {
                return;
            }

            int currentIndex = _appSettings.Contexts.FindIndex(context => string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            string nextContextId = _appSettings.Contexts[index].Id;
            if (string.Equals(_appSettings.ActiveContextId, nextContextId, StringComparison.Ordinal))
            {
                return;
            }

            _appSettings.ActiveContextId = nextContextId;
            _pendingContextAnimationDirection = index >= currentIndex ? 1 : -1;
            RefreshPanel();
            _ = SaveConfig();
        }

        private void BuildPanelContextMenu()
        {
            var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };

            foreach (var context in _appSettings.Contexts)
            {
                bool isActive = string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal);
                var item = new MenuItem
                {
                    Header = context.Name,
                    Style = (Style)FindResource("DarkMenuItem")
                };

                if (isActive)
                {
                    item.Icon = new System.Windows.Controls.TextBlock
                    {
                        Text = FluentGlyph(62261), // Accept/Checkmark
                        FontFamily = MenuIconFont,
                        FontSize = 18,
                        Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#007ACC")),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                    };
                }

                string targetContextId = context.Id;
                item.Click += (s, e) => ActivateContextById(targetContextId);
                menu.Items.Add(item);
            }

            RootBorder.ContextMenu = menu;
        }

        private ContextMenu BuildElementContextMenu(CustomElement element)
        {
            var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
            menu.Opened += (s, e) => _isElementContextMenuOpen = true;
            menu.Closed += (s, e) => _isElementContextMenuOpen = false;

            menu.Items.Add(CreateElementMenuItem(FluentGlyph(62034), "Редактировать", (s, e) =>
            {
                RunPanelInteraction(() => new SettingsWindow(this, element) { Owner = this }.ShowDialog());
            }));

            menu.Items.Add(CreateElementMenuItem(FluentGlyph(62251), "Дублировать", async (s, e) =>
            {
                await RunPanelInteractionAsync(() => DuplicateElementAsync(element));
            }));

            menu.Items.Add(CreateElementMenuItem(FluentGlyph(63081), "Переименовать", async (s, e) =>
            {
                await RunPanelInteractionAsync(() => RenameElementAsync(element));
            }));

            var moveTargets = _appSettings.Contexts
                .Where(context => !string.Equals(context.Id, element.ContextId, StringComparison.Ordinal))
                .Select(context => CreateElementMenuItem(FluentGlyph(61837), context.Name, async (s, e) =>
                {
                    await RunPanelInteractionAsync(() => MoveElementToContextAsync(element.Id, context.Id));
                }))
                .ToList();

            if (moveTargets.Count > 0)
            {
                var moveMenu = new MenuItem
                {
                    Header = "Переместить",
                    Icon = new System.Windows.Controls.TextBlock
                    {
                        Text = FluentGlyph(61837),
                        FontFamily = MenuIconFont,
                        FontSize = 18,
                        Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#E3E3E3")),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                    },
                    Style = (Style)FindResource("DarkMenuItem")
                };

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
                menu.Items.Add(CreateElementMenuItem(FluentGlyph(59537), "Открыть расположение", async (s, e) =>
                {
                    await RunPanelInteractionAsync(() => OpenElementLocationAsync(element));
                }));
            }

            menu.Items.Add(CreateElementMenuItem(FluentGlyph(62284), "Удалить", async (s, e) =>
            {
                await RunPanelInteractionAsync(() => DeleteElementAsync(element));
            }, isDanger: true));

            return menu;
        }

        private MenuItem CreateElementMenuItem(string glyph, string text, RoutedEventHandler onClick, bool isDanger = false)
        {
            var accentBrush = isDanger
                ? new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#FF5252"))
                : new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#E3E3E3"));

            var item = new MenuItem
            {
                Header = text,
                Icon = new System.Windows.Controls.TextBlock
                {
                    Text = glyph,
                    FontFamily = MenuIconFont,
                    FontSize = 18,
                    Foreground = accentBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                },
                Style = (Style)FindResource("DarkMenuItem")
            };

            if (isDanger)
            {
                item.Foreground = accentBrush;
            }

            item.Click += onClick;
            return item;
        }

        private async Task DuplicateElementAsync(CustomElement source)
        {
            var duplicate = CloneElement(source);
            duplicate.Id = Guid.NewGuid().ToString();
            duplicate.Name = BuildDuplicateElementName(source.Name);
            duplicate.LastUsedProfile = "";

            int sourceIndex = _elements.FindIndex(x => string.Equals(x.Id, source.Id, StringComparison.Ordinal));
            if (sourceIndex >= 0)
            {
                _elements.Insert(sourceIndex + 1, duplicate);
            }
            else
            {
                _elements.Add(duplicate);
            }

            await SaveConfig();
            RefreshPanel();
            new SettingsWindow(this, duplicate) { Owner = this }.ShowDialog();
        }

        private string BuildDuplicateElementName(string sourceName)
        {
            string baseName = string.IsNullOrWhiteSpace(sourceName) ? "Новая кнопка" : sourceName.Trim();
            string firstCandidate = $"{baseName} (копия)";
            if (_elements.All(x => !string.Equals(x.Name, firstCandidate, StringComparison.OrdinalIgnoreCase)))
            {
                return firstCandidate;
            }

            for (int index = 2; ; index++)
            {
                string candidate = $"{baseName} (копия {index})";
                if (_elements.All(x => !string.Equals(x.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }
        }

        private async Task RenameElementAsync(CustomElement source)
        {
            var elementToRename = _elements.FirstOrDefault(x => string.Equals(x.Id, source.Id, StringComparison.Ordinal));
            if (elementToRename == null)
            {
                return;
            }

            var dialog = new TextPromptDialog("Переименовать кнопку", "Новое имя", elementToRename.Name) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string newName = dialog.Value.Trim();
            if (string.Equals(elementToRename.Name, newName, StringComparison.Ordinal))
            {
                return;
            }

            elementToRename.Name = newName;
            await SaveConfig();
            RefreshPanel();
        }

        private async Task MoveElementToContextAsync(string elementId, string targetContextId)
        {
            var elementToMove = _elements.FirstOrDefault(x => string.Equals(x.Id, elementId, StringComparison.Ordinal));
            if (elementToMove == null || string.Equals(elementToMove.ContextId, targetContextId, StringComparison.Ordinal))
            {
                return;
            }

            elementToMove.ContextId = targetContextId;
            await SaveConfig();
            RefreshPanel();
        }

        private async Task DeleteElementAsync(CustomElement source)
        {
            var elementToDelete = _elements.FirstOrDefault(x => string.Equals(x.Id, source.Id, StringComparison.Ordinal));
            if (elementToDelete == null)
            {
                return;
            }

            var dialog = new DarkDialog($"Удалить '{elementToDelete.Name}'?", isConfirm: true) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _elements.RemoveAll(x => string.Equals(x.Id, elementToDelete.Id, StringComparison.Ordinal));
            await SaveConfig();
            RefreshPanel();
        }

        private bool TryCreateCopyActionMenuItem(CustomElement element, out MenuItem menuItem)
        {
            menuItem = null!;

            if (!TryGetCopyValue(element, out var caption, out var value))
            {
                return false;
            }

            menuItem = CreateElementMenuItem(FluentGlyph(62153), caption, (s, e) =>
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
                    caption = "Копировать URL";
                    value = element.ActionValue;
                    return true;
                case ActionType.Program:
                case ActionType.File:
                case ActionType.Folder:
                case ActionType.ScriptFile:
                    caption = "Копировать путь";
                    value = element.ActionValue;
                    return true;
                case ActionType.Command:
                    caption = "Копировать команду";
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

            int currentIndex = _appSettings.Contexts.FindIndex(context => string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int index = _appSettings.Contexts.FindIndex(context => string.Equals(context.Id, contextId, StringComparison.Ordinal));
            if (index < 0)
            {
                return;
            }

            _appSettings.ActiveContextId = contextId;
            _pendingContextAnimationDirection = index >= currentIndex ? 1 : -1;
            RefreshPanel();
            _ = SaveConfig();
        }

        private void ReorderActiveContextElements(int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= _activeContextElements.Count || targetIndex < 0 || targetIndex >= _activeContextElements.Count)
            {
                return;
            }

            var reordered = _activeContextElements.ToList();
            var moved = reordered[sourceIndex];
            reordered.RemoveAt(sourceIndex);
            reordered.Insert(targetIndex, moved);

            string activeContextId = _appSettings.ActiveContextId;
            var queue = new Queue<CustomElement>(reordered);

            for (int i = 0; i < _elements.Count; i++)
            {
                if (string.Equals(_elements[i].ContextId, activeContextId, StringComparison.Ordinal))
                {
                    _elements[i] = queue.Dequeue();
                }
            }
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
                contextCounts: _appSettings.Contexts.Select(context => _elements.Count(element => string.Equals(element.ContextId, context.Id, StringComparison.Ordinal))).ToList(),
                activeContextIndex: Math.Max(0, _appSettings.Contexts.FindIndex(context => string.Equals(context.Id, _appSettings.ActiveContextId, StringComparison.Ordinal))),
                systemContextIndex: 0);

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
            FixedPanel.Width = metrics.FixedWidth;
            FixedPanel.Height = metrics.FixedHeight;
            UserButtonsPanel.Width = metrics.UserWidth;
            UserButtonsPanel.Height = metrics.UserHeight;
            UserButtonsPanel.MaxWidth = metrics.UserWidth;
            UserButtonsPanel.MaxHeight = metrics.UserHeight;
            UserButtonsPanel.MinWidth = metrics.UserWidth;
            UserButtonsPanel.MinHeight = metrics.UserHeight;
        }

        private int GetVisibleSystemButtonCount()
        {
            int count = 0;
            if (_appSettings.ShowPresetSearch) count++;
            if (_appSettings.ShowPresetScreenshot) count++;
            if (_appSettings.ShowPresetVideo) count++;
            if (_appSettings.ShowPresetCalc) count++;
            return count;
        }

        private void RegisterGlobalHotkey()
        {
            UnregisterGlobalHotkey();
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            uint modifiers = 0;
            if (_appSettings.GlobalHotkeyCtrl) modifiers |= 0x0002;
            if (_appSettings.GlobalHotkeyAlt) modifiers |= 0x0001;
            if (_appSettings.GlobalHotkeyShift) modifiers |= 0x0004;
            if (_appSettings.GlobalHotkeyWin) modifiers |= 0x0008;

            if (Enum.TryParse(typeof(Key), _appSettings.GlobalHotkeyKey, out var k))
            {
                uint vk = (uint)KeyInterop.VirtualKeyFromKey((Key)k!);
                NativeMethods.RegisterHotKey(hwnd, HOTKEY_ID, modifiers, vk);
            }

            RegisterHotkeyBinding(hwnd, HOTKEY_CONTEXT_NEXT_ID, _appSettings.NextContextHotkey);
            RegisterHotkeyBinding(hwnd, HOTKEY_CONTEXT_PREVIOUS_ID, _appSettings.PreviousContextHotkey);
            RegisterHotkeyBinding(hwnd, HOTKEY_CONTEXT1_ID, _appSettings.Context1Hotkey);
            RegisterHotkeyBinding(hwnd, HOTKEY_CONTEXT2_ID, _appSettings.Context2Hotkey);
            RegisterHotkeyBinding(hwnd, HOTKEY_CONTEXT3_ID, _appSettings.Context3Hotkey);
            RegisterHotkeyBinding(hwnd, HOTKEY_CONTEXT4_ID, _appSettings.Context4Hotkey);
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
                HOTKEY_CONTEXT1_ID,
                HOTKEY_CONTEXT2_ID,
                HOTKEY_CONTEXT3_ID,
                HOTKEY_CONTEXT4_ID
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
                _shown = !_shown;
                Toggle(!_shown);
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
                    case HOTKEY_CONTEXT1_ID:
                        ActivateContextByIndex(0);
                        handled = true;
                        break;
                    case HOTKEY_CONTEXT2_ID:
                        ActivateContextByIndex(1);
                        handled = true;
                        break;
                    case HOTKEY_CONTEXT3_ID:
                        ActivateContextByIndex(2);
                        handled = true;
                        break;
                    case HOTKEY_CONTEXT4_ID:
                        ActivateContextByIndex(3);
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
                    if (!_shown) { _shown = true; Toggle(false); }
                }
                else if (e.Button == System.Windows.Forms.MouseButtons.Right) {
                    ShowTrayContextMenu();
                }
            };
        }

        private void ShowTrayContextMenu()
        {
            var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };

            menu.Items.Add(CreateTrayMenuItem(FluentGlyph(61453), "Открыть", (s, e) => { if (!_shown) { _shown = true; Toggle(false); } }));
            menu.Items.Add(CreateTrayMenuItem(FluentGlyph(62135), "Настройки программы", (s, e) => new AppSettingsWindow(this) { Owner = this }.ShowDialog()));
            menu.Items.Add(CreateTrayMenuItem(FluentGlyph(59718), "О программе", (s, e) => new AboutWindow { Owner = this }.ShowDialog()));
            menu.Items.Add(CreateTrayMenuItem(FluentGlyph(59613), "Справка", (s, e) => OpenUrl("https://codebdbd.github.io/intro/en/products/aitebar/guide.html")));
            menu.Items.Add(CreateTrayMenuItem(FluentGlyph(60049), "Поддержать автора", (s, e) => OpenUrl("https://codebdbd.github.io/intro/en/pages/donate.html")));
            menu.Items.Add(CreateTrayMenuItem(FluentGlyph(62284), "Закрыть и выйти", (s, e) => { _notifyIcon.Dispose(); Application.Current.Shutdown(); }));

            // Для того чтобы ContextMenu закрывалось при клике мимо, 
            // его нужно привязать к невидимому элементу или использовать Placement.
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;

            // Важный хак для WPF ContextMenu в трее: фокус окна
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            NativeMethods.SetForegroundWindow(hwnd);
        }

        private MenuItem CreateTrayMenuItem(string glyph, string text, RoutedEventHandler onClick)
        {
            var item = new MenuItem
            {
                Header = text,
                Icon = new System.Windows.Controls.TextBlock
                {
                    Text = glyph,
                    FontFamily = MenuIconFont,
                    FontSize = 16,
                    Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#E3E3E3")),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                },
                Style = (Style)FindResource("DarkMenuItem")
            };
            item.Click += onClick;
            return item;
        }

        private void InstallMouseHook()
        {
            try
            {
                if (_mouseHook != IntPtr.Zero) return;
                _mouseProc = MouseHookCallback;
                using var curProcess = Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule ?? throw new InvalidOperationException("MainModule is null");
                _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, NativeMethods.GetModuleHandle(curModule.ModuleName!), 0);
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        private void UninstallMouseHook()
        {
            if (_mouseHook == IntPtr.Zero) return;
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN && _shown && !_isAnimating && !IsPanelInteractionActive)
                {
                    var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    double x = hookStruct.pt.X, y = hookStruct.pt.Y;
                    if (x < _panelLeft || x > _panelRight || y < _panelTop || y > _panelBottom)
                    {
                        this.Dispatcher.InvokeAsync(async () => await HideDock());
                    }
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private bool IsPanelInteractionActive => _isElementContextMenuOpen || _isBlockingPanelInteraction;

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

            // Используем DesiredSize если ActualWidth еще не рассчитан (0)
            double width = ActualWidth > 0 ? ActualWidth : (RootBorder?.DesiredSize.Width ?? 0);
            double height = ActualHeight > 0 ? ActualHeight : (RootBorder?.DesiredSize.Height ?? 0);
            
            // Если все еще 0, пробуем запустить Measure
            if ((width == 0 || height == 0) && RootBorder != null)
            {
                RootBorder.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                width = RootBorder.DesiredSize.Width;
                height = RootBorder.DesiredSize.Height;
            }

            // Защита от нулевых размеров
            if (width == 0) width = 200;
            if (height == 0) height = 50;

            double centeredX = workArea.Left + Math.Max(0, (workArea.Width - width) / 2);
            double centeredY = workArea.Top + Math.Max(0, (workArea.Height - height) / 2);

            return _appSettings.Edge switch
            {
                DockEdge.Top => (centeredX, hide ? bounds.Top - height : workArea.Top),
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
                AttachSystemUtilityContextMenus();
                await LoadSettings();
                RegisterGlobalHotkey();
                RefreshPanel();
                PositionWindowImmediately(_shown);
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
                        
                        // Параметры из настроек
                        double zoneSizePercent = _appSettings.ActivationZoneSizePercent / 100.0;
                        int delayMs = _appSettings.ActivationDelayMs;
                        
                        bool inActivationZone = false;
                        
                        switch (_appSettings.Edge)
                        {
                            case DockEdge.Top:
                                inActivationZone = pt.Y == screenTop && pt.X > (screenLeft + screenWidth * (0.5 - zoneSizePercent/2)) && pt.X < (screenLeft + screenWidth * (0.5 + zoneSizePercent/2));
                                break;
                            case DockEdge.Bottom:
                                inActivationZone = pt.Y >= screenTop + screenHeight - 1 && pt.X > (screenLeft + screenWidth * (0.5 - zoneSizePercent/2)) && pt.X < (screenLeft + screenWidth * (0.5 + zoneSizePercent/2));
                                break;
                            case DockEdge.Left:
                                inActivationZone = pt.X == screenLeft && pt.Y > (screenTop + screenHeight * (0.5 - zoneSizePercent/2)) && pt.Y < (screenTop + screenHeight * (0.5 + zoneSizePercent/2));
                                break;
                            case DockEdge.Right:
                                inActivationZone = pt.X >= screenLeft + screenWidth - 1 && pt.Y > (screenTop + screenHeight * (0.5 - zoneSizePercent/2)) && pt.Y < (screenTop + screenHeight * (0.5 + zoneSizePercent/2));
                                break;
                        }

                        if (inActivationZone && !_shown) {
                            if (_hoverStartTime == null) _hoverStartTime = DateTime.Now;
                            else if ((DateTime.Now - _hoverStartTime.Value).TotalMilliseconds >= delayMs) {
                                _shown = true; _hoverStartTime = null; Toggle(false);
                            }
                        } else _hoverStartTime = null;
                    }
                };
                _timer.Start();
                InstallMouseHook();
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        private void UpdateOrientation()
        {
            bool isVertical = _appSettings.Edge == DockEdge.Left || _appSettings.Edge == DockEdge.Right;
            var orientation = isVertical ? System.Windows.Controls.Orientation.Vertical : System.Windows.Controls.Orientation.Horizontal;

            if (isVertical) { this.MinWidth = 0; this.MinHeight = 150; }
            else { this.MinWidth = 150; this.MinHeight = 0; }

            System.Windows.Controls.DockPanel.SetDock(DragHandle, isVertical ? System.Windows.Controls.Dock.Top : System.Windows.Controls.Dock.Left);
            FixedPanel.Orientation = orientation;
            UserButtonsPanel.Orientation = orientation;
            SystemUtilsPanel.Orientation = orientation;
            ControlBlock.Orientation = orientation;
            System.Windows.Controls.DockPanel.SetDock(FixedPanel, isVertical ? System.Windows.Controls.Dock.Top : System.Windows.Controls.Dock.Left);

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

            var separators = new[] { SepSystem, SepControl };
            foreach (var sep in separators)
            {
                if (isVertical) { sep.Width = 20; sep.Height = 1; sep.Margin = new Thickness(0, 6, 0, 6); }
                else { sep.Width = 1; sep.Height = 20; sep.Margin = new Thickness(6, 0, 6, 0); }
            }

            ApplyPanelSizeConstraints();
            PositionWindowImmediately(_shown);
        }

        public void RefreshPanel() {
            UpdateOrientation();
            UserButtonsPanel.Children.Clear();
            _userButtons.Clear();

            NormalizeAppState();
            BuildPanelContextMenu();
            string activeContextId = _appSettings.ActiveContextId;
            _activeContextElements = _elements
                .Where(element => string.Equals(element.ContextId, activeContextId, StringComparison.Ordinal))
                .ToList();
            
            bool showSystemUtils = ShouldShowSystemUtilsForContext(activeContextId);
            BtnSearch.Visibility = showSystemUtils && _appSettings.ShowPresetSearch ? Visibility.Visible : Visibility.Collapsed;
            BtnScreenshot.Visibility = showSystemUtils && _appSettings.ShowPresetScreenshot ? Visibility.Visible : Visibility.Collapsed;
            BtnRecord.Visibility = showSystemUtils && _appSettings.ShowPresetVideo ? Visibility.Visible : Visibility.Collapsed;
            BtnCalc.Visibility = showSystemUtils && _appSettings.ShowPresetCalc ? Visibility.Visible : Visibility.Collapsed;

            // Видимость зоны системных утилит
            bool hasSystemUtils = BtnSearch.Visibility == Visibility.Visible || BtnScreenshot.Visibility == Visibility.Visible || 
                                 BtnRecord.Visibility == Visibility.Visible || BtnCalc.Visibility == Visibility.Visible;
            SystemUtilsPanel.Visibility = hasSystemUtils ? Visibility.Visible : Visibility.Collapsed;

            foreach (var el in _activeContextElements) {
                var btn = new Button { 
                    ToolTip = el.Name, 
                    Foreground = (Brush)_brushConverter.ConvertFromString(el.Color ?? "#E3E3E3")!,
                    RenderTransform = new TranslateTransform()
                };

                if (!string.IsNullOrEmpty(el.ImagePath) && System.IO.File.Exists(el.ImagePath))
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(el.ImagePath);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    btn.Content = new System.Windows.Controls.Image { Source = bitmap, Width = 24, Height = 24, Stretch = Stretch.Uniform };
                }
                else { btn.Content = el.Icon; btn.FontFamily = FontHelper.Resolve(el.IconFont); }
                
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
                            ReorderActiveContextElements(_draggedOriginalIndex, newIndex);
                            await SaveConfig();
                        }
                        RefreshPanel();
                    } else {
                        // Если это был просто клик (не реордеринг), выполняем действие
                        await ExecuteCustomAction(capturedElement);
                    }
                    _draggedButton = null; _draggedElement = null; _isReordering = false;
                };

                btn.ContextMenu = BuildElementContextMenu(el);
                UserButtonsPanel.Children.Add(btn);
                _userButtons.Add(btn);
            }
            
            // Разделители
            SepSystem.Visibility = hasSystemUtils ? Visibility.Visible : Visibility.Collapsed;
            SepControl.Visibility = UserButtonsPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            AnimateContextTransitionIfNeeded();
            
            UpdatePanelBounds();
        }

        private static DockEdge GetClosestDockEdge(System.Drawing.Rectangle workArea, int cursorX, int cursorY)
        {
            var distances = new Dictionary<DockEdge, int>
            {
                [DockEdge.Top] = Math.Abs(cursorY - workArea.Top),
                [DockEdge.Bottom] = Math.Abs(workArea.Bottom - cursorY),
                [DockEdge.Left] = Math.Abs(cursorX - workArea.Left),
                [DockEdge.Right] = Math.Abs(workArea.Right - cursorX)
            };

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
                ? (Brush)_brushConverter.ConvertFromString("#007ACC")!
                : (Brush)_brushConverter.ConvertFromString("#35FFFFFF")!;
        }

        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_shown)
            {
                return;
            }

            _isPanelDragging = true;
            _panelDragChanged = false;
            _dragStartEdge = _appSettings.Edge;
            _dragStartMonitorIndex = _appSettings.MonitorIndex;
            DragHandle.CaptureMouse();
            SetDragHandleActive(true);
            e.Handled = true;
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
            DockEdge nextEdge = GetClosestDockEdge(targetScreen.WorkingArea, pt.X, pt.Y);

            if (_appSettings.MonitorIndex == nextMonitorIndex && _appSettings.Edge == nextEdge)
            {
                return;
            }

            _appSettings.MonitorIndex = nextMonitorIndex;
            _appSettings.Edge = nextEdge;
            _panelDragChanged = true;
            UpdateOrientation();
            PositionWindowImmediately(shown: true);
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

            if (_panelDragChanged)
            {
                await SaveConfig();
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

        private async Task SaveConfig() { await SaveAppSettings(_appSettings); }

        public async Task SaveElement(CustomElement updated, string? removeId = null)
        {
            if (removeId != null && !string.Equals(removeId, updated.Id, StringComparison.Ordinal))
                _elements.RemoveAll(x => x.Id == removeId);
            var existing = _elements.FirstOrDefault(x => x.Id == updated.Id);
            if (existing != null) _elements[_elements.IndexOf(existing)] = updated;
            else _elements.Add(updated);
            await SaveConfig(); RefreshPanel();
        }

        public IReadOnlyList<CustomElement> GetElementsSnapshot() => [.. _elements.Select(CloneElement)];

        private static CustomElement CloneElement(CustomElement s) => new() {
            Id = s.Id, Name = s.Name, Icon = s.Icon, IconFont = s.IconFont, Color = s.Color,
            ImagePath = s.ImagePath,
            ActionType = s.ActionType, ActionValue = s.ActionValue, Browser = s.Browser, ChromeProfile = s.ChromeProfile,
            IsAppMode = s.IsAppMode, IsIncognito = s.IsIncognito, UseRotation = s.UseRotation, OpenFullscreen = s.OpenFullscreen,
            IsTopmost = s.IsTopmost, LastUsedProfile = s.LastUsedProfile,
            Alt = s.Alt, Ctrl = s.Ctrl, Shift = s.Shift, Win = s.Win, Key = s.Key,
            ContextId = s.ContextId
        };

        private static List<CustomElement> NormalizeElements(IEnumerable<CustomElement> source, string defaultContextId)
        {
            var result = new List<CustomElement>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in source) {
                if (item == null) continue;
                string id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString() : item.Id;
                if (!seen.Add(id)) continue;
                string contextId = string.IsNullOrWhiteSpace(item.ContextId) ? defaultContextId : item.ContextId;
                result.Add(new CustomElement {
                    Id = id,
                    Name = item.Name ?? "", Icon = string.IsNullOrWhiteSpace(item.Icon) ? "\uE710" : item.Icon,
                    IconFont = string.IsNullOrWhiteSpace(item.IconFont) ? FontHelper.FluentKey : item.IconFont,
                    Color = string.IsNullOrWhiteSpace(item.Color) ? "#E3E3E3" : item.Color,
                    ImagePath = item.ImagePath ?? "",
                    ActionType = ActionTargetHelper.NormalizeActionType(item.ActionType ?? "", item.ActionValue ?? ""),
                    ActionValue = item.ActionValue ?? "", Browser = item.Browser, ChromeProfile = item.ChromeProfile ?? "",
                    IsAppMode = item.IsAppMode, IsIncognito = item.IsIncognito, UseRotation = item.UseRotation, OpenFullscreen = item.OpenFullscreen,
                    IsTopmost = item.IsTopmost, LastUsedProfile = item.LastUsedProfile ?? "",
                    Alt = item.Alt, Ctrl = item.Ctrl, Shift = item.Shift, Win = item.Win, Key = item.Key ?? "None",
                    ContextId = contextId
                });
            }
            return result;
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

            var animX = new DoubleAnimation(finalX, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var animY = new DoubleAnimation(finalY, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            
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

        private async Task HideDock() { if (_shown) { _shown = false; Toggle(true); await Task.Delay(250); } }

        private async void RootBorder_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_isAnimating || !_shown)
            {
                return;
            }

            if (e.Delta == 0)
            {
                return;
            }

            e.Handled = true;
            int direction = e.Delta > 0 ? -1 : 1;
            await SwitchActiveContextAsync(direction);
        }

        private static string FindExecutableOnPath(string fileName) {
            var pathValue = Environment.GetEnvironmentVariable("PATH"); if (string.IsNullOrWhiteSpace(pathValue)) return fileName;
            foreach (var dir in pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
                try { var candidate = Path.Combine(dir.Trim(), fileName); if (File.Exists(candidate)) return candidate; } catch { }
            }
            return fileName;
        }

        private static ProcessStartInfo CreateScriptProcessStartInfo(string scriptPath) {
            string workingDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory;
            string extension = Path.GetExtension(scriptPath).ToLowerInvariant();
            switch (extension) {
                case ".bat": case ".cmd":
                    var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, WorkingDirectory = workingDirectory };
                    psi.ArgumentList.Add("/c"); psi.ArgumentList.Add(scriptPath); return psi;
                case ".ps1":
                    string shell = FindExecutableOnPath("pwsh.exe"); if (!File.Exists(shell)) shell = FindExecutableOnPath("powershell.exe");
                    var psiPs = new ProcessStartInfo(shell) { UseShellExecute = false, WorkingDirectory = workingDirectory };
                    psiPs.ArgumentList.Add("-NoProfile"); if (Path.GetFileName(shell).Equals("powershell.exe", StringComparison.OrdinalIgnoreCase)) {
                        psiPs.ArgumentList.Add("-ExecutionPolicy"); psiPs.ArgumentList.Add("Bypass"); }
                    psiPs.ArgumentList.Add("-File"); psiPs.ArgumentList.Add(scriptPath); return psiPs;
                case ".py":
                    string pythonExe = FindExecutableOnPath("python.exe"); if (!File.Exists(pythonExe)) throw new InvalidOperationException("Python не найден.");
                    return new ProcessStartInfo("cmd.exe") 
                    { 
                        UseShellExecute = false, 
                        WorkingDirectory = workingDirectory,
                        Arguments = $"/c \"\"{pythonExe}\" \"{scriptPath}\"\"" 
                    };
                default: throw new InvalidOperationException("Неподдерживаемый скрипт.");
            }
        }

        private static Task StartScriptFile(string scriptPath) {
            var psi = CreateScriptProcessStartInfo(scriptPath);
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Запуск не удался.");
            return Task.CompletedTask;
        }

        private static void SendVirtualKey(byte virtualKey)
        {
            NativeMethods.INPUT[] inputs =
            [
                new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = virtualKey } }
                },
                new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = virtualKey, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
                }
            ];

            _ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        }

        private static async Task TryEnterFullscreenAsync(Process proc)
        {
            for (int i = 0; i < 25; i++)
            {
                await Task.Delay(200);
                proc.Refresh();
                if (proc.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                NativeMethods.SetForegroundWindow(proc.MainWindowHandle);
                await Task.Delay(100);
                SendVirtualKey((byte)KeyInterop.VirtualKeyFromKey(Key.F11));
                break;
            }
        }


        private async Task ExecuteCustomAction(CustomElement el) {
            try {
                await HideDock();
                if (Enum.TryParse<AiteBar.ActionType>(el.ActionType, out var actionType)) {
                    switch (actionType) {
                        case AiteBar.ActionType.Hotkey:
                            var downKeys = new List<byte>(); if (el.Ctrl) downKeys.Add(NativeMethods.VK_CONTROL); if (el.Shift) downKeys.Add(NativeMethods.VK_SHIFT); if (el.Alt) downKeys.Add(NativeMethods.VK_MENU); if (el.Win) downKeys.Add(NativeMethods.VK_LWIN);
                            byte mainVk = 0; if (Enum.TryParse(typeof(Key), el.Key, out var k)) mainVk = (byte)KeyInterop.VirtualKeyFromKey((Key)k!);
                            var inputs = new List<NativeMethods.INPUT>(); foreach (var vk in downKeys) inputs.Add(new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = vk } } });
                            if (mainVk != 0) { inputs.Add(new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = mainVk } } });
                                inputs.Add(new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = mainVk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } } }); }
                            foreach (var vk in Enumerable.Reverse(downKeys)) inputs.Add(new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } } });
                            _ = NativeMethods.SendInput((uint)inputs.Count, [.. inputs], Marshal.SizeOf<NativeMethods.INPUT>()); break;
                        case AiteBar.ActionType.Web:
                            string prof = el.UseRotation ? (BrowserHelper.AdvanceProfile(el.Browser, el.LastUsedProfile)) : el.ChromeProfile; el.LastUsedProfile = prof; await SaveConfig();
                            var psi = new ProcessStartInfo(BrowserHelper.GetExecutablePath(el.Browser)) { UseShellExecute = false };
                            if (el.IsAppMode) psi.ArgumentList.Add($"--app={el.ActionValue}"); else psi.ArgumentList.Add(el.ActionValue);
                            
                            if (el.IsIncognito) 
                            {
                                if (el.Browser == BrowserType.Edge) psi.ArgumentList.Add("-inprivate");
                                else if (el.Browser == BrowserType.Opera || el.Browser == BrowserType.OperaGX) psi.ArgumentList.Add("-private");
                                else if (el.Browser == BrowserType.Firefox) psi.ArgumentList.Add("-private-window");
                                else psi.ArgumentList.Add("--incognito");
                            }

                            if (!string.IsNullOrEmpty(prof)) 
                            {
                                if (el.Browser == BrowserType.Firefox) psi.ArgumentList.Add($"-P \"{Path.GetFileName(prof)}\"");
                                else psi.ArgumentList.Add($"--profile-directory={Path.GetFileName(prof)}");
                            }
                            using (var proc = Process.Start(psi))
                            {
                                if (proc != null && el.OpenFullscreen)
                                {
                                    await TryEnterFullscreenAsync(proc);
                                }
                            }
                            break;
                        case AiteBar.ActionType.Program:
                        case AiteBar.ActionType.File:
                        case AiteBar.ActionType.Folder:
                            Process.Start(new ProcessStartInfo(el.ActionValue) { UseShellExecute = true });
                            break;
                        case AiteBar.ActionType.ScriptFile: await StartScriptFile(el.ActionValue); break;
                        case AiteBar.ActionType.Command:
                            var confirm = new DarkDialog($"Выполнить команду:\n{el.ActionValue}?", isConfirm: true) { Owner = Application.Current.MainWindow };
                            if (confirm.ShowDialog() == true) Process.Start(new ProcessStartInfo("cmd.exe") { CreateNoWindow = true, UseShellExecute = false, Arguments = $"/c {el.ActionValue}" });
                            break;
                    }
                }
            } catch (Exception ex) { Logger.Log(ex); }
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e) {
            try { 
                string t = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : ""; 
                if (string.IsNullOrEmpty(t)) return; 
                await HideDock();
                using var proc = Process.Start(new ProcessStartInfo(BrowserHelper.GetExecutablePath(BrowserType.Chrome)) 
                { 
                    UseShellExecute = false, 
                    ArgumentList = { $"https://www.google.com/search?q={Uri.EscapeDataString(t)}" } 
                }) ?? throw new InvalidOperationException("Search failed");
            } catch { }
        }
        private async void BtnScreenshotRegion_Click(object sender, RoutedEventArgs e) { await HideDock(); Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }); }
        private async void BtnRecordVideo_Click(object sender, RoutedEventArgs e) { await HideDock(); Process.Start(new ProcessStartInfo("ms-screenclip:?type=recording") { UseShellExecute = true }); }
        private async void BtnCalc_Click(object sender, RoutedEventArgs e) { await HideDock(); Process.Start("calc.exe"); }
        private async void BtnSettings_Click(object sender, RoutedEventArgs e) { await HideDock(); new SettingsWindow(this).ShowDialog(); }
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
                    errorMessage = "Не удалось прочитать перетаскиваемый объект.";
                    return false;
                }

                if (droppedItems.Length > 1)
                {
                    errorMessage = "Можно перетаскивать только один объект за раз.";
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
                    errorMessage = "Поддерживаются только существующие файлы и папки.";
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
                    catch
                    {
                    }

                    errorMessage = "Поддерживаются только .url с http/https ссылкой.";
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
                errorMessage = "Можно перетаскивать только файл, папку или http/https ссылку.";
                return false;
            }

            if (Uri.TryCreate(text, UriKind.Absolute, out var textUri) &&
                (textUri.Scheme == Uri.UriSchemeHttp || textUri.Scheme == Uri.UriSchemeHttps))
            {
                value = text;
                type = ActionType.Web;
                return true;
            }

            errorMessage = "Поддерживаются только файлы, папки, .url и прямые http/https ссылки.";
            return false;
        }

        private async void Border_Drop(object sender, DragEventArgs e) {
            try {
                if (!TryGetDropTarget(e.Data, out string? val, out ActionType type, out string? errorMessage))
                {
                    new DarkDialog(errorMessage ?? "Этот объект нельзя добавить на панель.") { Owner = this }.ShowDialog();
                    return;
                }

                if (!string.IsNullOrEmpty(val)) { 
                    string? iconPath = null;
                    bool isWeb = val.StartsWith("http", StringComparison.OrdinalIgnoreCase) || val.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
                    
                    if (isWeb && !val.StartsWith("http", StringComparison.OrdinalIgnoreCase)) {
                        val = "https://" + val;
                    }

                    if (type == ActionType.Program || type == ActionType.ScriptFile) {
                        iconPath = IconHelper.ExtractAndSaveIcon(val);
                    }

                    var newElement = new CustomElement { 
                        Id = Guid.NewGuid().ToString(),
                        Name = isWeb ? (Uri.TryCreate(val, UriKind.Absolute, out var uri) ? uri.Host : val) : Path.GetFileNameWithoutExtension(val), 
                        ActionValue = val, 
                        ActionType = type.ToString(),
                        ImagePath = iconPath ?? "",
                        Browser = isWeb ? BrowserHelper.GetSystemDefaultBrowser() : BrowserType.Chrome,
                        ContextId = _appSettings.ActiveContextId
                    };
                    
                    // Мгновенное сохранение без открытия окна
                    _elements.Add(newElement);
                    await SaveConfig(); 
                    RefreshPanel();

                    // Асинхронная загрузка иконки для веб-ссылок
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
                                            el.ImagePath = webIcon;
                                            await SaveConfig();
                                            RefreshPanel();
                                        }
                                    });
                                }
                            }
                            catch { }
                        });
                    }
                }
            } catch (Exception ex) { Logger.Log(ex); }
        }
        protected override void OnClosed(EventArgs e) { _notifyIcon?.Dispose(); base.OnClosed(e); }
    }
}
