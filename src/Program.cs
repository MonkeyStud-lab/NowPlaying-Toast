using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Windows.Media.Control;
using Windows.Storage.Streams;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ToastApp());
    }
}

internal sealed class ToastApp : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ToastForm _form;
    private readonly SynchronizationContext _ui;
    private GlobalSystemMediaTransportControlsSessionManager? _mgr;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string _lastKey = "";
    private bool _primed;
    private int _busy; // 0/1 reentrancy guard

    public ToastApp()
    {
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _form = new ToastForm();
        _tray = new NotifyIcon
        {
            Visible = true,
            Text = "Now Playing Toast",
            Icon = SystemIcons.Application,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Items.Add("Show current track", null, (_, _) => _ = ShowNowAsync());
        _tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => ExitApp());

        _ = InitMediaAsync();
    }

    private async Task InitMediaAsync()
    {
        try
        {
            _mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _mgr.CurrentSessionChanged += (_, _) => Post(() => _ = OnSessionChangedAsync());
            await OnSessionChangedAsync();

            // One boot toast after a beat
            await Task.Delay(800);
            await ShowNowAsync();
        }
        catch { }
    }

    private void Post(Action a) => _ui.Post(_ => a(), null);

    private async Task OnSessionChangedAsync()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session = null;
        }

        if (_mgr is null) return;
        _session = _mgr.GetCurrentSession();
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
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            var meta = await NowPlaying.GetAsync(includeArt: false);
            if (meta is null)
            {
                _primed = true;
                return;
            }

            if (!_primed)
            {
                _lastKey = meta.Key;
                _primed = true;
                return;
            }

            if (meta.Key == _lastKey) return;
            _lastKey = meta.Key;
            var full = await NowPlaying.GetAsync(includeArt: true) ?? meta;
            _form.ShowTrack(full);
        }
        catch { }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }

    private async Task ShowNowAsync()
    {
        var np = await NowPlaying.GetAsync(includeArt: true);
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
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }
        _tray.Visible = false;
        _tray.Dispose();
        _form.Dispose();
        ExitThread();
    }
}

internal sealed class NowPlayingInfo
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string App { get; set; } = "";
    public Image? Art { get; set; }
    public string Key => Title + "|" + Artist + "|" + Album;
}

internal static class NowPlaying
{
    public static async Task<NowPlayingInfo?> GetAsync(bool includeArt)
    {
        var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = mgr.GetCurrentSession();
        if (session is null) return null;

        var props = await session.TryGetMediaPropertiesAsync();
        var title = (props.Title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title)) return null;

        var artist = (props.Artist ?? "").Trim();
        var album = (props.AlbumTitle ?? "").Trim();
        var app = NiceApp(session.SourceAppUserModelId ?? "");

        Image? art = null;
        if (includeArt)
        {
            try
            {
                if (props.Thumbnail is not null)
                {
                    using var ras = await props.Thumbnail.OpenReadAsync();
                    art = await DecodeImageAsync(ras);
                }
            }
            catch { }

            if (art is null)
                art = await TryItunesArtAsync(title, artist);
        }

        return new NowPlayingInfo
        {
            Title = title,
            Artist = artist,
            Album = album,
            App = app,
            Art = art
        };
    }

    private static string NiceApp(string app)
    {
        if (app.Contains("AppleMusic", StringComparison.OrdinalIgnoreCase) ||
            app.Contains("AppleInc.AppleMusic", StringComparison.OrdinalIgnoreCase))
            return "Apple Music";
        if (app.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        var bang = app.Split('!')[0];
        try { return Path.GetFileNameWithoutExtension(bang); } catch { return bang; }
    }

    private static async Task<Image?> DecodeImageAsync(IRandomAccessStreamWithContentType stream)
    {
        var size = (int)stream.Size;
        if (size <= 0 || size > 8_000_000) return null;
        var reader = new DataReader(stream);
        await reader.LoadAsync((uint)size);
        var bytes = new byte[size];
        reader.ReadBytes(bytes);
        reader.Dispose();
        using var ms = new MemoryStream(bytes);
        using var img = Image.FromStream(ms);
        var bmp = new Bitmap(img);
        if (bmp.Width > 300 || bmp.Height > 300)
        {
            var scaled = new Bitmap(300, 300);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bmp, 0, 0, 300, 300);
            }
            bmp.Dispose();
            return scaled;
        }
        return bmp;
    }

    private static async Task<Image?> TryItunesArtAsync(string title, string artist)
    {
        try
        {
            var artistClean = artist.Contains(" - ")
                ? artist.Split(new[] { " - " }, 2, StringSplitOptions.None)[0]
                : artist;
            var titleClean = title.Replace("/", " ").Replace("(feat.", " ").Replace("(Feat.", " ").Replace(")", " ");
            var queries = new[] { artistClean + " " + titleClean, artistClean + " " + title.Split('(')[0], titleClean };

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            foreach (var q in queries)
            {
                var url = "https://itunes.apple.com/search?term=" + Uri.EscapeDataString(q.Trim()) +
                          "&entity=song&limit=3&media=music";
                var json = await http.GetStringAsync(url);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                    continue;
                var artUrl = results[0].GetProperty("artworkUrl100").GetString();
                if (string.IsNullOrEmpty(artUrl)) continue;
                artUrl = artUrl.Replace("100x100bb", "300x300bb");
                var bytes = await http.GetByteArrayAsync(artUrl);
                using var ms = new MemoryStream(bytes);
                using var img = Image.FromStream(ms);
                return new Bitmap(img);
            }
        }
        catch { }
        return null;
    }
}

internal sealed class ToastForm : Form
{
    private static readonly Color AppleRed = Color.FromArgb(250, 45, 70);
    private static readonly Color AppleRedSoft = Color.FromArgb(180, 250, 45, 70);
    private static readonly Color CardBg = Color.FromArgb(28, 28, 30);

    private readonly System.Windows.Forms.Timer _anim;
    private readonly System.Windows.Forms.Timer _hold;

    private Image? _art;
    private string _title = "";
    private string _artist = "";
    private string _app = "";

    private enum Phase { Hidden, Opening, Holding, Closing }
    private Phase _phase = Phase.Hidden;
    private float _t;

    public ToastForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(400, 112);
        BackColor = CardBg;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 18, 18));

        _anim = new System.Windows.Forms.Timer { Interval = 16 };
        _anim.Tick += (_, _) => OnAnim();

        _hold = new System.Windows.Forms.Timer { Interval = 4200 };
        _hold.Tick += (_, _) =>
        {
            _hold.Stop();
            if (_phase == Phase.Holding)
            {
                _phase = Phase.Closing;
                _anim.Start();
            }
        };

        Paint += (_, e) => Render(e.Graphics);
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    public void ShowMessage(string title, string body)
    {
        _title = title;
        _artist = body;
        _app = "Now Playing Toast";
        _art?.Dispose();
        _art = null;
        BeginShow();
    }

    public void ShowTrack(NowPlayingInfo np)
    {
        _title = np.Title;
        _artist = string.IsNullOrWhiteSpace(np.Artist) ? np.Album : np.Artist;
        _app = np.App;
        _art?.Dispose();
        _art = np.Art is null ? null : new Bitmap(np.Art);
        np.Art?.Dispose();
        BeginShow();
    }

    private void BeginShow()
    {
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Left = wa.Right - Width - 18;
        Top = wa.Bottom - Height - 18;

        _t = 0f;
        _phase = Phase.Opening;
        _hold.Stop();
        Opacity = 0;

        if (!Visible) Show();
        SetWindowPos(Handle, HWND_TOPMOST, Left, Top, Width, Height, 0x0040);
        BringToFront();
        _anim.Start();
        Invalidate();
    }

    private void OnAnim()
    {
        if (_phase == Phase.Opening)
        {
            _t = Math.Min(1f, _t + 0.08f);
            Opacity = Math.Min(0.98, _t);
            Invalidate();
            if (_t >= 1f)
            {
                _phase = Phase.Holding;
                Opacity = 0.98;
                _anim.Stop();
                Invalidate();
                _hold.Stop();
                _hold.Start();
            }
        }
        else if (_phase == Phase.Closing)
        {
            _t = Math.Max(0f, _t - 0.1f);
            Opacity = Math.Max(0.05, _t);
            Invalidate();
            if (_t <= 0f)
            {
                _phase = Phase.Hidden;
                _anim.Stop();
                Opacity = 1;
                Hide();
            }
        }
        else
        {
            _anim.Stop();
        }
    }

    private void Render(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(CardBg);

        using (var path = RoundRect(1, 1, Width - 3, Height - 3, 16))
        {
            using var glow = new Pen(AppleRedSoft, 4f);
            g.DrawPath(glow, path);
            using var edge = new Pen(AppleRed, 1.8f);
            g.DrawPath(edge, path);
        }

        int artSize = 80;
        int artX = 14;
        int artY = (Height - artSize) / 2;
        var artRect = new Rectangle(artX, artY, artSize, artSize);
        using (var artPath = RoundRect(artX, artY, artSize, artSize, 10))
        {
            var old = g.Clip;
            g.SetClip(artPath);
            if (_art is not null)
                g.DrawImage(_art, artRect);
            else
            {
                using var bg = new SolidBrush(Color.FromArgb(50, 50, 54));
                g.FillRectangle(bg, artRect);
            }
            g.Clip = old;
            using var artBorder = new Pen(Color.FromArgb(60, AppleRed), 1f);
            g.DrawPath(artBorder, artPath);
        }

        int textX = artX + artSize + 14;
        int textW = Width - textX - 16;

        using var titleFont = new Font("Segoe UI Semibold", 12f);
        using var artistFont = new Font("Segoe UI", 10f);
        using var appFont = new Font("Segoe UI", 8.5f);
        using var titleBrush = new SolidBrush(Color.White);
        using var artistBrush = new SolidBrush(Color.FromArgb(190, 190, 195));
        using var appBrush = new SolidBrush(AppleRed);

        var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(_title, titleFont, titleBrush, new RectangleF(textX, 20, textW, 28), sf);
        g.DrawString(_artist, artistFont, artistBrush, new RectangleF(textX, 48, textW, 22), sf);
        g.DrawString(_app, appFont, appBrush, new RectangleF(textX, 74, textW, 18), sf);
    }

    private static GraphicsPath RoundRect(int x, int y, int w, int h, int r)
    {
        var p = new GraphicsPath();
        int d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _anim.Dispose();
            _hold.Dispose();
            _art?.Dispose();
        }
        base.Dispose(disposing);
    }
}
