using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class ProfileRotationHelperTests
{
    [Fact]
    public void GetEligibleProfiles_EmptySelectionMeansAllProfiles()
    {
        var profiles = CreateProfiles();

        var eligible = ProfileRotationHelper.GetEligibleProfiles(profiles, []);

        Assert.Equal(profiles, eligible);
    }

    [Fact]
    public void GetEligibleProfiles_UsesOnlySelectedProfiles()
    {
        var profiles = CreateProfiles();

        var eligible = ProfileRotationHelper.GetEligibleProfiles(profiles, [profiles[1].ProfilePath]);

        Assert.Equal([profiles[1]], eligible);
    }

    [Fact]
    public void AdvanceProfile_RotatesWithinSelectedProfiles()
    {
        var profiles = CreateProfiles();

        string next = ProfileRotationHelper.AdvanceProfile(profiles, [profiles[1].ProfilePath, profiles[2].ProfilePath], profiles[1].ProfileName);

        Assert.Equal(profiles[2].ProfileName, next);
    }

    [Fact]
    public void AdvanceProfile_ReturnsLaunchNameForFirefoxStyleProfiles()
    {
        BrowserProfileInfo[] profiles =
        [
            new()
            {
                DisplayName = "default-release",
                ProfilePath = @"C:\Users\User\AppData\Roaming\Mozilla\Firefox\Profiles\abc123.default-release",
                LaunchProfileName = "default-release"
            },
            new()
            {
                DisplayName = "Work",
                ProfilePath = @"C:\Users\User\AppData\Roaming\Mozilla\Firefox\Profiles\xyz987.work",
                LaunchProfileName = "Work"
            }
        ];

        string next = ProfileRotationHelper.AdvanceProfile(profiles, [profiles[0].ProfilePath, profiles[1].ProfilePath], "default-release");

        Assert.Equal("Work", next);
    }

    private static BrowserProfileInfo[] CreateProfiles() =>
    [
        new() { DisplayName = "Default", ProfilePath = @"C:\Browser\Default" },
        new() { DisplayName = "Work", ProfilePath = @"C:\Browser\Profile 1" },
        new() { DisplayName = "Test", ProfilePath = @"C:\Browser\Profile 2" }
    ];
}
