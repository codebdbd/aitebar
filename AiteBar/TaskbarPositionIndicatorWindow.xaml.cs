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
    public event EventHandler<IndicatorDragEventArgs>? DragRequested;
    public event EventHandler<IndicatorDragEventArgs>? DragCompleted;

    private const int DragThresholdPixels = 4;
    private NativeMethods.Win32Point _dragStart;
    private int _dragOffsetX;
    private int _dragOffsetY;
    private bool _isPointerDown;
    private bool _isDragging;

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
        ContextMenu = AppContextMenuFactory.CreateMenu(this);

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
        => AppContextMenuFactory.CreateItem(this, glyph, text, onClick);

    public void UpdateArrow(DockEdge edge)
    {
        ArrowText.Text = TaskbarGeometryHelper.GetArrowGlyph(edge);
    }

    public void UpdateTooltip(string text)
    {
        ToolTipService.SetToolTip(ArrowText, text);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var point = new NativeMethods.Win32Point();
        if (!NativeMethods.GetCursorPos(ref point))
        {
            return;
        }

        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        if (!NativeMethods.GetWindowRect(helper.Handle, out var windowRect))
        {
            return;
        }

        _dragStart = point;
        _dragOffsetX = point.X - windowRect.Left;
        _dragOffsetY = point.Y - windowRect.Top;
        _isPointerDown = true;
        _isDragging = false;
        Mouse.Capture(this);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isPointerDown || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var point = new NativeMethods.Win32Point();
        if (!NativeMethods.GetCursorPos(ref point))
        {
            return;
        }

        if (!_isDragging &&
            Math.Abs(point.X - _dragStart.X) < DragThresholdPixels &&
            Math.Abs(point.Y - _dragStart.Y) < DragThresholdPixels)
        {
            return;
        }

        _isDragging = true;
        DragRequested?.Invoke(this, new IndicatorDragEventArgs(point.X - _dragOffsetX, point.Y - _dragOffsetY));
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_isPointerDown)
        {
            return;
        }

        _isPointerDown = false;
        Mouse.Capture(null);
        if (_isDragging)
        {
            var point = new NativeMethods.Win32Point();
            if (NativeMethods.GetCursorPos(ref point))
            {
                DragCompleted?.Invoke(this, new IndicatorDragEventArgs(point.X - _dragOffsetX, point.Y - _dragOffsetY));
            }
        }
        else
        {
            TogglePanelRequested?.Invoke(this, EventArgs.Empty);
        }

        _isDragging = false;
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
        IndicatorBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x5A, 0x9E));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        IndicatorBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC));
    }
}

public sealed class IndicatorDragEventArgs(int left, int top) : EventArgs
{
    public int Left { get; } = left;
    public int Top { get; } = top;
}
