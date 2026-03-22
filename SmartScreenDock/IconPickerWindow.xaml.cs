using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

// Устранение неоднозначности
using Button = System.Windows.Controls.Button;
using FontFamily = System.Windows.Media.FontFamily;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;

namespace SmartScreenDock {
    public partial class IconPickerWindow : Window {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public string SelectedIcon { get; private set; } = "";

        public IconPickerWindow() {
            InitializeComponent();
            LoadAllIcons();
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int darkTheme = 1;
            DwmSetWindowAttribute(hwnd, 20, ref darkTheme, sizeof(int));
        }

        private async void LoadAllIcons() {
            Style btnStyle = (Style)FindResource("IconBtnStyle");
            int[] codes = Enumerable.Range(0xE700, 0xE9A0 - 0xE700 + 1).ToArray();

            const int batchSize = 50;
            for (int batch = 0; batch < codes.Length; batch += batchSize) {
                int end = Math.Min(batch + batchSize, codes.Length);
                for (int i = batch; i < end; i++) {
                    int code = codes[i];
                    var btn = new Button {
                        Content = char.ConvertFromUtf32(code),
                        FontFamily = new FontFamily("Segoe Fluent Icons"),
                        FontSize = 24, Width = 46, Height = 46, Margin = new Thickness(2),
                        Style = btnStyle, ToolTip = $"U+{code:X4}",
                        Background = Brushes.Transparent, Foreground = Brushes.White,
                        BorderThickness = new Thickness(0), Cursor = Cursors.Hand
                    };
                    btn.Click += (s, e) => {
                        SelectedIcon = char.ConvertFromUtf32(code);
                        this.DialogResult = true;
                    };
                    IconPanel.Children.Add(btn);
                }
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }
    }
}
