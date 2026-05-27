using System;
using System.Collections.Generic;
using System.Windows;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPanel = System.Windows.Controls.Panel;
using WpfSize = System.Windows.Size;

namespace AiteBar;

public sealed class OverflowWrapPanel : WpfPanel
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(WpfOrientation),
            typeof(OverflowWrapPanel),
            new FrameworkPropertyMetadata(
                WpfOrientation.Horizontal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty LeadingPrimaryReserveProperty =
        DependencyProperty.Register(
            nameof(LeadingPrimaryReserve),
            typeof(double),
            typeof(OverflowWrapPanel),
            new FrameworkPropertyMetadata(
                0.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty OverflowPrimaryReserveProperty =
        DependencyProperty.Register(
            nameof(OverflowPrimaryReserve),
            typeof(double),
            typeof(OverflowWrapPanel),
            new FrameworkPropertyMetadata(
                0.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public WpfOrientation Orientation
    {
        get => (WpfOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double LeadingPrimaryReserve
    {
        get => (double)GetValue(LeadingPrimaryReserveProperty);
        set => SetValue(LeadingPrimaryReserveProperty, value);
    }

    public double OverflowPrimaryReserve
    {
        get => (double)GetValue(OverflowPrimaryReserveProperty);
        set => SetValue(OverflowPrimaryReserveProperty, value);
    }

    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        WpfSize itemSize = MeasureChildren();
        int count = GetVisibleChildren().Count;
        if (count == 0)
        {
            return new WpfSize();
        }

        return Orientation == WpfOrientation.Vertical
            ? MeasureVertical(availableSize, itemSize, count)
            : MeasureHorizontal(availableSize, itemSize, count);
    }

    protected override WpfSize ArrangeOverride(WpfSize finalSize)
    {
        WpfSize itemSize = GetMaxDesiredChildSize();
        if (itemSize.Width <= 0 || itemSize.Height <= 0)
        {
            return finalSize;
        }

        if (Orientation == WpfOrientation.Vertical)
        {
            ArrangeVertical(finalSize, itemSize);
        }
        else
        {
            ArrangeHorizontal(finalSize, itemSize);
        }

        return finalSize;
    }

    private WpfSize MeasureChildren()
    {
        foreach (UIElement child in GetVisibleChildren())
        {
            child.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        }

        return GetMaxDesiredChildSize();
    }

    private WpfSize GetMaxDesiredChildSize()
    {
        double width = 0;
        double height = 0;
        foreach (UIElement child in GetVisibleChildren())
        {
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new WpfSize(width, height);
    }

    private List<UIElement> GetVisibleChildren()
    {
        var children = new List<UIElement>();
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility != Visibility.Collapsed)
            {
                children.Add(child);
            }
        }

        return children;
    }

    private WpfSize MeasureVertical(WpfSize availableSize, WpfSize itemSize, int count)
    {
        double leadingReserve = Math.Max(0, LeadingPrimaryReserve);
        double overflowReserve = Math.Max(0, OverflowPrimaryReserve);
        int firstColumnCapacity = GetCapacity(availableSize.Height - leadingReserve, itemSize.Height, count);
        if (count <= firstColumnCapacity)
        {
            return new WpfSize(itemSize.Width, leadingReserve + (count * itemSize.Height));
        }

        int overflowCount = count - firstColumnCapacity;
        double height = Math.Max(
            leadingReserve + (firstColumnCapacity * itemSize.Height),
            overflowReserve + (overflowCount * itemSize.Height));

        return new WpfSize(PanelLayoutHelper.MaxUserBands * itemSize.Width, height);
    }

    private static WpfSize MeasureHorizontal(WpfSize availableSize, WpfSize itemSize, int count)
    {
        int columnsPerRow = GetCapacity(availableSize.Width, itemSize.Width, count);
        int rows = (int)Math.Ceiling(count / (double)columnsPerRow);
        int visibleRows = Math.Min(PanelLayoutHelper.MaxUserBands, rows);
        int visibleColumns = Math.Min(count, columnsPerRow);

        return new WpfSize(visibleColumns * itemSize.Width, visibleRows * itemSize.Height);
    }

    private void ArrangeVertical(WpfSize finalSize, WpfSize itemSize)
    {
        List<UIElement> children = GetVisibleChildren();
        int count = children.Count;
        double leadingReserve = Math.Max(0, LeadingPrimaryReserve);
        double overflowReserve = Math.Max(0, OverflowPrimaryReserve);
        int firstColumnCapacity = GetCapacity(finalSize.Height - leadingReserve, itemSize.Height, count);

        for (int index = 0; index < count; index++)
        {
            bool isOverflow = index >= firstColumnCapacity;
            int column = isOverflow ? 1 : 0;
            int row = isOverflow ? index - firstColumnCapacity : index;

            children[index].Arrange(new Rect(
                column * itemSize.Width,
                (isOverflow ? overflowReserve : leadingReserve) + (row * itemSize.Height),
                itemSize.Width,
                itemSize.Height));
        }
    }

    private void ArrangeHorizontal(WpfSize finalSize, WpfSize itemSize)
    {
        List<UIElement> children = GetVisibleChildren();
        int count = children.Count;
        int columnsPerRow = GetCapacity(finalSize.Width, itemSize.Width, count);

        for (int index = 0; index < count; index++)
        {
            int row = index / columnsPerRow;
            int column = index % columnsPerRow;
            children[index].Arrange(new Rect(
                column * itemSize.Width,
                row * itemSize.Height,
                itemSize.Width,
                itemSize.Height));
        }
    }

    private static int GetCapacity(double availablePrimary, double itemPrimary, int count)
    {
        if (itemPrimary <= 0)
        {
            return Math.Max(1, count);
        }

        if (double.IsInfinity(availablePrimary) || double.IsNaN(availablePrimary))
        {
            return Math.Max(1, count);
        }

        return Math.Max(1, (int)Math.Floor(availablePrimary / itemPrimary));
    }
}
