using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Kalendarz1.Zywiec.Kalendarz.Services
{
    /// <summary>
    /// Migający pasek zadań (taskbar flash) dla okien WPF.
    /// Używamy WinAPI FlashWindowEx żeby zwrócić uwagę użytkownika gdy okno nieaktywne.
    /// </summary>
    public static class WindowFlasher
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        // FLASHW_ALL = 0x00000003 (caption + tray)
        // FLASHW_TIMERNOFG = 0x0000000C (do momentu fokusu)
        private const uint FLASHW_ALL = 0x00000003;
        private const uint FLASHW_TIMERNOFG = 0x0000000C;
        private const uint FLASHW_STOP = 0x00000000;

        /// <summary>
        /// Mignij paskiem zadań aż użytkownik nie aktywuje okna.
        /// Wywołuje się tylko jeśli okno NIE jest aktywne (focus).
        /// </summary>
        public static void Flash(Window window, uint count = 5)
        {
            if (window == null) return;
            if (window.IsActive) return; // nie migaj gdy okno na wierzchu

            try
            {
                var helper = new WindowInteropHelper(window);
                if (helper.Handle == IntPtr.Zero) return;

                var fi = new FLASHWINFO
                {
                    cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                    hwnd = helper.Handle,
                    dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                    uCount = count,
                    dwTimeout = 0
                };
                FlashWindowEx(ref fi);
            }
            catch
            {
                // Cichy fail - to tylko UX feature, nie krytyczne
            }
        }

        /// <summary>
        /// Zatrzymaj miganie (np. gdy użytkownik kliknie powiadomienie).
        /// </summary>
        public static void StopFlashing(Window window)
        {
            if (window == null) return;
            try
            {
                var helper = new WindowInteropHelper(window);
                if (helper.Handle == IntPtr.Zero) return;

                var fi = new FLASHWINFO
                {
                    cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                    hwnd = helper.Handle,
                    dwFlags = FLASHW_STOP,
                    uCount = 0,
                    dwTimeout = 0
                };
                FlashWindowEx(ref fi);
            }
            catch { }
        }
    }
}
