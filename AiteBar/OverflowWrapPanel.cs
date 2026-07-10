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
        List<UIElement> visible = GetVisibleChildren();
        WpfSize itemSize = MeasureChildren(visible);
        int count = visible.Count;
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
        List<UIElement> visible = GetVisibleChildren();
        WpfSize itemSize = GetMaxDesiredChildSize(visible);
        if (itemSize.Width <= 0 || itemSize.Height <= 0)
        {
            return finalSize;
        }

        if (Orientation == WpfOrientation.Vertical)
        {
            ArrangeVertical(visible, finalSize, itemSize);
        }
        else
        {
            ArrangeHorizontal(visible, finalSize, itemSize);
        }

        return finalSize;
    }

    private static WpfSize MeasureChildren(List<UIElement> children)
    {
        foreach (UIElement child in children)
        {
            child.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        }

        return GetMaxDesiredChildSize(children);
    }

    private static WpfSize GetMaxDesiredChildSize(List<UIElement> children)
    {
        double width = 0;
        double height = 0;
        foreach (UIElement child in children)
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

        int firstColumnCount = Math.Max(0, firstColumnCapacity);
        int remainingAfterFirst = count - firstColumnCount;
        int secondColumnCapacity = GetCapacity(availableSize.Height - overflowReserve, itemSize.Height, remainingAfterFirst);
        if (remainingAfterFirst <= secondColumnCapacity)
        {
            double height = Math.Max(
                leadingReserve + (firstColumnCount * itemSize.Height),
                overflowReserve + (remainingAfterFirst * itemSize.Height));
            return new WpfSize(2 * itemSize.Width, height);
        }

        int secondColumnCount = Math.Max(0, secondColumnCapacity);
        int thirdColumnCount = remainingAfterFirst - secondColumnCount;
        double totalHeight = Math.Max(
            leadingReserve + (firstColumnCount * itemSize.Height),
            Math.Max(
                overflowReserve + (secondColumnCount * itemSize.Height),
                overflowReserve + (thirdColumnCount * itemSize.Height)));

        return new WpfSize(3 * itemSize.Width, totalHeight);
    }

    private static WpfSize MeasureHorizontal(WpfSize availableSize, WpfSize itemSize, int count)
    {
        int columnsPerRow = GetCapacity(availableSize.Width, itemSize.Width, count);
        int rows = (int)Math.Ceiling(count / (double)columnsPerRow);
        int visibleRows = Math.Min(PanelLayoutHelper.MaxUserBands, rows);
        int visibleColumns = Math.Min(count, columnsPerRow);

        return new WpfSize(visibleColumns * itemSize.Width, visibleRows * itemSize.Height);
    }

    private void ArrangeVertical(List<UIElement> children, WpfSize finalSize, WpfSize itemSize)
    {
        int count = children.Count;
        double leadingReserve = Math.Max(0, LeadingPrimaryReserve);
        double overflowReserve = Math.Max(0, OverflowPrimaryReserve);
        int firstColumnCapacity = GetCapacity(finalSize.Height - leadingReserve, itemSize.Height, count);

        int firstColumnCount = Math.Min(count, firstColumnCapacity);
        int remainingAfterFirst = count - firstColumnCount;
        int secondColumnCapacity = GetCapacity(finalSize.Height - overflowReserve, itemSize.Height, remainingAfterFirst);
        int secondColumnCount = Math.Min(remainingAfterFirst, secondColumnCapacity);

        for (int index = 0; index < count; index++)
        {
            int column;
            int row;
            double verticalOffset;

            if (index < firstColumnCount)
            {
                column = 0;
                row = index;
                verticalOffset = leadingReserve;
            }
            else if (index < firstColumnCount + secondColumnCount)
            {
                column = 1;
                row = index - firstColumnCount;
                verticalOffset = overflowReserve;
            }
            else
            {
                column = 2;
                row = index - firstColumnCount - secondColumnCount;
                verticalOffset = overflowReserve;
            }

            children[index].Arrange(new Rect(
                column * itemSize.Width,
                verticalOffset + (row * itemSize.Height),
                itemSize.Width,
                itemSize.Height));
        }
    }

    private void ArrangeHorizontal(List<UIElement> children, WpfSize finalSize, WpfSize itemSize)
    {
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

    public Rect GetArrangedRectForIndex(int index, int totalCount, WpfSize finalSize)
    {
        WpfSize itemSize = GetMaxDesiredChildSize(GetVisibleChildren());
        if (itemSize.Width <= 0 || itemSize.Height <= 0)
        {
            return new Rect();
        }

        if (Orientation == WpfOrientation.Vertical)
        {
            double leadingReserve = Math.Max(0, LeadingPrimaryReserve);
            double overflowReserve = Math.Max(0, OverflowPrimaryReserve);
            int firstColumnCapacity = GetCapacity(finalSize.Height - leadingReserve, itemSize.Height, totalCount);
            int firstColumnCount = Math.Min(totalCount, firstColumnCapacity);
            int remainingAfterFirst = totalCount - firstColumnCount;
            int secondColumnCapacity = GetCapacity(finalSize.Height - overflowReserve, itemSize.Height, remainingAfterFirst);
            int secondColumnCount = Math.Min(remainingAfterFirst, secondColumnCapacity);

            int column;
            int row;
            double verticalOffset;

            if (index < firstColumnCount)
            {
                column = 0;
                row = index;
                verticalOffset = leadingReserve;
            }
            else if (index < firstColumnCount + secondColumnCount)
            {
                column = 1;
                row = index - firstColumnCount;
                verticalOffset = overflowReserve;
            }
            else
            {
                column = 2;
                row = index - firstColumnCount - secondColumnCount;
                verticalOffset = overflowReserve;
            }

            return new Rect(
                column * itemSize.Width,
                verticalOffset + (row * itemSize.Height),
                itemSize.Width,
                itemSize.Height);
        }
        else
        {
            int columnsPerRow = GetCapacity(finalSize.Width, itemSize.Width, totalCount);
            int row = index / columnsPerRow;
            int column = index % columnsPerRow;
            return new Rect(
                column * itemSize.Width,
                row * itemSize.Height,
                itemSize.Width,
                itemSize.Height);
        }
    }
}
