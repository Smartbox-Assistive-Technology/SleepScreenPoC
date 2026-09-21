using System;
using System.IO;

namespace SleepScreenPoC;

/// <summary>
/// Persists the "known good" display timeout, and the shortened value applied, across process or
/// device restarts - so a shortened timeout left in place by an unclean shutdown (device powered
/// off, or the app killed, before the normal restore path completed) can be repaired automatically
/// next startup, instead of leaving the user stuck with a very short timeout indefinitely.
/// </summary>
internal static class RecoveryStore
{
    private static readonly string RecoveryFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SleepScreenPoC", "display-timeout-recovery.txt");

    public static bool TrySave(Guid schemeGuid, uint acSeconds, uint dcSeconds, uint shortenedSeconds)
    {
        try
        {
            var directory = Path.GetDirectoryName(RecoveryFilePath);
            if (directory != null)
                Directory.CreateDirectory(directory);
            File.WriteAllText(RecoveryFilePath, $"{schemeGuid:D},{acSeconds},{dcSeconds},{shortenedSeconds}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Write($"RecoveryStore: failed to persist recovery record: {ex.Message}");
            return false;
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(RecoveryFilePath))
                File.Delete(RecoveryFilePath);
        }
        catch (Exception ex)
        {
            Log.Write($"RecoveryStore: failed to clear recovery record: {ex.Message}");
        }
    }

    public static bool TryLoad(out Guid schemeGuid, out uint acSeconds, out uint dcSeconds, out uint shortenedSeconds)
    {
        schemeGuid = Guid.Empty;
        acSeconds = 0;
        dcSeconds = 0;
        shortenedSeconds = 0;

        try
        {
            if (!File.Exists(RecoveryFilePath))
                return false;

            var parts = File.ReadAllText(RecoveryFilePath).Split(',');
            return parts.Length == 4
                && Guid.TryParse(parts[0], out schemeGuid)
                && uint.TryParse(parts[1], out acSeconds)
                && uint.TryParse(parts[2], out dcSeconds)
                && uint.TryParse(parts[3], out shortenedSeconds);
        }
        catch (Exception ex)
        {
            Log.Write($"RecoveryStore: failed to read recovery record: {ex.Message}");
            return false;
        }
    }
}
