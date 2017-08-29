using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;

namespace Hellosam.Net.TransparentPaint
{
    /// <summary>
    /// /// EnumDesktopWindows Demo - shows the caption of all desktop windows.
    /// /// Authors: Svetlin Nakov, Martin Kulov 
    /// /// Bulgarian Association of Software Developers - http://www.devbg.org/en/
    /// /// </summary>
    public static class User32
    {
        /// <summary>
        /// filter function
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        public delegate bool EnumDelegate(IntPtr hWnd, int lParam);

        /// <summary>
        /// check if windows visible
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        /// <summary>
        /// return windows text
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="lpWindowText"></param>
        /// <param name="nMaxCount"></param>
        /// <returns></returns>
        [DllImport("user32.dll", EntryPoint = "GetWindowText",
        ExactSpelling = false, CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpWindowText, int nMaxCount);

        /// <summary>
        /// enumarator on all desktop windows
        /// </summary>
        /// <param name="hDesktop"></param>
        /// <param name="lpEnumCallbackFunction"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        [DllImport("user32.dll", EntryPoint = "EnumDesktopWindows",
        ExactSpelling = false, CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDelegate lpEnumCallbackFunction, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(IntPtr hWnd, out POINT lpPoint);
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        public static decimal GetDpiRatio(Window window)
        {
            var dpi = GetDpi(window, DpiType.Effective);
            decimal ratio = 1;
            if (dpi > 96)
                ratio = (decimal)dpi / 96M;

            return ratio;
        }
        public static decimal GetDpiRatio(IntPtr hwnd)
        {
            var dpi = GetDpi(hwnd, DpiType.Effective);
            decimal ratio = 1;
            if (dpi > 96)
                ratio = (decimal)dpi / 96M;

            return ratio;
        }

        public static uint GetDpi(IntPtr hwnd, DpiType dpiType)
        {
            var screen = Screen.FromHandle(hwnd);
            var pnt = new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
            var mon = MonitorFromPoint(pnt, 2 /*MONITOR_DEFAULTTONEAREST*/);

            try
            {
                uint dpiX, dpiY;
                GetDpiForMonitor(mon, dpiType, out dpiX, out dpiY);
                return dpiX;
            }
            catch
            {
                // fallback for Windows 7 and older - not 100% reliable
                Graphics graphics = Graphics.FromHwnd(hwnd);
                float dpiXX = graphics.DpiX;
                return Convert.ToUInt32(dpiXX);
            }
        }
        
        public static uint GetDpi(Window window, DpiType dpiType)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            return GetDpi(hwnd, dpiType);
        }

        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromPoint([In]System.Drawing.Point pt, [In]uint dwFlags);

        [DllImport("Shcore.dll")]
        private static extern IntPtr GetDpiForMonitor([In]IntPtr hmonitor, [In]DpiType dpiType, [Out]out uint dpiX, [Out]out uint dpiY);
        
        public enum DpiType
        {
            Effective = 0,
            Angular = 1,
            Raw = 2,
        }

        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_NOMOVE = 0x0002;
        public static bool SendToBottom(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            return SetWindowPos(hwnd, new IntPtr(1), 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE);
        }
    }
}
