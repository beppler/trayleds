using System.Runtime.InteropServices;

namespace TrayLeds
{
    internal static class NativeMethods
    {
        public enum VirtualKeyStates: int
        {
            VK_CAPITAL = 0x14,
            VK_NUMLOCK = 0x90,
            VK_SCROLL  = 0x91,
        }

        [DllImport("USER32.dll")]
        public static extern short GetKeyState(VirtualKeyStates nVirtKey);
    }
}
