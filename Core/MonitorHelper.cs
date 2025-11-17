/*
 * Switcheroo - The incremental-search task switcher for Windows.
 * https://github.com/coezbek/switcheroo
 * Copyright 2009, 2010 James Sulak
 * Copyright 2014 Regin Larsen
 *
 * Switcheroo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Switcheroo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Switcheroo.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Runtime.InteropServices;

namespace Switcheroo.Core
{
    public static class MonitorHelper
    {
        /// <summary>
        /// Gets the DPI scaling factor for a specific monitor
        /// </summary>
        /// <param name="hMonitor">Handle to the monitor</param>
        /// <returns>DPI scale factor (1.0 = 96 DPI / 100% scaling)</returns>
        private static double GetDpiScale(IntPtr hMonitor)
        {
            // Try to get per-monitor DPI (Windows 8.1+)
            try
            {
                uint dpiX, dpiY;
                int result = WinApi.GetDpiForMonitor(hMonitor, WinApi.MonitorDpiType.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                if (result == 0) // S_OK
                {
                    return dpiX / 96.0; // 96 DPI is 100% scaling
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] GetDpiForMonitor failed, falling back to system DPI: {ex.Message}");
                // GetDpiForMonitor not available (pre-Windows 8.1) or failed
                // Fall back to system DPI
            }

            // Fallback: Use system-wide DPI
            IntPtr hdc = WinApi.GetDC(IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                int dpiX = WinApi.GetDeviceCaps(hdc, WinApi.LOGPIXELSX);
                WinApi.ReleaseDC(IntPtr.Zero, hdc);
                return dpiX / 96.0;
            }

            return 1.0; // Default to no scaling
        }

        /// <summary>
        /// Gets the monitor information for the monitor that contains the mouse cursor
        /// </summary>
        /// <returns>Monitor info or null if failed</returns>
        public static MonitorInfo GetMonitorFromCursor()
        {
            ManagedWinapi.Windows.POINT cursorPos;
            if (!WinApi.GetCursorPos(out cursorPos))
            {
                Console.WriteLine("[ERROR] GetCursorPos failed in GetMonitorFromCursor");
                return null;
            }

            // Console.WriteLine($"[DEBUG] Cursor position: ({cursorPos.X}, {cursorPos.Y})");

            IntPtr hMonitor = WinApi.MonitorFromPoint(cursorPos, WinApi.MonitorOptions.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero)
            {
                Console.WriteLine("[ERROR] MonitorFromPoint returned null");
                return null;
            }

            WinApi.MONITORINFO monitorInfo = new WinApi.MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);

            if (!WinApi.GetMonitorInfo(hMonitor, ref monitorInfo))
                return null;

            double dpiScale = GetDpiScale(hMonitor);

            var result = new MonitorInfo
            {
                WorkArea = new MonitorRectangle
                {
                    Left = monitorInfo.rcWork.Left,
                    Top = monitorInfo.rcWork.Top,
                    Right = monitorInfo.rcWork.Right,
                    Bottom = monitorInfo.rcWork.Bottom
                },
                MonitorArea = new MonitorRectangle
                {
                    Left = monitorInfo.rcMonitor.Left,
                    Top = monitorInfo.rcMonitor.Top,
                    Right = monitorInfo.rcMonitor.Right,
                    Bottom = monitorInfo.rcMonitor.Bottom
                },
                IsPrimary = (monitorInfo.dwFlags & 1) != 0,
                DpiScale = dpiScale
            };

            // Console.WriteLine($"[DEBUG] Monitor: WorkArea=({result.WorkArea.Left},{result.WorkArea.Top})-({result.WorkArea.Right},{result.WorkArea.Bottom}), DPI={dpiScale:F2}, IsPrimary={result.IsPrimary}");
            // Console.WriteLine($"[DEBUG] Monitor WPF: Position(Left={result.WpfWorkAreaLeft:F0}, Top={result.WpfWorkAreaTop:F0}) [physical pixels], Size(W={result.WpfWorkAreaWidth:F0}, H={result.WpfWorkAreaHeight:F0}) [DIPs]");

            return result;
        }
    }

    public class MonitorInfo
    {
        public MonitorRectangle WorkArea { get; set; }
        public MonitorRectangle MonitorArea { get; set; }
        public bool IsPrimary { get; set; }
        public double DpiScale { get; set; } = 1.0;

        public int Width => MonitorArea.Right - MonitorArea.Left;
        public int Height => MonitorArea.Bottom - MonitorArea.Top;
        public int WorkAreaWidth => WorkArea.Right - WorkArea.Left;
        public int WorkAreaHeight => WorkArea.Bottom - WorkArea.Top;

        // Scale for WPF:
        public double WpfWorkAreaLeft => WorkArea.Left / DpiScale;
        public double WpfWorkAreaTop => WorkArea.Top / DpiScale;
        public double WpfWorkAreaWidth => WorkAreaWidth / DpiScale;
        public double WpfWorkAreaHeight => WorkAreaHeight / DpiScale;
    }

    public class MonitorRectangle
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }

        public override string ToString()
        {
            return $"({Left}, {Top}) - ({Right}, {Bottom})";
        }
    }
}
