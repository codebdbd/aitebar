namespace AiteBar.Tests;

public sealed class SettingsWindowValidationHelperTests
{
    [Fact]
    public void Validate_RejectsMissingName()
    {
        var state = SettingsWindowValidationHelper.Validate(" ", ActionType.Web, "https://example.com", "None");

        Assert.True(state.IsNameMissing);
        Assert.False(state.IsValid);
    }

    [Fact]
    public void Validate_RejectsMissingActionValueForStandardActions()
    {
        var state = SettingsWindowValidationHelper.Validate("Open site", ActionType.Web, "", "None");

        Assert.True(state.IsActionValueMissing);
        Assert.False(state.IsValid);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]
    [InlineData("localhost")]
    public void Validate_RejectsInvalidWebUrls(string actionValue)
    {
        var state = SettingsWindowValidationHelper.Validate("Open site", ActionType.Web, actionValue, "None");

        Assert.True(state.IsWebUrlInvalid);
        Assert.False(state.IsValid);
    }

    [Fact]
    public void Validate_AcceptsValidWebUrls()
    {
        var state = SettingsWindowValidationHelper.Validate("Open site", ActionType.Web, "example.com", "None");

        Assert.True(state.IsValid);
    }

    [Fact]
    public void Validate_RequiresHotkeyKeyForHotkeyActions()
    {
        var state = SettingsWindowValidationHelper.Validate("Lock", ActionType.Hotkey, "", "None");

        Assert.True(state.IsHotkeyKeyMissing);
        Assert.False(state.IsValid);
    }
}
