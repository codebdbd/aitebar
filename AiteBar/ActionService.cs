using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    public class ActionService
    {
        private readonly AppSettingsService _settingsService;
        private QuickNoteWindow? _quickNoteWindow;
        private const int FullscreenActivationAttempts = 25;
        private const int FullscreenWindowPollDelayMs = 200;
        private const int FullscreenForegroundDelayMs = 100;

        public ActionService(AppSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<ActionExecutionResult> ExecuteCustomActionAsync(CustomElement el, Func<Task>? onBeforeExecute = null)
        {
            try
            {
                if (onBeforeExecute != null)
                {
                    await onBeforeExecute();
                }

                if (Enum.TryParse<ActionType>(el.ActionType, out var actionType))
                {
                    switch (actionType)
                    {
                        case ActionType.Hotkey:
                            ExecuteHotkey(el);
                            break;
                        case ActionType.Web:
                            await ExecuteWebActionAsync(el);
                            break;
                        case ActionType.Program:
                        case ActionType.File:
                        case ActionType.Folder:
                            Process.Start(new ProcessStartInfo(el.ActionValue) { UseShellExecute = true });
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

        private void ExecuteHotkey(CustomElement el)
        {
            var downKeys = new List<byte>();
            if (el.Ctrl) downKeys.Add(NativeMethods.VK_CONTROL);
            if (el.Shift) downKeys.Add(NativeMethods.VK_SHIFT);
            if (el.Alt) downKeys.Add(NativeMethods.VK_MENU);
            if (el.Win) downKeys.Add(NativeMethods.VK_LWIN);

            byte mainVk = 0;
            if (Enum.TryParse(typeof(Key), el.Key, out var k))
                mainVk = (byte)KeyInterop.VirtualKeyFromKey((Key)k!);

            var inputs = new List<NativeMethods.INPUT>();
            foreach (var vk in downKeys)
                inputs.Add(new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = vk } } });

            if (mainVk != 0)
            {
                inputs.Add(new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = mainVk } } });
                inputs.Add(new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = mainVk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } } });
            }

            foreach (var vk in Enumerable.Reverse(downKeys))
                inputs.Add(new NativeMethods.INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } } });

            _ = NativeMethods.SendInput((uint)inputs.Count, [.. inputs], Marshal.SizeOf<NativeMethods.INPUT>());
        }

        private async Task ExecuteWebActionAsync(CustomElement el)
        {
            string prof = el.UseRotation ? AdvanceRotationProfile(el) : el.ChromeProfile;
            el.LastUsedProfile = prof;
            await _settingsService.SaveAsync();

            var psi = BuildWebActionProcessStartInfo(el, prof);
            using var proc = Process.Start(psi);
            if (proc != null && el.OpenFullscreen)
            {
                await TryEnterFullscreenAsync(proc);
            }
        }

        private static string AdvanceRotationProfile(CustomElement el)
        {
            var profiles = BrowserHelper.GetProfiles(el.Browser);
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
            var confirm = new DarkDialog(LocalizationService.Format("Action_ConfirmCommand", command), isConfirm: true) { Owner = System.Windows.Application.Current.MainWindow };
            if (confirm.ShowDialog() == true)
            {
                Process.Start(new ProcessStartInfo("cmd.exe")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = $"/c {command}"
                });
            }
        }

        public async Task StartSearchAsync(string text, Func<Task>? onBeforeExecute = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            
            if (onBeforeExecute != null) await onBeforeExecute();
            
            using var proc = Process.Start(new ProcessStartInfo(BrowserHelper.GetExecutablePath(BrowserType.Chrome))
            {
                UseShellExecute = false,
                ArgumentList = { $"https://www.google.com/search?q={Uri.EscapeDataString(text)}" }
            }) ?? throw new InvalidOperationException("Search failed");
        }

        public async Task StartScreenshotAsync(Func<Task>? onBeforeExecute = null)
        {
            if (onBeforeExecute != null) await onBeforeExecute();
            Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
        }

        public async Task StartRecordVideoAsync(Func<Task>? onBeforeExecute = null)
        {
            if (onBeforeExecute != null) await onBeforeExecute();
            Process.Start(new ProcessStartInfo("ms-screenclip:?type=recording") { UseShellExecute = true });
        }

        public async Task StartCalculatorAsync(Func<Task>? onBeforeExecute = null)
        {
            if (onBeforeExecute != null) await onBeforeExecute();
            Process.Start("calc.exe");
        }

        public async Task StartExplorerAsync(Func<Task>? onBeforeExecute = null)
        {
            if (onBeforeExecute != null) await onBeforeExecute();
            Process.Start(BuildShellLaunchProcessStartInfo("explorer.exe"));
        }

        public async Task StartDownloadsAsync(Func<Task>? onBeforeExecute = null)
        {
            if (onBeforeExecute != null) await onBeforeExecute();
            Process.Start(BuildShellLaunchProcessStartInfo("shell:Downloads"));
        }

        public async Task StartColorPickerAsync(Func<Task>? onBeforeExecute = null)
        {
            if (onBeforeExecute != null) await onBeforeExecute();
            await Task.Delay(120);
            new ScreenColorPickerWindow { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();
        }

        public async Task StartQuickNoteAsync(Func<Task>? onBeforeExecute = null)
        {
            if (onBeforeExecute != null)
            {
                await onBeforeExecute();
            }

            if (_quickNoteWindow is { IsVisible: true })
            {
                _quickNoteWindow.Activate();
                return;
            }

            _quickNoteWindow = new QuickNoteWindow(new QuickNoteService(), _settingsService)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            _quickNoteWindow.Closed += (_, _) => _quickNoteWindow = null;
            _quickNoteWindow.ShowSliding(_settingsService.Settings);
        }

        internal static ProcessStartInfo BuildShellLaunchProcessStartInfo(string target) => new(target)
        {
            UseShellExecute = true
        };

        private static async Task StartScriptFileAsync(string scriptPath)
        {
            var confirm = new DarkDialog(LocalizationService.Format("Action_ConfirmScript", scriptPath), isConfirm: true) { Owner = System.Windows.Application.Current.MainWindow };
            if (confirm.ShowDialog() != true)
            {
                return;
            }

            var psi = CreateScriptProcessStartInfo(scriptPath);
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException(LocalizationService.Get("Action_LaunchFailed"));
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
                    var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, WorkingDirectory = workingDirectory };
                    psi.ArgumentList.Add("/c"); psi.ArgumentList.Add(scriptPath); return psi;
                case ".ps1":
                    string shell = FindExecutableOnPath("pwsh.exe"); if (!File.Exists(shell)) shell = FindExecutableOnPath("powershell.exe");
                    var psiPs = new ProcessStartInfo(shell) { UseShellExecute = false, WorkingDirectory = workingDirectory };
                    psiPs.ArgumentList.Add("-NoProfile"); if (Path.GetFileName(shell).Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        psiPs.ArgumentList.Add("-ExecutionPolicy"); psiPs.ArgumentList.Add("Bypass");
                    }
                    psiPs.ArgumentList.Add("-File"); psiPs.ArgumentList.Add(scriptPath); return psiPs;
                case ".py":
                    string pythonExe = FindExecutableOnPath("python.exe"); if (!File.Exists(pythonExe)) throw new InvalidOperationException(LocalizationService.Get("Action_PythonNotFound"));
                    var psiPy = new ProcessStartInfo(pythonExe) { UseShellExecute = false, WorkingDirectory = workingDirectory };
                    psiPy.ArgumentList.Add(scriptPath);
                    return psiPy;
                default: throw new InvalidOperationException(LocalizationService.Get("Action_UnsupportedScript"));
            }
        }

        private static string FindExecutableOnPath(string fileName)
        {
            var pathValue = Environment.GetEnvironmentVariable("PATH"); if (string.IsNullOrWhiteSpace(pathValue)) return fileName;
            foreach (var dir in pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try { var candidate = Path.Combine(dir.Trim(), fileName); if (File.Exists(candidate)) return candidate; }
                catch (Exception ex) { Logger.Log(ex); }
            }
            return fileName;
        }

        private static async Task TryEnterFullscreenAsync(Process proc)
        {
            for (int i = 0; i < FullscreenActivationAttempts; i++)
            {
                await Task.Delay(FullscreenWindowPollDelayMs);
                proc.Refresh();
                if (proc.MainWindowHandle == IntPtr.Zero) continue;

                NativeMethods.SetForegroundWindow(proc.MainWindowHandle);
                await Task.Delay(FullscreenForegroundDelayMs);
                SendVirtualKey((byte)KeyInterop.VirtualKeyFromKey(Key.F11));
                break;
            }
        }

        private static void SendVirtualKey(byte virtualKey)
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

            _ = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        }
    }
}
