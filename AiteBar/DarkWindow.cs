using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AiteBar
{
    public class DarkWindow : Window
    {
        private bool _isLocalizationSubscribed;

        protected DarkWindow()
        {
            LocalizationService.EnsureAppliedCulture();
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            if (_isLocalizationSubscribed)
            {
                return;
            }

            LocalizationService.CultureChanged += HandleCultureChanged;
            _isLocalizationSubscribed = true;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int darkTheme = 1;
            DwmSetWindowAttribute(hwnd, 20, ref darkTheme, sizeof(int));
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_isLocalizationSubscribed)
            {
                LocalizationService.CultureChanged -= HandleCultureChanged;
                _isLocalizationSubscribed = false;
            }

            base.OnClosed(e);
        }

        protected virtual void OnLocalizationChanged()
        {
        }

        private void HandleCultureChanged(object? sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => HandleCultureChanged(sender, e));
                return;
            }

            LocalizationService.RefreshLocalizedBindings(this);
            OnLocalizationChanged();
        }
    }
}
