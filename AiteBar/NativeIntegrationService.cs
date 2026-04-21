using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace AiteBar
{
    public class NativeIntegrationService : IDisposable
    {
        private NativeMethods.LowLevelMouseProc? _mouseProc;
        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _windowHandle;

        public event Action<int, int>? MouseDownOutside;
        public event Action<int>? HotkeyPressed;

        public NativeIntegrationService(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public void InstallMouseHook()
        {
            try
            {
                if (_mouseHook != IntPtr.Zero) return;
                _mouseProc = MouseHookCallback;
                using var curProcess = Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule ?? throw new InvalidOperationException("MainModule is null");
                _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, NativeMethods.GetModuleHandle(curModule.ModuleName!), 0);
            }
            catch (Exception ex) 
            { 
                Logger.Log(ex); 
            }
        }

        public void UninstallMouseHook()
        {
            if (_mouseHook == IntPtr.Zero) return;
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN)
                {
                    var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    MouseDownOutside?.Invoke(hookStruct.pt.X, hookStruct.pt.Y);
                }
            }
            catch (Exception ex) 
            { 
                Logger.Log(ex); 
            }
            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        public void RegisterHotkey(int id, uint modifiers, uint vk)
        {
            NativeMethods.RegisterHotKey(_windowHandle, id, modifiers, vk);
        }

        public void UnregisterHotkey(int id)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
        }

        public void Dispose()
        {
            UninstallMouseHook();
        }
    }
}
