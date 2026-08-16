using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AiteBar
{
    public enum UtilityWindowClass
    {
        OverlayWidget,
        UtilityTool,
        Workspace
    }

    public class DarkWindow : Window
    {
        private bool _isLocalizationSubscribed;

        public static readonly DependencyProperty WindowClassProperty =
            DependencyProperty.Register(nameof(WindowClass), typeof(UtilityWindowClass), typeof(DarkWindow),
                new PropertyMetadata(UtilityWindowClass.UtilityTool));

        public UtilityWindowClass WindowClass
        {
            get => (UtilityWindowClass)GetValue(WindowClassProperty);
            set => SetValue(WindowClassProperty, value);
        }

        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register(nameof(IsPinned), typeof(bool), typeof(DarkWindow),
                new PropertyMetadata(false));

        public bool IsPinned
        {
            get => (bool)GetValue(IsPinnedProperty);
            set => SetValue(IsPinnedProperty, value);
        }

        protected DarkWindow()
        {
            LocalizationService.EnsureAppliedCulture();
            Deactivated += DarkWindow_Deactivated;
        }

        private void DarkWindow_Deactivated(object? sender, EventArgs e)
        {
            if (WindowClass == UtilityWindowClass.OverlayWidget && !IsPinned)
            {
                OnDeactivatedAutoDismiss();
            }
        }

        protected virtual void OnDeactivatedAutoDismiss()
        {
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
                Dispatcher.BeginInvoke(() => HandleCultureChanged(sender, e));
                return;
            }

            LocalizationService.RefreshLocalizedBindings(this);
            OnLocalizationChanged();
        }
    }
}
