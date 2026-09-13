using System.Threading;
using System.Diagnostics;
using Windows.Media.Control;

namespace NowPlayingToast;

internal sealed class ToastApp : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ToastForm _form;
    private readonly SynchronizationContext _ui;
    private readonly System.Windows.Forms.Timer _musicWatch;
    private readonly Icon _appIcon;
    private AppSettings _settings;
    private GlobalSystemMediaTransportControlsSessionManager? _mgr;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string _lastKey = "";
    private bool _primed;
    private bool _musicActive;
    private int _busy;
    private string? _updateNote;
    private bool _updateAvailable;
    private readonly EventWaitHandle? _flashEvent;
    private readonly System.Windows.Forms.Timer _flashWatch;

    public ToastApp(AppSettings settings, EventWaitHandle? flashEvent = null)
    {
        _settings = settings;
        _flashEvent = flashEvent;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _appIcon = AppIcon.Create();
        _form = new ToastForm(_settings);
        _tray = new NotifyIcon
        {
            Visible = true,
            Text = "Now Playing Toast - waiting for Apple Music",
            Icon = _appIcon,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Items.Add("Show current track", null, (_, _) => _ = ShowNowAsync());
        _tray.ContextMenuStrip.Items.Add("Settings...", null, (_, _) => OpenSettings());
        _tray.ContextMenuStrip.Items.Add("About...", null, (_, _) => OpenAbout());
        _tray.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => ExitApp());

        _musicWatch = new System.Windows.Forms.Timer { Interval = 2000 };
        _musicWatch.Tick += (_, _) => OnMusicWatchTick();
        _musicWatch.Start();
        OnMusicWatchTick();

        _flashWatch = new System.Windows.Forms.Timer { Interval = 400 };
        _flashWatch.Tick += (_, _) =>
        {
            if (_flashEvent is not null && _flashEvent.WaitOne(0))
                FlashTray();
        };
        _flashWatch.Start();

        if (_settings.CheckForUpdatesOnStartup)
            _ = RunUpdateCheckAsync();
    }

    public void FlashTray()
    {
        try
        {
            _tray.ShowBalloonTip(1500, "Now Playing Toast", "Already running.", ToolTipIcon.Info);
        }
        catch { }
    }

    private async Task RunUpdateCheckAsync()
    {
        var result = await UpdateCheck.CheckAsync();
        _updateNote = result.Message;
        _updateAvailable = result.UpdateAvailable;
        if (result.UpdateAvailable)
        {
            Post(() =>
            {
                try
                {
                    _tray.ShowBalloonTip(
                        5000,
                        "Update available",
                        result.Message + "\n" + (result.ReleaseUrl ?? UpdateCheck.GitHubUrl),
                        ToolTipIcon.Info);
                }
                catch { }
            });
        }
    }

    private void OpenSettings()
    {
        using var dlg = new SettingsForm(_settings);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _settings = AppSettings.Load();
            _form.ApplySettings(_settings);
        }
    }

    private void OpenAbout()
    {
        using var dlg = new AboutForm(_updateNote);
        dlg.ShowDialog();
    }

    private static bool IsAppleMusicRunning()
        => Process.GetProcessesByName("AppleMusic").Length > 0;

    private void OnMusicWatchTick()
    {
        var running = IsAppleMusicRunning();
        if (running && !_musicActive)
        {
            _musicActive = true;
            _tray.Text = "Now Playing Toast";
            _ = ActivateForMusicAsync();
        }
        else if (!running && _musicActive)
        {
            _musicActive = false;
            DeactivateFromMusic();
            _tray.Text = "Now Playing Toast - waiting for Apple Music";
        }
    }

    private async Task ActivateForMusicAsync()
    {
        try
        {
            _lastKey = "";
            _primed = false;
            _mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _mgr.CurrentSessionChanged += OnCurrentSessionChanged;
            _mgr.SessionsChanged += OnSessionsChanged;
            await OnSessionChangedAsync();
            await Task.Delay(800);
            if (_musicActive && _settings.ShowOnAppleMusicActivate)
                await ShowNowAsync();
        }
        catch { }
    }

    private void DeactivateFromMusic()
    {
        try
        {
            if (_mgr is not null)
            {
                _mgr.CurrentSessionChanged -= OnCurrentSessionChanged;
                _mgr.SessionsChanged -= OnSessionsChanged;
            }
            DetachSession();
            _mgr = null;
            _lastKey = "";
            _primed = false;
            _form.HideImmediate();
        }
        catch { }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        => Post(() => _ = OnSessionChangedAsync());

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        => Post(() => _ = OnSessionChangedAsync());

    private void Post(Action a) => _ui.Post(_ => a(), null);

    private void DetachSession()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session = null;
        }
    }

    private async Task OnSessionChangedAsync()
    {
        DetachSession();
        if (_mgr is null || !_musicActive) return;

        // Apple Music only - ignore other SMTC sessions even while AppleMusic.exe is running.
        _session = NowPlaying.FindAppleMusicSession(_mgr);
        if (_session is null) return;

        _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        await HandlePossibleTrackChangeAsync();
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        => Post(() => _ = HandlePossibleTrackChangeAsync());

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        => Post(() => _ = HandlePossibleTrackChangeAsync());

    private async Task HandlePossibleTrackChangeAsync()
    {
        if (!_musicActive) return;
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            var meta = await NowPlaying.GetAsync(_mgr, includeArt: false);
            if (meta is null)
            {
                _primed = true;
                return;
            }

            // Extra guard: never toast non-Apple Music sessions.
            if (!NowPlaying.IsAppleMusicSession(meta.SourceAppUserModelId))
                return;

            if (!_primed)
            {
                _lastKey = meta.Key;
                _primed = true;
                return;
            }

            if (meta.Key == _lastKey) return;
            _lastKey = meta.Key;
            var full = await NowPlaying.GetAsync(_mgr, includeArt: true) ?? meta;
            _form.ShowTrack(full);
        }
        catch { }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }

    private async Task ShowNowAsync()
    {
        if (!_musicActive)
        {
            _form.ShowMessage("Waiting for Apple Music", "Open Apple Music, then try again.");
            return;
        }

        var np = await NowPlaying.GetAsync(_mgr, includeArt: true);
        if (np is null)
        {
            _form.ShowMessage("Nothing playing", "Start a song in Apple Music, then try again.");
            return;
        }
        _lastKey = np.Key;
        _primed = true;
        _form.ShowTrack(np);
    }

    private void ExitApp()
    {
        _musicWatch.Stop();
        _musicWatch.Dispose();
        _flashWatch.Stop();
        _flashWatch.Dispose();
        DeactivateFromMusic();
        _tray.Visible = false;
        _tray.Dispose();
        _form.Dispose();
        _appIcon.Dispose();
        ExitThread();
    }
}


