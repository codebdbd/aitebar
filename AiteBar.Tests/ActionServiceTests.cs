using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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

    [Fact]
    public void BuildWebActionProcessStartInfo_ChromeWithProfile_UsesProfileArgument()
    {
        var element = new CustomElement
        {
            Browser = BrowserType.Chrome,
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com",
            ChromeProfile = "Profile 1"
        };

        var psi = ActionService.BuildWebActionProcessStartInfo(element, "Profile 1");

        string[] args = psi.ArgumentList.ToArray();

        Assert.Equal("https://example.com", args[0]);
        Assert.Contains("--profile-directory=Profile 1", args);
    }

    [Fact]
    public void BuildWebActionProcessStartInfo_EdgeWithAppMode_UsesAppMode()
    {
        var element = new CustomElement
        {
            Browser = BrowserType.Edge,
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com",
            IsAppMode = true
        };

        var psi = ActionService.BuildWebActionProcessStartInfo(element, null!);

        string[] args = psi.ArgumentList.ToArray();

        Assert.Contains("--app=https://example.com", args);
    }

    [Fact]
    public void BuildWebActionProcessStartInfo_BraveWithIncognito_AddsIncognito()
    {
        var element = new CustomElement
        {
            Browser = BrowserType.Brave,
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com",
            IsIncognito = true
        };

        var psi = ActionService.BuildWebActionProcessStartInfo(element, null!);

        string[] args = psi.ArgumentList.ToArray();

        Assert.Equal("https://example.com", args[0]);
        Assert.Contains("--incognito", args);
    }

    [Fact]
    public void BuildWebActionProcessStartInfo_YandexWithRotation_AddsRotation()
    {
        var element = new CustomElement
        {
            Browser = BrowserType.Yandex,
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com",
            UseRotation = true,
            RotationProfilePaths = new System.Collections.Generic.List<string> { "Profile1", "Profile2" }
        };

        var psi = ActionService.BuildWebActionProcessStartInfo(element, "Profile2");

        string[] args = psi.ArgumentList.ToArray();

        Assert.Equal("https://example.com", args[0]);
        Assert.Contains("--profile-directory=Profile2", args);
    }

    [Fact]
    public void BuildWebActionProcessStartInfo_OperaGxWithIncognito_UsesPrivateFlag()
    {
        var element = new CustomElement
        {
            Browser = BrowserType.OperaGX,
            ActionType = nameof(ActionType.Web),
            ActionValue = "https://example.com",
            IsIncognito = true
        };

        var psi = ActionService.BuildWebActionProcessStartInfo(element, string.Empty);

        Assert.Contains("-private", psi.ArgumentList);
        Assert.DoesNotContain(psi.ArgumentList, argument => argument.StartsWith("--profile-directory=", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateScriptProcessStartInfo_CmdScript_UsesCmdWithSeparateArguments()
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"), "test script.cmd");

        var psi = ActionService.CreateScriptProcessStartInfo(scriptPath);

        Assert.Equal("cmd.exe", psi.FileName);
        Assert.False(psi.UseShellExecute);
        Assert.Equal(Path.GetDirectoryName(scriptPath), psi.WorkingDirectory);
        Assert.Equal(["/c", scriptPath], psi.ArgumentList.ToArray());
    }

    [Fact]
    public void CreateScriptProcessStartInfo_PowerShellScript_PrefersPwshWithoutExecutionPolicyBypass()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string pwshExe = Path.Combine(tempRoot, "pwsh.exe");
        string powershellExe = Path.Combine(tempRoot, "powershell.exe");
        string scriptPath = Path.Combine(tempRoot, "test-script.ps1");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            File.WriteAllText(pwshExe, string.Empty);
            File.WriteAllText(powershellExe, string.Empty);
            Environment.SetEnvironmentVariable("PATH", tempRoot + ";" + originalPath);

            var psi = ActionService.CreateScriptProcessStartInfo(scriptPath);
            string[] args = psi.ArgumentList.ToArray();

            Assert.Equal(pwshExe, psi.FileName);
            Assert.Equal(["-NoProfile", "-File", scriptPath], args);
            Assert.DoesNotContain("Bypass", args);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateScriptProcessStartInfo_PowerShellScript_FallsBackToWindowsPowerShellWithBypass()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string powershellExe = Path.Combine(tempRoot, "powershell.exe");
        string scriptPath = Path.Combine(tempRoot, "test-script.ps1");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            File.WriteAllText(powershellExe, string.Empty);
            Environment.SetEnvironmentVariable("PATH", tempRoot);

            var psi = ActionService.CreateScriptProcessStartInfo(scriptPath);
            string[] args = psi.ArgumentList.ToArray();

            Assert.Equal(powershellExe, psi.FileName);
            Assert.Equal(["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath], args);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateScriptProcessStartInfo_UnsupportedExtension_Throws()
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"), "test-script.js");

        Assert.Throws<InvalidOperationException>(() => ActionService.CreateScriptProcessStartInfo(scriptPath));
    }

    [Fact]
    public void CreateScriptProcessStartInfo_PythonWithoutInterpreter_Throws()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string scriptPath = Path.Combine(tempRoot, "test-script.py");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", tempRoot);

            Assert.Throws<InvalidOperationException>(() => ActionService.CreateScriptProcessStartInfo(scriptPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteCustomActionAsync_UnknownActionType_ReturnsOkAfterCallback()
    {
        var service = new ActionService(new AppSettingsService());
        var element = new CustomElement { ActionType = "NotARealActionType" };
        bool callbackInvoked = false;

        ActionExecutionResult result = await service.ExecuteCustomActionAsync(element, () =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        Assert.True(callbackInvoked);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteCustomActionAsync_InvalidProgramPath_ReturnsFailure()
    {
        var service = new ActionService(new AppSettingsService());
        var element = new CustomElement
        {
            ActionType = nameof(ActionType.Program),
            ActionValue = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.exe")
        };

        ActionExecutionResult result = await service.ExecuteCustomActionAsync(element);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Theory]
    [InlineData(ActionType.File)]
    [InlineData(ActionType.Folder)]
    public async Task ExecuteCustomActionAsync_InvalidShellTarget_ReturnsFailure(ActionType actionType)
    {
        var service = new ActionService(new AppSettingsService());
        var element = new CustomElement
        {
            ActionType = actionType.ToString(),
            ActionValue = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-target")
        };

        ActionExecutionResult result = await service.ExecuteCustomActionAsync(element);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task StartSearchAsync_WhitespaceInput_DoesNotInvokeCallback()
    {
        var service = new ActionService(new AppSettingsService());
        bool callbackInvoked = false;

        await service.StartSearchAsync("   ", () =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        Assert.False(callbackInvoked);
    }

    [Fact]
    public async Task ExecuteCustomActionAsync_HotkeyAction_SendsInputsAndDelays()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);
        var element = new CustomElement
        {
            ActionType = nameof(ActionType.Hotkey),
            Ctrl = true,
            Alt = true,
            Key = nameof(Key.K)
        };

        ActionExecutionResult result = await service.ExecuteCustomActionAsync(element);

        Assert.True(result.Success);
        Assert.Equal([30, 30, 30, 30, 30, 30], runtime.DelayCalls);
        Assert.Equal(6, runtime.SendInputCalls.Count);
        Assert.Equal(NativeMethods.VK_CONTROL, runtime.SendInputCalls[0][0].U.ki.wVk);
        Assert.Equal(NativeMethods.VK_MENU, runtime.SendInputCalls[1][0].U.ki.wVk);
        Assert.Equal((byte)KeyInterop.VirtualKeyFromKey(Key.K), runtime.SendInputCalls[2][0].U.ki.wVk);
        Assert.Equal(NativeMethods.KEYEVENTF_KEYUP, runtime.SendInputCalls[3][0].U.ki.dwFlags);
        Assert.Equal(NativeMethods.KEYEVENTF_KEYUP, runtime.SendInputCalls[4][0].U.ki.dwFlags);
        Assert.Equal(NativeMethods.KEYEVENTF_KEYUP, runtime.SendInputCalls[5][0].U.ki.dwFlags);
    }

    [Fact]
    public async Task ExecuteCustomActionAsync_WebAction_SavesProfileStartsProcessAndEntersFullscreen()
    {
        string root = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string settingsPath = Path.Combine(root, "settings.json");
        string configPath = Path.Combine(root, "custom_buttons.json");

        try
        {
            var runtime = new FakeActionServiceRuntime();
            runtime.ProcessesToReturn.Enqueue(new FakeActionProcessHandle([IntPtr.Zero, new IntPtr(42)]));
            var settingsService = new AppSettingsService(configPath, settingsPath);
            var service = new ActionService(settingsService, runtime);
            var element = new CustomElement
            {
                ActionType = nameof(ActionType.Web),
                ActionValue = "https://example.com",
                Browser = BrowserType.Chrome,
                ChromeProfile = "Profile 7",
                OpenFullscreen = true
            };

            ActionExecutionResult result = await service.ExecuteCustomActionAsync(element);

            Assert.True(result.Success);
            Assert.Equal("Profile 7", element.LastUsedProfile);
            Assert.True(File.Exists(settingsPath));
            Assert.Single(runtime.StartedProcessInfos);
            Assert.Equal("https://example.com", runtime.StartedProcessInfos[0].ArgumentList[0]);
            Assert.Equal([200, 200, 100], runtime.DelayCalls);
            Assert.Equal([new IntPtr(42)], runtime.ForegroundWindowCalls);
            Assert.Single(runtime.SendInputCalls);
            Assert.Equal(2, runtime.SendInputCalls[0].Length);
            Assert.Equal((byte)KeyInterop.VirtualKeyFromKey(Key.F11), runtime.SendInputCalls[0][0].U.ki.wVk);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task StartCalculatorAsync_LaunchesCalculatorAfterCallback()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);
        bool callbackInvoked = false;

        await service.StartCalculatorAsync(() =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        Assert.True(callbackInvoked);
        Assert.Equal(["calc.exe"], runtime.StartedFileNames);
    }

    [Fact]
    public async Task StartColorPickerAsync_WaitsAndShowsDialog()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);

        await service.StartColorPickerAsync();

        Assert.Equal([120], runtime.DelayCalls);
        Assert.Equal(1, runtime.ColorPicker.ShowDialogCalls);
    }

    [Fact]
    public async Task StartScreenshotAsync_LaunchesScreenClipProtocol()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);

        await service.StartScreenshotAsync();

        Assert.Single(runtime.StartedProcessInfos);
        Assert.Equal("ms-screenclip:", runtime.StartedProcessInfos[0].FileName);
        Assert.True(runtime.StartedProcessInfos[0].UseShellExecute);
    }

    [Fact]
    public async Task StartRecordVideoAsync_LaunchesRecordingProtocol()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);

        await service.StartRecordVideoAsync();

        Assert.Single(runtime.StartedProcessInfos);
        Assert.Equal("ms-screenclip:?type=recording", runtime.StartedProcessInfos[0].FileName);
        Assert.True(runtime.StartedProcessInfos[0].UseShellExecute);
    }

    [Fact]
    public async Task StartExplorerAsync_LaunchesExplorerShell()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);

        await service.StartExplorerAsync();

        Assert.Single(runtime.StartedProcessInfos);
        Assert.Equal("explorer.exe", runtime.StartedProcessInfos[0].FileName);
        Assert.True(runtime.StartedProcessInfos[0].UseShellExecute);
    }

    [Fact]
    public async Task StartDownloadsAsync_LaunchesDownloadsShell()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);

        await service.StartDownloadsAsync();

        Assert.Single(runtime.StartedProcessInfos);
        Assert.Equal("shell:Downloads", runtime.StartedProcessInfos[0].FileName);
        Assert.True(runtime.StartedProcessInfos[0].UseShellExecute);
    }

    [Fact]
    public async Task StartQuickNoteAsync_CreatesAndShowsWindowOnceThenReactivates()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);

        await service.StartQuickNoteAsync();
        runtime.QuickNoteWindow.IsVisible = true;
        await service.StartQuickNoteAsync();

        Assert.Equal(1, runtime.CreateQuickNoteWindowCalls);
        Assert.Equal(1, runtime.QuickNoteWindow.ShowSlidingCalls);
        Assert.Equal(1, runtime.QuickNoteWindow.ActivateCalls);
    }

    [Fact]
    public async Task StartQuickNoteAsync_AfterWindowClosed_CreatesNewWindow()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);

        await service.StartQuickNoteAsync();
        runtime.QuickNoteWindow.RaiseClosed();
        runtime.QuickNoteWindow = new FakeQuickNoteToolWindow();
        await service.StartQuickNoteAsync();

        Assert.Equal(2, runtime.CreateQuickNoteWindowCalls);
    }

    [Fact]
    public async Task StartFileSorterAsync_CreatesAndShowsWindowOnceThenReactivates()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);

        await service.StartFileSorterAsync();
        runtime.FileSorterWindow.IsVisible = true;
        await service.StartFileSorterAsync();

        Assert.Equal(1, runtime.CreateFileSorterWindowCalls);
        Assert.Equal(1, runtime.FileSorterWindow.ShowNearPanelCalls);
        Assert.Equal(1, runtime.FileSorterWindow.ActivateCalls);
    }

    [Fact]
    public async Task StartFileSorterAsync_AfterWindowClosed_CreatesNewWindow()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);

        await service.StartFileSorterAsync();
        runtime.FileSorterWindow.RaiseClosed();
        runtime.FileSorterWindow = new FakeFileSorterToolWindow();
        await service.StartFileSorterAsync();

        Assert.Equal(2, runtime.CreateFileSorterWindowCalls);
    }

    [Fact]
    public async Task StartTimerStopwatchAsync_CreatesAndShowsWindowOnceThenReactivates()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);

        await service.StartTimerStopwatchAsync();
        runtime.TimerWindow.IsVisible = true;
        await service.StartTimerStopwatchAsync();

        Assert.Equal(1, runtime.CreateTimerWindowCalls);
        Assert.Equal(1, runtime.TimerWindow.ShowNearPanelCalls);
        Assert.Equal(1, runtime.TimerWindow.ActivateCalls);
    }

    [Fact]
    public void FindExecutableOnPath_SkipsInvalidPathSegmentsAndFindsMatch()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string matchingExe = Path.Combine(tempRoot, "tool.exe");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            File.WriteAllText(matchingExe, string.Empty);
            Environment.SetEnvironmentVariable("PATH", "|;" + tempRoot);
            MethodInfo method = typeof(ActionService).GetMethod("FindExecutableOnPath", BindingFlags.NonPublic | BindingFlags.Static)!;

            string resolved = (string)method.Invoke(null, ["tool.exe"])!;

            Assert.Equal(matchingExe, resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void AdvanceRotationProfile_UsesBrowserProfilesAndLastUsedProfile()
    {
        string basePath = BrowserHelper.GetUserDataPath(BrowserType.Yandex);
        string firstProfilePath = Path.Combine(basePath, "Profile 900031");
        string secondProfilePath = Path.Combine(basePath, "Profile 900032");

        try
        {
            Directory.CreateDirectory(firstProfilePath);
            Directory.CreateDirectory(secondProfilePath);
            File.WriteAllText(Path.Combine(firstProfilePath, "Preferences"), "{}");
            File.WriteAllText(Path.Combine(secondProfilePath, "Preferences"), "{}");

            var element = new CustomElement
            {
                Browser = BrowserType.Yandex,
                RotationProfilePaths = [firstProfilePath, secondProfilePath],
                LastUsedProfile = "Profile 900031"
            };
            MethodInfo method = typeof(ActionService).GetMethod("AdvanceRotationProfile", BindingFlags.NonPublic | BindingFlags.Static)!;

            string next = (string)method.Invoke(null, [element])!;

            Assert.Equal("Profile 900032", next);
        }
        finally
        {
            TryDeleteDirectory(firstProfilePath);
            TryDeleteDirectory(secondProfilePath);
        }
    }

    [Fact]
    public async Task StartSearchAsync_NonEmptyInput_UsesRuntimeProcessStart()
    {
        var runtime = new FakeActionServiceRuntime();
        var service = new ActionService(new AppSettingsService(), runtime);

        await service.StartSearchAsync("coverage");

        Assert.Single(runtime.StartedProcessInfos);
        Assert.Contains("https://www.google.com/search?q=coverage", runtime.StartedProcessInfos[0].ArgumentList);
    }

    [Fact]
    public async Task ExecuteCustomActionAsync_ScriptFile_WhenConfirmationAccepted_StartsScriptProcess()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string powershellExe = Path.Combine(tempRoot, "powershell.exe");
        string scriptPath = Path.Combine(tempRoot, "script.ps1");
        string? originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            File.WriteAllText(powershellExe, string.Empty);
            File.WriteAllText(scriptPath, "Write-Host test");
            Environment.SetEnvironmentVariable("PATH", tempRoot);

            var runtime = new FakeActionServiceRuntime { ConfirmResult = true };
            var service = new ActionService(new AppSettingsService(), runtime);
            var element = new CustomElement
            {
                ActionType = nameof(ActionType.ScriptFile),
                ActionValue = scriptPath
            };

            ActionExecutionResult result = await service.ExecuteCustomActionAsync(element);

            Assert.True(result.Success);
            Assert.Single(runtime.ConfirmMessages);
            Assert.Single(runtime.StartedProcessInfos);
            Assert.Equal(powershellExe, runtime.StartedProcessInfos[0].FileName);
            Assert.Equal(["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath], runtime.StartedProcessInfos[0].ArgumentList.ToArray());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteCustomActionAsync_ScriptFile_WhenConfirmationRejected_DoesNotStartProcess()
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"), "script.ps1");
        var runtime = new FakeActionServiceRuntime { ConfirmResult = false };
        var service = new ActionService(new AppSettingsService(), runtime);
        var element = new CustomElement
        {
            ActionType = nameof(ActionType.ScriptFile),
            ActionValue = scriptPath
        };

        ActionExecutionResult result = await service.ExecuteCustomActionAsync(element);

        Assert.True(result.Success);
        Assert.Single(runtime.ConfirmMessages);
        Assert.Empty(runtime.StartedProcessInfos);
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

    private sealed class FakeActionServiceRuntime : IActionServiceRuntime
    {
        public List<int> DelayCalls { get; } = [];
        public List<NativeMethods.INPUT[]> SendInputCalls { get; } = [];
        public List<ProcessStartInfo> StartedProcessInfos { get; } = [];
        public List<string> StartedFileNames { get; } = [];
        public List<IntPtr> ForegroundWindowCalls { get; } = [];
        public Queue<IActionProcessHandle?> ProcessesToReturn { get; } = [];
        public HashSet<byte> PressedKeys { get; } = [];
        public FakeColorPickerDialog ColorPicker { get; } = new();
        public FakeFileSorterToolWindow FileSorterWindow { get; set; } = new();
        public FakeQuickNoteToolWindow QuickNoteWindow { get; set; } = new();
        public FakeTimerStopwatchToolWindow TimerWindow { get; set; } = new();
        public List<string> ConfirmMessages { get; } = [];
        public bool ConfirmResult { get; set; } = true;
        public int CreateFileSorterWindowCalls { get; private set; }
        public int CreateQuickNoteWindowCalls { get; private set; }
        public int CreateTimerWindowCalls { get; private set; }

        public Task DelayAsync(int milliseconds)
        {
            DelayCalls.Add(milliseconds);
            return Task.CompletedTask;
        }

        public bool IsKeyPressed(byte virtualKey) => PressedKeys.Contains(virtualKey);

        public uint SendInput(NativeMethods.INPUT[] inputs)
        {
            SendInputCalls.Add(inputs);
            return (uint)inputs.Length;
        }

        public bool SetForegroundWindow(IntPtr handle)
        {
            ForegroundWindowCalls.Add(handle);
            return true;
        }

        public bool Confirm(string message, Window? owner)
        {
            ConfirmMessages.Add(message);
            return ConfirmResult;
        }

        public IActionProcessHandle? StartProcess(ProcessStartInfo startInfo)
        {
            StartedProcessInfos.Add(startInfo);
            return ProcessesToReturn.Count > 0 ? ProcessesToReturn.Dequeue() : new FakeActionProcessHandle();
        }

        public IActionProcessHandle? StartProcess(string fileName)
        {
            StartedFileNames.Add(fileName);
            return ProcessesToReturn.Count > 0 ? ProcessesToReturn.Dequeue() : new FakeActionProcessHandle();
        }

        public IColorPickerDialog CreateColorPickerDialog(Window? owner) => ColorPicker;

        public IFileSorterToolWindow CreateFileSorterWindow(AppSettingsService settingsService, Window? owner)
        {
            CreateFileSorterWindowCalls++;
            return FileSorterWindow;
        }

        public IQuickNoteToolWindow CreateQuickNoteWindow(AppSettingsService settingsService, Window? owner)
        {
            CreateQuickNoteWindowCalls++;
            return QuickNoteWindow;
        }

        public ITimerStopwatchToolWindow CreateTimerStopwatchWindow(Window? owner)
        {
            CreateTimerWindowCalls++;
            return TimerWindow;
        }

        public Window? GetMainWindow() => null;
    }

    private sealed class FakeActionProcessHandle : IActionProcessHandle
    {
        private readonly Queue<IntPtr> _handles;
        private IntPtr _currentHandle;

        public FakeActionProcessHandle()
            : this([])
        {
        }

        public FakeActionProcessHandle(IEnumerable<IntPtr> handles)
        {
            _handles = new Queue<IntPtr>(handles);
        }

        public IntPtr MainWindowHandle => _currentHandle;

        public int RefreshCalls { get; private set; }

        public void Refresh()
        {
            RefreshCalls++;
            if (_handles.Count > 0)
            {
                _currentHandle = _handles.Dequeue();
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeColorPickerDialog : IColorPickerDialog
    {
        public int ShowDialogCalls { get; private set; }

        public bool? ShowDialog()
        {
            ShowDialogCalls++;
            return true;
        }
    }

    private sealed class FakeQuickNoteToolWindow : IQuickNoteToolWindow
    {
        public bool IsVisible { get; set; }
        public int ActivateCalls { get; private set; }
        public int ShowSlidingCalls { get; private set; }
        public AppSettings? LastSettings { get; private set; }
        public event EventHandler? Closed;

        public bool Activate()
        {
            ActivateCalls++;
            return true;
        }

        public void ShowSliding(AppSettings settings)
        {
            ShowSlidingCalls++;
            LastSettings = settings;
        }

        public void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeFileSorterToolWindow : IFileSorterToolWindow
    {
        public bool IsVisible { get; set; }
        public int ActivateCalls { get; private set; }
        public int ShowNearPanelCalls { get; private set; }
        public AppSettingsService? LastSettingsService { get; private set; }
        public event EventHandler? Closed;

        public bool Activate()
        {
            ActivateCalls++;
            return true;
        }

        public void ShowNearPanel(AppSettingsService settingsService)
        {
            ShowNearPanelCalls++;
            LastSettingsService = settingsService;
        }

        public void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeTimerStopwatchToolWindow : ITimerStopwatchToolWindow
    {
        public bool IsVisible { get; set; }
        public int ActivateCalls { get; private set; }
        public int ShowNearPanelCalls { get; private set; }
        public AppSettingsService? LastSettingsService { get; private set; }
        public event EventHandler? Closed;

        public bool Activate()
        {
            ActivateCalls++;
            return true;
        }

        public void ShowNearPanel(AppSettingsService settingsService)
        {
            ShowNearPanelCalls++;
            LastSettingsService = settingsService;
        }

        public void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);
    }
}
