namespace NowPlayingToast;

internal sealed class TrackNotificationState
{
    private bool _initialized;
    public string? LastKey { get; private set; }

    public bool Observe(string? key, bool showOnActivate, bool forceShow = false)
    {
        if (string.IsNullOrEmpty(key))
        {
            // A session with no song can start later. Preserve an already-known song.
            _initialized = true;
            return false;
        }
        var show = forceShow || (_initialized ? key != LastKey : showOnActivate);
        LastKey = key;
        _initialized = true;
        return show;
    }

    public void Reset()
    {
        _initialized = false;
        LastKey = null;
    }
}
