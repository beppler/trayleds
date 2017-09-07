using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TrayLeds
{
    public class AppContext : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon;
        private readonly Timer timer;
        int currentState = -1;

        public AppContext()
        {
            notifyIcon = new NotifyIcon
            {
                Text = $"{Application.ProductName} {Application.ProductVersion}",
                Icon = Properties.Resources.N0C0S0,
                ContextMenu = new ContextMenu(new MenuItem[]
                {
                    new MenuItem("Exit", ExitMenuItem_OnClick)
                })
            };
            timer = new Timer
            {
                Interval = 500
            };
            timer.Tick += Timer_Tick;
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
            UpdateIcon();
            timer.Enabled = true;
        }

        public void Exit()
        {
            timer.Enabled = false;
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

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            e.Cancel = false;
            SystemEvents.SessionEnding -= SystemEvents_SessionEnding;
            Exit();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateIcon();
        }
    }
}
