# Sleep Screen PoC

Minimal, standalone Avalonia app comparing two approaches to blanking the display.

## What it demonstrates

- **Legacy (unsupported)**: `BroadcastSystemMessage(WM_SYSCOMMAND, SC_MONITORPOWER)` - the original mechanism, confirmed by Microsoft to be unsupported for this purpose.
- **Supported**: temporarily shortens the active power plan's own "turn off display" (`VIDEOIDLE`) timeout via `powrprof.dll`, then lets Windows' own kernel-mode idle path blank the display. No `SetThreadExecutionState` call is made as part of this flow.

The majority of the code here is defensive code to ensure that the original power plan  is not left stuck with a short timeout. We restore the original timeout in several different ways:

1. As soon as Windows confirms the display is off (`GUID_SESSION_DISPLAY_STATUS`).
2. A fallback timer, in case that notification is missed or continued input keeps resetting Windows' own idle timer. 
3. Immediately on process exit or an unhandled exception ("Simulate crash" button).
4. On the next startup, if a leftover shortened timeout was persisted before a hard
   kill/BSOD/power-cut gave the app no chance to run any code ("Simulate hard kill" button, then relaunch the app).

## Running it

```
dotnet run --project SleepScreenPoC
```

The on-screen log is also mirrored to `%LocalAppData%\SleepScreenPoC\log.txt` (path shown at the bottom of the window) so it can be copied out directly.
