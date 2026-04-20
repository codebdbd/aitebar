using System;
using System.Windows;
namespace AiteBar
{
    public partial class EdgePickerControl : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty SelectedEdgeProperty =
            DependencyProperty.Register(
                nameof(SelectedEdge),
                typeof(DockEdge),
                typeof(EdgePickerControl),
                new FrameworkPropertyMetadata(DockEdge.Top, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualPropertyChanged));

        public static readonly DependencyProperty PanelPercentProperty =
            DependencyProperty.Register(
                nameof(PanelPercent),
                typeof(double),
                typeof(EdgePickerControl),
                new PropertyMetadata(80d, OnVisualPropertyChanged));

        public static readonly DependencyProperty ActivationPercentProperty =
            DependencyProperty.Register(
                nameof(ActivationPercent),
                typeof(double),
                typeof(EdgePickerControl),
                new PropertyMetadata(30d, OnVisualPropertyChanged));

        private bool _isDragging;
        private Rect _monitorRect;

        public EdgePickerControl()
        {
            InitializeComponent();
            Loaded += (_, _) => UpdateVisuals();
            MouseMove += EdgePickerControl_MouseMove;
            MouseLeftButtonUp += EdgePickerControl_MouseLeftButtonUp;
            LostMouseCapture += (_, _) => _isDragging = false;
        }

        public DockEdge SelectedEdge
        {
            get => (DockEdge)GetValue(SelectedEdgeProperty);
            set => SetValue(SelectedEdgeProperty, value);
        }

        public double PanelPercent
        {
            get => (double)GetValue(PanelPercentProperty);
            set => SetValue(PanelPercentProperty, value);
        }

        public double ActivationPercent
        {
            get => (double)GetValue(ActivationPercentProperty);
            set => SetValue(ActivationPercentProperty, value);
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EdgePickerControl picker)
            {
                picker.UpdateVisuals();
            }
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisuals();
        }

        private void EdgeZone_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element &&
                Enum.TryParse<DockEdge>(element.Tag?.ToString(), out var edge))
            {
                SelectedEdge = edge;
                UpdateVisuals();
                e.Handled = true;
            }
        }

        private void PanelIndicator_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDragging = true;
            CaptureMouse();
            UpdateEdgeFromPoint(e.GetPosition(OverlayCanvas));
            e.Handled = true;
        }

        private void EdgePickerControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            UpdateEdgeFromPoint(e.GetPosition(OverlayCanvas));
        }

        private void EdgePickerControl_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            ReleaseMouseCapture();
            UpdateEdgeFromPoint(e.GetPosition(OverlayCanvas));
        }

        private void UpdateEdgeFromPoint(System.Windows.Point point)
        {
            if (_monitorRect.Width <= 0 || _monitorRect.Height <= 0)
            {
                return;
            }

            double dx = point.X - (_monitorRect.Left + _monitorRect.Width / 2);
            double dy = point.Y - (_monitorRect.Top + _monitorRect.Height / 2);

            if (Math.Abs(dx) > Math.Abs(dy))
            {
                SelectedEdge = dx < 0 ? DockEdge.Left : DockEdge.Right;
            }
            else
            {
                SelectedEdge = dy < 0 ? DockEdge.Top : DockEdge.Bottom;
            }

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (!IsLoaded || RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0)
            {
                return;
            }

            double width = RootGrid.ActualWidth;
            double height = RootGrid.ActualHeight;
            double horizontalInset = Math.Max(18, width * 0.12);
            double verticalInset = Math.Max(16, height * 0.16);
            double monitorWidth = Math.Max(140, width - horizontalInset * 2);
            double monitorHeight = Math.Max(88, height - verticalInset * 2);

            _monitorRect = new Rect(horizontalInset, verticalInset, monitorWidth, monitorHeight);

            OverlayCanvas.Width = width;
            OverlayCanvas.Height = height;

            MonitorFrame.Width = _monitorRect.Width;
            MonitorFrame.Height = _monitorRect.Height;
            System.Windows.Controls.Canvas.SetLeft(MonitorFrame, _monitorRect.Left);
            System.Windows.Controls.Canvas.SetTop(MonitorFrame, _monitorRect.Top);

            PositionEdgeZones();
            PositionIndicators();
            UpdateLabel();
        }

        private void PositionEdgeZones()
        {
            const double zoneThickness = 20;

            TopZone.Width = _monitorRect.Width;
            TopZone.Height = zoneThickness;
            System.Windows.Controls.Canvas.SetLeft(TopZone, _monitorRect.Left);
            System.Windows.Controls.Canvas.SetTop(TopZone, _monitorRect.Top - zoneThickness / 2);

            BottomZone.Width = _monitorRect.Width;
            BottomZone.Height = zoneThickness;
            System.Windows.Controls.Canvas.SetLeft(BottomZone, _monitorRect.Left);
            System.Windows.Controls.Canvas.SetTop(BottomZone, _monitorRect.Bottom - zoneThickness / 2);

            LeftZone.Width = zoneThickness;
            LeftZone.Height = _monitorRect.Height;
            System.Windows.Controls.Canvas.SetLeft(LeftZone, _monitorRect.Left - zoneThickness / 2);
            System.Windows.Controls.Canvas.SetTop(LeftZone, _monitorRect.Top);

            RightZone.Width = zoneThickness;
            RightZone.Height = _monitorRect.Height;
            System.Windows.Controls.Canvas.SetLeft(RightZone, _monitorRect.Right - zoneThickness / 2);
            System.Windows.Controls.Canvas.SetTop(RightZone, _monitorRect.Top);
        }

        private void PositionIndicators()
        {
            double panelPercent = ClampPercent(PanelPercent);
            double activationPercent = ClampPercent(ActivationPercent);
            bool isVertical = SelectedEdge is DockEdge.Left or DockEdge.Right;

            double edgeThickness = 10;
            double panelLength = isVertical
                ? _monitorRect.Height * panelPercent / 100d
                : _monitorRect.Width * panelPercent / 100d;
            double activationLength = isVertical
                ? _monitorRect.Height * activationPercent / 100d
                : _monitorRect.Width * activationPercent / 100d;

            double panelLeft = _monitorRect.Left;
            double panelTop = _monitorRect.Top;
            double panelWidth = _monitorRect.Width;
            double panelHeight = _monitorRect.Height;

            double activationLeft = _monitorRect.Left;
            double activationTop = _monitorRect.Top;
            double activationWidth = _monitorRect.Width;
            double activationHeight = _monitorRect.Height;

            switch (SelectedEdge)
            {
                case DockEdge.Top:
                    panelWidth = panelLength;
                    panelHeight = edgeThickness;
                    panelLeft = _monitorRect.Left + (_monitorRect.Width - panelLength) / 2;
                    panelTop = _monitorRect.Top - edgeThickness / 2;

                    activationWidth = activationLength;
                    activationHeight = edgeThickness + 6;
                    activationLeft = _monitorRect.Left + (_monitorRect.Width - activationLength) / 2;
                    activationTop = _monitorRect.Top - 3;
                    break;

                case DockEdge.Bottom:
                    panelWidth = panelLength;
                    panelHeight = edgeThickness;
                    panelLeft = _monitorRect.Left + (_monitorRect.Width - panelLength) / 2;
                    panelTop = _monitorRect.Bottom - edgeThickness / 2;

                    activationWidth = activationLength;
                    activationHeight = edgeThickness + 6;
                    activationLeft = _monitorRect.Left + (_monitorRect.Width - activationLength) / 2;
                    activationTop = _monitorRect.Bottom - edgeThickness - 3;
                    break;

                case DockEdge.Left:
                    panelWidth = edgeThickness;
                    panelHeight = panelLength;
                    panelLeft = _monitorRect.Left - edgeThickness / 2;
                    panelTop = _monitorRect.Top + (_monitorRect.Height - panelLength) / 2;

                    activationWidth = edgeThickness + 6;
                    activationHeight = activationLength;
                    activationLeft = _monitorRect.Left - 3;
                    activationTop = _monitorRect.Top + (_monitorRect.Height - activationLength) / 2;
                    break;

                case DockEdge.Right:
                    panelWidth = edgeThickness;
                    panelHeight = panelLength;
                    panelLeft = _monitorRect.Right - edgeThickness / 2;
                    panelTop = _monitorRect.Top + (_monitorRect.Height - panelLength) / 2;

                    activationWidth = edgeThickness + 6;
                    activationHeight = activationLength;
                    activationLeft = _monitorRect.Right - edgeThickness - 3;
                    activationTop = _monitorRect.Top + (_monitorRect.Height - activationLength) / 2;
                    break;
            }

            ActivationIndicator.Width = activationWidth;
            ActivationIndicator.Height = activationHeight;
            System.Windows.Controls.Canvas.SetLeft(ActivationIndicator, activationLeft);
            System.Windows.Controls.Canvas.SetTop(ActivationIndicator, activationTop);

            PanelIndicator.Width = panelWidth;
            PanelIndicator.Height = panelHeight;
            System.Windows.Controls.Canvas.SetLeft(PanelIndicator, panelLeft);
            System.Windows.Controls.Canvas.SetTop(PanelIndicator, panelTop);

            HighlightZone();
        }

        private void HighlightZone()
        {
            System.Windows.Media.Brush activeBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#12007ACC"));
            System.Windows.Media.Brush transparent = System.Windows.Media.Brushes.Transparent;

            TopZone.Background = SelectedEdge == DockEdge.Top ? activeBrush : transparent;
            BottomZone.Background = SelectedEdge == DockEdge.Bottom ? activeBrush : transparent;
            LeftZone.Background = SelectedEdge == DockEdge.Left ? activeBrush : transparent;
            RightZone.Background = SelectedEdge == DockEdge.Right ? activeBrush : transparent;
        }

        private void UpdateLabel()
        {
            EdgeLabel.Text = SelectedEdge switch
            {
                DockEdge.Top => "Сверху",
                DockEdge.Bottom => "Снизу",
                DockEdge.Left => "Слева",
                DockEdge.Right => "Справа",
                _ => ""
            };

            System.Windows.Controls.Canvas.SetLeft(EdgeLabel, _monitorRect.Left);
            System.Windows.Controls.Canvas.SetTop(EdgeLabel, Math.Max(0, _monitorRect.Top - 24));
        }

        private static double ClampPercent(double value)
        {
            return Math.Max(5, Math.Min(100, value));
        }
    }
}
