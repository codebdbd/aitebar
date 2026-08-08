using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AiteBar;

/// <summary>
/// Draws a glyph by its ink bounds, keeping icons centered independently of font baselines.
/// </summary>
internal sealed class CenteredGlyphTextBlock : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(CenteredGlyphTextBlock),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
        typeof(CenteredGlyphTextBlock), new FrameworkPropertyMetadata(System.Windows.SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
        typeof(CenteredGlyphTextBlock), new FrameworkPropertyMetadata(System.Windows.SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontStyleProperty = TextElement.FontStyleProperty.AddOwner(
        typeof(CenteredGlyphTextBlock), new FrameworkPropertyMetadata(FontStyles.Normal, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(
        typeof(CenteredGlyphTextBlock), new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontStretchProperty = TextElement.FontStretchProperty.AddOwner(
        typeof(CenteredGlyphTextBlock), new FrameworkPropertyMetadata(FontStretches.Normal, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(CenteredGlyphTextBlock), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public System.Windows.FontStyle FontStyle { get => (System.Windows.FontStyle)GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public FontStretch FontStretch { get => (FontStretch)GetValue(FontStretchProperty); set => SetValue(FontStretchProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (string.IsNullOrEmpty(Text) || RenderSize.Width <= 0 || RenderSize.Height <= 0)
        {
            return;
        }

        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var formatted = new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            FlowDirection,
            typeface,
            FontSize,
            Foreground,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        Geometry geometry = formatted.BuildGeometry(new Point());
        Rect bounds = geometry.Bounds;
        if (bounds.IsEmpty)
        {
            return;
        }

        drawingContext.PushTransform(new TranslateTransform(
            (RenderSize.Width - bounds.Width) / 2 - bounds.X,
            (RenderSize.Height - bounds.Height) / 2 - bounds.Y));
        drawingContext.DrawGeometry(Foreground, null, geometry);
        drawingContext.Pop();
    }
}
