using System;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using WpfClipboard = System.Windows.Clipboard;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    public sealed class ScreenColorPickerWindow : Window
    {
        private readonly Drawing.Bitmap _screen;
        private readonly int _left;
        private readonly int _top;
        private readonly Canvas _root;
        private readonly Border _previewPanel;
        private readonly Border _colorSwatch;
        private readonly System.Windows.Controls.Image _magnifier;
        private readonly TextBlock _hexText;
        private readonly TextBlock _rgbText;

        private const int ZoomPixels = 11;
        private const int ZoomSize = 110;
        private const int PreviewPanelWidth = 226;
        private const int PreviewPanelHeight = 206;

        public ScreenColorPickerWindow()
        {
            var bounds = Forms.SystemInformation.VirtualScreen;
            _left = bounds.Left;
            _top = bounds.Top;
            _screen = new Drawing.Bitmap(bounds.Width, bounds.Height);

            using (var graphics = Drawing.Graphics.FromImage(_screen))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
            }

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(32, 0, 0, 0));
            Topmost = true;
            ShowInTaskbar = false;
            Cursor = System.Windows.Input.Cursors.Cross;
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            Focusable = true;

            _magnifier = new System.Windows.Controls.Image
            {
                Width = ZoomSize,
                Height = ZoomSize,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(_magnifier, BitmapScalingMode.NearestNeighbor);

            _colorSwatch = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(90, 90, 94))
            };

            _hexText = new TextBlock
            {
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold
            };

            _rgbText = new TextBlock
            {
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 190, 196)),
                FontSize = 11
            };

            _previewPanel = new Border
            {
                Width = PreviewPanelWidth,
                MinHeight = PreviewPanelHeight,
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(244, 37, 37, 38)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 74)),
                BorderThickness = new Thickness(1),
                Child = new StackPanel
                {
                    Children =
                    {
                        new Border
                        {
                            Width = ZoomSize,
                            Height = ZoomSize,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 92, 98)),
                            BorderThickness = new Thickness(1),
                            Child = new Grid
                            {
                                Children =
                                {
                                    _magnifier,
                                    new Border
                                    {
                                        Width = ZoomSize / ZoomPixels,
                                        Height = ZoomSize / ZoomPixels,
                                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                                        BorderThickness = new Thickness(1),
                                        BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204))
                                    }
                                }
                            }
                        },
                        new Grid
                        {
                            Margin = new Thickness(0, 10, 0, 0),
                            ColumnDefinitions =
                            {
                                new ColumnDefinition { Width = GridLength.Auto },
                                new ColumnDefinition { Width = new GridLength(10) },
                                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                            },
                            Children =
                            {
                                _colorSwatch,
                                CreateValuePanel()
                            }
                        },
                        new TextBlock
                        {
                            Text = LocalizationService.Get("ColorPicker_Instruction"),
                            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(150, 150, 156)),
                            FontSize = 10,
                            Margin = new Thickness(0, 10, 0, 0),
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            };

            _root = new Canvas();
            _root.Children.Add(_previewPanel);
            Content = _root;

            Loaded += (_, _) =>
            {
                Focus();
                UpdatePreview(PointToScreen(Mouse.GetPosition(this)));
            };
        }

        private StackPanel CreateValuePanel()
        {
            var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(_hexText);
            panel.Children.Add(_rgbText);
            Grid.SetColumn(panel, 2);
            return panel;
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            UpdatePreview(PointToScreen(e.GetPosition(this)));
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            var position = PointToScreen(e.GetPosition(this));
            var color = GetScreenPixel(position);
            string hex = ToHex(color);

            WpfClipboard.SetText(hex);
            Close();
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }

            base.OnKeyDown(e);
        }

        private void UpdatePreview(System.Windows.Point screenPoint)
        {
            var color = GetScreenPixel(screenPoint);
            string hex = ToHex(color);
            _hexText.Text = hex;
            _rgbText.Text = $"RGB {color.R}, {color.G}, {color.B}";
            _colorSwatch.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
            _magnifier.Source = BuildMagnifierBitmap(screenPoint);

            double localX = screenPoint.X - _left;
            double localY = screenPoint.Y - _top;
            double panelX = localX + 24;
            double panelY = localY + 24;

            if (panelX + _previewPanel.Width > Width - 8)
            {
                panelX = localX - _previewPanel.Width - 24;
            }

            if (panelY + PreviewPanelHeight > Height - 8)
            {
                panelY = localY - PreviewPanelHeight - 24;
            }

            Canvas.SetLeft(_previewPanel, Math.Clamp(panelX, 8, Math.Max(8, Width - _previewPanel.Width - 8)));
            Canvas.SetTop(_previewPanel, Math.Clamp(panelY, 8, Math.Max(8, Height - PreviewPanelHeight - 8)));
        }

        private Drawing.Color GetScreenPixel(System.Windows.Point screenPoint)
        {
            int x = Math.Clamp((int)Math.Round(screenPoint.X) - _left, 0, _screen.Width - 1);
            int y = Math.Clamp((int)Math.Round(screenPoint.Y) - _top, 0, _screen.Height - 1);
            return _screen.GetPixel(x, y);
        }

        private BitmapSource BuildMagnifierBitmap(System.Windows.Point screenPoint)
        {
            int centerX = Math.Clamp((int)Math.Round(screenPoint.X) - _left, 0, _screen.Width - 1);
            int centerY = Math.Clamp((int)Math.Round(screenPoint.Y) - _top, 0, _screen.Height - 1);
            var pixels = new byte[ZoomPixels * ZoomPixels * 4];
            int half = ZoomPixels / 2;

            for (int y = 0; y < ZoomPixels; y++)
            {
                for (int x = 0; x < ZoomPixels; x++)
                {
                    int sourceX = Math.Clamp(centerX + x - half, 0, _screen.Width - 1);
                    int sourceY = Math.Clamp(centerY + y - half, 0, _screen.Height - 1);
                    var color = _screen.GetPixel(sourceX, sourceY);
                    int offset = ((y * ZoomPixels) + x) * 4;
                    pixels[offset] = color.B;
                    pixels[offset + 1] = color.G;
                    pixels[offset + 2] = color.R;
                    pixels[offset + 3] = 255;
                }
            }

            var bitmap = BitmapSource.Create(
                ZoomPixels,
                ZoomPixels,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                pixels,
                ZoomPixels * 4);
            bitmap.Freeze();
            return bitmap;
        }

        private static string ToHex(Drawing.Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        protected override void OnClosed(EventArgs e)
        {
            _screen.Dispose();
            base.OnClosed(e);
        }
    }
}
