using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AiteBar;

internal sealed class MarqueeTextBlock : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(MarqueeTextBlock),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
        typeof(MarqueeTextBlock), new FrameworkPropertyMetadata(System.Windows.SystemFonts.MessageFontFamily, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
        typeof(MarqueeTextBlock), new FrameworkPropertyMetadata(System.Windows.SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty FontStyleProperty = TextElement.FontStyleProperty.AddOwner(
        typeof(MarqueeTextBlock), new FrameworkPropertyMetadata(FontStyles.Normal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(
        typeof(MarqueeTextBlock), new FrameworkPropertyMetadata(FontWeights.Normal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty FontStretchProperty = TextElement.FontStretchProperty.AddOwner(
        typeof(MarqueeTextBlock), new FrameworkPropertyMetadata(FontStretches.Normal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnVisualPropertyChanged));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(MarqueeTextBlock), new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty ScrollOffsetProperty = DependencyProperty.Register(
        nameof(ScrollOffset), typeof(double), typeof(MarqueeTextBlock),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public System.Windows.FontStyle FontStyle { get => (System.Windows.FontStyle)GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public FontStretch FontStretch { get => (FontStretch)GetValue(FontStretchProperty); set => SetValue(FontStretchProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        FormattedText formatted = CreateFormattedText();
        double desiredWidth = double.IsInfinity(availableSize.Width)
            ? formatted.WidthIncludingTrailingWhitespace
            : Math.Min(formatted.WidthIncludingTrailingWhitespace, availableSize.Width);
        double desiredHeight = formatted.Height;
        return new System.Windows.Size(desiredWidth, desiredHeight);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (RenderSize.Width <= 0 || RenderSize.Height <= 0 || string.IsNullOrWhiteSpace(Text))
        {
            return;
        }

        FormattedText formatted = CreateFormattedText();
        drawingContext.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        drawingContext.DrawText(formatted, new Point(-ScrollOffset, (RenderSize.Height - formatted.Height) / 2));
        drawingContext.Pop();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        StartMarqueeIfNeeded();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        StopMarquee();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (!IsMouseOver)
        {
            StopMarquee();
        }
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarqueeTextBlock marquee && !marquee.IsMouseOver)
        {
            marquee.StopMarquee();
        }
    }

    private FormattedText CreateFormattedText() =>
        new(
            Text ?? string.Empty,
            CultureInfo.CurrentUICulture,
            FlowDirection,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            Foreground,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private void StartMarqueeIfNeeded()
    {
        FormattedText formatted = CreateFormattedText();
        double overflow = formatted.WidthIncludingTrailingWhitespace - RenderSize.Width;
        if (overflow <= 8)
        {
            StopMarquee();
            return;
        }

        double durationSeconds = Math.Clamp(overflow / 28d, 1.8, 6.0);
        var animation = new DoubleAnimation
        {
            From = 0,
            To = overflow + 12,
            BeginTime = TimeSpan.FromMilliseconds(450),
            Duration = TimeSpan.FromSeconds(durationSeconds),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        BeginAnimation(ScrollOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void StopMarquee()
    {
        BeginAnimation(ScrollOffsetProperty, null);
        ScrollOffset = 0;
    }
}
