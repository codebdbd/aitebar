using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AiteBar;

internal static class AppContextMenuFactory
{
    private static readonly Brush DefaultIconBrush =
        CreateFrozenBrush(0xE3, 0xE3, 0xE3);
    private static readonly Brush DangerBrush =
        CreateFrozenBrush(0xFF, 0x52, 0x52);

    public static ContextMenu CreateMenu(FrameworkElement resourceOwner)
    {
        var menu = new ContextMenu
        {
            Style = (Style)resourceOwner.FindResource("DarkContextMenu")
        };
        menu.SetValue(TextBlock.LineHeightProperty, double.NaN);
        menu.SetValue(
            TextBlock.LineStackingStrategyProperty,
            LineStackingStrategy.MaxHeight);
        return menu;
    }

    public static MenuItem CreateItem(
        FrameworkElement resourceOwner,
        string glyph,
        string text,
        RoutedEventHandler? onClick = null,
        bool isDanger = false,
        bool isActive = false,
        bool isEnabled = true,
        string? inputGesture = null,
        FontFamily? iconFont = null)
    {
        Brush iconBrush = isDanger
            ? DangerBrush
            : isActive
                ? (Brush)resourceOwner.FindResource("AccentColor")
                : DefaultIconBrush;
        var icon = new CenteredGlyphTextBlock
        {
            Text = glyph,
            FontFamily = iconFont ?? FontHelper.Resolve(FontHelper.FluentKey),
            Foreground = iconBrush,
            Style = (Style)resourceOwner.FindResource("ContextMenuIconTextStyle")
        };
        var item = new MenuItem
        {
            Header = text,
            Style = (Style)resourceOwner.FindResource("DarkMenuItem"),
            Padding = new Thickness(0),
            Icon = icon,
            IsEnabled = isEnabled,
            InputGestureText = inputGesture ?? string.Empty
        };

        if (isDanger)
        {
            item.Foreground = iconBrush;
        }

        if (onClick is not null)
        {
            item.Click += onClick;
        }

        return item;
    }

    public static Separator CreateSeparator(FrameworkElement resourceOwner) =>
        new()
        {
            Style = (Style)resourceOwner.FindResource("DarkMenuSeparator")
        };

    private static Brush CreateFrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(
            System.Windows.Media.Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
