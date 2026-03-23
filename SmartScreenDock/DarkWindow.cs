using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SmartScreenDock
{
    public class DarkWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int darkTheme = 1;
            DwmSetWindowAttribute(hwnd, 20, ref darkTheme, sizeof(int));
        }
    }
}
