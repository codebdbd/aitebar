using System;
using System.IO;
using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class InstalledAppsServiceTests
{
    [Theory]
    [InlineData("Uninstall Google Chrome", @"C:\Program Files\Google\Chrome\uninstall.exe", true)]
    [InlineData("unins000", @"C:\Games\Game\unins000.exe", true)]
    [InlineData("Calculator", @"C:\Windows\calc.exe", false)]
    [InlineData("Visual Studio Code", @"C:\Users\AppData\Local\Programs\Microsoft VS Code\Code.exe", false)]
    public void IsUninstaller_CorrectlyIdentifiesUninstallers(string name, string path, bool expected)
    {
        bool actual = InstalledAppsService.IsUninstaller(name, path);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"https://google.com", true)]
    [InlineData(@"http://example.com/page", true)]
    [InlineData(@"C:\Docs\manual.pdf", true)]
    [InlineData(@"C:\Docs\help.chm", true)]
    [InlineData(@"C:\Docs\readme.txt", true)]
    [InlineData(@"C:\Windows\notepad.exe", false)]
    [InlineData(@"C:\Tools\script.bat", false)]
    [InlineData(@"C:\Tools\deploy.ps1", false)]
    public void IsDocumentOrWebTarget_CorrectlyFiltersNonPrograms(string path, bool expected)
    {
        bool actual = InstalledAppsService.IsDocumentOrWebTarget(path);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveKnownFolderGuidPath_ResolvesKnownProgramFilesGuid()
    {
        string raw = @"{6D809377-6AF0-444B-8957-A3773F02200E}\App\tool.exe";
        string resolved = InstalledAppsService.ResolveKnownFolderGuidPath(raw);

        string expectedProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        Assert.StartsWith(expectedProgramFiles, resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(@"App\tool.exe", resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActionTargetHelper_IsProgramPath_RecognizesShellAppsFolder()
    {
        string uwpPath = @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
        bool isProgram = ActionTargetHelper.IsProgramPath(uwpPath);
        Assert.True(isProgram);
    }

    [Fact]
    public void InstalledAppInfo_PropertiesAndUwpCheck()
    {
        var app1 = new InstalledAppInfo("Calculator", @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", InstalledAppInfo.TypeUwp);
        Assert.True(app1.IsUwp);
        Assert.Equal("Calculator", app1.Name);

        var app2 = new InstalledAppInfo("Notepad", @"C:\Windows\notepad.exe", InstalledAppInfo.TypeDesktop);
        Assert.False(app2.IsUwp);
    }

    [Fact]
    public void ShellIconHelper_ExtractsIcon_FromWindowsSystemExe()
    {
        string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string cmdPath = Path.Combine(sysDir, "cmd.exe");
        if (!File.Exists(cmdPath)) return;

        string? iconPath = ShellIconHelper.ExtractAndSaveShellIcon(cmdPath, targetSize: 32);
        Assert.NotNull(iconPath);
        Assert.True(File.Exists(iconPath));
        Assert.EndsWith(".png", iconPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(new FileInfo(iconPath).Length > 0);
    }

    [Fact]
    public async Task GetInstalledAppsAsync_ReturnsNonEmptyList()
    {
        var apps = await InstalledAppsService.GetInstalledAppsAsync(forceRefresh: true);
        Assert.NotNull(apps);
        Assert.NotEmpty(apps);
        Assert.Contains(apps, a => !string.IsNullOrEmpty(a.Name) && !string.IsNullOrEmpty(a.Path));
    }

    [Fact]
    public async Task InstalledAppsWindow_InstantiatesWithoutException()
    {
        await RunInStaAsync(() =>
        {
            var window = new InstalledAppsWindow();
            Assert.NotNull(window);
            Assert.NotNull(window.FindName("TxtSearch"));
            Assert.NotNull(window.FindName("LstApps"));
            Assert.NotNull(window.FindName("BtnBrowseDisk"));
            Assert.NotNull(window.FindName("BtnSelect"));
        });
    }

    private static Task RunInStaAsync(Action action)
    {
        var tcs = new TaskCompletionSource<object?>();
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    _ = new System.Windows.Application();
                }

                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }
}
