using System.Linq;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class ActionServiceTests
{
    [Fact]
    public void BuildWebActionProcessStartInfo_FirefoxProfile_UsesSeparateArguments()
    {
        var element = new CustomElement
        {
            Browser = BrowserType.Firefox,
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com",
            IsIncognito = true
        };

        var psi = ActionService.BuildWebActionProcessStartInfo(element, @"C:\Users\ostee\AppData\Roaming\Mozilla\Firefox\Profiles\Work Profile");

        string[] args = psi.ArgumentList.ToArray();

        Assert.Equal("https://example.com", args[0]);
        Assert.Contains("-private-window", args);
        Assert.Contains("-P", args);
        Assert.Contains("Work Profile", args);
        Assert.DoesNotContain("-P \"Work Profile\"", args);
    }
}
