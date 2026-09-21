using System;

namespace SleepScreenPoC;

/// <summary>
/// The approach Grid used for years: force the display off directly via
/// BroadcastSystemMessage(WM_SYSCOMMAND, SC_MONITORPOWER). Microsoft confirmed this is not an
/// officially supported display power management API - it doesn't reliably coordinate with the
/// WDDM display driver, GPU power states, or Modern Standby, which plausibly explains both
/// flicker-on-wake and full-device freezes seen in the field. 
/// </summary>
internal static class LegacyMonitorSleep
{
    public static void TurnOff()
    {
        Log.Write("LegacyMonitorSleep: BroadcastSystemMessage(WM_SYSCOMMAND, SC_MONITORPOWER, 2) - forcing the display off directly");
        var recipients = NativeMethods.BSM_APPLICATIONS;
        NativeMethods.BroadcastSystemMessage(NativeMethods.BSF_FORCEIFHUNG, ref recipients, NativeMethods.WM_SYSCOMMAND, (IntPtr)NativeMethods.SC_MONITORPOWER, (IntPtr)2);
    }
}
