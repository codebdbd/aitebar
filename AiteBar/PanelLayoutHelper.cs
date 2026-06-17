using System;
using System.Collections.Generic;
using System.Linq;

namespace AiteBar;

public static class PanelLayoutHelper
{
    public const double ButtonOuterSize = Constants.ButtonOuterSize;
    public const double SeparatorSize = Constants.SeparatorSize;
    public const double PanelChrome = Constants.PanelChrome;
    public const int MaxUserBands = 3;

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

    public static PanelLayoutMetrics Calculate(
        bool isVertical,
        double availablePrimary,
        double panelPercent,
        int totalButtonCount,
        int controlButtonCount,
        int trailingControlButtonCount)
    {
        double normalizedPercent = Math.Clamp(panelPercent, 50, 100) / 100.0;
        double maxPrimary = Math.Max(ButtonOuterSize, availablePrimary * normalizedPercent);

        int fixedSeparatorCount = totalButtonCount > 0 ? 1 : 0;
        double fixedPrimary = (controlButtonCount * ButtonOuterSize) + (fixedSeparatorCount * SeparatorSize);
        double fixedCross = (controlButtonCount > 0 || totalButtonCount > 0) ? ButtonOuterSize : 0;
        double trailingPrimary = (trailingControlButtonCount * ButtonOuterSize) + (totalButtonCount > 0 && trailingControlButtonCount > 0 ? SeparatorSize : 0);
        double trailingCross = trailingControlButtonCount > 0 ? ButtonOuterSize : 0;

        (UserLayout userLayout, double userLeadingReserve, double userOverflowReserve) = isVertical
            ? CalculateVerticalUserSection(totalButtonCount, maxPrimary, fixedPrimary, trailingPrimary)
            : CalculateHorizontalUserSection(totalButtonCount, maxPrimary, fixedPrimary, trailingPrimary);

        double panelPrimary = Math.Max(
            ButtonOuterSize + PanelChrome,
            (isVertical ? userLayout.Primary + trailingPrimary : fixedPrimary + userLayout.Primary + trailingPrimary) + PanelChrome);
        double panelCross = Math.Max(ButtonOuterSize + PanelChrome, Math.Max(Math.Max(fixedCross, trailingCross), userLayout.Cross) + PanelChrome);

        return isVertical
            ? new PanelLayoutMetrics(
                IsVertical: true,
                PanelWidth: panelCross,
                PanelHeight: panelPrimary,
                FixedWidth: fixedCross,
                FixedHeight: fixedPrimary,
                TrailingWidth: trailingCross,
                TrailingHeight: trailingPrimary,
                UserWidth: userLayout.Cross,
                UserHeight: userLayout.Primary,
                UserBands: userLayout.Bands,
                SystemWidth: 0,
                SystemHeight: 0,
                UserLeadingReserve: userLeadingReserve,
                UserOverflowReserve: userOverflowReserve)
            : new PanelLayoutMetrics(
                IsVertical: false,
                PanelWidth: panelPrimary,
                PanelHeight: panelCross,
                FixedWidth: fixedPrimary,
                FixedHeight: fixedCross,
                TrailingWidth: trailingPrimary,
                TrailingHeight: trailingCross,
                UserWidth: userLayout.Primary,
                UserHeight: userLayout.Cross,
                UserBands: userLayout.Bands,
                SystemWidth: 0,
                SystemHeight: 0,
                UserLeadingReserve: 0,
                UserOverflowReserve: 0);
    }

    private static (UserLayout User, double LeadingReserve, double OverflowReserve) CalculateVerticalUserSection(
        int count,
        double maxPrimary,
        double fixedPrimary,
        double trailingPrimary)
    {
        double userOverflowReserve = 0;
        UserLayout userLayout = CalculateUserLayoutForPanel(
            isVertical: true,
            count,
            maxPrimary,
            fixedPrimary,
            userOverflowReserve,
            trailingPrimary,
            out double userLeadingReserve);

        // Check if we need additional reserves for 2nd or 3rd column
        if (count > 0)
        {
            int requiredBands = CalculateRequiredVerticalBands(count, maxPrimary, fixedPrimary, trailingPrimary, 0);
            if (requiredBands >= 2)
            {
                userOverflowReserve = fixedPrimary;
                userLayout = CalculateUserLayoutForPanel(
                    isVertical: true,
                    count,
                    maxPrimary,
                    fixedPrimary,
                    userOverflowReserve,
                    trailingPrimary,
                    out userLeadingReserve);
            }
        }

        return (userLayout, userLeadingReserve, userOverflowReserve);
    }

    private static int CalculateRequiredVerticalBands(int count, double maxPrimary, double fixedPrimary, double trailingPrimary, double overflowReserve)
    {
        if (count <= 0) return 0;

        double verticalUserPrimaryLimit = Math.Max(ButtonOuterSize, maxPrimary - PanelChrome);
        double firstColumnAvailable = Math.Max(0, verticalUserPrimaryLimit - fixedPrimary - trailingPrimary);
        int firstColumnCapacity = (int)Math.Floor(firstColumnAvailable / ButtonOuterSize);
        if (count <= firstColumnCapacity) return 1;

        int firstColumnCount = Math.Max(0, firstColumnCapacity);
        int overflowCount = count - firstColumnCount;

        double secondColumnAvailable = Math.Max(0, verticalUserPrimaryLimit - overflowReserve - trailingPrimary);
        int secondColumnCapacity = (int)Math.Floor(secondColumnAvailable / ButtonOuterSize);
        if (overflowCount <= secondColumnCapacity) return 2;

        return 3;
    }

    private static (UserLayout User, double LeadingReserve, double OverflowReserve) CalculateHorizontalUserSection(
        int count,
        double maxPrimary,
        double fixedPrimary,
        double trailingPrimary)
    {
        UserLayout userLayout = CalculateUserLayoutForPanel(
            isVertical: false,
            count,
            maxPrimary,
            fixedPrimary,
            0,
            trailingPrimary,
            out double userLeadingReserve);

        return (userLayout, userLeadingReserve, 0);
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
            if (isVertical)
            {
                return new UserLayout(fixedPrimary, Math.Max(fixedPrimary > 0 ? ButtonOuterSize : 0, trailingPrimary > 0 ? ButtonOuterSize : 0), 1);
            }
            return new UserLayout(0, 0, 0);
        }

        if (!isVertical)
        {
            double userPrimaryLimit = Math.Max(ButtonOuterSize, maxPrimary - fixedPrimary - trailingPrimary - PanelChrome);
            return CalculateUserLayout(count, userPrimaryLimit);
        }

        double verticalUserPrimaryLimit = Math.Max(ButtonOuterSize, maxPrimary - PanelChrome);
        userLeadingReserve = fixedPrimary;
        return CalculateReservedVerticalUserLayout(count, verticalUserPrimaryLimit, fixedPrimary, overflowReserve, trailingPrimary);
    }

    private static UserLayout CalculateReservedVerticalUserLayout(
        int buttonCount,
        double userAreaPrimaryLimit,
        double leadingReserve,
        double overflowReserve,
        double trailingReserve)
    {
        int normalizedCount = Math.Max(0, buttonCount);
        if (normalizedCount == 0 || userAreaPrimaryLimit <= 0)
        {
            return new UserLayout(leadingReserve, ButtonOuterSize, 1);
        }

        double firstColumnAvailable = Math.Max(0, userAreaPrimaryLimit - leadingReserve - trailingReserve);
        int firstColumnCapacity = (int)Math.Floor(firstColumnAvailable / ButtonOuterSize);
        if (normalizedCount <= firstColumnCapacity)
        {
            return new UserLayout(leadingReserve + (normalizedCount * ButtonOuterSize), ButtonOuterSize, 1);
        }

        int firstColumnCount = Math.Max(0, firstColumnCapacity);
        int remainingAfterFirst = normalizedCount - firstColumnCount;

        double secondColumnAvailable = Math.Max(0, userAreaPrimaryLimit - overflowReserve - trailingReserve);
        int secondColumnCapacity = (int)Math.Floor(secondColumnAvailable / ButtonOuterSize);
        if (remainingAfterFirst <= secondColumnCapacity)
        {
            double height = Math.Min(
                userAreaPrimaryLimit,
                Math.Max(
                    leadingReserve + (firstColumnCount * ButtonOuterSize),
                    overflowReserve + (remainingAfterFirst * ButtonOuterSize)));
            return new UserLayout(height, ButtonOuterSize * 2, 2);
        }

        int secondColumnCount = Math.Max(0, secondColumnCapacity);
        int thirdColumnCount = remainingAfterFirst - secondColumnCount;

        double primaryHeight = Math.Min(
            userAreaPrimaryLimit,
            Math.Max(
                leadingReserve + (firstColumnCount * ButtonOuterSize),
                Math.Max(
                    overflowReserve + (secondColumnCount * ButtonOuterSize),
                    thirdColumnCount * ButtonOuterSize)));

        return new UserLayout(primaryHeight, ButtonOuterSize * 3, 3);
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
}
