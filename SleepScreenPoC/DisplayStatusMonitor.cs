using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace SleepScreenPoC;

/// <summary>
/// Watches the OS's own primary-display power notification (GUID_SESSION_DISPLAY_STATUS via
/// WM_POWERBROADCAST/PBT_POWERSETTINGCHANGE). This tells us definitively when the display actually
/// turns off/on, regardless of what caused it (our shortened timeout, continued input defeating
/// it, the user's power button, etc.) - no polling or guessed delay required. Matches what Grid's
/// own PowerSettingAwareBehavior listens to in production.
/// </summary>
internal static class DisplayStatusMonitor
{
    private static IntPtr _registrationHandle;
    private static Win32Properties.CustomWndProcHookCallback? _hook;
    private static TopLevel? _topLevel;

    public static void Attach(TopLevel topLevel)
    {
        if (_topLevel != null)
            return;

        var hwnd = topLevel.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
        {
            Log.Write("DisplayStatusMonitor: no native window handle available, skipping registration");
            return;
        }

        _topLevel = topLevel;
        _hook = OnWndProc;
        Win32Properties.AddWndProcHookCallback(topLevel, _hook);

        var guid = NativeMethods.GuidSessionDisplayStatus;
        _registrationHandle = NativeMethods.RegisterPowerSettingNotification(hwnd, ref guid, NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);
        Log.Write(_registrationHandle != IntPtr.Zero
            ? "DisplayStatusMonitor: registered for GUID_SESSION_DISPLAY_STATUS notifications"
            : "DisplayStatusMonitor: RegisterPowerSettingNotification failed");
    }

    private static IntPtr OnWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_POWERBROADCAST && (long)wParam == NativeMethods.PBT_POWERSETTINGCHANGE && lParam != IntPtr.Zero)
        {
            var setting = Marshal.PtrToStructure<NativeMethods.POWERBROADCAST_SETTING>(lParam);
            if (setting.PowerSetting == NativeMethods.GuidSessionDisplayStatus)
            {
                // Data: 0 = Off, 1 = On, 2 = Dimmed.
                var isOff = setting.Data == 0;
                Log.Write($"DisplayStatusMonitor: GUID_SESSION_DISPLAY_STATUS changed, Data={setting.Data} ({(isOff ? "Off" : "On/Dimmed")})");
                if (isOff)
                    VideoIdleMonitorSleep.NotifyConfirmedOff();
            }
        }

        return IntPtr.Zero;
    }
}
