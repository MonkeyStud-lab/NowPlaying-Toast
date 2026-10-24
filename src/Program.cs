using System.Diagnostics;

namespace NowPlayingToast;

internal static class Program
{
    internal const string MutexName = @"Local\NowPlayingToast.SingleInstance";
    internal const string FlashEventName = @"Local\NowPlayingToast.FlashTray";
    internal const string ExitEventName = @"Local\NowPlayingToast.Exit";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0].Equals("--exit", StringComparison.OrdinalIgnoreCase))
            return StopExistingInstance();
        if (args.Length != 0) return 1;

        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            TrySignal(FlashEventName);
            return 0;
        }

        try
        {
            using var flashEvent = new EventWaitHandle(false, EventResetMode.AutoReset, FlashEventName);
            using var exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
            ApplicationConfiguration.Initialize();
            using var app = new ToastApp(AppSettings.Load(), flashEvent, exitEvent);
            Application.Run(app);
            return 0;
        }
        finally { mutex.ReleaseMutex(); }
    }

    private static bool TrySignal(string eventName)
    {
        try
        {
            using var signal = EventWaitHandle.OpenExisting(eventName);
            return signal.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static int StopExistingInstance()
    {
        try
        {
            using var mutex = Mutex.OpenExisting(MutexName);
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                // Allow the first process time to finish creating its event handles.
                TrySignal(ExitEventName);
                try
                {
                    if (!mutex.WaitOne(100)) continue;
                }
                catch (AbandonedMutexException) { }
                mutex.ReleaseMutex();
                return 0;
            }
            return 1;
        }
        catch (WaitHandleCannotBeOpenedException) { return 0; }
        catch (UnauthorizedAccessException) { return 1; }
    }
}
