using System;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class TaskbarPositionIndicatorWindow : Window
{
    public event EventHandler? TogglePanelRequested;
    public event EventHandler? ShowSettingsRequested;
    public event EventHandler? HideIndicatorRequested;

    private static FontFamily? _menuIconFont;
    private static FontFamily MenuIconFont => _menuIconFont ??= FontHelper.Resolve(FontHelper.FluentKey);

    private static class MenuIcons
    {
        public const int Open = 62849; // ic_fluent_open_16_regular
        public const int Settings = 63144; // ic_fluent_settings_16_regular
        public const int Hide = 62314; // U+F36A
    }

    public TaskbarPositionIndicatorWindow()
    {
        InitializeComponent();
        InitializeContextMenu();
    }

    public void Initialize(AppSettingsService appSettingsService)
    {
        // This method is kept for backwards compatibility with TaskbarPositionIndicatorService
    }

    private void InitializeContextMenu()
    {
        LocalizationService.EnsureAppliedCulture();
        ContextMenu = new ContextMenu
        {
            Style = (Style)FindResource("DarkContextMenu")
        };

        var showPanelItem = CreateMenuItem(FluentGlyph(MenuIcons.Open), LocalizationService.Get("ShowPanel"),
            (s, e) => TogglePanelRequested?.Invoke(this, EventArgs.Empty));

        var settingsItem = CreateMenuItem(FluentGlyph(MenuIcons.Settings), LocalizationService.Get("Menu_ProgramSettings"),
            (s, e) => ShowSettingsRequested?.Invoke(this, EventArgs.Empty));

        var hideItem = CreateMenuItem(FluentGlyph(MenuIcons.Hide), LocalizationService.Get("HideIndicator"),
            (s, e) => HideIndicatorRequested?.Invoke(this, EventArgs.Empty));

        ContextMenu.Items.Add(showPanelItem);
        ContextMenu.Items.Add(settingsItem);
        ContextMenu.Items.Add(hideItem);
    }

    private static string FluentGlyph(int codePoint) => char.ConvertFromUtf32(codePoint);

    private MenuItem CreateMenuItem(string glyph, string text, RoutedEventHandler? onClick = null)
    {
        var icon = new System.Windows.Controls.TextBlock
        {
            Text = glyph,
            FontFamily = MenuIconFont,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE3, 0xE3, 0xE3)),
            Style = (Style)FindResource("ContextMenuIconTextStyle")
        };

        var item = new MenuItem
        {
            Header = text,
            Style = (Style)FindResource("DarkMenuItem"),
            Padding = new Thickness(0),
            Icon = icon
        };

        if (onClick != null)
        {
            item.Click += onClick;
        }

        return item;
    }

    public void UpdateArrow(DockEdge edge)
    {
        ArrowText.Text = TaskbarGeometryHelper.GetArrowGlyph(edge);
    }

    public void UpdateTooltip(string text)
    {
        ToolTipService.SetToolTip(ArrowText, text);
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        TogglePanelRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ContextMenu != null)
        {
            ContextMenu.PlacementTarget = this;
            ContextMenu.IsOpen = true;
        }
        e.Handled = true;
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        ArrowText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ArrowText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0));
    }
}
