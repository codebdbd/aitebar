using System;
using System.Collections.Generic;
using System.Linq;

namespace AiteBar;

public static class PanelLayoutHelper
{
    public const double ButtonOuterSize = 44;
    public const double SeparatorSize = 9;
    public const double PanelChrome = 8;
    public const int MaxUserBands = 2;

    public readonly record struct UserLayout(double Primary, double Cross, int Bands);

    public readonly record struct PanelLayoutMetrics(
        bool IsVertical,
        double PanelWidth,
        double PanelHeight,
        double FixedWidth,
        double FixedHeight,
        double TrailingWidth,
        double TrailingHeight,
        double UserWidth,
        double UserHeight,
        int UserBands,
        double SystemWidth,
        double SystemHeight,
        double UserLeadingReserve,
        double UserOverflowReserve);

    private readonly record struct FixedLayout(double Primary, double Cross, UserLayout System);

    public static PanelLayoutMetrics Calculate(
        bool isVertical,
        double availablePrimary,
        double panelPercent,
        int visibleSystemButtonCount,
        int controlButtonCount,
        IReadOnlyList<int> contextCounts,
        int activeContextIndex,
        int systemContextIndex = 0,
        int trailingControlButtonCount = 0,
        bool hideControlSeparator = false)
    {
        double normalizedPercent = Math.Clamp(panelPercent, 20, 100) / 100.0;
        double maxPrimary = Math.Max(ButtonOuterSize, availablePrimary * normalizedPercent);

        List<int> counts = contextCounts.Select(count => Math.Max(0, count)).DefaultIfEmpty(0).ToList();
        int normalizedActiveIndex = counts.Count == 0 ? 0 : Math.Clamp(activeContextIndex, 0, counts.Count - 1);
        int normalizedSystemIndex = counts.Count == 0 ? 0 : Math.Clamp(systemContextIndex, 0, counts.Count - 1);

        List<(double FixedPrimary, double FixedCross, UserLayout System, UserLayout Trailing, UserLayout User, double UserLeadingReserve, double UserOverflowReserve, double PanelPrimary, double PanelCross)> perContext = [];
        for (int index = 0; index < counts.Count; index++)
        {
            int count = counts[index];
            int systemCount = index == normalizedSystemIndex ? Math.Max(0, visibleSystemButtonCount) : 0;
            int controlsCount = Math.Max(0, controlButtonCount);
            int trailingControlsCount = Math.Max(0, trailingControlButtonCount);
            bool hasUserButtons = count > 0;

            int fixedSeparatorCount = 0;
            if (systemCount > 0 && controlsCount > 0)
            {
                fixedSeparatorCount++;
            }

            if (hasUserButtons && (systemCount > 0 || controlsCount > 0) && !hideControlSeparator)
            {
                fixedSeparatorCount++;
            }

            int trailingSeparatorCount = hasUserButtons && trailingControlsCount > 0 ? 1 : 0;
            UserLayout horizontalSystemLayout = new(
                systemCount * ButtonOuterSize,
                systemCount > 0 ? ButtonOuterSize : 0,
                systemCount > 0 ? 1 : 0);
            FixedLayout fixedLayout = isVertical
                ? CalculateFixedVerticalLayout(systemCount, controlsCount, fixedSeparatorCount)
                : new FixedLayout(
                    ((systemCount + controlsCount) * ButtonOuterSize) + (fixedSeparatorCount * SeparatorSize),
                    (systemCount > 0 || controlsCount > 0) ? ButtonOuterSize : 0,
                    horizontalSystemLayout);
            double fixedPrimary = fixedLayout.Primary;
            double trailingPrimary = (trailingControlsCount * ButtonOuterSize) + (trailingSeparatorCount * SeparatorSize);
            double userOverflowReserve = isVertical && controlsCount > 0 && (systemCount > 0 || hasUserButtons)
                ? ButtonOuterSize + SeparatorSize
                : 0;
            UserLayout userLayout = CalculateUserLayoutForPanel(
                isVertical,
                count,
                maxPrimary,
                fixedPrimary,
                userOverflowReserve,
                trailingPrimary,
                out double userLeadingReserve);
            double fixedCross = fixedLayout.Cross;
            double trailingCross = trailingControlsCount > 0 ? ButtonOuterSize : 0;
            double panelPrimary = Math.Max(
                ButtonOuterSize + PanelChrome,
                (isVertical && hasUserButtons ? userLayout.Primary : fixedPrimary + userLayout.Primary) + trailingPrimary + PanelChrome);
            double panelCross = Math.Max(ButtonOuterSize + PanelChrome, Math.Max(Math.Max(fixedCross, trailingCross), userLayout.Cross) + PanelChrome);

            if (isVertical && systemCount > 1 && panelPrimary > maxPrimary)
            {
                double reservedUserPrimary = hasUserButtons ? ButtonOuterSize : 0;
                double fixedPrimaryLimit = Math.Max(
                    ButtonOuterSize,
                    maxPrimary - trailingPrimary - PanelChrome - reservedUserPrimary);
                fixedLayout = CalculateFixedVerticalLayout(systemCount, controlsCount, fixedSeparatorCount, fixedPrimaryLimit);
                fixedPrimary = fixedLayout.Primary;
                userLayout = CalculateUserLayoutForPanel(
                    isVertical,
                    count,
                    maxPrimary,
                    fixedPrimary,
                    userOverflowReserve,
                    trailingPrimary,
                    out userLeadingReserve);
                fixedCross = fixedLayout.Cross;
                panelPrimary = Math.Max(
                    ButtonOuterSize + PanelChrome,
                    (isVertical && hasUserButtons ? userLayout.Primary : fixedPrimary + userLayout.Primary) + trailingPrimary + PanelChrome);
                panelCross = Math.Max(ButtonOuterSize + PanelChrome, Math.Max(Math.Max(fixedCross, trailingCross), userLayout.Cross) + PanelChrome);
            }

            perContext.Add((fixedPrimary, fixedCross, fixedLayout.System, new UserLayout(trailingPrimary, trailingCross, 0), userLayout, userLeadingReserve, userOverflowReserve, panelPrimary, panelCross));
        }

        double maxPanelPrimary = perContext.Max(layout => layout.PanelPrimary);
        double maxPanelCross = perContext.Max(layout => layout.PanelCross);
        var active = perContext[normalizedActiveIndex];

        return isVertical
            ? new PanelLayoutMetrics(
                IsVertical: true,
                PanelWidth: active.PanelCross,
                PanelHeight: maxPanelPrimary,
                FixedWidth: active.FixedCross,
                FixedHeight: active.FixedPrimary,
                TrailingWidth: active.Trailing.Cross,
                TrailingHeight: active.Trailing.Primary,
                UserWidth: active.User.Cross,
                UserHeight: active.User.Primary,
                UserBands: active.User.Bands,
                SystemWidth: active.System.Cross,
                SystemHeight: active.System.Primary,
                UserLeadingReserve: active.UserLeadingReserve,
                UserOverflowReserve: active.UserOverflowReserve)
            : new PanelLayoutMetrics(
                IsVertical: false,
                PanelWidth: maxPanelPrimary,
                PanelHeight: maxPanelCross,
                FixedWidth: active.FixedPrimary,
                FixedHeight: active.FixedCross,
                TrailingWidth: active.Trailing.Primary,
                TrailingHeight: active.Trailing.Cross,
                UserWidth: active.User.Primary,
                UserHeight: active.User.Cross,
                UserBands: active.User.Bands,
                SystemWidth: active.System.Primary,
                SystemHeight: active.System.Cross,
                UserLeadingReserve: 0,
                UserOverflowReserve: 0);
    }

    private static UserLayout CalculateUserLayoutForPanel(
        bool isVertical,
        int count,
        double maxPrimary,
        double fixedPrimary,
        double overflowReserve,
        double trailingPrimary,
        out double userLeadingReserve)
    {
        userLeadingReserve = 0;
        if (count <= 0)
        {
            return new UserLayout(0, 0, 0);
        }

        if (!isVertical)
        {
            double userPrimaryLimit = Math.Max(ButtonOuterSize, maxPrimary - fixedPrimary - trailingPrimary - PanelChrome);
            return CalculateUserLayout(count, userPrimaryLimit);
        }

        double verticalUserPrimaryLimit = Math.Max(ButtonOuterSize, maxPrimary - trailingPrimary - PanelChrome);
        userLeadingReserve = fixedPrimary;
        return CalculateReservedVerticalUserLayout(count, verticalUserPrimaryLimit, fixedPrimary, overflowReserve);
    }

    private static UserLayout CalculateReservedVerticalUserLayout(
        int buttonCount,
        double userAreaPrimaryLimit,
        double leadingReserve,
        double overflowReserve)
    {
        int normalizedCount = Math.Max(0, buttonCount);
        if (normalizedCount == 0 || userAreaPrimaryLimit <= 0)
        {
            return new UserLayout(0, 0, 0);
        }

        double firstColumnAvailable = Math.Max(0, userAreaPrimaryLimit - leadingReserve);
        int firstColumnCapacity = (int)Math.Floor(firstColumnAvailable / ButtonOuterSize);
        if (normalizedCount <= firstColumnCapacity)
        {
            return new UserLayout(leadingReserve + (normalizedCount * ButtonOuterSize), ButtonOuterSize, 1);
        }

        int firstColumnCount = Math.Max(0, firstColumnCapacity);
        int overflowCount = normalizedCount - firstColumnCount;
        double primary = Math.Min(
            userAreaPrimaryLimit,
            Math.Max(
                leadingReserve + (firstColumnCount * ButtonOuterSize),
                overflowReserve + (overflowCount * ButtonOuterSize)));

        return new UserLayout(primary, ButtonOuterSize * MaxUserBands, MaxUserBands);
    }

    public static UserLayout CalculateUserLayout(int buttonCount, double userPrimaryLimit)
    {
        int normalizedCount = Math.Max(0, buttonCount);
        if (normalizedCount == 0 || userPrimaryLimit <= 0)
        {
            return new UserLayout(0, 0, 0);
        }

        int maxItemsPerBand = Math.Max(1, (int)Math.Floor(userPrimaryLimit / ButtonOuterSize));
        int requiredBands = (int)Math.Ceiling(normalizedCount / (double)maxItemsPerBand);
        int bands = Math.Min(MaxUserBands, Math.Max(1, requiredBands));

        int itemsPerBand = bands > 1 ? maxItemsPerBand : normalizedCount;
        double primary = Math.Min(userPrimaryLimit, itemsPerBand * ButtonOuterSize);
        double cross = bands * ButtonOuterSize;

        return new UserLayout(primary, cross, bands);
    }

    private static FixedLayout CalculateFixedVerticalLayout(
        int systemCount,
        int controlsCount,
        int separatorCount,
        double? fixedPrimaryLimit = null)
    {
        UserLayout controls = CalculateSingleColumnRows(controlsCount);
        double systemPrimaryLimit = fixedPrimaryLimit.HasValue
            ? Math.Max(ButtonOuterSize, fixedPrimaryLimit.Value - controls.Primary - (separatorCount * SeparatorSize))
            : 0;
        UserLayout system = fixedPrimaryLimit.HasValue
            ? CalculateUserLayout(systemCount, systemPrimaryLimit)
            : CalculateSingleColumnRows(systemCount);

        double primary = controls.Primary + system.Primary + (separatorCount * SeparatorSize);
        double cross = Math.Max(controls.Cross, system.Cross);

        return new FixedLayout(primary, cross, system);
    }

    private static UserLayout CalculateSingleColumnRows(int buttonCount)
    {
        int normalizedCount = Math.Max(0, buttonCount);
        return normalizedCount == 0
            ? new UserLayout(0, 0, 0)
            : new UserLayout(normalizedCount * ButtonOuterSize, ButtonOuterSize, 1);
    }
}
