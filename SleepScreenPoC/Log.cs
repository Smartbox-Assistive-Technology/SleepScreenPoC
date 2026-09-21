using System;
using System.Collections.ObjectModel;
using System.IO;

namespace SleepScreenPoC;

/// <summary>
/// Timestamped log shared by both sleep implementations, shown live in the UI and mirrored to a
/// file on disk so it can be copied out and shared verbatim.
/// </summary>
internal static class Log
{
    public static readonly ObservableCollection<string> Lines = new();

    public static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SleepScreenPoC", "log.txt");

    static Log()
    {
        var directory = Path.GetDirectoryName(LogFilePath);
        if (directory != null)
            Directory.CreateDirectory(directory);
    }

    public static void Write(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {message}";

        // Guarded: this can run before Avalonia's dispatcher exists yet (the startup recovery
        // check in Program.Main runs before BuildAvaloniaApp), when it's safe to just add directly.
        try
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                Lines.Add(line);
            else
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Lines.Add(line));
        }
        catch
        {
            Lines.Add(line);
        }

        try
        {
            File.AppendAllText(LogFilePath, line + Environment.NewLine);
        }
        catch
        {
            // Best effort only - the in-memory/UI copy is what matters for a demo app.
        }
    }
}
