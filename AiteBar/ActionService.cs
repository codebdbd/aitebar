using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

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
    internal static bool DisableStandardPythonProbing { get; set; }

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
                await onBeforeExecute().ConfigureAwait(false);
            }

            if (!Enum.TryParse<ActionType>(el.ActionType, out ActionType actionType))
            {
                return ActionExecutionResult.Failed(LocalizationService.Get("Action_LaunchFailed"));
            }

            switch (actionType)
            {
                case ActionType.Hotkey:
                    return await ExecuteHotkeyAsync(el).ConfigureAwait(false);
                case ActionType.Web:
                    await ExecuteWebActionAsync(el).ConfigureAwait(false);
                    break;
                case ActionType.Program:
                case ActionType.File:
                case ActionType.Folder:
                    var psi = new ProcessStartInfo(el.ActionValue) { UseShellExecute = true };
                    if (actionType == ActionType.Program && el.RunAsAdmin)
                    {
                        psi.Verb = "runas";
                    }
                    using (var _ = _runtime.StartProcess(psi))
                    {
                        break;
                    }
                case ActionType.ScriptFile:
                    await StartScriptFileAsync(el).ConfigureAwait(false);
                    break;
                case ActionType.Command:
                    ExecuteCommand(el.ActionValue);
                    break;
            }

            return ActionExecutionResult.Ok;
        }
        catch (Exception ex)
        {
            if (ex is Win32Exception { NativeErrorCode: 1223 })
            {
                return ActionExecutionResult.Ok;
            }

            TelemetryService.CaptureException(ex, "custom_action", new Dictionary<string, string?>
            {
                ["action_type"] = el.ActionType,
                ["browser"] = el.Browser.ToString(),
                ["is_app_mode"] = el.IsAppMode.ToString(),
                ["open_fullscreen"] = el.OpenFullscreen.ToString()
            });
            if (Enum.TryParse<ActionType>(el.ActionType, out var failedActionType) &&
                ex is Win32Exception { NativeErrorCode: 2 })
            {
                if (failedActionType == ActionType.Web)
                {
                    string browserName = LocalizationService.Get($"Browser_{el.Browser}");
                    if (browserName.StartsWith("Browser_", StringComparison.Ordinal))
                    {
                        browserName = el.Browser.ToString();
                    }

                    return ActionExecutionResult.Failed(LocalizationService.Format("Action_BrowserNotFound", browserName));
                }

                if (failedActionType is ActionType.Program or ActionType.File or ActionType.Folder or ActionType.ScriptFile)
                {
                    return ActionExecutionResult.Failed(LocalizationService.Format("Action_TargetNotFound", el.ActionValue));
                }
            }

            if (ex is FileNotFoundException fnf)
            {
                return ActionExecutionResult.Failed(fnf.Message);
            }

            if (ex is InvalidOperationException ioe)
            {
                return ActionExecutionResult.Failed(ioe.Message);
            }

            return ActionExecutionResult.Failed(LocalizationService.Get("Action_LaunchFailed"));
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
                    await _runtime.DelayAsync(KeyDelayMs).ConfigureAwait(false);
                }
            }

            if (mainVk != 0)
            {
                var downInput = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = mainVk } } };
                SendKeyboardInputOrThrow(downInput, $"main key down: VK={mainVk}");
                mainKeyDown = true;
                await _runtime.DelayAsync(KeyDelayMs).ConfigureAwait(false);

                var upInput = new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = mainVk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } } };
                SendKeyboardInputOrThrow(upInput, $"main key up: VK={mainVk}");
                mainKeyDown = false;
                await _runtime.DelayAsync(KeyDelayMs).ConfigureAwait(false);
            }

            await ReleaseInjectedModifiersAsync(pressedModifiers, KeyDelayMs, throwOnFailure: true).ConfigureAwait(false);
            return ActionExecutionResult.Ok;
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            TelemetryService.CaptureException(ex, "hotkey_execution");
            if (Enum.TryParse<ActionType>(el.ActionType, out var failedActionType) &&
                ex is Win32Exception { NativeErrorCode: 2 })
            {
                if (failedActionType == ActionType.Web)
                {
                    string browserName = LocalizationService.Get($"Browser_{el.Browser}");
                    if (browserName.StartsWith("Browser_", StringComparison.Ordinal))
                    {
                        browserName = el.Browser.ToString();
                    }

                    return ActionExecutionResult.Failed(LocalizationService.Format("Action_BrowserNotFound", browserName));
                }

                if (failedActionType is ActionType.Program or ActionType.File or ActionType.Folder or ActionType.ScriptFile)
                {
                    return ActionExecutionResult.Failed(LocalizationService.Format("Action_TargetNotFound", el.ActionValue));
                }
            }

            return ActionExecutionResult.Failed(LocalizationService.Get("Action_LaunchFailed"));
        }
        finally
        {
            if (mainKeyDown)
            {
                TrySendKeyUp(mainVk, "main key cleanup");
            }

            await ReleaseInjectedModifiersAsync(pressedModifiers, KeyDelayMs, throwOnFailure: false).ConfigureAwait(false);
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
            await _runtime.DelayAsync(delayMs).ConfigureAwait(false);
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
        if (!ActionTargetHelper.TryNormalizeWebUrl(el.ActionValue, out string normalizedUrl))
        {
            throw new InvalidOperationException("The web action URL must use HTTP or HTTPS and have a valid host.");
        }

        CustomElement launchElement = _settingsService.CloneElement(el);
        string enteredUrl = el.ActionValue.Trim();
        launchElement.ActionValue = enteredUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                  enteredUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? enteredUrl
            : normalizedUrl;
        string prof = launchElement.UseRotation ? AdvanceRotationProfile(launchElement) : launchElement.ChromeProfile;
        // Keep the active instance in sync with the persisted launch state.
        el.LastUsedProfile = prof;
        bool isPersistedElement = _settingsService.Elements.Any(element =>
            string.Equals(element.Id, el.Id, StringComparison.Ordinal));
        if (!isPersistedElement)
        {
            await _settingsService.SaveAsync().ConfigureAwait(false);
        }
        else
        {
            await _settingsService.UpdateElementAsync(el.Id, element => element.LastUsedProfile = prof).ConfigureAwait(false);
        }

        ProcessStartInfo psi = BuildWebActionProcessStartInfo(launchElement, prof);
        using var proc = _runtime.StartProcess(psi);
        if (proc != null && launchElement.OpenFullscreen)
        {
            await TryEnterFullscreenAsync(proc).ConfigureAwait(false);
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
            var psi = new ProcessStartInfo("cmd.exe")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(command);
            using var _ = _runtime.StartProcess(psi);
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

        if (onBeforeExecute != null) await onBeforeExecute().ConfigureAwait(false);

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
        if (onBeforeExecute != null) await onBeforeExecute().ConfigureAwait(false);
        using var process = _runtime.StartProcess(
            new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true }) ??
            throw new InvalidOperationException(LocalizationService.Get("Action_LaunchFailed"));
    }

    public async Task StartRecordVideoAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute().ConfigureAwait(false);
        using var process = _runtime.StartProcess(
            new ProcessStartInfo("ms-screenclip:?type=recording") { UseShellExecute = true }) ??
            throw new InvalidOperationException(LocalizationService.Get("Action_LaunchFailed"));
    }

    public async Task StartCalculatorAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute().ConfigureAwait(false);
        using var process = _runtime.StartProcess("calc.exe") ??
            throw new InvalidOperationException(LocalizationService.Get("Action_LaunchFailed"));
    }

    public async Task StartExplorerAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute().ConfigureAwait(false);
        using var process = _runtime.StartProcess(BuildShellLaunchProcessStartInfo("explorer.exe")) ??
            throw new InvalidOperationException(LocalizationService.Get("Action_LaunchFailed"));
    }

    public async Task StartDownloadsAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute().ConfigureAwait(false);
        _runtime.StartProcess(BuildShellLaunchProcessStartInfo("shell:Downloads"));
    }

    public async Task StartShowDesktopAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute().ConfigureAwait(false);
        _runtime.StartProcess(BuildShellLaunchProcessStartInfo("shell:::{3080F90D-D7AD-11D9-BD98-0000947B0257}"));
    }

    public async Task StartAppsFolderAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute().ConfigureAwait(false);
        _runtime.StartProcess(BuildShellLaunchProcessStartInfo("shell:AppsFolder"));
    }

    public async Task StartCopilotAsync(Func<Task>? onBeforeExecute = null)
    {
        if (onBeforeExecute != null) await onBeforeExecute().ConfigureAwait(false);

        const int KeyDelayMs = 30;
        var pressedModifiers = new List<byte>();

        try
        {
            // Press Win key
            var winDownInput = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_LWIN } }
            };
            SendKeyboardInputOrThrow(winDownInput, "Win key down");
            pressedModifiers.Add(NativeMethods.VK_LWIN);
            await _runtime.DelayAsync(KeyDelayMs).ConfigureAwait(false);

            // Press C key
            var cDownInput = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = 0x43 } }
            };
            SendKeyboardInputOrThrow(cDownInput, "C key down");
            await _runtime.DelayAsync(KeyDelayMs).ConfigureAwait(false);

            // Release C key
            var cUpInput = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = 0x43, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
            };
            SendKeyboardInputOrThrow(cUpInput, "C key up");
            await _runtime.DelayAsync(KeyDelayMs).ConfigureAwait(false);
        }
        finally
        {
            await ReleaseInjectedModifiersAsync(pressedModifiers, KeyDelayMs, throwOnFailure: false).ConfigureAwait(false);
        }
    }

    public async Task LaunchUtilityAsync(string utilityId, Func<Task>? onBeforeExecute = null)
    {
        var utility = UtilityRegistry.GetById(utilityId);
        if (utility != null)
        {
            await utility.LaunchAsync(_settingsService, _runtime.GetMainWindow(), onBeforeExecute).ConfigureAwait(false);
        }
    }

    internal static ProcessStartInfo BuildShellLaunchProcessStartInfo(string target) => new(target)
    {
        UseShellExecute = true
    };

    internal async Task StartScriptFileAsync(CustomElement el)
    {
        string? rawPath = el.ActionValue?.Trim();
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new FileNotFoundException(
                LocalizationService.Format("Action_TargetNotFound", el.ActionValue),
                el.ActionValue);
        }

        string normalizedPath = Environment.ExpandEnvironmentVariables(rawPath.Trim('\"'));
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException(
                LocalizationService.Format("Action_TargetNotFound", el.ActionValue),
                el.ActionValue);
        }

        if (!el.SkipScriptConfirmation &&
            !_runtime.Confirm(LocalizationService.Format("Action_ConfirmScript", normalizedPath), _runtime.GetMainWindow()))
        {
            return;
        }

        ProcessStartInfo psi;
        try
        {
            psi = CreateScriptProcessStartInfo(
                normalizedPath,
                el.ScriptArguments,
                el.HideScriptWindow,
                el.RunAsAdmin,
                el.KeepScriptWindowOpen);
        }
        catch (InvalidOperationException)
        {
            // Fallback: If custom interpreter resolution fails, execute via Windows Shell
            psi = new ProcessStartInfo(normalizedPath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(normalizedPath) ?? Environment.CurrentDirectory
            };
            if (!string.IsNullOrWhiteSpace(el.ScriptArguments))
            {
                psi.Arguments = el.ScriptArguments.Trim();
            }
            if (el.RunAsAdmin)
            {
                psi.Verb = "runas";
            }
        }

        EnsureProcessStartInfoCompatibility(psi);

        using var proc = _runtime.StartProcess(psi) ?? throw new InvalidOperationException(LocalizationService.Get("Action_LaunchFailed"));
        await Task.CompletedTask.ConfigureAwait(false);
    }

    internal Task StartScriptFileAsync(string scriptPath) =>
        StartScriptFileAsync(new CustomElement { ActionValue = scriptPath, SkipScriptConfirmation = true });

    internal static bool IsUsablePowerShellExecutable(string path)
    {
        if (!File.Exists(path)) return false;

        if (path.Contains(@"Microsoft\WindowsApps", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var fi = new FileInfo(path);
                if (fi.Length == 0) return false;
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    internal static string? ResolvePowerShellExecutable()
    {
        // 1. Check PATH for PowerShell 7 (pwsh.exe)
        string? shell = PathHelper.FindExecutableOnPath("pwsh.exe");
        if (shell != null && IsUsablePowerShellExecutable(shell))
        {
            return shell;
        }

        // 2. Check PATH for Windows PowerShell (powershell.exe)
        shell = PathHelper.FindExecutableOnPath("powershell.exe");
        if (shell != null && IsUsablePowerShellExecutable(shell))
        {
            return shell;
        }

        // 3. Fallback: check standard installation locations if PATH is missing entries
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pwshProgramFiles = Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
        if (File.Exists(pwshProgramFiles))
        {
            return pwshProgramFiles;
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string pwshWinApps = Path.Combine(localAppData, "Microsoft", "WindowsApps", "pwsh.exe");
        if (IsUsablePowerShellExecutable(pwshWinApps))
        {
            return pwshWinApps;
        }

        string systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string defaultPowershell = Path.Combine(systemFolder, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(defaultPowershell))
        {
            return defaultPowershell;
        }

        return null;
    }

    internal static bool IsUsablePythonExecutable(string path)
    {
        if (!File.Exists(path)) return false;

        // Microsoft Store dummy redirector (AppInstallerPythonRedirector) has Length == 0
        if (path.Contains(@"Microsoft\WindowsApps", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var fi = new FileInfo(path);
                if (fi.Length == 0) return false;
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private static string? FindNewestPythonInDirectory(string parentDir, string targetExe, string? dirPrefix = null)
    {
        if (!Directory.Exists(parentDir)) return null;

        try
        {
            var dirs = Directory.GetDirectories(parentDir);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(dirs);

            foreach (string dir in dirs)
            {
                if (dirPrefix != null && !Path.GetFileName(dir).StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = Path.Combine(dir, targetExe);
                if (File.Exists(candidate) && IsUsablePythonExecutable(candidate))
                {
                    return candidate;
                }

                string scriptsCandidate = Path.Combine(dir, "Scripts", targetExe);
                if (File.Exists(scriptsCandidate) && IsUsablePythonExecutable(scriptsCandidate))
                {
                    return scriptsCandidate;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }

        return null;
    }

    internal static string? ResolvePythonExecutable(string? scriptPath = null, bool windowless = false)
    {
        string targetExe = windowless ? "pythonw.exe" : "python.exe";

        // 1. Check local virtual environment near the script
        if (!string.IsNullOrWhiteSpace(scriptPath))
        {
            try
            {
                string? scriptDir = Path.GetDirectoryName(scriptPath);
                if (!string.IsNullOrEmpty(scriptDir) && Directory.Exists(scriptDir))
                {
                    string venvExe = Path.Combine(scriptDir, ".venv", "Scripts", targetExe);
                    if (File.Exists(venvExe)) return venvExe;

                    string venvExe2 = Path.Combine(scriptDir, "venv", "Scripts", targetExe);
                    if (File.Exists(venvExe2)) return venvExe2;

                    string? parentDir = Directory.GetParent(scriptDir)?.FullName;
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                    {
                        string parentVenvExe = Path.Combine(parentDir, ".venv", "Scripts", targetExe);
                        if (File.Exists(parentVenvExe)) return parentVenvExe;

                        string parentVenvExe2 = Path.Combine(parentDir, "venv", "Scripts", targetExe);
                        if (File.Exists(parentVenvExe2)) return parentVenvExe2;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        // 2. Check PATH, ignoring 0-byte WindowsApps dummy stubs
        string? fromPath = PathHelper.FindExecutableOnPath(targetExe);
        if (fromPath != null && IsUsablePythonExecutable(fromPath))
        {
            return fromPath;
        }

        // 3. Fallback: check standard Python installation directories
        if (!DisableStandardPythonProbing)
        {
            // 3a. LocalAppData Programs Python (e.g. %LOCALAPPDATA%\Programs\Python\Python312\python.exe)
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programsPython = Path.Combine(localAppData, "Programs", "Python");
            string? foundInPrograms = FindNewestPythonInDirectory(programsPython, targetExe);
            if (foundInPrograms != null) return foundInPrograms;

            // 3b. uv python installations (e.g. %APPDATA%\uv\python\cpython-3.12.13-windows-x86_64-none\python.exe)
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string uvPython = Path.Combine(appData, "uv", "python");
            string? foundInUv = FindNewestPythonInDirectory(uvPython, targetExe);
            if (foundInUv != null) return foundInUv;

            // 3c. ProgramFiles Python
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string? foundInPf = FindNewestPythonInDirectory(programFiles, targetExe, dirPrefix: "Python");
            if (foundInPf != null) return foundInPf;

            // 3d. ProgramFilesX86 Python
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string? foundInPfX86 = FindNewestPythonInDirectory(programFilesX86, targetExe, dirPrefix: "Python");
            if (foundInPfX86 != null) return foundInPfX86;

            // 3e. Chocolatey
            string chocoPython = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "bin", targetExe);
            if (File.Exists(chocoPython) && IsUsablePythonExecutable(chocoPython)) return chocoPython;

            // 3f. Pyenv-win
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string pyenvPython = Path.Combine(userProfile, ".pyenv", "pyenv-win", "shims", targetExe);
            if (File.Exists(pyenvPython) && IsUsablePythonExecutable(pyenvPython)) return pyenvPython;

            // 3g. py.exe launcher as fallback (for console python)
            if (!windowless)
            {
                string systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
                string pyLauncher = Path.Combine(Path.GetDirectoryName(systemFolder) ?? @"C:\Windows", "py.exe");
                if (File.Exists(pyLauncher)) return pyLauncher;
            }
        }

        // If windowless pythonw was requested, but only python.exe exists, check if console python can be found
        if (windowless)
        {
            string? fallbackConsolePython = ResolvePythonExecutable(scriptPath, windowless: false);
            if (fallbackConsolePython != null) return fallbackConsolePython;
        }

        // If nothing else found, return fromPath if it exists (even if in WindowsApps) as a final attempt
        if (fromPath != null && File.Exists(fromPath))
        {
            return fromPath;
        }

        return null;
    }

    internal static ProcessStartInfo CreateScriptProcessStartInfo(
        string scriptPath,
        string? scriptArguments = null,
        bool hideWindow = false,
        bool runAsAdmin = false,
        bool keepWindowOpen = false)
    {
        string workingDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory;
        string extension = Path.GetExtension(scriptPath).ToLowerInvariant();
        ProcessStartInfo psi;
        switch (extension)
        {
            case ".bat":
            case ".cmd":
                if (!hideWindow && keepWindowOpen)
                {
                    psi = new ProcessStartInfo("cmd.exe")
                    {
                        UseShellExecute = true,
                        WorkingDirectory = workingDirectory,
                        Arguments = "/k \"\"" + scriptPath + "\"" +
                            (string.IsNullOrWhiteSpace(scriptArguments) ? "" : " " + scriptArguments.Trim()) + "\""
                    };
                }
                else
                {
                    psi = new ProcessStartInfo(scriptPath)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = workingDirectory,
                        Arguments = scriptArguments?.Trim() ?? ""
                    };
                }

                if (hideWindow)
                {
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                }

                if (runAsAdmin)
                {
                    psi.Verb = "runas";
                }

                return psi;
            case ".ps1":
                string? shell = ResolvePowerShellExecutable();
                if (shell == null)
                {
                    throw new InvalidOperationException(LocalizationService.Get("Action_LaunchFailed"));
                }
                psi = new ProcessStartInfo(shell)
                {
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory
                };
                psi.ArgumentList.Add("-NoProfile");
                if (!hideWindow && keepWindowOpen)
                {
                    psi.ArgumentList.Add("-NoExit");
                }
                psi.ArgumentList.Add("-ExecutionPolicy");
                psi.ArgumentList.Add("Bypass");
                psi.ArgumentList.Add("-File");
                psi.ArgumentList.Add(scriptPath);
                break;
            case ".pyw":
                string? pythonwExe = ResolvePythonExecutable(scriptPath, windowless: true);
                if (pythonwExe == null || !File.Exists(pythonwExe))
                {
                    throw new InvalidOperationException(LocalizationService.Get("Action_PythonNotFound"));
                }
                psi = new ProcessStartInfo(pythonwExe)
                {
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory
                };
                psi.ArgumentList.Add(scriptPath);
                break;
            case ".py":
                string? pythonExe = ResolvePythonExecutable(scriptPath, windowless: false);
                if (pythonExe == null || !File.Exists(pythonExe))
                {
                    throw new InvalidOperationException(LocalizationService.Get("Action_PythonNotFound"));
                }
                psi = new ProcessStartInfo(pythonExe)
                {
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory
                };
                if (!hideWindow && keepWindowOpen)
                {
                    psi.ArgumentList.Add("-i");
                }
                psi.ArgumentList.Add(scriptPath);
                break;
            default: throw new InvalidOperationException(LocalizationService.Get("Action_UnsupportedScript"));
        }

        AppendScriptArguments(psi, scriptArguments);
        ApplyWindowAndElevationOptions(psi, hideWindow, runAsAdmin);
        return psi;
    }

    internal static IEnumerable<string> ParseArgumentTokens(string argumentString)
    {
        if (string.IsNullOrWhiteSpace(argumentString))
            yield break;

        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < argumentString.Length; i++)
        {
            char c = argumentString[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }

    private static void AppendScriptArguments(ProcessStartInfo psi, string? scriptArguments)
    {
        if (string.IsNullOrWhiteSpace(scriptArguments)) return;
        foreach (string token in ParseArgumentTokens(scriptArguments))
        {
            psi.ArgumentList.Add(token);
        }
    }

    private static void ApplyWindowAndElevationOptions(ProcessStartInfo psi, bool hideWindow, bool runAsAdmin)
    {
        if (hideWindow)
        {
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
        }

        if (runAsAdmin)
        {
            psi.UseShellExecute = true;
            psi.Verb = "runas";
        }
    }

    internal static void EnsureProcessStartInfoCompatibility(ProcessStartInfo psi)
    {
        if (psi.UseShellExecute && psi.ArgumentList.Count > 0)
        {
            psi.Arguments = BuildArgumentsString(psi.ArgumentList);
            psi.ArgumentList.Clear();
        }
    }

    internal static string BuildArgumentsString(IEnumerable<string> arguments)
    {
        var sb = new StringBuilder();
        foreach (string arg in arguments)
        {
            if (sb.Length > 0) sb.Append(' ');
            if (string.IsNullOrEmpty(arg))
            {
                sb.Append("\"\"");
            }
            else if (arg.IndexOfAny([' ', '\t', '\n', '\v', '\"']) >= 0)
            {
                sb.Append('\"');
                for (int i = 0; i < arg.Length; i++)
                {
                    char c = arg[i];
                    if (c == '\"')
                    {
                        sb.Append('\\');
                    }
                    sb.Append(c);
                }
                sb.Append('\"');
            }
            else
            {
                sb.Append(arg);
            }
        }
        return sb.ToString();
    }

    private async Task TryEnterFullscreenAsync(IActionProcessHandle proc)
    {
        for (int i = 0; i < FullscreenActivationAttempts; i++)
        {
            await _runtime.DelayAsync(FullscreenWindowPollDelayMs).ConfigureAwait(false);
            proc.Refresh();
            if (proc.MainWindowHandle == IntPtr.Zero) continue;

            _runtime.SetForegroundWindow(proc.MainWindowHandle);
            await _runtime.DelayAsync(FullscreenForegroundDelayMs).ConfigureAwait(false);
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
        Dispatcher? dispatcher = owner?.Dispatcher ?? Application.Current?.Dispatcher;
        return InvokeOnUiDispatcher(dispatcher, () =>
        {
            var dialog = new DarkDialog(message, isConfirm: true) { Owner = owner };
            return dialog.ShowDialog() == true;
        });
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

    public Window? GetMainWindow()
    {
        Application? application = Application.Current;
        return application == null
            ? null
            : InvokeOnUiDispatcher(application.Dispatcher, () => application.MainWindow);
    }

    internal static T InvokeOnUiDispatcher<T>(Dispatcher? dispatcher, Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return dispatcher == null || dispatcher.CheckAccess()
            ? action()
            : dispatcher.Invoke(action);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}

internal sealed class ActionProcessHandle(Process process) : IActionProcessHandle
{
    public IntPtr MainWindowHandle => process.MainWindowHandle;

    public void Refresh() => process.Refresh();

    public void Dispose() => process.Dispose();
}
