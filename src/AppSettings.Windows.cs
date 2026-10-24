namespace NowPlayingToast;

public sealed partial class AppSettings
{
    public Screen ResolveScreen()
    {
        var screens = Screen.AllScreens;
        if (MonitorIndex >= 0 && MonitorIndex < screens.Length)
            return screens[MonitorIndex];
        return Screen.PrimaryScreen ?? screens[0];
    }
}
