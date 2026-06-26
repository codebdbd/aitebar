using System;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AiteBar;

[SupportedOSPlatform("windows6.1")]
public partial class ColorPickerDialog : DarkWindow
{
    private bool _isUpdating;
    private readonly string[] _presetColors = new string[]
    {
        "#000000", "#1D1D1F", "#333333", "#48484A", "#5E5E60", "#747476", "#8A8A8C", "#A0A0A2", "#B6B6B8", "#CCCCCE", "#E2E2E4", "#FFFFFF",
        "#FF0000", "#FF6B00", "#FFD300", "#8FF900", "#00FF94", "#00FFFF", "#0094FF", "#0026FF", "#7600FF", "#FF00FF", "#FF0094",
        "#7A0000", "#9B4A00", "#998A00", "#4F8A00", "#008A5C", "#008A8A", "#005599", "#00147A", "#460099", "#990099", "#990055",
        "#FF9999", "#FFCC99", "#FFFF99", "#CCFF99", "#99FFCC", "#99FFFF", "#99CCFF", "#9999FF", "#CC99FF", "#FF99FF", "#FF99CC"
    };

    public System.Windows.Media.Color SelectedColor { get; private set; }

    public ColorPickerDialog(System.Windows.Media.Color initialColor)
    {
        InitializeComponent();
        SelectedColor = initialColor;
        InitializePalette();
        UpdateControlsFromColor(initialColor);
    }

    private void InitializePalette()
    {
        foreach (string hex in _presetColors)
        {
            if (TryParseColor(hex, out System.Windows.Media.Color color))
            {
                Button btn = new Button
                {
                    Background = new SolidColorBrush(color),
                    Style = (Style)FindResource("ColorSwatchButtonStyle")
                };
                btn.Click += (s, e) =>
                {
                    UpdateControlsFromColor(color);
                };
                ColorPalette.Children.Add(btn);
            }
        }
    }

    private void UpdateControlsFromColor(System.Windows.Media.Color color)
    {
        _isUpdating = true;
        try
        {
            var (h, s, v) = RgbToHsv(color.R, color.G, color.B);
            
            SliderHue.Value = h;
            SliderSaturation.Value = s;
            SliderValue.Value = v;
            
            TxtHue.Text = $"{(int)h}";
            TxtSaturation.Text = $"{(int)s}%";
            TxtValue.Text = $"{(int)v}%";
            TxtHex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            
            ColorPreview.Background = new SolidColorBrush(color);
            SelectedColor = color;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void SliderHue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
        UpdateFromHsv();
    }

    private void SliderSaturation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
        UpdateFromHsv();
    }

    private void SliderValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;
        UpdateFromHsv();
    }

    private void UpdateFromHsv()
    {
        _isUpdating = true;
        try
        {
            var (r, g, b) = HsvToRgb(SliderHue.Value, SliderSaturation.Value, SliderValue.Value);
            System.Windows.Media.Color color = System.Windows.Media.Color.FromRgb(r, g, b);
            
            TxtHue.Text = $"{(int)SliderHue.Value}";
            TxtSaturation.Text = $"{(int)SliderSaturation.Value}%";
            TxtValue.Text = $"{(int)SliderValue.Value}%";
            TxtHex.Text = $"#{r:X2}{g:X2}{b:X2}";
            ColorPreview.Background = new SolidColorBrush(color);
            SelectedColor = color;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void TxtHex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (TryParseColor(TxtHex.Text, out System.Windows.Media.Color color))
        {
            UpdateControlsFromColor(color);
        }
    }

    private bool TryParseColor(string hex, out System.Windows.Media.Color color)
    {
        color = System.Windows.Media.Colors.Black;
        try
        {
            if (hex.StartsWith("#") && hex.Length == 7)
            {
                byte r = Convert.ToByte(hex.Substring(1, 2), 16);
                byte g = Convert.ToByte(hex.Substring(3, 2), 16);
                byte b = Convert.ToByte(hex.Substring(5, 2), 16);
                color = System.Windows.Media.Color.FromRgb(r, g, b);
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    // Преобразование RGB -> HSV
    private (double h, double s, double v) RgbToHsv(byte r, byte g, byte b)
    {
        double rf = r / 255.0;
        double gf = g / 255.0;
        double bf = b / 255.0;

        double max = Math.Max(rf, Math.Max(gf, bf));
        double min = Math.Min(rf, Math.Min(gf, bf));
        double delta = max - min;

        double h = 0;
        double s = max == 0 ? 0 : (delta / max);
        double v = max;

        if (delta > 0)
        {
            if (max == rf)
                h = 60 * (((gf - bf) / delta) % 6);
            else if (max == gf)
                h = 60 * (((bf - rf) / delta) + 2);
            else
                h = 60 * (((rf - gf) / delta) + 4);
        }

        if (h < 0) h += 360;

        return (h, s * 100, v * 100);
    }

    // Преобразование HSV -> RGB
    private (byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
    {
        s /= 100;
        v /= 100;

        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = v - c;

        double rf, gf, bf;

        if (h >= 0 && h < 60) { rf = c; gf = x; bf = 0; }
        else if (h >= 60 && h < 120) { rf = x; gf = c; bf = 0; }
        else if (h >= 120 && h < 180) { rf = 0; gf = c; bf = x; }
        else if (h >= 180 && h < 240) { rf = 0; gf = x; bf = c; }
        else if (h >= 240 && h < 300) { rf = x; gf = 0; bf = c; }
        else { rf = c; gf = 0; bf = x; }

        byte r = (byte)((rf + m) * 255);
        byte g = (byte)((gf + m) * 255);
        byte b = (byte)((bf + m) * 255);

        return (r, g, b);
    }

    private void BtnOK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }
}
