using Avalonia;
using System;

namespace SleepScreenPoC;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Runs before any window exists, so this is the only place a leftover shortened timeout
        // from a previous run that never got a chance to restore (crash, hard kill, power loss)
        // can be repaired. See VideoIdleMonitorSleep.TryRecoverStuckTimeoutOnStartup for details.
        VideoIdleMonitorSleep.TryRecoverStuckTimeoutOnStartup();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
