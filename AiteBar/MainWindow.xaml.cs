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
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using Point = System.Windows.Point;

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
        private List<Button> _userButtons = [];

        private const int HOTKEY_ID = 9000;
        private const int WM_HOTKEY = 0x0312;

        public MainWindow()
        {
            InitializeComponent();
            this.Top = -2000; 

            this.SizeChanged += (s, e) => {
                bool isVertical = _appSettings.Edge == DockEdge.Left || _appSettings.Edge == DockEdge.Right;
                if (isVertical)
                {
                    this.MaxHeight = SystemParameters.WorkArea.Height - 20;
                    this.Top = (SystemParameters.PrimaryScreenHeight - this.ActualHeight) / 2;
                    if (!_shown && !double.IsNaN(this.ActualWidth)) this.Left = -this.ActualWidth;
                }
                else
                {
                    this.MaxWidth = SystemParameters.WorkArea.Width - 20;
                    this.Left = (SystemParameters.PrimaryScreenWidth - this.ActualWidth) / 2;
                    if (!_shown && !double.IsNaN(this.ActualHeight)) this.Top = -this.ActualHeight;
                }
                UpdatePanelBounds();
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

        private async Task LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    string json = await File.ReadAllTextAsync(_settingsFile);
                    _appSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
                }
                else if (File.Exists(_configFile))
                {
                    string json = await File.ReadAllTextAsync(_configFile);
                    _appSettings.Elements = JsonSerializer.Deserialize<List<CustomElement>>(json) ?? [];
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
        }

        private void UnregisterGlobalHotkey()
        {
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero) NativeMethods.UnregisterHotKey(hwnd, HOTKEY_ID);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _shown = !_shown;
                Toggle(!_shown);
                handled = true;
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

            var trayMenu = new System.Windows.Forms.ContextMenuStrip();
            trayMenu.Items.Add("Открыть", null, (s, e) => { if (!_shown) { _shown = true; Toggle(false); } });
            
            trayMenu.Items.Add("Настройки программы", null, (s, e) => Dispatcher.Invoke(() => new AppSettingsWindow(this) { Owner = this }.ShowDialog()));
            trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            trayMenu.Items.Add("О программе", null, (s, e) => Dispatcher.Invoke(() => new AboutWindow { Owner = this }.ShowDialog()));
            trayMenu.Items.Add("Справка", null, (s, e) => OpenUrl("https://codebdbd.github.io/intro/en/products/aitebar/guide.html"));
            trayMenu.Items.Add("Поддержать автора", null, (s, e) => OpenUrl("https://codebdbd.github.io/intro/en/pages/donate.html"));
            trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            trayMenu.Items.Add("Закрыть и выйти", null, (s, e) => { _notifyIcon.Dispose(); Application.Current.Shutdown(); });

            _notifyIcon.ContextMenuStrip = trayMenu;
            _notifyIcon.MouseClick += (s, e) => {
                if (e.Button == System.Windows.Forms.MouseButtons.Left) {
                    if (!_shown) { _shown = true; Toggle(false); }
                }
            };
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
                if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN && _shown && !_isAnimating)
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

        private async void Window_Loaded(object sender, RoutedEventArgs e) {
            try
            {
                await LoadSettings();
                RefreshPanel();
                _timer.Tick += (s, ev) => {
                    if (_isAnimating) return;
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

            MainPanel.Orientation = orientation;
            UserButtonsPanel.Orientation = orientation;
            SystemUtilsPanel.Orientation = orientation;
            ControlBlock.Orientation = orientation;

            var separators = new[] { SepSystem, SepControl };
            foreach (var sep in separators)
            {
                if (isVertical) { sep.Width = 20; sep.Height = 1; sep.Margin = new Thickness(0, 6, 0, 6); }
                else { sep.Width = 1; sep.Height = 20; sep.Margin = new Thickness(6, 0, 6, 0); }
            }
        }

        public void RefreshPanel() {
            UpdateOrientation();
            UserButtonsPanel.Children.Clear();
            _userButtons.Clear();

            _elements = NormalizeElements(_appSettings.Elements);
            
            BtnSearch.Visibility = _appSettings.ShowPresetSearch ? Visibility.Visible : Visibility.Collapsed;
            BtnScreenshot.Visibility = _appSettings.ShowPresetScreenshot ? Visibility.Visible : Visibility.Collapsed;
            BtnRecord.Visibility = _appSettings.ShowPresetVideo ? Visibility.Visible : Visibility.Collapsed;
            BtnCalc.Visibility = _appSettings.ShowPresetCalc ? Visibility.Visible : Visibility.Collapsed;

            // Видимость зоны системных утилит
            bool hasSystemUtils = BtnSearch.Visibility == Visibility.Visible || BtnScreenshot.Visibility == Visibility.Visible || 
                                 BtnRecord.Visibility == Visibility.Visible || BtnCalc.Visibility == Visibility.Visible;
            SystemUtilsPanel.Visibility = hasSystemUtils ? Visibility.Visible : Visibility.Collapsed;

            foreach (var el in _elements) {
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
                        if (newIndex >= 0 && newIndex < _elements.Count && newIndex != _draggedOriginalIndex) {
                            var element = _elements[_draggedOriginalIndex];
                            _elements.RemoveAt(_draggedOriginalIndex);
                            _elements.Insert(newIndex, element);
                            await SaveConfig();
                        }
                        RefreshPanel();
                    } else {
                        // Если это был просто клик (не реордеринг), выполняем действие
                        await ExecuteCustomAction(capturedElement);
                    }
                    _draggedButton = null; _draggedElement = null; _isReordering = false;
                };

                var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
                var editItem = new MenuItem { Header = "Редактировать", Style = (Style)FindResource("DarkMenuItem") };
                editItem.Click += (s, e) => new SettingsWindow(this, el).ShowDialog();
                var delItem = new MenuItem { Header = "Удалить", Style = (Style)FindResource("DarkMenuItem") };
                var capturedId = el.Id; var capturedName = el.Name;
                delItem.Click += async (s, e) => {
                    var dlg = new DarkDialog($"Удалить '{capturedName}'?", isConfirm: true) { Owner = this };
                    if (dlg.ShowDialog() == true) {
                        _elements.RemoveAll(x => x.Id == capturedId);
                        await SaveConfig(); RefreshPanel();
                    }
                };
                menu.Items.Add(editItem); menu.Items.Add(delItem); btn.ContextMenu = menu;
                UserButtonsPanel.Children.Add(btn);
                _userButtons.Add(btn);
            }
            
            // Разделители
            SepSystem.Visibility = (UserButtonsPanel.Children.Count > 0) && hasSystemUtils ? Visibility.Visible : Visibility.Collapsed;
            SepControl.Visibility = (UserButtonsPanel.Children.Count > 0 || hasSystemUtils) ? Visibility.Visible : Visibility.Collapsed;
            
            UpdatePanelBounds();
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
             
             // Берем размер кнопки + отступы (margin=4 с каждой стороны = 8)
             double offset = isVertical ? _userButtons[0].ActualHeight + 8 : _userButtons[0].ActualWidth + 8;
 
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
            IsAppMode = s.IsAppMode, IsIncognito = s.IsIncognito, UseRotation = s.UseRotation,
            IsTopmost = s.IsTopmost, LastUsedProfile = s.LastUsedProfile,
            Alt = s.Alt, Ctrl = s.Ctrl, Shift = s.Shift, Win = s.Win, Key = s.Key
        };

        private static List<CustomElement> NormalizeElements(IEnumerable<CustomElement> source)
        {
            var result = new List<CustomElement>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in source) {
                if (item == null) continue;
                string id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString() : item.Id;
                if (!seen.Add(id)) continue;
                result.Add(new CustomElement {
                    Id = id,
                    Name = item.Name ?? "", Icon = string.IsNullOrWhiteSpace(item.Icon) ? "\uE710" : item.Icon,
                    IconFont = string.IsNullOrWhiteSpace(item.IconFont) ? FontHelper.FluentKey : item.IconFont,
                    Color = string.IsNullOrWhiteSpace(item.Color) ? "#E3E3E3" : item.Color,
                    ImagePath = item.ImagePath ?? "",
                    ActionType = Enum.TryParse<ActionType>(item.ActionType, out _) ? item.ActionType : nameof(ActionType.Web),
                    ActionValue = item.ActionValue ?? "", Browser = item.Browser, ChromeProfile = item.ChromeProfile ?? "",
                    IsAppMode = item.IsAppMode, IsIncognito = item.IsIncognito, UseRotation = item.UseRotation,
                    IsTopmost = item.IsTopmost, LastUsedProfile = item.LastUsedProfile ?? "",
                    Alt = item.Alt, Ctrl = item.Ctrl, Shift = item.Shift, Win = item.Win, Key = item.Key ?? "None"
                });
            }
            return result;
        }


        private void Toggle(bool hide) {
            _isAnimating = true; _timer.Stop();
            
            var screens = Screen.AllScreens;
            var screen = (_appSettings.MonitorIndex >= 0 && _appSettings.MonitorIndex < screens.Length) 
                ? screens[_appSettings.MonitorIndex] 
                : Screen.PrimaryScreen;

            if (screen == null) return;

            var bounds = screen.Bounds;
            double screenLeft = bounds.Left / _cachedDpi;
            double screenTop = bounds.Top / _cachedDpi;
            double screenWidth = bounds.Width / _cachedDpi;
            double screenHeight = bounds.Height / _cachedDpi;
            
            if (!hide) { 
                this.Topmost = false; 
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE); 
                this.Topmost = true; 
            }

            double finalX = this.Left;
            double finalY = this.Top;

            switch (_appSettings.Edge)
            {
                case DockEdge.Top:
                    this.Left = screenLeft + (screenWidth - this.ActualWidth) / 2;
                    finalX = this.Left;
                    finalY = hide ? screenTop - this.ActualHeight : screenTop;
                    break;
                case DockEdge.Bottom:
                    this.Left = screenLeft + (screenWidth - this.ActualWidth) / 2;
                    finalX = this.Left;
                    finalY = hide ? screenTop + screenHeight : screenTop + screenHeight - this.ActualHeight;
                    break;
                case DockEdge.Left:
                    this.Top = screenTop + (screenHeight - this.ActualHeight) / 2;
                    finalY = this.Top;
                    finalX = hide ? screenLeft - this.ActualWidth : screenLeft;
                    break;
                case DockEdge.Right:
                    this.Top = screenTop + (screenHeight - this.ActualHeight) / 2;
                    finalY = this.Top;
                    finalX = hide ? screenLeft + screenWidth : screenLeft + screenWidth - this.ActualWidth;
                    break;
            }

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
                            using (var proc = Process.Start(psi)) { if (proc != null && el.IsTopmost) { for (int i = 0; i < 25; i++) { await Task.Delay(200); proc.Refresh();
                                        if (proc.MainWindowHandle != IntPtr.Zero) { NativeMethods.SetWindowPos(proc.MainWindowHandle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE); break; } } } }
                            break;
                        case AiteBar.ActionType.Exe: Process.Start(new ProcessStartInfo(el.ActionValue) { UseShellExecute = true }); break;
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
        private void Border_DragOver(object sender, DragEventArgs e) { e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText)) ? DragDropEffects.Link : DragDropEffects.None; e.Handled = true; }
        private static readonly string[] ScriptExtensions = [".bat", ".cmd", ".ps1", ".py"];

        private async void Border_Drop(object sender, DragEventArgs e) {
            try {
                string? val = null; ActionType type = ActionType.Web;
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) { 
                    var f = (string[])e.Data.GetData(DataFormats.FileDrop); 
                    if (f?.Length > 0) { 
                        val = f[0]; 
                        string ext = Path.GetExtension(val).ToLowerInvariant();
                        if (ext == ".url") {
                            try {
                                var lines = File.ReadAllLines(val);
                                var urlLine = lines.FirstOrDefault(l => l.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
                                if (urlLine != null) {
                                    val = urlLine[4..].Trim();
                                    type = ActionType.Web;
                                }
                            } catch { }
                        } else if (ScriptExtensions.Contains(ext)) {
                            type = ActionType.ScriptFile;
                        } else {
                            type = ActionType.Exe;
                        }
                    }
                } else if (e.Data.GetDataPresent(DataFormats.UnicodeText)) {
                    val = ((string)e.Data.GetData(DataFormats.UnicodeText)).Trim();
                } else if (e.Data.GetDataPresent(DataFormats.Text)) {
                    val = ((string)e.Data.GetData(DataFormats.Text)).Trim();
                }

                if (!string.IsNullOrEmpty(val)) { 
                    string? iconPath = null;
                    bool isWeb = val.StartsWith("http", StringComparison.OrdinalIgnoreCase) || val.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
                    
                    if (isWeb && !val.StartsWith("http", StringComparison.OrdinalIgnoreCase)) {
                        val = "https://" + val;
                    }

                    if (type == ActionType.Exe || type == ActionType.ScriptFile) {
                        iconPath = IconHelper.ExtractAndSaveIcon(val);
                    }

                    var newElement = new CustomElement { 
                        Id = Guid.NewGuid().ToString(),
                        Name = isWeb ? (Uri.TryCreate(val, UriKind.Absolute, out var uri) ? uri.Host : val) : Path.GetFileNameWithoutExtension(val), 
                        ActionValue = val, 
                        ActionType = type.ToString(),
                        ImagePath = iconPath ?? "",
                        Browser = isWeb ? BrowserHelper.GetSystemDefaultBrowser() : BrowserType.Chrome
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
