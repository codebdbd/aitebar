using System;
using System.Windows.Controls.Primitives;

namespace AiteBar.AiteProfilesUtility;

internal readonly record struct AiteProfilesQuickLinkPopupLayout(PlacementMode Placement, double MaxHeight, double VerticalOffset);

internal static class AiteProfilesQuickLinkPopupLayoutHelper
{
    public static AiteProfilesQuickLinkPopupLayout Calculate(double spaceAbove, double spaceBelow)
    {
        double above = Math.Max(0, spaceAbove);
        double below = Math.Max(0, spaceBelow);

        bool openBelow = below > above;
        double maxHeight = openBelow ? below : above;
        double verticalOffset = openBelow ? 2 : -2;

        return new AiteProfilesQuickLinkPopupLayout(
            openBelow ? PlacementMode.Bottom : PlacementMode.Top,
            maxHeight,
            verticalOffset);
    }
}
