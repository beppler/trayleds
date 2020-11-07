using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TrayLeds
{
    public class AppContext : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon;
        private readonly Timer timer;
        private readonly IntPtr hook = IntPtr.Zero;
        private readonly NativeMethods.HookProc hookProc;
        private int currentState = -1;

        public AppContext()
        {
            var contextMenu = new ContextMenuStrip();
            var exitItem = contextMenu.Items.Add("Exit");
            exitItem.Click += ExitItem_OnClick;
            notifyIcon = new NotifyIcon
            {
                Text = $"{Application.ProductName} {Application.ProductVersion}",
                ContextMenuStrip = contextMenu
            };
            timer = new Timer
            {
                Interval = 100
            };
            timer.Tick += Timer_Tick;
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
            hookProc = new NativeMethods.HookProc(HookCallback);
            var mainModule = Process.GetCurrentProcess().MainModule;
            hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, hookProc, NativeMethods.GetModuleHandle(mainModule.FileName), 0);
            if (hook == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            Timer_Tick(this, EventArgs.Empty);
        }

        private void ExitItem_OnClick(object sender, EventArgs e)
        {
            if (hook != IntPtr.Zero)
                NativeMethods.UnhookWindowsHookEx(hook);
            if (timer != null)
                timer.Enabled = false;
            if (notifyIcon != null)
                notifyIcon.Visible = false;
            Application.Exit();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt32() == NativeMethods.WM_KEYUP)
            {
                Keys key = (Keys)Marshal.ReadInt32(lParam);
                if (key == Keys.NumLock || key == Keys.CapsLock || key == Keys.Scroll)
                {
                    timer.Enabled = false;
                    timer.Enabled = true;
                }
            }
            return NativeMethods.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            e.Cancel = false;
            SystemEvents.SessionEnding -= SystemEvents_SessionEnding;
            ExitItem_OnClick(this, EventArgs.Empty);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            timer.Enabled = false;
            int state = 0;
            if (Control.IsKeyLocked(Keys.NumLock))
                state |= 4;
            if (Control.IsKeyLocked(Keys.CapsLock))
                state |= 2;
            if (Control.IsKeyLocked(Keys.Scroll))
                state |= 1;
            if (state != currentState)
            {
                switch (state)
                {
                    case 0:
                        notifyIcon.Icon = Properties.Resources.N0C0S0;
                        break;
                    case 1:
                        notifyIcon.Icon = Properties.Resources.N0C0S1;
                        break;
                    case 2:
                        notifyIcon.Icon = Properties.Resources.N0C1S0;
                        break;
                    case 3:
                        notifyIcon.Icon = Properties.Resources.N0C1S1;
                        break;
                    case 4:
                        notifyIcon.Icon = Properties.Resources.N1C0S0;
                        break;
                    case 5:
                        notifyIcon.Icon = Properties.Resources.N1C0S1;
                        break;
                    case 6:
                        notifyIcon.Icon = Properties.Resources.N1C1S0;
                        break;
                    case 7:
                        notifyIcon.Icon = Properties.Resources.N1C1S1;
                        break;
                }
                notifyIcon.Visible = true;
                currentState = state;
            }
        }
    }
}
