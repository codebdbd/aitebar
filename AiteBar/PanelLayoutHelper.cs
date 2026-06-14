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

    public static PanelLayoutMetrics Calculate(
        bool isVertical,
        double availablePrimary,
        double panelPercent,
        int totalButtonCount,
        int controlButtonCount,
        int trailingControlButtonCount)
    {
        double normalizedPercent = Math.Clamp(panelPercent, 20, 100) / 100.0;
        double maxPrimary = Math.Max(ButtonOuterSize, availablePrimary * normalizedPercent);

        int fixedSeparatorCount = totalButtonCount > 0 ? 1 : 0;
        double fixedPrimary = (controlButtonCount * ButtonOuterSize) + (fixedSeparatorCount * SeparatorSize);
        double fixedCross = (controlButtonCount > 0 || totalButtonCount > 0) ? ButtonOuterSize : 0;
        double trailingPrimary = (trailingControlButtonCount * ButtonOuterSize) + (totalButtonCount > 0 && trailingControlButtonCount > 0 ? SeparatorSize : 0);
        double trailingCross = trailingControlButtonCount > 0 ? ButtonOuterSize : 0;

        (UserLayout userLayout, double userLeadingReserve, double userOverflowReserve) = isVertical
            ? CalculateVerticalUserSection(totalButtonCount, maxPrimary, fixedPrimary)
            : CalculateHorizontalUserSection(totalButtonCount, maxPrimary, fixedPrimary, trailingPrimary);

        double panelPrimary = Math.Max(
            ButtonOuterSize + PanelChrome,
            (isVertical ? userLayout.Primary : fixedPrimary + userLayout.Primary + trailingPrimary) + PanelChrome);
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
        double fixedPrimary)
    {
        double userOverflowReserve = 0;
        UserLayout userLayout = CalculateUserLayoutForPanel(
            isVertical: true,
            count,
            maxPrimary,
            fixedPrimary,
            userOverflowReserve,
            0,
            out double userLeadingReserve);

        if (count > 0 && userLayout.Bands == MaxUserBands)
        {
            userOverflowReserve = fixedPrimary;
            userLayout = CalculateUserLayoutForPanel(
                isVertical: true,
                count,
                maxPrimary,
                fixedPrimary,
                userOverflowReserve,
                0,
                out userLeadingReserve);
        }

        return (userLayout, userLeadingReserve, userOverflowReserve);
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
            return new UserLayout(0, 0, 0);
        }

        if (!isVertical)
        {
            double userPrimaryLimit = Math.Max(ButtonOuterSize, maxPrimary - fixedPrimary - trailingPrimary - PanelChrome);
            return CalculateUserLayout(count, userPrimaryLimit);
        }

        double verticalUserPrimaryLimit = Math.Max(ButtonOuterSize, maxPrimary - PanelChrome);
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
}
