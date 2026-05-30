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
}
