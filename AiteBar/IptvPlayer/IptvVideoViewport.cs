using System;
using System.Windows;
using System.Windows.Controls;
using Size = System.Windows.Size;

namespace AiteBar.IptvPlayer;

public sealed class IptvVideoViewport : Decorator
{
    internal const double AspectRatio = 16.0 / 9.0;
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(bool),
        typeof(IptvVideoViewport), new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public bool Fill
    {
        get => (bool)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        Size size = Fill && double.IsFinite(constraint.Width) && double.IsFinite(constraint.Height)
            ? constraint : Fit(constraint);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Size size = Fill ? arrangeSize : Fit(arrangeSize);
        Child?.Arrange(new Rect((arrangeSize.Width - size.Width) / 2,
            (arrangeSize.Height - size.Height) / 2, size.Width, size.Height));
        return arrangeSize;
    }

    internal static Size Fit(Size available)
    {
        double width = Math.Min(available.Width, available.Height * AspectRatio);
        return double.IsInfinity(width) ? new Size() : new Size(width, width / AspectRatio);
    }

    internal static string GetCropGeometry(bool fill, double width, double height) =>
        fill && width >= 1 && height >= 1 && double.IsFinite(width) && double.IsFinite(height)
            ? FormattableString.Invariant($"{Math.Round(width)}:{Math.Round(height)}")
            : string.Empty;
}
