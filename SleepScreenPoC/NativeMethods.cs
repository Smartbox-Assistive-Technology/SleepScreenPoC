using System;
using System.Runtime.InteropServices;

namespace SleepScreenPoC;

internal static class NativeMethods
{
    // --- Legacy (now unsupported?) approach: force the display off/on via WM_SYSCOMMAND ---

    public const int WM_SYSCOMMAND = 0x0112;
    public const int SC_MONITORPOWER = 0xF170;
    public const int BSM_APPLICATIONS = 0x8;
    public const int BSF_FORCEIFHUNG = 0x20;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int BroadcastSystemMessage(int flags, ref int recipients, int hMsg, IntPtr wParam, IntPtr lParam);

    // --- Shorten the active power plan's own VIDEOIDLE timeout ---
    // See https://learn.microsoft.com/en-us/windows/win32/power/power-policy-settings

    public static readonly Guid GuidVideoSubgroup = new("7516b95f-f776-4464-8c53-06167f40cc99");
    public static readonly Guid GuidVideoPowerdownTimeout = new("3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e");

    [DllImport("powrprof.dll")]
    public static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    public static extern uint PowerReadACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupGuid, ref Guid powerSettingGuid, out uint acValueIndex);

    [DllImport("powrprof.dll")]
    public static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupGuid, ref Guid powerSettingGuid, out uint dcValueIndex);

    [DllImport("powrprof.dll")]
    public static extern uint PowerWriteACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupGuid, ref Guid powerSettingGuid, uint acValueIndex);

    [DllImport("powrprof.dll")]
    public static extern uint PowerWriteDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupGuid, ref Guid powerSettingGuid, uint dcValueIndex);

    [DllImport("powrprof.dll")]
    public static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr hMem);

    // --- Confirmed-off detection: GUID_SESSION_DISPLAY_STATUS via RegisterPowerSettingNotification ---

    public static readonly Guid GuidSessionDisplayStatus = new("2B84C20E-AD23-4ddf-93DB-05FFBD7EFCA5");
    public const int WM_POWERBROADCAST = 0x0218;
    public const int PBT_POWERSETTINGCHANGE = 0x8013;
    public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public uint DataLength;
        public byte Data;
    }

    // --- Simulating "the user just did something" (only used by the demo's Postpone button,
    // representing what Grid's own input pipeline does on every recognised input event; the
    // supported sleep flow itself no longer calls this - see VideoIdleMonitorSleep) ---

    private const uint EsDisplayRequired = 0x00000002;
    private const uint EsSystemRequired = 0x00000001;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    public static void SimulateUserInputPostpone() => SetThreadExecutionState(EsDisplayRequired | EsSystemRequired);
}
