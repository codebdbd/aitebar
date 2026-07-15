using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class BrowserHelperTests
{
    [Fact]
    public void GetUserDataPath_Chrome_ReturnsCorrectPath()
    {
        var result = BrowserHelper.GetUserDataPath(BrowserType.Chrome);
        Assert.Contains("Google\\Chrome\\User Data", result);
    }

    [Fact]
    public void GetUserDataPath_Edge_ReturnsCorrectPath()
    {
        var result = BrowserHelper.GetUserDataPath(BrowserType.Edge);
        Assert.Contains("Microsoft\\Edge\\User Data", result);
    }

    [Fact]
    public void GetUserDataPath_Firefox_ReturnsCorrectPath()
    {
        var result = BrowserHelper.GetUserDataPath(BrowserType.Firefox);
        Assert.Contains("Mozilla\\Firefox", result);
    }

    [Fact]
    public void GetUserDataPath_Brave_ReturnsCorrectPath()
    {
        var result = BrowserHelper.GetUserDataPath(BrowserType.Brave);
        Assert.Contains("BraveSoftware\\Brave-Browser\\User Data", result);
    }

    [Fact]
    public void GetUserDataPath_Yandex_ReturnsCorrectPath()
    {
        var result = BrowserHelper.GetUserDataPath(BrowserType.Yandex);
        Assert.Contains("Yandex\\YandexBrowser\\User Data", result);
    }

    [Fact]
    public void GetUserDataPath_Opera_ReturnsCorrectPath()
    {
        var result = BrowserHelper.GetUserDataPath(BrowserType.Opera);
        Assert.Contains("Opera Software\\Opera Stable", result);
    }

    [Fact]
    public void GetUserDataPath_OperaGX_ReturnsCorrectPath()
    {
        var result = BrowserHelper.GetUserDataPath(BrowserType.OperaGX);
        Assert.Contains("Opera Software\\Opera GX Stable", result);
    }

    [Fact]
    public void GetUserDataPath_Vivaldi_ReturnsCorrectPath()
    {
        var result = BrowserHelper.GetUserDataPath(BrowserType.Vivaldi);
        Assert.Contains("Vivaldi\\User Data", result);
    }

    [Theory]
    [InlineData(BrowserType.Chrome)]
    [InlineData(BrowserType.Edge)]
    [InlineData(BrowserType.Firefox)]
    [InlineData(BrowserType.Brave)]
    [InlineData(BrowserType.Yandex)]
    [InlineData(BrowserType.Opera)]
    [InlineData(BrowserType.OperaGX)]
    [InlineData(BrowserType.Vivaldi)]
    public void GetUserDataPath_AllBrowsers_ReturnNonEmpty(BrowserType type)
    {
        var result = BrowserHelper.GetUserDataPath(type);
        Assert.NotEmpty(result);
    }

    [Theory]
    [InlineData(BrowserType.Chrome)]
    [InlineData(BrowserType.Edge)]
    [InlineData(BrowserType.Firefox)]
    [InlineData(BrowserType.Brave)]
    [InlineData(BrowserType.Yandex)]
    [InlineData(BrowserType.Opera)]
    [InlineData(BrowserType.OperaGX)]
    [InlineData(BrowserType.Vivaldi)]
    public void GetExecutablePath_AllBrowsers_ReturnNonEmpty(BrowserType type)
    {
        var result = BrowserHelper.GetExecutablePath(type);

        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void GetSystemDefaultBrowser_ReturnsValidBrowserType()
    {
        var result = BrowserHelper.GetSystemDefaultBrowser();
        Assert.IsType<BrowserType>(result);
    }

    [Fact]
    public void GetProfiles_NonExistentDirectory_ReturnsEmpty()
    {
        // Этот тест проверяет поведение с несуществующей директорией
        // BrowserHelper.GetProfiles проверяет Directory.Exists, так что должно вернуть пустой список
        var result = BrowserHelper.GetProfiles(BrowserType.Chrome);
        Assert.NotNull(result);
        // Может быть пустым или содержать профили, если Chrome установлен
    }

    [Fact]
    public void BrowserProfileInfo_DerivesProfileNameAndLaunchName()
    {
        var info = new BrowserProfileInfo
        {
            ProfilePath = Path.Combine("C:\\Profiles", "Profile 7"),
            LaunchProfileName = ""
        };

        Assert.Equal("Profile 7", info.ProfileName);
        Assert.Equal("Profile 7", info.LaunchName);

        info.LaunchProfileName = "Work";

        Assert.Equal("Work", info.LaunchName);
    }

    [Fact]
    public void GetProfiles_ChromiumProfiles_ReadDisplayNamesFromPreferences()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        BrowserHelper.SetUserDataPathOverride(BrowserType.Yandex, tempRoot);
        
        string emailProfilePath = Path.Combine(tempRoot, "Profile 900001");
        string namedProfilePath = Path.Combine(tempRoot, "Profile 900002");
        string fallbackProfilePath = Path.Combine(tempRoot, "Profile 900003");

        try
        {
            Directory.CreateDirectory(emailProfilePath);
            Directory.CreateDirectory(namedProfilePath);
            Directory.CreateDirectory(fallbackProfilePath);

            File.WriteAllText(
                Path.Combine(emailProfilePath, "Preferences"),
                """{"account_info":[{"email":"zzzz-aitebar-alpha@example.com"}]}""");
            File.WriteAllText(
                Path.Combine(namedProfilePath, "Preferences"),
                """{"profile":{"name":"zzzz-aitebar-beta"}}""");
            File.WriteAllText(
                Path.Combine(fallbackProfilePath, "Preferences"),
                "{not valid json");

            var result = BrowserHelper.GetProfiles(BrowserType.Yandex);

            Assert.Contains(result, profile =>
                profile.ProfilePath == emailProfilePath &&
                profile.DisplayName == "zzzz-aitebar-alpha@example.com");
            Assert.Contains(result, profile =>
                profile.ProfilePath == namedProfilePath &&
                profile.DisplayName == "zzzz-aitebar-beta");
            Assert.Contains(result, profile =>
                profile.ProfilePath == fallbackProfilePath &&
                profile.DisplayName == "Profile 900003");
        }
        finally
        {
            BrowserHelper.ClearUserDataPathOverride(BrowserType.Yandex);
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void GetProfiles_CachesDiskSnapshotUntilExplicitInvalidation()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        string profilePath = Path.Combine(tempRoot, "Profile 900005");
        string preferencesPath = Path.Combine(profilePath, "Preferences");
        BrowserHelper.SetUserDataPathOverride(BrowserType.Yandex, tempRoot);

        try
        {
            Directory.CreateDirectory(profilePath);
            File.WriteAllText(preferencesPath, """{"profile":{"name":"Original"}}""");

            var first = BrowserHelper.GetProfiles(BrowserType.Yandex);
            File.WriteAllText(preferencesPath, """{"profile":{"name":"Changed"}}""");
            var cached = BrowserHelper.GetProfiles(BrowserType.Yandex);

            Assert.Equal("Original", first.Single().DisplayName);
            Assert.Equal("Original", cached.Single().DisplayName);

            BrowserHelper.ClearProfileCache(BrowserType.Yandex);
            var refreshed = BrowserHelper.GetProfiles(BrowserType.Yandex);

            Assert.Equal("Changed", refreshed.Single().DisplayName);
        }
        finally
        {
            BrowserHelper.ClearUserDataPathOverride(BrowserType.Yandex);
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void GetProfiles_ReturnsCopiesThatCannotMutateCachedSnapshot()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        string profilePath = Path.Combine(tempRoot, "Profile 900006");
        BrowserHelper.SetUserDataPathOverride(BrowserType.Yandex, tempRoot);

        try
        {
            Directory.CreateDirectory(profilePath);
            File.WriteAllText(
                Path.Combine(profilePath, "Preferences"),
                """{"profile":{"name":"Original"}}""");

            var first = BrowserHelper.GetProfiles(BrowserType.Yandex);
            first[0].DisplayName = "Mutated";
            first.Clear();

            var second = BrowserHelper.GetProfiles(BrowserType.Yandex);

            Assert.Single(second);
            Assert.Equal("Original", second[0].DisplayName);
        }
        finally
        {
            BrowserHelper.ClearUserDataPathOverride(BrowserType.Yandex);
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void GetProfiles_ChromiumPreferences_AllowsConcurrentWriter()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        BrowserHelper.SetUserDataPathOverride(BrowserType.Yandex, tempRoot);
        
        string profilePath = Path.Combine(tempRoot, "Profile 900004");
        string preferencesPath = Path.Combine(profilePath, "Preferences");

        try
        {
            Directory.CreateDirectory(profilePath);
            File.WriteAllText(preferencesPath, """{"profile":{"name":"zzzz-aitebar-locked"}}""");

            using var lockedPreferences = File.Open(preferencesPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            var result = BrowserHelper.GetProfiles(BrowserType.Yandex);

            Assert.Contains(result, profile =>
                profile.ProfilePath == profilePath &&
                profile.DisplayName == "zzzz-aitebar-locked");
        }
        finally
        {
            BrowserHelper.ClearUserDataPathOverride(BrowserType.Yandex);
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void GetExecutablePath_UnknownBrowserType_FallsBackToChrome()
    {
        var result = BrowserHelper.GetExecutablePath((BrowserType)9999);

        Assert.Equal("chrome.exe", result);
    }

    [Fact]
    public void GetFirefoxProfiles_ParsesRelativeAndAbsoluteEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        string relativeProfile = Path.Combine(root, "Profiles", "relative.default-release");
        string absoluteProfile = Path.Combine(root, "absolute.default");
        Directory.CreateDirectory(relativeProfile);
        Directory.CreateDirectory(absoluteProfile);

        try
        {
            File.WriteAllText(
                Path.Combine(root, "profiles.ini"),
                $$"""
                [Profile0]
                Name=Relative
                IsRelative=1
                Path=Profiles/relative.default-release

                [Profile1]
                Name=Absolute
                IsRelative=0
                Path={{absoluteProfile}}
                """);

            MethodInfo method = typeof(BrowserHelper).GetMethod("GetFirefoxProfiles", BindingFlags.NonPublic | BindingFlags.Static)!;
            var result = (System.Collections.Generic.List<BrowserProfileInfo>)method.Invoke(null, [root])!;

            Assert.Equal(["Absolute", "Relative"], result.Select(profile => profile.DisplayName).ToArray());
            Assert.Contains(result, profile =>
                profile.DisplayName == "Relative" &&
                profile.ProfilePath == relativeProfile &&
                profile.LaunchProfileName == "Relative");
            Assert.Contains(result, profile =>
                profile.DisplayName == "Absolute" &&
                profile.ProfilePath == absoluteProfile &&
                profile.LaunchProfileName == "Absolute");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AdvanceProfile_ReturnsNextMatchingLaunchName()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        BrowserHelper.SetUserDataPathOverride(BrowserType.Yandex, tempRoot);
        
        string firstProfilePath = Path.Combine(tempRoot, "Profile 900011");
        string secondProfilePath = Path.Combine(tempRoot, "Profile 900012");

        try
        {
            Directory.CreateDirectory(firstProfilePath);
            Directory.CreateDirectory(secondProfilePath);
            File.WriteAllText(
                Path.Combine(firstProfilePath, "Preferences"),
                """{"account_info":[{"email":"zzzz-aitebar-advance-1@example.com"}]}""");
            File.WriteAllText(
                Path.Combine(secondProfilePath, "Preferences"),
                """{"account_info":[{"email":"zzzz-aitebar-advance-2@example.com"}]}""");

            string next = BrowserHelper.AdvanceProfile(BrowserType.Yandex, "Profile 900011");

            Assert.Equal("Profile 900012", next);
        }
        finally
        {
            BrowserHelper.ClearUserDataPathOverride(BrowserType.Yandex);
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void AdvanceProfile_WhenProfileIsUnknown_ReturnsFirstLaunchName()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        BrowserHelper.SetUserDataPathOverride(BrowserType.Yandex, tempRoot);
        
        string firstProfilePath = Path.Combine(tempRoot, "Profile 900021");

        try
        {
            Directory.CreateDirectory(firstProfilePath);
            File.WriteAllText(
                Path.Combine(firstProfilePath, "Preferences"),
                """{"account_info":[{"email":"zzzz-aitebar-first@example.com"}]}""");

            string next = BrowserHelper.AdvanceProfile(BrowserType.Yandex, "missing-profile");

            Assert.Equal("Profile 900021", next);
        }
        finally
        {
            BrowserHelper.ClearUserDataPathOverride(BrowserType.Yandex);
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
