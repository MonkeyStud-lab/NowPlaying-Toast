using System.ComponentModel;
using Windows.Media.Control;

namespace NowPlayingToast;

internal sealed class ToastApp : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ToastForm _form;
    private readonly SynchronizationContext _ui;
    private readonly Icon _appIcon;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly System.Windows.Forms.Timer _managerRetry;
    private readonly TrackRefreshQueue _refreshQueue;
    private readonly TrackNotificationState _trackState = new();
    private readonly RegisteredWaitHandle _flashWait;
    private readonly RegisteredWaitHandle _exitWait;
    private AppSettings _settings;
    private GlobalSystemMediaTransportControlsSessionManager? _mgr;
    private GlobalSystemMediaTransportControlsSession? _session;
    private CancellationTokenSource? _artworkRequest;
    private long _sessionGeneration;
    private bool _initializing;
    private volatile bool _exiting;
    private string? _updateNote;
    private string? _balloonUrl;

    public ToastApp(AppSettings settings, EventWaitHandle flashEvent, EventWaitHandle exitEvent)
    {
        _settings = settings;
        _appIcon = AppIcon.Create();
        _form = new ToastForm(_settings);
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _refreshQueue = new TrackRefreshQueue(RefreshTrackAsync, ex => AppLog.Write("Read current track", ex));
        _tray = new NotifyIcon
        {
            Visible = true,
            Text = "Now Playing Toast - waiting for Apple Music",
            Icon = _appIcon,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Items.Add("Show current track", null, (_, _) => _ = _refreshQueue.RequestAsync(forceShow: true));
        _tray.ContextMenuStrip.Items.Add("Settings...", null, (_, _) => OpenSettings());
        _tray.ContextMenuStrip.Items.Add("About...", null, (_, _) => OpenAbout());
        _tray.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => ExitThread());
        _tray.BalloonTipClicked += (_, _) => OpenBalloonLink();

        // These waits sleep until signaled; idle operation needs no process scanning.
        _flashWait = ThreadPool.RegisterWaitForSingleObject(flashEvent,
            (_, _) => Post(FlashTray), null, Timeout.Infinite, executeOnlyOnce: false);
        _exitWait = ThreadPool.RegisterWaitForSingleObject(exitEvent,
            (_, _) => Post(ExitThread), null, Timeout.Infinite, executeOnlyOnce: true);

        // Retry only if Windows' media-session service is temporarily unavailable.
        _managerRetry = new System.Windows.Forms.Timer { Interval = 5000 };
        _managerRetry.Tick += (_, _) => _ = InitializeMediaAsync();
        Post(() => _ = InitializeMediaAsync());
        if (_settings.CheckForUpdatesOnStartup)
            Post(() => _ = RunUpdateCheckAsync());
    }

    private async Task InitializeMediaAsync()
    {
        if (_initializing || _mgr is not null || _exiting) return;
        _initializing = true;
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync()
                .AsTask(_lifetime.Token).WaitAsync(TimeSpan.FromSeconds(10), _lifetime.Token);
            if (_exiting) return;
            try
            {
                manager.CurrentSessionChanged += OnCurrentSessionChanged;
                manager.SessionsChanged += OnSessionsChanged;
            }
            catch
            {
                // A partial subscription must not leave initialization permanently marked ready.
                try { manager.CurrentSessionChanged -= OnCurrentSessionChanged; } catch { }
                try { manager.SessionsChanged -= OnSessionsChanged; } catch { }
                throw;
            }
            _mgr = manager;
            _managerRetry.Stop();
            UpdateSession();
        }
        catch (OperationCanceledException) when (_exiting) { }
        catch (Exception ex)
        {
            AppLog.Write("Connect to Windows media sessions", ex);
            if (!_exiting) _managerRetry.Start();
        }
        finally { _initializing = false; }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        => Post(() => { if (Equals(sender, _mgr)) UpdateSession(); });

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        => Post(() => { if (Equals(sender, _mgr)) UpdateSession(); });

    private void UpdateSession()
    {
        if (_mgr is null || _exiting) return;
        try
        {
            var next = NowPlaying.FindAppleMusicSession(_mgr);
            if (Equals(next, _session))
            {
                if (next is not null) _ = _refreshQueue.RequestAsync();
                return;
            }
            DetachSession();
            _session = next;
            if (_session is null)
            {
                _tray.Text = "Now Playing Toast - waiting for Apple Music";
                _form.HideImmediate();
                return;
            }
            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _tray.Text = "Now Playing Toast";
            _ = _refreshQueue.RequestAsync();
        }
        catch (Exception ex) { AppLog.Write("Select Apple Music session", ex); }
    }

    private void DetachSession()
    {
        ++_sessionGeneration;
        CancelArtwork();
        if (_session is not null)
        {
            try
            {
                _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }
            catch (Exception ex) { AppLog.Write("Disconnect media session", ex); }
            _session = null;
        }
        _trackState.Reset();
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        => Post(() => { if (Equals(sender, _session)) _ = _refreshQueue.RequestAsync(); });

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        => Post(() => { if (Equals(sender, _session)) _ = _refreshQueue.RequestAsync(); });

    private async Task RefreshTrackAsync(bool forceShow, CancellationToken cancellationToken)
    {
        if (_exiting) return;
        var session = _session;
        var generation = _sessionGeneration;
        if (session is null)
        {
            if (forceShow) _form.ShowMessage("Waiting for Apple Music", "Open Apple Music, then try again.");
            return;
        }

        try
        {
            using var readDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readDeadline.CancelAfter(TimeSpan.FromSeconds(5));
            // Bound even a Windows provider that ignores cancellation; queued changes can then run.
            var info = await NowPlaying.ReadAsync(session, readDeadline.Token).WaitAsync(readDeadline.Token);
            if (_exiting || generation != _sessionGeneration || !Equals(session, _session)) return;
            if (info is null)
            {
                _trackState.Observe(null, _settings.ShowOnAppleMusicActivate);
                if (forceShow) _form.ShowMessage("Nothing playing", "Start a song in Apple Music, then try again.");
                return;
            }
            if (!NowPlaying.IsAppleMusicSession(info.SourceAppUserModelId)) return;
            if (!_trackState.Observe(info.Key, _settings.ShowOnAppleMusicActivate, forceShow)) return;

            CancelArtwork();
            // Do not delay the title or subsequent track events while artwork is loading.
            try { _form.ShowTrack(info); }
            finally { info.Art?.Dispose(); }
            _artworkRequest = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _ = LoadArtworkAsync(info, generation, _artworkRequest);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppLog.Write("Read Apple Music metadata", ex);
            if (forceShow && !_exiting && generation == _sessionGeneration)
                _form.ShowMessage("Track unavailable", "Apple Music is updating. Please try again.");
        }
    }

    private async Task LoadArtworkAsync(NowPlayingInfo info, long generation, CancellationTokenSource request)
    {
        try
        {
            using var artwork = await NowPlaying.GetArtworkAsync(info, request.Token);
            if (!_exiting && !request.IsCancellationRequested &&
                ReferenceEquals(request, _artworkRequest) && generation == _sessionGeneration)
                _form.UpdateArtwork(info.Key, artwork);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppLog.Write("Load album artwork", ex); }
        finally
        {
            if (ReferenceEquals(request, _artworkRequest)) _artworkRequest = null;
            request.Dispose();
        }
    }

    private void CancelArtwork()
    {
        var request = _artworkRequest;
        _artworkRequest = null;
        request?.Cancel(); // Its async continuation owns disposal.
    }

    private void Post(Action action)
    {
        if (_exiting) return;
        try
        {
            _ui.Post(_ =>
            {
                if (_exiting) return;
                try { action(); }
                catch (Exception ex) { AppLog.Write("UI callback", ex); }
            }, null);
        }
        catch (InvalidAsynchronousStateException) when (_exiting) { }
        catch (ObjectDisposedException) when (_exiting) { }
    }

    private void FlashTray()
    {
        _balloonUrl = null;
        _tray.ShowBalloonTip(1500, "Now Playing Toast", "Already running.", ToolTipIcon.Info);
    }

    private async Task RunUpdateCheckAsync()
    {
        var result = await UpdateCheck.CheckAsync(_lifetime.Token);
        if (_exiting) return;
        _updateNote = result.Message;
        if (result.UpdateAvailable)
        {
            _balloonUrl = result.ReleaseUrl ?? UpdateCheck.GitHubUrl + "/releases";
            _tray.ShowBalloonTip(5000, "Update available", result.Message + "\nClick to view the release.", ToolTipIcon.Info);
        }
    }

    private void OpenBalloonLink()
    {
        if (_balloonUrl is null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _balloonUrl,
                UseShellExecute = true
            })?.Dispose();
        }
        catch (Exception ex) { AppLog.Write("Open release page", ex); }
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog() == DialogResult.OK && !_exiting)
        {
            _settings = AppSettings.Load();
            _form.ApplySettings(_settings);
        }
    }

    private void OpenAbout()
    {
        using var dialog = new AboutForm(_updateNote);
        dialog.ShowDialog();
    }

    private void Cleanup()
    {
        if (_exiting) return;
        _exiting = true;
        _flashWait.Unregister(null);
        _exitWait.Unregister(null);
        _managerRetry.Stop();
        _managerRetry.Dispose();
        _refreshQueue.Dispose();
        _lifetime.Cancel();
        DetachSession();
        if (_mgr is not null)
        {
            try
            {
                _mgr.CurrentSessionChanged -= OnCurrentSessionChanged;
                _mgr.SessionsChanged -= OnSessionsChanged;
            }
            catch (Exception ex) { AppLog.Write("Disconnect media manager", ex); }
            _mgr = null;
        }
        _tray.Visible = false;
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose();
        _form.Dispose();
        _appIcon.Dispose();
        _lifetime.Dispose();
    }

    protected override void ExitThreadCore()
    {
        Cleanup();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Cleanup();
        base.Dispose(disposing);
    }
}


