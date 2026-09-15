using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TrayLeds
{
    static class Program
    {
        static void Main()
        {
            try
            {
                using var app = new TrayApp();
                app.Run();
            }
            catch (Exception ex)
            {
                _ = NativeMethods.MessageBoxW(
                    IntPtr.Zero,
                    ex.Message,
                    "TrayLeds",
                    NativeMethods.MB_OK | NativeMethods.MB_ICONERROR
                );
            }
        }
    }
}
