using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace AiteBar;

public static class KeyboardFocusVisualService
{
    private static int _initialized;

    public static readonly DependencyProperty ShowKeyboardFocusCueProperty =
        DependencyProperty.RegisterAttached(
            "ShowKeyboardFocusCue",
            typeof(bool),
            typeof(KeyboardFocusVisualService),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetShowKeyboardFocusCue(DependencyObject element)
    {
        return (bool)element.GetValue(ShowKeyboardFocusCueProperty);
    }

    public static void SetShowKeyboardFocusCue(DependencyObject element, bool value)
    {
        element.SetValue(ShowKeyboardFocusCueProperty, value);
    }

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        EventManager.RegisterClassHandler(typeof(Window), Keyboard.PreviewKeyDownEvent, new System.Windows.Input.KeyEventHandler(OnPreviewKeyDown), true);
        EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown), true);
        EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewTouchDownEvent, new EventHandler<TouchEventArgs>(OnPreviewTouchDown), true);
    }

    private static void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not DependencyObject root || !IsFocusNavigationKey(e.Key))
        {
            return;
        }

        SetShowKeyboardFocusCue(root, true);
    }

    private static void OnPreviewMouseDown(object? sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject root)
        {
            SetShowKeyboardFocusCue(root, false);
        }
    }

    private static void OnPreviewTouchDown(object? sender, TouchEventArgs e)
    {
        if (sender is DependencyObject root)
        {
            SetShowKeyboardFocusCue(root, false);
        }
    }

    private static bool IsFocusNavigationKey(Key key)
    {
        return key switch
        {
            Key.Tab => true,
            Key.Left => true,
            Key.Right => true,
            Key.Up => true,
            Key.Down => true,
            Key.Home => true,
            Key.End => true,
            Key.PageUp => true,
            Key.PageDown => true,
            _ => false
        };
    }
}
