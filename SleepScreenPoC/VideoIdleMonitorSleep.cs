using System;
using System.Threading;

namespace SleepScreenPoC;

/// <summary>
/// Temporarily shorten the active  power plan's own display idle timeout (VIDEOIDLE), letting Windows'
/// own kernel-mode idle path blank the display once nothing resets it.
/// </summary>
internal static class VideoIdleMonitorSleep
{
    // Practical floor: VIDEOIDLE is a whole-second DWORD and 0 is reserved by Windows for "Never".
    public const uint ShortTimeoutSeconds = 1;

    // Safety margin (seconds) added on top of the short timeout for the fallback restore, so it
    // comfortably outlasts Windows' own idle-check even allowing for scheduling jank.
    private const uint FallbackRestoreMarginSeconds = 2;

    private static readonly object Lock = new();
    private static Timer? _fallbackRestoreTimer;
    private static Guid? _schemeGuid;
    private static uint? _originalAcSeconds;
    private static uint? _originalDcSeconds;

    static VideoIdleMonitorSleep()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreOnShutdown("process exiting");
        AppDomain.CurrentDomain.UnhandledException += (_, _) => RestoreOnShutdown("unhandled exception");
    }

    public static bool TryGetActiveScheme(out Guid schemeGuid)
    {
        schemeGuid = Guid.Empty;

        if (NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out var schemePtr) != 0 || schemePtr == IntPtr.Zero)
            return false;

        try
        {
            schemeGuid = System.Runtime.InteropServices.Marshal.PtrToStructure<Guid>(schemePtr);
            return true;
        }
        finally
        {
            NativeMethods.LocalFree(schemePtr);
        }
    }

    public static void ReadVideoIdleTimeouts(Guid schemeGuid, out uint acSeconds, out uint dcSeconds)
    {
        var videoSubgroup = NativeMethods.GuidVideoSubgroup;
        var videoTimeout = NativeMethods.GuidVideoPowerdownTimeout;
        NativeMethods.PowerReadACValueIndex(IntPtr.Zero, ref schemeGuid, ref videoSubgroup, ref videoTimeout, out acSeconds);
        NativeMethods.PowerReadDCValueIndex(IntPtr.Zero, ref schemeGuid, ref videoSubgroup, ref videoTimeout, out dcSeconds);
    }

    public static void WriteVideoIdleTimeouts(Guid schemeGuid, uint acSeconds, uint dcSeconds)
    {
        var videoSubgroup = NativeMethods.GuidVideoSubgroup;
        var videoTimeout = NativeMethods.GuidVideoPowerdownTimeout;
        NativeMethods.PowerWriteACValueIndex(IntPtr.Zero, ref schemeGuid, ref videoSubgroup, ref videoTimeout, acSeconds);
        NativeMethods.PowerWriteDCValueIndex(IntPtr.Zero, ref schemeGuid, ref videoSubgroup, ref videoTimeout, dcSeconds);

        // Only force the scheme active if it still is - avoid yanking the user back to a scheme
        // they've since switched away from; the write into its stored profile still takes effect
        // automatically whenever they switch back to it.
        if (TryGetActiveScheme(out var currentlyActive) && currentlyActive == schemeGuid)
        {
            var schemeGuidCopy = schemeGuid;
            if (NativeMethods.PowerSetActiveScheme(IntPtr.Zero, ref schemeGuidCopy) != 0)
                Log.Write("VideoIdleMonitorSleep: PowerSetActiveScheme failed, the shortened display timeout may not take effect immediately");
        }
    }

    /// <summary>Shortens the display timeout so Windows' own idle path blanks the display shortly afterwards.</summary>
    public static void PrepareOff(uint shortTimeoutSeconds)
    {
        if (shortTimeoutSeconds == 0)
            throw new ArgumentOutOfRangeException(nameof(shortTimeoutSeconds), "0 is reserved by Windows for \"Never\".");

        lock (Lock)
        {
            if (_schemeGuid == null || _originalAcSeconds == null || _originalDcSeconds == null)
            {
                if (!TryGetActiveScheme(out var schemeGuid))
                {
                    Log.Write("VideoIdleMonitorSleep: could not determine the active power scheme, aborting");
                    return;
                }

                ReadVideoIdleTimeouts(schemeGuid, out var originalAc, out var originalDc);
                _schemeGuid = schemeGuid;
                _originalAcSeconds = originalAc;
                _originalDcSeconds = originalDc;
            }

            // If this fails, there's no crash-recovery safety net for the timeout we're about to
            // write below, so abort rather than shorten it with nothing to fall back on.
            if (!RecoveryStore.TrySave(_schemeGuid.Value, _originalAcSeconds.Value, _originalDcSeconds.Value, shortTimeoutSeconds))
            {
                Log.Write("VideoIdleMonitorSleep: could not persist the recovery record, aborting");
                return;
            }

            Log.Write($"VideoIdleMonitorSleep: shortening VIDEOIDLE to {shortTimeoutSeconds}s (scheme={_schemeGuid}, original AC={_originalAcSeconds}, DC={_originalDcSeconds})");
            WriteVideoIdleTimeouts(_schemeGuid.Value, shortTimeoutSeconds, shortTimeoutSeconds);

            ScheduleFallbackRestore(shortTimeoutSeconds);
        }
    }

    /// <summary>Called when the OS confirms the display has actually turned off - see DisplayStatusMonitor.</summary>
    public static void NotifyConfirmedOff() => Restore("confirmed display off");

    private static void ScheduleFallbackRestore(uint shortTimeoutSeconds)
    {
        CancelFallbackTimer();

        var dueTime = TimeSpan.FromSeconds(shortTimeoutSeconds + FallbackRestoreMarginSeconds);
        Log.Write($"VideoIdleMonitorSleep: fallback timer armed for {dueTime.TotalSeconds}s from now");
        _fallbackRestoreTimer = new Timer(_ => Restore("fallback timer"), null, dueTime, Timeout.InfiniteTimeSpan);
    }

    private static void CancelFallbackTimer()
    {
        _fallbackRestoreTimer?.Dispose();
        _fallbackRestoreTimer = null;
    }

    private static void Restore(string reason)
    {
        lock (Lock)
        {
            CancelFallbackTimer();

            if (_schemeGuid == null || _originalAcSeconds == null || _originalDcSeconds == null)
            {
                Log.Write($"VideoIdleMonitorSleep: restore requested ({reason}) but nothing is currently shortened - no-op");
                return;
            }

            Log.Write($"VideoIdleMonitorSleep: restoring the display timeout to AC={_originalAcSeconds}, DC={_originalDcSeconds} (scheme={_schemeGuid}, {reason})");
            WriteVideoIdleTimeouts(_schemeGuid.Value, _originalAcSeconds.Value, _originalDcSeconds.Value);
            RecoveryStore.Clear();
            _schemeGuid = null;
            _originalAcSeconds = null;
            _originalDcSeconds = null;
        }
    }

    private static void RestoreOnShutdown(string reason)
    {
        try
        {
            Restore(reason);
        }
        catch (Exception ex)
        {
            Log.Write($"VideoIdleMonitorSleep: failed to restore the display timeout on shutdown ({reason}): {ex.Message}");
        }
    }

    /// <summary>
    /// Checked once, at process startup, before any window exists. Repairs a shortened timeout
    /// left over from a previous run that never got a chance to restore it (crash before the
    /// unhandled-exception hook could run, a hard kill, a BSOD, or a power cut) - the last line of
    /// defence for the "genuinely no chance to run any code at all" gap. Only restores if the
    /// current live timeout still matches the shortened value recorded, so a deliberate change made
    /// via Windows Settings in the meantime should not be overwritten. Attempts to restore into the exact scheme that
    /// was recorded, regardless of whatever scheme happens to be active now.
    /// </summary>
    public static void TryRecoverStuckTimeoutOnStartup()
    {
        try
        {
            if (!RecoveryStore.TryLoad(out var schemeGuid, out var acSeconds, out var dcSeconds, out var shortenedSeconds))
                return;

            ReadVideoIdleTimeouts(schemeGuid, out var currentAc, out var currentDc);

            if (currentAc != shortenedSeconds || currentDc != shortenedSeconds)
            {
                Log.Write("VideoIdleMonitorSleep: found a recovery record but the current display timeout no longer matches the shortened value, leaving it alone (likely changed since)");
                RecoveryStore.Clear();
                return;
            }

            Log.Write($"VideoIdleMonitorSleep: found a shortened display timeout left over from a previous run (unclean shutdown), restoring it to AC={acSeconds}, DC={dcSeconds} (scheme={schemeGuid}) now");
            WriteVideoIdleTimeouts(schemeGuid, acSeconds, dcSeconds);
            RecoveryStore.Clear();
        }
        catch (Exception ex)
        {
            Log.Write($"VideoIdleMonitorSleep: failed to check/recover a stuck display timeout on startup: {ex.Message}");
        }
    }
}
