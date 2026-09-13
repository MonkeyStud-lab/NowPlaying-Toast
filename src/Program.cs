using System.Threading;

namespace NowPlayingToast;

internal static class Program
{
    internal const string MutexName = @"Local\NowPlayingToast.SingleInstance";
    internal const string FlashEventName = @"Local\NowPlayingToast.FlashTray";

    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            try
            {
                using var flash = EventWaitHandle.OpenExisting(FlashEventName);
                flash.Set();
            }
            catch { }
            return;
        }

        using var flashEvent = new EventWaitHandle(false, EventResetMode.AutoReset, FlashEventName);
        var settings = AppSettings.Load();
        var app = new ToastApp(settings, flashEvent);
        Application.Run(app);
        GC.KeepAlive(mutex);
    }
}
