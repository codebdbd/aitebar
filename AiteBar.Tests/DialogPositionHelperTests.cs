using System.Windows;
using Xunit;

namespace AiteBar.Tests;

public class DialogPositionHelperTests
{
    private static readonly Rect DefaultWorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void CalculatePosition_NullOwner_CentersOnWorkArea()
    {
        var (left, top) = DialogPositionHelper.CalculatePosition(
            ownerBounds: null,
            isOwnerVisible: false,
            workArea: DefaultWorkArea,
            dialogWidth: 420,
            dialogHeight: 200);

        Assert.Equal((1920 - 420) / 2.0, left);
        Assert.Equal((1040 - 200) / 2.0, top);
    }

    [Fact]
    public void CalculatePosition_OwnerInvisible_CentersOnWorkArea()
    {
        var (left, top) = DialogPositionHelper.CalculatePosition(
            ownerBounds: new Rect(100, 100, 800, 600),
            isOwnerVisible: false,
            workArea: DefaultWorkArea,
            dialogWidth: 420,
            dialogHeight: 200);

        Assert.Equal((1920 - 420) / 2.0, left);
        Assert.Equal((1040 - 200) / 2.0, top);
    }

    [Fact]
    public void CalculatePosition_OwnerOffscreenTop_CentersOnWorkArea()
    {
        // Dock hidden offscreen at Top = -48
        var (left, top) = DialogPositionHelper.CalculatePosition(
            ownerBounds: new Rect(400, -48, 1120, 48),
            isOwnerVisible: true,
            workArea: DefaultWorkArea,
            dialogWidth: 420,
            dialogHeight: 200);

        Assert.Equal((1920 - 420) / 2.0, left);
        Assert.Equal((1040 - 200) / 2.0, top);
    }

    [Fact]
    public void CalculatePosition_OwnerOffscreenLeft_CentersOnWorkArea()
    {
        // Dock hidden offscreen at Left = -48
        var (left, top) = DialogPositionHelper.CalculatePosition(
            ownerBounds: new Rect(-48, 200, 48, 600),
            isOwnerVisible: true,
            workArea: DefaultWorkArea,
            dialogWidth: 420,
            dialogHeight: 200);

        Assert.Equal((1920 - 420) / 2.0, left);
        Assert.Equal((1040 - 200) / 2.0, top);
    }

    [Fact]
    public void CalculatePosition_VisibleOnscreenOwner_CentersOnOwner()
    {
        var (left, top) = DialogPositionHelper.CalculatePosition(
            ownerBounds: new Rect(200, 200, 600, 400),
            isOwnerVisible: true,
            workArea: DefaultWorkArea,
            dialogWidth: 400,
            dialogHeight: 200);

        // Owner center: X = 200 + 300 = 500. Dialog left = 500 - 200 = 300.
        // Owner center: Y = 200 + 200 = 400. Dialog top = 400 - 100 = 300.
        Assert.Equal(300, left);
        Assert.Equal(300, top);
    }

    [Fact]
    public void CalculatePosition_ClampsToWorkArea_WhenOwnerNearEdge()
    {
        var (left, top) = DialogPositionHelper.CalculatePosition(
            ownerBounds: new Rect(1800, 10, 100, 100),
            isOwnerVisible: true,
            workArea: DefaultWorkArea,
            dialogWidth: 400,
            dialogHeight: 200);

        // Should be clamped to workArea.Right - dialogWidth = 1920 - 400 = 1520
        Assert.Equal(1520, left);
        Assert.Equal(0, top); // (10 + 50 - 100) = -40 -> clamped to 0
    }
}
