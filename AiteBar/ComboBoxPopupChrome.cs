namespace AiteBar;

public static class ComboBoxPopupChrome
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ComboBoxPopupChrome),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty OpensAboveProperty = DependencyProperty.RegisterAttached(
        "OpensAbove",
        typeof(bool),
        typeof(ComboBoxPopupChrome),
        new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetOpensAbove(DependencyObject element) =>
        (bool)element.GetValue(OpensAboveProperty);

    public static void SetOpensAbove(DependencyObject element, bool value) =>
        element.SetValue(OpensAboveProperty, value);

    internal static bool IsPopupAbove(double comboTop, double popupTop) =>
        popupTop < comboTop;

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ComboBox comboBox)
        {
            return;
        }

        if ((bool)e.OldValue)
        {
            comboBox.DropDownOpened -= ComboBox_DropDownOpened;
            comboBox.DropDownClosed -= ComboBox_DropDownClosed;
        }

        if ((bool)e.NewValue)
        {
            comboBox.DropDownOpened += ComboBox_DropDownOpened;
            comboBox.DropDownClosed += ComboBox_DropDownClosed;
        }
    }

    private static void ComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox)
        {
            return;
        }

        comboBox.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!comboBox.IsDropDownOpen ||
                comboBox.Template.FindName("PART_Popup", comboBox) is not Popup { Child: FrameworkElement popupChild })
            {
                return;
            }

            double comboTop = comboBox.PointToScreen(new Point(0, 0)).Y;
            double popupTop = popupChild.PointToScreen(new Point(0, 0)).Y;
            SetOpensAbove(comboBox, IsPopupAbove(comboTop, popupTop));
        });
    }

    private static void ComboBox_DropDownClosed(object? sender, EventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            SetOpensAbove(comboBox, false);
        }
    }
}
