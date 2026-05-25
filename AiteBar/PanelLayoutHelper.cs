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
        int UserBands);

    public static PanelLayoutMetrics Calculate(
        bool isVertical,
        double availablePrimary,
        double panelPercent,
        int visibleSystemButtonCount,
        int controlButtonCount,
        IReadOnlyList<int> contextCounts,
        int activeContextIndex,
        int systemContextIndex = 0,
        int trailingControlButtonCount = 0)
    {
        double normalizedPercent = Math.Clamp(panelPercent, 20, 100) / 100.0;
        double maxPrimary = isVertical
            ? Math.Max(ButtonOuterSize, availablePrimary)
            : Math.Max(ButtonOuterSize, availablePrimary * normalizedPercent);

        List<int> counts = contextCounts.Select(count => Math.Max(0, count)).DefaultIfEmpty(0).ToList();
        int normalizedActiveIndex = counts.Count == 0 ? 0 : Math.Clamp(activeContextIndex, 0, counts.Count - 1);
        int normalizedSystemIndex = counts.Count == 0 ? 0 : Math.Clamp(systemContextIndex, 0, counts.Count - 1);

        List<(double FixedPrimary, double FixedCross, UserLayout Trailing, UserLayout User, double PanelPrimary, double PanelCross)> perContext = [];
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

            if (hasUserButtons && (systemCount > 0 || controlsCount > 0))
            {
                fixedSeparatorCount++;
            }

            int trailingSeparatorCount = hasUserButtons && trailingControlsCount > 0 ? 1 : 0;
            double fixedPrimary = ((systemCount + controlsCount) * ButtonOuterSize) + (fixedSeparatorCount * SeparatorSize);
            double trailingPrimary = (trailingControlsCount * ButtonOuterSize) + (trailingSeparatorCount * SeparatorSize);
            double userPrimaryLimit = hasUserButtons
                ? Math.Max(ButtonOuterSize, maxPrimary - fixedPrimary - trailingPrimary - PanelChrome)
                : 0;
            UserLayout userLayout = CalculateUserLayout(count, userPrimaryLimit);
            double fixedCross = (systemCount > 0 || controlsCount > 0) ? ButtonOuterSize : 0;
            double trailingCross = trailingControlsCount > 0 ? ButtonOuterSize : 0;
            double panelPrimary = Math.Max(ButtonOuterSize + PanelChrome, fixedPrimary + userLayout.Primary + trailingPrimary + PanelChrome);
            double panelCross = Math.Max(ButtonOuterSize + PanelChrome, Math.Max(Math.Max(fixedCross, trailingCross), userLayout.Cross) + PanelChrome);

            perContext.Add((fixedPrimary, fixedCross, new UserLayout(trailingPrimary, trailingCross, 0), userLayout, panelPrimary, panelCross));
        }

        double maxPanelPrimary = perContext.Max(layout => layout.PanelPrimary);
        double maxPanelCross = perContext.Max(layout => layout.PanelCross);
        var active = perContext[normalizedActiveIndex];

        return isVertical
            ? new PanelLayoutMetrics(
                IsVertical: true,
                PanelWidth: maxPanelCross,
                PanelHeight: maxPanelPrimary,
                FixedWidth: active.FixedCross,
                FixedHeight: active.FixedPrimary,
                TrailingWidth: active.Trailing.Cross,
                TrailingHeight: active.Trailing.Primary,
                UserWidth: active.User.Cross,
                UserHeight: active.User.Primary,
                UserBands: active.User.Bands)
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
                UserBands: active.User.Bands);
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

        int itemsPerBand = Math.Min(maxItemsPerBand, (int)Math.Ceiling(normalizedCount / (double)bands));
        double primary = Math.Min(userPrimaryLimit, itemsPerBand * ButtonOuterSize);
        double cross = bands * ButtonOuterSize;

        return new UserLayout(primary, cross, bands);
    }
}
