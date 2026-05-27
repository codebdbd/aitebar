using System;
using System.IO;
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

        var psi = ActionService.BuildWebActionProcessStartInfo(element, "Work Profile");

        string[] args = psi.ArgumentList.ToArray();

        Assert.Equal("https://example.com", args[0]);
        Assert.Contains("-private-window", args);
        Assert.Contains("-P", args);
        Assert.Contains("Work Profile", args);
        Assert.DoesNotContain("-P \"Work Profile\"", args);
    }

    [Theory]
    [InlineData("explorer.exe")]
    [InlineData("shell:Downloads")]
    public void BuildShellLaunchProcessStartInfo_UsesWindowsShell(string target)
    {
        var psi = ActionService.BuildShellLaunchProcessStartInfo(target);

        Assert.Equal(target, psi.FileName);
        Assert.True(psi.UseShellExecute);
    }

    [Fact]
    public void CreateScriptProcessStartInfo_PythonScript_UsesArgumentListWithoutCmdShell()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string pythonExe = Path.Combine(tempRoot, "python.exe");
        string scriptPath = Path.Combine(tempRoot, "script & whoami.py");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            File.WriteAllText(pythonExe, "");
            Environment.SetEnvironmentVariable("PATH", tempRoot + ";" + originalPath);

            var psi = ActionService.CreateScriptProcessStartInfo(scriptPath);
            string[] args = psi.ArgumentList.ToArray();

            Assert.Equal(pythonExe, psi.FileName);
            Assert.False(psi.UseShellExecute);
            Assert.Equal(tempRoot, psi.WorkingDirectory);
            Assert.Equal([scriptPath], args);
            Assert.True(string.IsNullOrEmpty(psi.Arguments));
            Assert.False(string.Equals("cmd.exe", psi.FileName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }
}
