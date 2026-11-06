using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WorkAgent.Tracking;

public sealed record ActiveAppInfo(string ProcessName, string? WindowTitle);

public static class ForegroundAppTracker
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    public static ActiveAppInfo? GetCurrentForegroundApp(bool captureWindowTitle, string[]? maskedApps)
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero) return null;

        GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0) return null;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var processName = process.ProcessName.ToLowerInvariant();

            // Privacy Filter: Check if process matches sensitive or masked application list
            if (maskedApps != null && maskedApps.Any(m => processName.Contains(m.ToLowerInvariant())))
            {
                return new ActiveAppInfo("[private]", null);
            }

            string? title = null;
            if (captureWindowTitle)
            {
                var sb = new StringBuilder(512);
                if (GetWindowText(handle, sb, 512) > 0)
                {
                    title = sb.ToString().Trim();
                }
            }

            return new ActiveAppInfo(processName, title);
        }
        catch
        {
            // Process may have exited or access denied
            return null;
        }
    }
}
