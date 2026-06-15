using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AiteBar;

internal interface IActionServiceRuntime
{
    Task DelayAsync(int milliseconds);
    bool IsKeyPressed(byte virtualKey);
    uint SendInput(NativeMethods.INPUT[] inputs);
    bool SetForegroundWindow(IntPtr handle);
    bool Confirm(string message, Window? owner);
    IActionProcessHandle? StartProcess(ProcessStartInfo startInfo);
    IActionProcessHandle? StartProcess(string fileName);
    Window? GetMainWindow();
}

internal interface IActionProcessHandle : IDisposable
{
    IntPtr MainWindowHandle { get; }
    void Refresh();
}

[SupportedOSPlatform("windows6.1")]
public class ActionService
{
    private readonly AppSettingsService _settingsService;
    private readonly IActionServiceRuntime _runtime;
    private const int FullscreenActivationAttempts = 25;
    private const int FullscreenWindowPollDelayMs = 200;
    private const int FullscreenForegroundDelayMs = 100;

    public ActionService(AppSettingsService settingsService)
        : this(settingsService, new ActionServiceRuntime())
    {
    }

    internal ActionService(AppSettingsService settingsService, IActionServiceRuntime runtime)
    {
        _settingsService = settingsService;
        _runtime = runtime;
    }

    public async Task<ActionExecutionResult> ExecuteCustomActionAsync(CustomElement el, Func<Task>? onBeforeExecute = null)
    {
        try
        {
            if (onBeforeExecute != null)
            {
                await onBeforeExecute();
            }

            if (Enum.TryParse<ActionType>(el.ActionType, out ActionType actionType))
            {
                switch (actionType)
                {
                    case ActionType.Hotkey:
                        return await ExecuteHotkeyAsync(el);
                    case ActionType.Web:
                        await ExecuteWebActionAsync(el);
                        break;
                    case ActionType.Program:
                    case ActionType.File:
                    case ActionType.Folder:
                        _runtime.StartProcess(new ProcessStartInfo(el.ActionValue) { UseShellExecute = true });
                        break;
                    case ActionType.ScriptFile:
                        await StartScriptFileAsync(el.ActionValue);
                        break;
                    case ActionType.Command:
                        ExecuteCommand(el.ActionValue);
                        break;
                }
            }

            return ActionExecutionResult.Ok;
        }
        catch (Exception ex)
        {
            TelemetryService.CaptureException(ex, "custom_action", new Dictionary<string, string?>
            {
                ["action_type"] = el.ActionType,
                ["browser"] = el.Browser.ToString(),
                ["is_app_mode"] = el.IsAppMode.ToString(),
                ["open_fullscreen"] = el.OpenFullscreen.ToString()
            });
            return ActionExecutionResult.Failed(ex.Message);
        }
    }

    private async Task<ActionExecutionResult> ExecuteHotkeyAsync(CustomElement el)
    {
        const int KeyDelayMs = 30;
        var pressedModifiers = new List<byte>();
        byte mainVk = 0;
        bool mainKeyDown = false;

        try
        {
            var downKeys = new List<byte>();
            if (el.Ctrl) downKeys.Add(NativeMethods.VK_CONTROL);
            if (el.Shift) downKeys.Add(NativeMethods.VK_SHIFT);
            if (el.Alt) downKeys.Add(NativeMethods.VK_MENU);
            if (el.Win) downKeys.Add(NativeMethods.VK_LWIN);

            if (Enum.TryParse(typeof(Key), el.Key, out object? k))
                mainVk = (byte)KeyInterop.VirtualKeyFromKey((Key)k!);

            foreach (byte vk in downKeys)
            {
                if (!_runtime.IsKeyPressed(vk))
                {
                    var input = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = vk } } };
                    SendKeyboardInputOrThrow(input, $"modifier key down: VK={vk}");
                    pressedModifiers.Add(vk);
                    await _runtime.DelayAsync(KeyDelayMs);
                }
            }

            if (mainVk != 0)
            {
                var downInput = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = mainVk } } };
                SendKeyboardInputOrThrow(downInput, $"main key down: VK={mainVk}");
                mainKeyDown = true;
                await _runtime.DelayAsync(KeyDelayMs);

                var upInput = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = mainVk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } } };
                SendKeyboardInputOrThrow(upInput, $"main key up: VK={mainVk}");
                mainKeyDown = false;
                await _runtime.DelayAsync(KeyDelayMs);
            }

            await ReleaseInjectedModifiersAsync(pressedModifiers, KeyDelayMs, throwOnFailure: true);
            return ActionExecutionResult.Ok;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            TelemetryService.CaptureException(ex, "hotkey_execution");
            return ActionExecutionResult.Failed(ex.Message);
        }
        finally
        {
            if (mainKeyDown)
            {
                TrySendKeyUp(mainVk, "main key cleanup");
            }

            await ReleaseInjectedModifiersAsync(pressedModifiers, KeyDelayMs, throwOnFailure: false);
        }
    }

    private void SendKeyboardInputOrThrow(NativeMethods.INPUT input, string operation)
    {
        uint sent = _runtime.SendInput([input]);
        if (sent != 1)
        {
            throw new InvalidOperationException($"Failed to send {operation}.");
        }
    }

    private async Task ReleaseInjectedModifiersAsync(List<byte> pressedModifiers, int delayMs, bool throwOnFailure)
    {
        for (int index = pressedModifiers.Count - 1; index >= 0; index--)
        {
            byte vk = pressedModifiers[index];
            var upInput = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
            };

            uint sent = _runtime.SendInput([upInput]);
            if (sent != 1)
            {
                var exception = new InvalidOperationException($"Failed to send modifier key up: VK={vk}.");
                Logger.Log(exception);
                if (throwOnFailure)
                {
                    throw exception;
                }
                continue;
            }

            pressedModifiers.RemoveAt(index);
            await _runtime.DelayAsync(delayMs);
        }
    }

    private void TrySendKeyUp(byte virtualKey, string operation)
    {
        try
        {
            var input = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = virtualKey, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
            };
            SendKeyboardInputOrThrow(input, operation);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private async Task ExecuteWebActionAsync(CustomElement el)
    {
        string prof = el.UseRotation ? AdvanceRotationProfile(el) : el.ChromeProfile;
        el.LastUsedProfile = prof;
        await _settingsService.SaveAsync();

        ProcessStartInfo psi = BuildWebActionProcessStartInfo(el, prof);
        using var proc = _runtime.StartProcess(psi);
        if (proc != null && el.OpenFullscreen)
        {
            await TryEnterFullscreenAsync(proc);
        }
    }

    private static string AdvanceRotationProfile(CustomElement el)
    {
        List<BrowserProfileInfo> profiles = BrowserHelper.GetProfiles(el.Browser);
        return ProfileRotationHelper.AdvanceProfile(profiles, el.RotationProfilePaths, el.LastUsedProfile);
    }

    internal static ProcessStartInfo BuildWebActionProcessStartInfo(CustomElement el, string profilePathOrName)
    {
        var psi = new ProcessStartInfo(BrowserHelper.GetExecutablePath(el.Browser)) { UseShellExecute = false };
        if (el.IsAppMode) psi.ArgumentList.Add($"--app={el.ActionValue}"); else psi.ArgumentList.Add(el.ActionValue);

        if (el.IsIncognito)
        {
            if (el.Browser == BrowserType.Edge) psi.ArgumentList.Add("-inprivate");
            else if (el.Browser == BrowserType.Opera || el.Browser == BrowserType.OperaGX) psi.ArgumentList.Add("-private");
            else if (el.Browser == BrowserType.Firefox) psi.ArgumentList.Add("-private-window");
            else psi.ArgumentList.Add("--incognito");
        }

        if (!string.IsNullOrEmpty(profilePathOrName))
        {
            if (el.Browser == BrowserType.Firefox)
            {
                psi.ArgumentList.Add("-P");
                psi.ArgumentList.Add(profilePathOrName);
            }
            else
            {
                psi.ArgumentList.Add($"--profile-directory={Path.GetFileName(profilePathOrName)}");
            }
        }

        return psi;
    }

    private void ExecuteCommand(string command)
    {
        if (_runtime.Confirm(BuildCommandConfirmationMessage(command), _runtime.GetMainWindow()))
        {
            _runtime.StartProcess(new ProcessStartInfo("cmd.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                Arguments = $"/c {command}"
            });
        }
    }

    internal static string BuildCommandConfirmationMessage(string command)
    {
        string message = LocalizationService.Format("Action_ConfirmCommand", command);
        if (ContainsPotentiallyDangerousCommandSyntax(command))
        {
            message += Environment.NewLine + Environment.NewLine + LocalizationService.Get("Action_CommandDangerWarning");
        }

        return message;
    }

    internal static bool ContainsPotentiallyDangerousCommandSyntax(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        if (command.IndexOf('&') >= 0 ||
            command.IndexOf('|') >= 0 ||
            command.IndexOf('>') >= 0 ||
            command.IndexOf('<') >= 0)
        {
            return true;
        }

        return Regex.IsMatch(
            command,
            @"(^|[\s;&|])(?:del|erase|rd|rmdir|rm|remove-item|format|shutdown|restart-computer|stop-computer|bcdedit|diskpart|cipher|taskkill|stop-process|reg\s+delete|takeown|ri)(?:\.exe|\.com|\.ps1|\.bat|\.cmd)?($|[\s;&|:/\\-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public async Task StartSearchAsync(string text, Func<Task>? onBeforeExecute = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (onBeforeExecute != null) await onBeforeExecute();

        // Try Chrome first, then Edge, then system default browser
        var browserPath = BrowserHelper.GetExecutablePath(BrowserType.Chrome);
        if (!File.Exists(browserPath))
        {
            browserPath = BrowserHelper.GetExecutablePath(BrowserType.Edge);
        }

        if (File.Exists(browserPath))
        {
            ProcessStartInfo psi = new ProcessStartInfo(browserPath)
            {
                UseShellExecute = false,
                ArgumentList = { $"https://www.google.com/search?q={Uri.EscapeDataString(text)}" }
            };
            using var proc = _runtime.StartProcess(psi) ?? throw new InvalidOperationException(LocalizationService.Get("Action_SearchFailed"));
        }
        else
        {
            // Fallback to system default browser
            ProcessStartInfo psi = new ProcessStartInfo($"https://www.google.com/search?q={Uri.EscapeDataString(text)}")
            {
                UseShellExecute = true
            };
            using var proc = _runtime.StartProcess(psi) ?? throw new InvalidOperationException(LocalizationService.Get("Action_SearchFailed"));
        }
    }

    public async Task StartScreenshotAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute();
        _runtime.StartProcess(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
    }

    public async Task StartRecordVideoAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute();
        _runtime.StartProcess(new ProcessStartInfo("ms-screenclip:?type=recording") { UseShellExecute = true });
    }

    public async Task StartCalculatorAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute();
        _runtime.StartProcess("calc.exe");
    }

    public async Task StartExplorerAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute();
        _runtime.StartProcess(BuildShellLaunchProcessStartInfo("explorer.exe"));
    }

    public async Task StartDownloadsAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute();
        _runtime.StartProcess(BuildShellLaunchProcessStartInfo("shell:Downloads"));
    }

    public async Task LaunchUtilityAsync(string utilityId, Func<Task>? onBeforeExecute = null)
    {
        var utility = UtilityRegistry.GetById(utilityId);
        if (utility != null)
        {
            await utility.LaunchAsync(_settingsService, _runtime.GetMainWindow(), onBeforeExecute);
        }
    }

    internal static ProcessStartInfo BuildShellLaunchProcessStartInfo(string target) => new(target)
    {
        UseShellExecute = true
    };

    private async Task StartScriptFileAsync(string scriptPath)
    {
        if (!_runtime.Confirm(LocalizationService.Format("Action_ConfirmScript", scriptPath), _runtime.GetMainWindow()))
        {
            return;
        }

        var psi = CreateScriptProcessStartInfo(scriptPath);
        using var proc = _runtime.StartProcess(psi) ?? throw new InvalidOperationException(LocalizationService.Get("Action_LaunchFailed"));
        await Task.CompletedTask;
    }

    internal static ProcessStartInfo CreateScriptProcessStartInfo(string scriptPath)
    {
        string workingDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory;
        string extension = Path.GetExtension(scriptPath).ToLowerInvariant();
        switch (extension)
        {
            case ".bat":
            case ".cmd":
                var psi = new ProcessStartInfo("cmd.exe")
                {
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory
                };
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(scriptPath);
                return psi;
            case ".ps1":
                string? shell = FindExecutableOnPath("pwsh.exe");
                if (shell == null || !File.Exists(shell))
                {
                    shell = FindExecutableOnPath("powershell.exe");
                }
                if (shell == null)
                {
                    throw new InvalidOperationException(LocalizationService.Get("Action_LaunchFailed"));
                }
                var psiPs = new ProcessStartInfo(shell)
                {
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory
                };
                psiPs.ArgumentList.Add("-NoProfile");
                if (Path.GetFileName(shell).Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
                {
                    psiPs.ArgumentList.Add("-ExecutionPolicy");
                    psiPs.ArgumentList.Add("Bypass");
                }
                psiPs.ArgumentList.Add("-File");
                psiPs.ArgumentList.Add(scriptPath);
                return psiPs;
            case ".py":
                string? pythonExe = FindExecutableOnPath("python.exe");
                if (pythonExe == null || !File.Exists(pythonExe))
                {
                    throw new InvalidOperationException(LocalizationService.Get("Action_PythonNotFound"));
                }
                var psiPy = new ProcessStartInfo(pythonExe)
                {
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory
                };
                psiPy.ArgumentList.Add(scriptPath);
                return psiPy;
            default: throw new InvalidOperationException(LocalizationService.Get("Action_UnsupportedScript"));
        }
    }

    private static string? FindExecutableOnPath(string fileName)
    {
        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (string dir in pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(dir.Trim(), fileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
        }

        return null;
    }

    private async Task TryEnterFullscreenAsync(IActionProcessHandle proc)
    {
        for (int i = 0; i < FullscreenActivationAttempts; i++)
        {
            await _runtime.DelayAsync(FullscreenWindowPollDelayMs);
            proc.Refresh();
            if (proc.MainWindowHandle == IntPtr.Zero) continue;

            _runtime.SetForegroundWindow(proc.MainWindowHandle);
            await _runtime.DelayAsync(FullscreenForegroundDelayMs);
            SendVirtualKey((byte)KeyInterop.VirtualKeyFromKey(Key.F11));
            break;
        }
    }

    private void SendVirtualKey(byte virtualKey)
    {
        NativeMethods.INPUT[] inputs =
        [
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = virtualKey } }
            },
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = virtualKey, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
            }
        ];

        uint sent = _runtime.SendInput(inputs);
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException("Failed to send virtual key input.");
        }
    }
}

[SupportedOSPlatform("windows6.1")]
internal sealed class ActionServiceRuntime : IActionServiceRuntime
{
    public Task DelayAsync(int milliseconds) => Task.Delay(milliseconds);

    public bool IsKeyPressed(byte virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    public uint SendInput(NativeMethods.INPUT[] inputs) =>
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());

    public bool SetForegroundWindow(IntPtr handle) => NativeMethods.SetForegroundWindow(handle);

    public bool Confirm(string message, Window? owner)
    {
        var dialog = new DarkDialog(message, isConfirm: true) { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    public IActionProcessHandle? StartProcess(ProcessStartInfo startInfo)
    {
        var process = Process.Start(startInfo);
        return process == null ? null : new ActionProcessHandle(process);
    }

    public IActionProcessHandle? StartProcess(string fileName)
    {
        var process = Process.Start(fileName);
        return process == null ? null : new ActionProcessHandle(process);
    }

    public Window? GetMainWindow() => System.Windows.Application.Current?.MainWindow;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}

internal sealed class ActionProcessHandle(Process process) : IActionProcessHandle
{
    public IntPtr MainWindowHandle => process.MainWindowHandle;

    public void Refresh() => process.Refresh();

    public void Dispose() => process.Dispose();
}
