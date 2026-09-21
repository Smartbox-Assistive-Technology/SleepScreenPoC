using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace SleepScreenPoC;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        LogListBox.ItemsSource = Log.Lines;
        StatusText.Text = $"Log file: {Log.LogFilePath}";

        Opened += (_, _) => DisplayStatusMonitor.Attach(this);

        Log.Write("App started.");
    }

    private void OnLegacyOffClick(object? sender, RoutedEventArgs e) => LegacyMonitorSleep.TurnOff();

    private void OnSupportedOffClick(object? sender, RoutedEventArgs e) => VideoIdleMonitorSleep.PrepareOff(VideoIdleMonitorSleep.ShortTimeoutSeconds);

    private void OnSimulateCrashClick(object? sender, RoutedEventArgs e)
    {
        Log.Write("Simulating a crash: throwing on a background thread to trigger AppDomain.UnhandledException...");
        // Give the log line above a moment to flush to disk before the process potentially exits.
        Dispatcher.UIThread.Post(() => System.Threading.Tasks.Task.Run(() => throw new Exception("Simulated crash for the sleep-screen PoC")));
    }

    private void OnSimulateHardKillClick(object? sender, RoutedEventArgs e)
    {
        Log.Write("Simulating a hard kill: Process.Kill() bypasses AppDomain.ProcessExit entirely - the shortened timeout (if any) is left in place. Relaunch this app to see the startup recovery check repair it.");
        Process.GetCurrentProcess().Kill();
    }
}
