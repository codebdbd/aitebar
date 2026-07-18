using System.Windows.Automation.Peers;

namespace AiteBar;

public sealed class HotkeyCaptureBox : ContentControl
{
    public static readonly RoutedCommand ClearCommand = new(nameof(ClearCommand), typeof(HotkeyCaptureBox));

    public static readonly DependencyProperty NotAssignedTextProperty = DependencyProperty.Register(
        nameof(NotAssignedText),
        typeof(string),
        typeof(HotkeyCaptureBox),
        new FrameworkPropertyMetadata("Not assigned", OnDisplayPropertyChanged));

    public static readonly DependencyProperty CapturePromptTextProperty = DependencyProperty.Register(
        nameof(CapturePromptText),
        typeof(string),
        typeof(HotkeyCaptureBox),
        new FrameworkPropertyMetadata("Press a shortcut", OnDisplayPropertyChanged));

    public static readonly DependencyProperty HasAssignedBindingProperty = DependencyProperty.Register(
        nameof(HasAssignedBinding),
        typeof(bool),
        typeof(HotkeyCaptureBox),
        new FrameworkPropertyMetadata(false));

    private HotkeyBinding _binding = new();

    static HotkeyCaptureBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HotkeyCaptureBox),
            new FrameworkPropertyMetadata(typeof(HotkeyCaptureBox)));
        FocusableProperty.OverrideMetadata(
            typeof(HotkeyCaptureBox),
            new FrameworkPropertyMetadata(true));
    }

    public HotkeyCaptureBox()
    {
        CommandBindings.Add(new CommandBinding(ClearCommand, (_, _) => Clear(), (_, e) => e.CanExecute = IsEnabled));
        RefreshDisplay();
    }

    public string NotAssignedText
    {
        get => (string)GetValue(NotAssignedTextProperty);
        set => SetValue(NotAssignedTextProperty, value);
    }

    public string CapturePromptText
    {
        get => (string)GetValue(CapturePromptTextProperty);
        set => SetValue(CapturePromptTextProperty, value);
    }

    public bool HasAssignedBinding
    {
        get => (bool)GetValue(HasAssignedBindingProperty);
        private set => SetValue(HasAssignedBindingProperty, value);
    }

    public void SetBinding(HotkeyBinding? binding)
    {
        _binding = HotkeyCaptureHelper.Clone(binding);
        RefreshDisplay();
    }

    public HotkeyBinding GetBinding() => HotkeyCaptureHelper.Clone(_binding);

    public void RefreshDisplay()
    {
        HasAssignedBinding = HotkeyValidationHelper.HasAssignedKey(_binding);
        Content = HotkeyCaptureHelper.Format(_binding, NotAssignedText);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Content = CapturePromptText;
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        RefreshDisplay();
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsKeyboardFocusWithin)
        {
            if (IsClickInsideClearButton(e.OriginalSource))
            {
                base.OnPreviewMouseLeftButtonDown(e);
                return;
            }

            Focus();
            e.Handled = true;
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key is Key.Delete or Key.Back)
        {
            Clear();
            e.Handled = true;
            return;
        }

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab or Key.Escape)
        {
            return;
        }

        if (HotkeyCaptureHelper.TryCreateBinding(key, Keyboard.Modifiers, out HotkeyBinding binding))
        {
            _binding = binding;
            RefreshDisplay();
        }

        e.Handled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    private static bool IsClickInsideClearButton(object originalSource)
    {
        var current = originalSource as System.Windows.DependencyObject;
        while (current != null)
        {
            if (current is System.Windows.Controls.Button)
            {
                return true;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void Clear()
    {
        _binding = new HotkeyBinding();
        RefreshDisplay();
        Focus();
    }

    private static void OnDisplayPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((HotkeyCaptureBox)dependencyObject).RefreshDisplay();
    }
}
