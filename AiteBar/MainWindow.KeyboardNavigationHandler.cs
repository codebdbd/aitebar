

namespace AiteBar;

public enum PanelInputMode
{
    Pointer,
    Keyboard
}

public partial class MainWindow
{
    private bool IsPanelKeyboardMode => _panelInputMode == PanelInputMode.Keyboard;

    private void SetPanelInputMode(PanelInputMode mode, bool clearFocus)
    {
        _panelInputMode = mode;

        if (mode != PanelInputMode.Pointer)
        {
            KeyboardFocusVisualService.SetShowKeyboardFocusCue(this, true);
            return;
        }

        KeyboardFocusVisualService.SetShowKeyboardFocusCue(this, false);
        _focusPanelButtonsOnShow = true;
        unchecked { _panelFocusRequestVersion++; }

        if (clearFocus)
        {
            Keyboard.ClearFocus();
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        SetPanelInputMode(PanelInputMode.Pointer, clearFocus: false);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_shown)
            return;

        if (!IsPanelKeyboardMode)
        {
            var isNavigationKey = e.Key == Key.Tab ||
                e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.Up || e.Key == Key.Down;

            if (isNavigationKey)
            {
                EnablePanelKeyboardMode();
            }
            else
            {
                return;
            }
        }

        var focusableButtons = GetAllFocusableButtons();
        if (focusableButtons.Count == 0)
            return;

        var currentFocus = Keyboard.FocusedElement as Button;
        int currentIndex = currentFocus != null ? focusableButtons.IndexOf(currentFocus) : -1;

        switch (e.Key)
        {
            case Key.Tab:
                e.Handled = true;
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (currentIndex > 0)
                        FocusPanelButton(focusableButtons[currentIndex - 1]);
                    else
                        FocusPanelButton(focusableButtons[focusableButtons.Count - 1]);
                }
                else
                {
                    if (currentIndex < focusableButtons.Count - 1)
                        FocusPanelButton(focusableButtons[currentIndex + 1]);
                    else
                        FocusPanelButton(focusableButtons[0]);
                }
                break;
            case Key.Left:
            case Key.Up:
                MovePanelFocusSpatially(
                    focusableButtons,
                    currentIndex,
                    e.Key == Key.Left ? PanelNavigationDirection.Left : PanelNavigationDirection.Up);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Down:
                MovePanelFocusSpatially(
                    focusableButtons,
                    currentIndex,
                    e.Key == Key.Right ? PanelNavigationDirection.Right : PanelNavigationDirection.Down);
                e.Handled = true;
                break;
            case Key.Enter:
                e.Handled = true;
                currentFocus?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, currentFocus));
                break;
            case Key.Home:
                e.Handled = true;
                FocusPanelButton(focusableButtons[0]);
                break;
            case Key.End:
                e.Handled = true;
                FocusPanelButton(focusableButtons[^1]);
                break;
            case Key.Escape:
                e.Handled = true;
                _ = HideDock();
                break;
        }
    }

    private void FocusPanelForKeyboard()
    {
        if (!_shown || !IsPanelKeyboardMode)
        {
            return;
        }

        int focusRequestVersion = unchecked(++_panelFocusRequestVersion);
        Dispatcher.InvokeAsync(async () =>
        {
            if (focusRequestVersion != _panelFocusRequestVersion || !IsPanelKeyboardMode || HasVisibleOwnedWindow())
                return;

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                ForceForegroundWindow(hwnd);
                Activate();
                Focus();

                var focusTarget = GetFirstFocusablePanelButton();
                FocusPanelButton(focusTarget);

                if (this.IsKeyboardFocusWithin)
                {
                    break;
                }

                if (attempt < 3)
                {
                    await Task.Delay(50);
                    if (focusRequestVersion != _panelFocusRequestVersion || !IsPanelKeyboardMode || HasVisibleOwnedWindow())
                        return;
                }
            }
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private Button GetFirstFocusablePanelButton()
    {
        return _unifiedButtons.FirstOrDefault()
            ?? _overflowButton
            ?? BtnAdd;
    }

    private void MovePanelFocusSpatially(
        IReadOnlyList<Button> buttons,
        int currentIndex,
        PanelNavigationDirection direction)
    {
        var bounds = new List<Rect>(buttons.Count);
        foreach (Button button in buttons)
        {
            Point origin = button.TranslatePoint(new Point(0, 0), MainPanel);
            bounds.Add(new Rect(origin, new System.Windows.Size(button.ActualWidth, button.ActualHeight)));
        }

        int nextIndex = PanelFocusNavigationHelper.FindNextIndex(bounds, currentIndex, direction);
        if (nextIndex >= 0)
        {
            FocusPanelButton(buttons[nextIndex]);
        }
    }

    private List<Button> GetAllFocusableButtons()
    {
        var buttons = new List<Button>();
        buttons.Add(ContextIndicator);
        buttons.Add(BtnAdd);
        buttons.AddRange(_unifiedButtons);
        if (_overflowButton is not null)
            buttons.Add(_overflowButton);
        if (BtnAppSettings.Visibility == Visibility.Visible)
            buttons.Add(BtnAppSettings);
        return buttons;
    }

    private static void FocusPanelButton(Button button)
    {
        button.Focusable = true;
        if (!ReferenceEquals(Keyboard.Focus(button), button))
        {
            button.Focus();
        }
    }

    private static bool ForceForegroundWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        IntPtr foregroundHwnd = NativeMethods.GetForegroundWindow();
        if (foregroundHwnd == hwnd) return true;

        uint foregroundThreadId = NativeMethods.GetWindowThreadProcessId(foregroundHwnd, out _);
        uint currentThreadId = NativeMethods.GetCurrentThreadId();

        bool attached = false;
        try
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
            {
                attached = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            return NativeMethods.SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    private void EnablePanelKeyboardMode(bool focusButtons = true)
    {
        if (!_shown)
        {
            return;
        }

        _focusPanelButtonsOnShow = focusButtons;
        SetPanelInputMode(PanelInputMode.Keyboard, clearFocus: false);
        _activationDwellTracker.Reset();
        if (focusButtons)
        {
            FocusPanelForKeyboard();
        }
    }
}
