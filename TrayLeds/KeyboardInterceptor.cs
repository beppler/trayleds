using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayLeds
{
    public class KeyboardInterceptor
    {
        private IntPtr hook;

        public void Start()
        {
            if (hook == IntPtr.Zero)
            {
                hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, Callback, IntPtr.Zero, 0);
                if (hook == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void Stop()
        {
            if (hook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            }
        }

        public event EventHandler<KeyCodeEventArgs> KeyDown;
        public event EventHandler<KeyCodeEventArgs> KeyUp;

        private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                if (message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN)
                {
                    Keys keyCode = (Keys)Marshal.ReadInt32(lParam);
                    KeyDown?.Invoke(this, new KeyCodeEventArgs(keyCode));
                }
                else if (message == NativeMethods.WM_KEYUP || message == NativeMethods.WM_SYSKEYUP)
                {
                    Keys keyCode = (Keys)Marshal.ReadInt32(lParam);
                    KeyUp?.Invoke(this, new KeyCodeEventArgs(keyCode));
                }
            }
            IntPtr result = NativeMethods.CallNextHookEx(hook, nCode, wParam, lParam);
            return result;
        }

        private static class NativeMethods
        {
            public const int WH_KEYBOARD_LL = 13;
            public const int WM_KEYDOWN     = 0x100;
            public const int WM_SYSKEYDOWN  = 0x104;
            public const int WM_KEYUP       = 0x101;
            public const int WM_SYSKEYUP    = 0x105;

            public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hInstance, uint threadId);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool UnhookWindowsHookEx(IntPtr hInstance);

            [DllImport("user32.dll")]
            public static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);
        }
    }

    public class KeyCodeEventArgs : EventArgs
    {
        public KeyCodeEventArgs(Keys keyCode)
        {
            KeyCode = keyCode;
        }

        public Keys KeyCode { get; }
    }
}
