using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Munyu.Services
{
    public static class MonitorHelper
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        public static Rect GetActiveMonitorWorkArea(Visual visual)
        {
            GetCursorPos(out POINT cursorPt);
            IntPtr hMonitor = MonitorFromPoint(cursorPt, MONITOR_DEFAULTTONEAREST);

            MONITORINFOEX mi = new MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(mi);

            double scaleX = 1.0;
            double scaleY = 1.0;

            try
            {
                DpiScale dpi = VisualTreeHelper.GetDpi(visual);
                scaleX = dpi.DpiScaleX;
                scaleY = dpi.DpiScaleY;
            }
            catch
            {
                // Fallback scale
            }

            if (GetMonitorInfo(hMonitor, ref mi))
            {
                // Convert physical pixels to WPF logical coordinates
                return new Rect(
                    mi.rcWork.Left / scaleX,
                    mi.rcWork.Top / scaleY,
                    mi.rcWork.Width / scaleX,
                    mi.rcWork.Height / scaleY
                );
            }

            // Fallback to primary screen work area
            return new Rect(
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height
            );
        }
    }
}
