using System;
using System.Runtime.InteropServices;

namespace WorkAgent.Tracking;

/// <summary>
/// Detects system-wide user inactivity using Win32 GetLastInputInfo.
/// Safe, non-invasive, and never intercepts keystrokes or clipboard data.
/// </summary>
public static class IdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static TimeSpan GetIdleTime()
    {
        var lastInputInfo = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
        };

        if (!GetLastInputInfo(ref lastInputInfo))
        {
            return TimeSpan.Zero;
        }

        // Environment.TickCount handles uint wrap-around after ~49.7 days of uptime
        uint currentTick = (uint)Environment.TickCount;
        uint idleMilliseconds = currentTick - lastInputInfo.dwTime;

        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }
}
