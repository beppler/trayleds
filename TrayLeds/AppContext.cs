using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TrayLeds
{
    public class AppContext : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon;
        private readonly Timer updateTimer;
        private readonly IntPtr hook;
        int currentState = -1;

        public AppContext()
        {
            notifyIcon = new NotifyIcon
            {
                Text = $"{Application.ProductName} {Application.ProductVersion}",
                ContextMenu = new ContextMenu(new MenuItem[]
                {
                    new MenuItem("Exit", ExitMenuItem_OnClick)
                })
            };
            updateTimer = new Timer
            {
                Interval = 100
            };
            updateTimer.Tick += Timer_Tick;
            hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, HookCallback, IntPtr.Zero, 0);
            if (hook == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
            UpdateIcon();
        }

        public void Exit()
        {
            if (hook != IntPtr.Zero)
                NativeMethods.UnhookWindowsHookEx(hook);
            if (updateTimer != null)
                updateTimer.Enabled = false;
            if (notifyIcon != null)
                notifyIcon.Visible = false;
            Application.Exit();
        }

        public void UpdateIcon()
        {
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

        private void ExitMenuItem_OnClick(object sender, EventArgs e)
        {
            Exit();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_KEYUP)
            {
                Keys key = (Keys)Marshal.ReadInt32(lParam);
                if (key == Keys.NumLock || key == Keys.CapsLock || key == Keys.Scroll)
                {
                    updateTimer.Enabled = false;
                    updateTimer.Enabled = true;
                }
            }
            return NativeMethods.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            e.Cancel = false;
            SystemEvents.SessionEnding -= SystemEvents_SessionEnding;
            Exit();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            updateTimer.Enabled = false;
            UpdateIcon();
        }
    }
}
