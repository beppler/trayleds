using System;
using System.Runtime.InteropServices;

namespace TrayLeds
{
    internal class NativeMethods
    {
        public const int WH_KEYBOARD_LL = 13;

        public const int WM_KEYDOWN = 0x100;
        public const int WM_SYSKEYDOWN = 0x104;
        public const int WM_KEYUP = 0x101;
        public const int WM_SYSKEYUP = 0x105;

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr hInstance, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string moduleName);
    }
}
