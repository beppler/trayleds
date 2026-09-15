using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TrayLeds
{
    public class TrayApp : IDisposable
    {
        private const string WindowClassName = "TrayLedsMessageWindow";
        private static readonly IntPtr TimerId = 1;
        private readonly IntPtr hWindow;
        private readonly IntPtr hookId = IntPtr.Zero;

        private const uint IdmExit = 1;

        private static readonly string[] IconFileNames =
        {
            "N0C0S0.ico", "N0C0S1.ico", "N0C1S0.ico", "N0C1S1.ico",
            "N1C0S0.ico", "N1C0S1.ico", "N1C1S0.ico", "N1C1S1.ico",
        };
        private readonly IntPtr[] icons = new IntPtr[IconFileNames.Length];
        private NativeMethods.NOTIFYICONDATA iconData;

        private bool disposed;
        private bool notifyIconAdded;

        private int currentState = -1;


        public TrayApp()
        {
            using Process process = Process.GetCurrentProcess();
            using ProcessModule mainModule = process.MainModule;
            IntPtr hInstance = NativeMethods.GetModuleHandle(mainModule.ModuleName);

            Assembly assembly = Assembly.GetExecutingAssembly();
            string product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "TrayLeds";
            string version = assembly.GetName().Version?.ToString() ?? string.Empty;
            string trayText = $"{product} {version}";

            for (int i = 0; i < IconFileNames.Length; i++)
            {
                icons[i] = LoadEmbeddedIcon(IconFileNames[i]);
            }

            NativeMethods.WndProc wndProc = WndProcCallback;
            var wndClass = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
                hInstance = hInstance,
                lpszClassName = WindowClassName,
            };
            if (NativeMethods.RegisterClassEx(ref wndClass) == 0)
            {
                throw new Win32Exception();
            }

            hWindow = NativeMethods.CreateWindowEx(
                0, WindowClassName, trayText, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero
            );
            if (hWindow == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            iconData = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = hWindow,
                uID = 1,
                uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                uCallbackMessage = NativeMethods.WM_TRAYICON,
                szTip = trayText,
            };

            NativeMethods.HookProc hookProc = HookCallback;
            hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, hookProc, hInstance, 0);
            if (hookId == IntPtr.Zero)
            {
                throw new Win32Exception();
            }

            UpdateIcon();
        }

        public void Run()
        {
            while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            if (hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(hookId);
            }

            NativeMethods.KillTimer(hWindow, TimerId);

            if (notifyIconAdded)
            {
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref iconData);
            }

            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] != IntPtr.Zero)
                {
                    NativeMethods.DestroyIcon(icons[i]);
                    icons[i] = IntPtr.Zero;
                }
            }

            if (hWindow != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(hWindow);
            }
        }

        private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case NativeMethods.WM_TRAYICON:
                    if ((uint)lParam.ToInt64() == NativeMethods.WM_RBUTTONUP || (uint)lParam.ToInt64() == NativeMethods.WM_CONTEXTMENU)
                        ShowContextMenu();
                    return IntPtr.Zero;

                case NativeMethods.WM_COMMAND:
                    if ((wParam.ToInt64() & 0xFFFF) == IdmExit)
                        Dispose();
                    return IntPtr.Zero;

                case NativeMethods.WM_TIMER:
                    if (wParam == TimerId)
                    {
                        NativeMethods.KillTimer(hWindow, TimerId);
                        UpdateIcon();
                    }
                    return IntPtr.Zero;

                case NativeMethods.WM_QUERYENDSESSION:
                    return (IntPtr)1;

                case NativeMethods.WM_ENDSESSION:
                    if (wParam != IntPtr.Zero)
                        Dispose();
                    return IntPtr.Zero;

                case NativeMethods.WM_DESTROY:
                    NativeMethods.PostQuitMessage(0);
                    return IntPtr.Zero;

                default:
                    return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
            }
        }

        private void ShowContextMenu()
        {
            IntPtr hMenu = NativeMethods.CreatePopupMenu();
            NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, IdmExit, "Exit");
            NativeMethods.GetCursorPos(out var pt);
            NativeMethods.SetForegroundWindow(hWindow);
            NativeMethods.TrackPopupMenuEx(hMenu, NativeMethods.TPM_RIGHTBUTTON, pt.x, pt.y, hWindow, IntPtr.Zero);
            NativeMethods.PostMessage(hWindow, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);
            NativeMethods.DestroyMenu(hMenu);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt32() == NativeMethods.WM_KEYUP)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == NativeMethods.VK_NUMLOCK || vkCode == NativeMethods.VK_CAPITAL || vkCode == NativeMethods.VK_SCROLL)
                {
                    NativeMethods.KillTimer(hWindow, TimerId);
                    NativeMethods.SetTimer(hWindow, TimerId, 100, IntPtr.Zero);
                }
            }
            return NativeMethods.CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private void UpdateIcon()
        {
            int state = 0;
            if ((NativeMethods.GetKeyState(NativeMethods.VK_NUMLOCK) & 1) != 0)
                state |= 4;
            if ((NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 1) != 0)
                state |= 2;
            if ((NativeMethods.GetKeyState(NativeMethods.VK_SCROLL) & 1) != 0)
                state |= 1;

            if (state != currentState)
            {
                SetIconForState(state);
                currentState = state;
            }
        }

        private void SetIconForState(int state)
        {
            if (!notifyIconAdded)
            {
                notifyIconAdded = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref iconData);
            }
            if (notifyIconAdded) {
                iconData.hIcon = icons[state];
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref iconData);
            }
        }

        private static IntPtr LoadEmbeddedIcon(string fileName)
        {
            using var stream = typeof(TrayApp).Assembly.GetManifestResourceStream($"TrayLeds.Resources.{fileName}")
                ?? throw new InvalidOperationException($"Missing embedded icon resource: {fileName}");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            byte[] bytes = ms.ToArray();

            int width = bytes[6];
            int height = bytes[7];
            uint imageSize = BitConverter.ToUInt32(bytes, 14);
            uint imageOffset = BitConverter.ToUInt32(bytes, 18);
            byte[] imageBytes = new byte[imageSize];
            Array.Copy(bytes, imageOffset, imageBytes, 0, imageSize);

            return NativeMethods.CreateIconFromResourceEx(imageBytes, imageSize, true, 0x00030000, width, height, 0);
        }
    }
}
