using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
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
        [DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [StructLayout(LayoutKind.Sequential)] internal struct Win32Point { public int X; public int Y; }

        const byte VK_LWIN = 0x5B, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
        const uint KEYUP = 0x0002;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;

        private DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
        private bool _shown = false, _isAnimating = false;
        private readonly string _configFile;
        private List<CustomElement> _elements = new();
        private System.Windows.Forms.NotifyIcon _notifyIcon = null!;

        public MainWindow()
        {
            InitializeComponent();
            this.Top = -2000; 

            this.SizeChanged += (s, e) => {
                this.MaxWidth = SystemParameters.WorkArea.Width - 20;
                this.Left = (SystemParameters.PrimaryScreenWidth - this.ActualWidth) / 2;
                if (!_shown && !double.IsNaN(this.ActualHeight)) this.Top = -this.ActualHeight;
            };
            
            string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Codebdbd", "Aite Deck");
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
            _configFile = Path.Combine(configDir, "custom_buttons.json");

            InitTrayIcon();
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

            _notifyIcon.Text = "SmartScreenDock";
            _notifyIcon.Visible = true;

            var trayMenu = new System.Windows.Forms.ContextMenuStrip();
            trayMenu.Items.Add("Открыть", null, (s, e) => { if (!_shown) { _shown = true; Toggle(0); } });
            trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            trayMenu.Items.Add("О программе", null, (s, e) => OpenUrl("https://github.com/codebdbd/intro/en/products/aitedeck/index.html"));
            trayMenu.Items.Add("Справка", null, (s, e) => OpenUrl("https://github.com/codebdbd/intro/en/products/aitedeck/guide.html"));
            trayMenu.Items.Add("Поддержать автора", null, (s, e) => OpenUrl("https://github.com/codebdbd/intro/en/pages/donate.html"));
            trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            trayMenu.Items.Add("Закрыть и выйти", null, (s, e) => { _notifyIcon.Dispose(); Application.Current.Shutdown(); });

            _notifyIcon.ContextMenuStrip = trayMenu;
            _notifyIcon.MouseClick += (s, e) => {
                if (e.Button == System.Windows.Forms.MouseButtons.Left) {
                    if (!_shown) { _shown = true; Toggle(0); }
                }
            };
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
                        if (inActivationZone && !_shown) { _shown = true; Toggle(0); }
                        else if (pt.Y > this.ActualHeight + 20 && _shown) { _shown = false; Toggle(-1); }
                    }
                };
                _timer.Start();
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
                try {
                    string json = await File.ReadAllTextAsync(_configFile);
                    _elements = JsonSerializer.Deserialize<List<CustomElement>>(json) ?? new();
                } catch (Exception ex) { Logger.Log(ex); _elements = new(); }

                foreach (var el in _elements) {
                    var btn = new Button { 
                        Content = el.Icon, 
                        ToolTip = el.Name, 
                        Foreground = new BrushConverter().ConvertFromString(el.Color) as Brush ?? Brushes.White
                    };
                    btn.Click += async (s, e) => await ExecuteCustomAction(el);
                    
                    var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
                    var editItem = new MenuItem { Header = "Редактировать", Style = (Style)FindResource("DarkMenuItem") };
                    editItem.Click += (s, e) => new SettingsWindow(this, el).ShowDialog();
                    var delItem = new MenuItem { Header = "Удалить", Style = (Style)FindResource("DarkMenuItem") };
                    delItem.Click += async (s, e) => {
                        try
                        {
                            var dlg = new DarkDialog($"Удалить '{el.Name}'?", isConfirm: true) { Owner = this };
                            if (dlg.ShowDialog() == true) {
                                _elements.Remove(el); await SaveConfig(); await RefreshPanel();
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
            Sep1.Visibility = Block2_AI.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            Sep2.Visibility = Block3_Web.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            Sep3.Visibility = Block4_Scripts.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            Sep4.Visibility = Block5_Other.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task SaveConfig() {
            try { await File.WriteAllTextAsync(_configFile, JsonSerializer.Serialize(_elements, new JsonSerializerOptions { WriteIndented = true })); }
            catch (Exception ex) { Logger.Log(ex); Debug.WriteLine($"Ошибка сохранения: {ex.Message}"); }
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

        private void Toggle(double targetY) {
            _isAnimating = true;
            _timer.Stop();
            double finalY = targetY < 0 ? -this.ActualHeight : 0;
            var anim = new DoubleAnimation(finalY, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            anim.Completed += (s, ev) => { this.BeginAnimation(TopProperty, null); this.Top = finalY; _isAnimating = false; _timer.Start(); };
            this.BeginAnimation(TopProperty, anim);
        }

        private async Task HideDock() { if (_shown) { _shown = false; Toggle(-1); await Task.Delay(250); } }
        private void Press(params byte[] keys) { foreach (var k in keys) keybd_event(k, 0, 0, 0); foreach (var k in keys) keybd_event(k, 0, KEYUP, 0); }

        private string AdvanceProfile(CustomElement el) {
            string basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data");
            if (!Directory.Exists(basePath)) return "";
            var profiles = new List<string> { "Default" };
            profiles.AddRange(Directory.GetDirectories(basePath, "Profile *").Select(p => Path.GetFileName(p)!));
            profiles = profiles.OrderBy(p => p).ToList();
            int idx = profiles.IndexOf(el.LastUsedProfile);
            return profiles[(idx + 1) % profiles.Count];
        }

        private async Task ExecuteCustomAction(CustomElement el) {
            try {
                await HideDock();
                if (el.ActionType == "Hotkey") {
                    if (el.Ctrl) keybd_event(VK_CONTROL, 0, 0, 0);
                    if (el.Shift) keybd_event(VK_SHIFT, 0, 0, 0);
                    if (el.Alt) keybd_event(VK_MENU, 0, 0, 0);
                    if (el.Win) keybd_event(VK_LWIN, 0, 0, 0);
                    if (Enum.TryParse(typeof(Key), el.Key, out var k)) {
                        byte vk = (byte)KeyInterop.VirtualKeyFromKey((Key)k!);
                        keybd_event(vk, 0, 0, 0); keybd_event(vk, 0, KEYUP, 0);
                    }
                    if (el.Win) keybd_event(VK_LWIN, 0, KEYUP, 0);
                    if (el.Alt) keybd_event(VK_MENU, 0, KEYUP, 0);
                    if (el.Shift) keybd_event(VK_SHIFT, 0, KEYUP, 0);
                    if (el.Ctrl) keybd_event(VK_CONTROL, 0, KEYUP, 0);
                } 
                else if (el.ActionType == "Web") {
                    string chromePath = GetChromePath(); List<string> args = new();
                    if (el.IsAppMode) args.Add($"--app=\"{el.ActionValue}\""); else args.Add($"\"{el.ActionValue}\"");
                    if (el.IsIncognito) args.Add("--incognito");
                    string prof = el.ChromeProfile;
                    if (el.UseRotation) { prof = AdvanceProfile(el); el.LastUsedProfile = prof; await SaveConfig(); }
                    if (!string.IsNullOrEmpty(prof)) args.Add($"--profile-directory=\"{Path.GetFileName(prof)}\"");
                    var proc = Process.Start(new ProcessStartInfo(chromePath, string.Join(" ", args)) { UseShellExecute = true });
                    if (el.IsTopmost && proc != null) {
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
                else if (el.ActionType == "Exe" || el.ActionType == "ScriptFile") { if (File.Exists(el.ActionValue)) Process.Start(new ProcessStartInfo(el.ActionValue) { UseShellExecute = true }); else throw new FileNotFoundException("Файл не найден."); }
                else if (el.ActionType == "Command") {
                    var confirm = new DarkDialog($"Будет выполнена команда:\n\n{el.ActionValue}\n\nПродолжить?", isConfirm: true);
                    confirm.Owner = Application.Current.MainWindow;
                    if (confirm.ShowDialog() != true) return;
                    Process.Start(new ProcessStartInfo("cmd.exe", $"/c {el.ActionValue}") { CreateNoWindow = true, UseShellExecute = false });
                }
            } catch (Exception ex) { Logger.Log(ex); new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog(); }
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e) {
            try
            {
                string t = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : "";
                if (string.IsNullOrEmpty(t)) { new DarkDialog("Буфер обмена пуст.") { Owner = this }.ShowDialog(); return; }
                await HideDock(); Process.Start(new ProcessStartInfo(GetChromePath(), $"\"https://www.google.com/search?q={Uri.EscapeDataString(t)}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog();
            }
        }

        private async void BtnScreenshotRegion_Click(object sender, RoutedEventArgs e) {
            try { await HideDock(); Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }); }
            catch (Exception ex) { Logger.Log(ex); new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog(); }
        }
        private async void BtnRecordVideo_Click(object sender, RoutedEventArgs e) {
            try { await HideDock(); Process.Start(new ProcessStartInfo("ms-screenclip:?type=recording") { UseShellExecute = true }); }
            catch (Exception ex) { Logger.Log(ex); new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog(); }
        }
        private async void BtnClipboard_Click(object sender, RoutedEventArgs e) {
            try { await HideDock(); Press(VK_LWIN, 0x56); }
            catch (Exception ex) { Logger.Log(ex); new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog(); }
        }
        private void BtnCalc_Click(object sender, RoutedEventArgs e) => Process.Start("calc.exe");
        private void BtnSettings_Click(object sender, RoutedEventArgs e) => new SettingsWindow(this).ShowDialog();
        private void BtnClose_Click(object sender, RoutedEventArgs e) { _notifyIcon?.Dispose(); Application.Current.Shutdown(); }

        protected override void OnClosed(EventArgs e) { _notifyIcon?.Dispose(); base.OnClosed(e); }
    }
}
