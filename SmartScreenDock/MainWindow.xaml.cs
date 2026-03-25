using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Drawing;

// Псевдонимы для устранения неоднозначности (WPF vs WinForms)
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace SmartScreenDock
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(ref Win32Point pt);
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [StructLayout(LayoutKind.Sequential)] internal struct Win32Point { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public Win32Point pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // Win32 INPUT содержит union; для корректного marshaling нужен полный layout.
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        const byte VK_LWIN = 0x5B, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const string AppCompany = "Codebdbd";
        private const string AppName = "Aite Bar";
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const int WH_MOUSE_LL = 14;
        const int WM_LBUTTONDOWN = 0x0201;

        private DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
        private DateTime? _hoverStartTime;
        private bool _shown = false, _isAnimating = false;
        private double _panelLeft, _panelTop, _panelRight, _panelBottom, _cachedDpi = 1.0;
        private static readonly BrushConverter _brushConverter = new();
        private readonly SemaphoreSlim _saveSemaphore = new(1, 1);
        private readonly string _configFile;
        private List<CustomElement> _elements = new();
        private System.Windows.Forms.NotifyIcon _notifyIcon = null!;
        // Поле обязательно: делегат передаётся в SetWindowsHookEx и должен оставаться живым.
        // Если убрать поле — GC соберёт делегат, и хук упадёт с AccessViolationException.
        private LowLevelMouseProc? _mouseProc;
        private IntPtr _mouseHook = IntPtr.Zero;

        public MainWindow()
        {
            InitializeComponent();
            this.Top = -2000; 

            this.SizeChanged += (s, e) => {
                this.MaxWidth = SystemParameters.WorkArea.Width - 20;
                this.Left = (SystemParameters.PrimaryScreenWidth - this.ActualWidth) / 2;
                if (!_shown && !double.IsNaN(this.ActualHeight)) this.Top = -this.ActualHeight;
                UpdatePanelBounds();
            };
            
            string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppCompany, AppName);
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
            _configFile = Path.Combine(configDir, "custom_buttons.json");

            InitTrayIcon();

            Application.Current.Exit += (_, _) => UninstallMouseHook();
        }

        private void InitTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            try {
                // Загрузка иконки из ресурсов проекта
                var iconUri = new Uri("pack://application:,,,/Resources/app.ico");
                var streamInfo = Application.GetResourceStream(iconUri);
                if (streamInfo != null) {
                    using (var stream = streamInfo.Stream) _notifyIcon.Icon = new Icon(stream);
                } else _notifyIcon.Icon = SystemIcons.Application;
            } catch (Exception ex) { Logger.Log(ex); _notifyIcon.Icon = SystemIcons.Application; }

            _notifyIcon.Text = "AiteBar (Smart Control Panel)";
            _notifyIcon.Visible = true;

            var trayMenu = new System.Windows.Forms.ContextMenuStrip();
            trayMenu.Items.Add("Открыть", null, (s, e) => { if (!_shown) { _shown = true; Toggle(false); } });
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
                _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(curModule.ModuleName!), 0);
                if (_mouseHook == IntPtr.Zero) Logger.Log(new Exception("SetWindowsHookEx failed"));
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        private void UninstallMouseHook()
        {
            if (_mouseHook == IntPtr.Zero) return;
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN && _shown && !_isAnimating)
                {
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    double x = hookStruct.pt.X;
                    double y = hookStruct.pt.Y;

                    bool clickedOutside = x < _panelLeft || x > _panelRight || y < _panelTop || y > _panelBottom;

                    if (clickedOutside)
                    {
                        this.Dispatcher.InvokeAsync(async () => await HideDock());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
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

        private void OpenUrl(string url) {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) {
                Logger.Log(ex);
                new DarkDialog($"Не удалось открыть ссылку:\n{ex.Message}") { Owner = this }.ShowDialog();
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e) {
            try
            {
                await RefreshPanel();
                _timer.Tick += (s, ev) => {
                    if (_isAnimating) return;
                    Win32Point pt = new();
                    if (GetCursorPos(ref pt)) {
                        double screenWidth = SystemParameters.PrimaryScreenWidth;
                        bool inActivationZone = pt.Y == 0 && pt.X > (screenWidth * 0.35) && pt.X < (screenWidth * 0.65);
                        if (inActivationZone && !_shown) {
                            if (_hoverStartTime == null) _hoverStartTime = DateTime.Now;
                            else if ((DateTime.Now - _hoverStartTime.Value).TotalMilliseconds >= 250) {
                                _shown = true;
                                _hoverStartTime = null;
                                Toggle(false);
                            }
                        } else {
                            _hoverStartTime = null;
                        }
                    }
                };
                _timer.Start();
                InstallMouseHook();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog();
            }
        }


        public async Task RefreshPanel() {
            var userUtils = Block1_Utils.Children.OfType<Button>().Where(b => b.ContextMenu != null).ToList();
            foreach (var btn in userUtils) Block1_Utils.Children.Remove(btn);
            Block2_AI.Children.Clear(); Block3_Web.Children.Clear(); Block4_Scripts.Children.Clear(); Block5_Other.Children.Clear();

            if (File.Exists(_configFile)) {
                await _saveSemaphore.WaitAsync();
                try {
                    string json = await File.ReadAllTextAsync(_configFile);
                    _elements = JsonSerializer.Deserialize<List<CustomElement>>(json) ?? new();
                } catch (Exception ex) { Logger.Log(ex); _elements = new(); }
                finally { _saveSemaphore.Release(); }

                foreach (var el in _elements) {
                    var btn = new Button { 
                        Content = el.Icon, 
                        ToolTip = el.Name, 
                        FontFamily = FontHelper.Resolve(el.IconFont),
                        Foreground = _brushConverter.ConvertFromString(el.Color) as Brush ?? Brushes.White
                    };
                    btn.Click += async (s, e) => await ExecuteCustomAction(el);
                    
                    var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
                    var editItem = new MenuItem { Header = "Редактировать", Style = (Style)FindResource("DarkMenuItem") };
                    editItem.Click += (s, e) => new SettingsWindow(this, el).ShowDialog();
                    var delItem = new MenuItem { Header = "Удалить", Style = (Style)FindResource("DarkMenuItem") };
                    var capturedId = el.Id;
                    var capturedName = el.Name;
                    delItem.Click += async (s, e) => {
                        try
                        {
                            var dlg = new DarkDialog($"Удалить '{capturedName}'?", isConfirm: true) { Owner = this };
                            if (dlg.ShowDialog() == true) {
                                _elements.RemoveAll(x => x.Id == capturedId);
                                await SaveConfig();
                                await RefreshPanel();
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(ex);
                            new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog();
                        }
                    };
                    menu.Items.Add(editItem); menu.Items.Add(delItem); btn.ContextMenu = menu;

                    switch ((DockBlock)el.BlockId) {
                        case DockBlock.Utils: Block1_Utils.Children.Add(btn); break;
                        case DockBlock.AI: Block2_AI.Children.Add(btn); break;
                        case DockBlock.Web: Block3_Web.Children.Add(btn); break;
                        case DockBlock.Scripts: Block4_Scripts.Children.Add(btn); break;
                        case DockBlock.Other: Block5_Other.Children.Add(btn); break;
                    }
                }
            }
            bool hasAi = Block2_AI.Children.Count > 0;
            bool hasWeb = Block3_Web.Children.Count > 0;
            bool hasScripts = Block4_Scripts.Children.Count > 0;
            bool hasOther = Block5_Other.Children.Count > 0;

            Sep2.Visibility = hasAi && hasWeb ? Visibility.Visible : Visibility.Collapsed;
            Sep3.Visibility = (hasAi || hasWeb) && hasScripts ? Visibility.Visible : Visibility.Collapsed;
            Sep4.Visibility = (hasAi || hasWeb || hasScripts) && hasOther ? Visibility.Visible : Visibility.Collapsed;
            SepUtils.Visibility = (hasAi || hasWeb || hasScripts || hasOther) ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task SaveConfig() {
            await _saveSemaphore.WaitAsync();
            try
            {
                await File.WriteAllTextAsync(_configFile,
                    JsonSerializer.Serialize(_elements, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { Logger.Log(ex); Debug.WriteLine($"Ошибка сохранения: {ex.Message}"); }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        public async Task SaveElement(CustomElement updated, string? removeId = null)
        {
            if (removeId != null)
                _elements.RemoveAll(x => x.Id == removeId);

            var existing = _elements.FirstOrDefault(x => x.Id == updated.Id);
            if (existing != null)
                _elements[_elements.IndexOf(existing)] = updated;
            else
                _elements.Add(updated);

            await SaveConfig();
            await RefreshPanel();
        }

        private string GetChromePath() {
            try
            {
                var regVal = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
                    "", null) as string;
                if (!string.IsNullOrEmpty(regVal) && File.Exists(regVal))
                    return regVal;
            }
            catch (Exception ex) { Logger.Log(ex); }

            string[] paths = { 
                @"C:\Program Files\Google\Chrome\Application\chrome.exe", 
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe", 
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe") 
            };
            foreach (var p in paths) if (File.Exists(p)) return p;
            return "chrome.exe";
        }

        private void Toggle(bool hide) {
            _isAnimating = true;
            _timer.Stop();
            if (!hide)
            {
                this.Topmost = false;
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
                this.Topmost = true;
            }
            double finalY = hide ? -this.ActualHeight : 0;
            var anim = new DoubleAnimation(finalY, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            anim.Completed += (s, ev) => { this.BeginAnimation(TopProperty, null); this.Top = finalY; _isAnimating = false; _timer.Start(); UpdatePanelBounds(); };
            this.BeginAnimation(TopProperty, anim);
        }

        private async Task HideDock() { if (_shown) { _shown = false; Toggle(true); await Task.Delay(250); } }

        private static string FindExecutableOnPath(string fileName)
        {
            var pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
                return fileName;

            foreach (var dir in pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim(), fileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                }
            }

            return fileName;
        }

        private static ProcessStartInfo CreateScriptProcessStartInfo(string scriptPath)
        {
            string workingDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory;
            string extension = Path.GetExtension(scriptPath).ToLowerInvariant();
            switch (extension)
            {
                case ".bat":
                case ".cmd":
                {
                    var psi = new ProcessStartInfo("cmd.exe")
                    {
                        UseShellExecute = false,
                        WorkingDirectory = workingDirectory
                    };
                    psi.ArgumentList.Add("/c");
                    psi.ArgumentList.Add(scriptPath);
                    return psi;
                }

                case ".ps1":
                {
                    string shell = FindExecutableOnPath("pwsh.exe");
                    if (!File.Exists(shell))
                        shell = FindExecutableOnPath("powershell.exe");

                    var psi = new ProcessStartInfo(shell)
                    {
                        UseShellExecute = false,
                        WorkingDirectory = workingDirectory
                    };
                    psi.ArgumentList.Add("-NoProfile");
                    if (Path.GetFileName(shell).Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        psi.ArgumentList.Add("-ExecutionPolicy");
                        psi.ArgumentList.Add("Bypass");
                    }
                    psi.ArgumentList.Add("-File");
                    psi.ArgumentList.Add(scriptPath);
                    return psi;
                }

                case ".py":
                {
                    string pythonExe = FindExecutableOnPath("python.exe");
                    if (!File.Exists(pythonExe))
                        throw new InvalidOperationException("Python не найден в PATH.");

                    string pythonDir = Path.GetDirectoryName(pythonExe) ?? "";
                    string tclLibrary = Path.Combine(pythonDir, "tcl", "tcl8.6");
                    string tkLibrary = Path.Combine(pythonDir, "tcl", "tk8.6");

                    var psi = new ProcessStartInfo("cmd.exe")
                    {
                        UseShellExecute = false,
                        WorkingDirectory = workingDirectory
                    };
                    psi.Arguments = $"/c \"\"{pythonExe}\" \"{scriptPath}\"\"";
                    if (Directory.Exists(tclLibrary))
                        psi.Environment["TCL_LIBRARY"] = tclLibrary;
                    if (Directory.Exists(tkLibrary))
                        psi.Environment["TK_LIBRARY"] = tkLibrary;
                    return psi;
                }

                default:
                    throw new InvalidOperationException("Поддерживаются только .bat, .cmd, .ps1 и .py.");
            }
        }

        private Task StartScriptFile(string scriptPath)
        {
            var psi = CreateScriptProcessStartInfo(scriptPath);
            using var proc = Process.Start(psi);
            if (proc == null)
                throw new InvalidOperationException("Не удалось запустить скрипт.");
            return Task.CompletedTask;
        }

        private void Press(params byte[] keys)
        {
            var inputs = new INPUT[keys.Length * 2];
            for (int i = 0; i < keys.Length; i++)
            {
                inputs[i] = new INPUT { type = INPUT_KEYBOARD, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = keys[i] } } };
                inputs[keys.Length + i] = new INPUT { type = INPUT_KEYBOARD, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = keys[i], dwFlags = KEYEVENTF_KEYUP } } };
            }
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        private string AdvanceProfile(CustomElement el) {
            string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data");
            if (!Directory.Exists(basePath)) return "";
            var profiles = new List<string> { "Default" };
            profiles.AddRange(Directory.GetDirectories(basePath, "Profile *").Select(p => Path.GetFileName(p)!));
            profiles = profiles.OrderBy(p => p).ToList();
            int idx = profiles.IndexOf(el.LastUsedProfile);
            if (idx < 0) return profiles[0];
            return profiles[(idx + 1) % profiles.Count];
        }

        private async Task ExecuteCustomAction(CustomElement el) {
            try {
                await HideDock();
                if (Enum.TryParse<SmartScreenDock.ActionType>(el.ActionType, out var actionType))
                {
                    switch (actionType)
                    {
                        case SmartScreenDock.ActionType.Hotkey:
                        {
                            var downKeys = new List<byte>();
                            if (el.Ctrl) downKeys.Add(VK_CONTROL);
                            if (el.Shift) downKeys.Add(VK_SHIFT);
                            if (el.Alt) downKeys.Add(VK_MENU);
                            if (el.Win) downKeys.Add(VK_LWIN);

                            byte mainVk = 0;
                            if (Enum.TryParse(typeof(Key), el.Key, out var k))
                                mainVk = (byte)KeyInterop.VirtualKeyFromKey((Key)k!);

                            var inputs = new List<INPUT>();
                            foreach (var vk in downKeys)
                                inputs.Add(new INPUT { type = INPUT_KEYBOARD, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk } } });
                            if (mainVk != 0)
                            {
                                inputs.Add(new INPUT { type = INPUT_KEYBOARD, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = mainVk } } });
                                inputs.Add(new INPUT { type = INPUT_KEYBOARD, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = mainVk, dwFlags = KEYEVENTF_KEYUP } } });
                            }
                            foreach (var vk in Enumerable.Reverse(downKeys))
                                inputs.Add(new INPUT { type = INPUT_KEYBOARD, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } } });

                            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
                            break;
                        }

                        case SmartScreenDock.ActionType.Web:
                            string chromePath = GetChromePath();
                            string prof = el.ChromeProfile;
                            if (el.UseRotation) { prof = AdvanceProfile(el); el.LastUsedProfile = prof; await SaveConfig(); }
                            var psi = new ProcessStartInfo(chromePath) { UseShellExecute = false };
                            if (el.IsAppMode)
                                psi.ArgumentList.Add($"--app={el.ActionValue}");
                            else
                                psi.ArgumentList.Add(el.ActionValue);
                            if (el.IsIncognito) psi.ArgumentList.Add("--incognito");
                            if (!string.IsNullOrEmpty(prof)) psi.ArgumentList.Add($"--profile-directory={Path.GetFileName(prof)}");
                            using (var proc = Process.Start(psi))
                            {
                                if (proc != null && el.IsTopmost) {
                                    for (int i = 0; i < 25; i++) {
                                        await Task.Delay(200);
                                        proc.Refresh();
                                        if (proc.MainWindowHandle != IntPtr.Zero) {
                                            SetWindowPos(proc.MainWindowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
                                            break;
                                        }
                                    }
                                }
                            }
                            break;

                        case SmartScreenDock.ActionType.Exe:
                            if (!File.Exists(el.ActionValue))
                                throw new FileNotFoundException("Файл не найден.");
                            using (Process.Start(new ProcessStartInfo(el.ActionValue) { UseShellExecute = true })) { }
                            break;

                        case SmartScreenDock.ActionType.ScriptFile:
                            if (!File.Exists(el.ActionValue))
                                throw new FileNotFoundException("Файл не найден.");
                            await StartScriptFile(el.ActionValue);
                            break;

                        case SmartScreenDock.ActionType.Command:
                            var confirm = new DarkDialog($"Будет выполнена команда:\n\n{el.ActionValue}\n\nПродолжить?", isConfirm: true);
                            confirm.Owner = Application.Current.MainWindow;
                            if (confirm.ShowDialog() != true) return;
                            var psiCmd = new ProcessStartInfo("cmd.exe") { CreateNoWindow = true, UseShellExecute = false };
                            psiCmd.ArgumentList.Add("/c");
                            psiCmd.ArgumentList.Add(el.ActionValue);
                            using (Process.Start(psiCmd)) { }
                            break;
                    }
                }
            } catch (Exception ex) { Logger.Log(ex); new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog(); }
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e) {
            try
            {
                string t = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";
                if (string.IsNullOrEmpty(t)) { new DarkDialog("Буфер обмена пуст.") { Owner = this }.ShowDialog(); return; }
                await HideDock();
                var psi = new ProcessStartInfo(GetChromePath()) { UseShellExecute = false };
                psi.ArgumentList.Add($"https://www.google.com/search?q={Uri.EscapeDataString(t)}");
                using (Process.Start(psi)) { }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog();
            }
        }

        private async void BtnScreenshotRegion_Click(object sender, RoutedEventArgs e) {
            try { await HideDock(); using (Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true })) { } }
            catch (Exception ex) { Logger.Log(ex); new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog(); }
        }
        private async void BtnRecordVideo_Click(object sender, RoutedEventArgs e) {
            try { await HideDock(); using (Process.Start(new ProcessStartInfo("ms-screenclip:?type=recording") { UseShellExecute = true })) { } }
            catch (Exception ex) { Logger.Log(ex); new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog(); }
        }
        private async void BtnCalc_Click(object sender, RoutedEventArgs e)
        {
            try { await HideDock(); Process.Start("calc.exe"); }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog($"Не удалось открыть калькулятор:\n{ex.Message}") { Owner = this }.ShowDialog();
            }
        }
        private async void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            await HideDock();
            new SettingsWindow(this).ShowDialog();
        }
        private async void BtnClose_Click(object sender, RoutedEventArgs e) { await HideDock(); }

        protected override void OnClosed(EventArgs e) { _notifyIcon?.Dispose(); base.OnClosed(e); }
    }
}
