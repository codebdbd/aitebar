namespace AiteBar.Tests;

public sealed class SettingsWindowPreviewTests
{
    [Fact]
    public void IsCurrentPreviewRequest_AcceptsOnlyLatestRequestForSelectedPath()
    {
        Assert.True(SettingsWindow.IsCurrentPreviewRequest(3, 3, "latest.png", "latest.png"));
        Assert.False(SettingsWindow.IsCurrentPreviewRequest(2, 3, "old.png", "latest.png"));
        Assert.False(SettingsWindow.IsCurrentPreviewRequest(3, 3, "old.png", "latest.png"));
    }
}
