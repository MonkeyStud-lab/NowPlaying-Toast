using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Windows.Media.Control;
using Windows.Storage.Streams;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool demo = args.Any(a => a.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        Application.Run(new ToastApp(demo));
    }
}

internal sealed class ToastApp : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly ToastForm _form;
    private string _lastKey = "";
    private bool _primed;
    private readonly bool _demo;

    public ToastApp(bool demo)
    {
        _demo = demo;
        _form = new ToastForm();

        _tray = new NotifyIcon
        {
            Visible = true,
            Text = "Now Playing Toast",
            Icon = SystemIcons.Application,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Items.Add("Show current track", null, async (_, _) => await ShowNowAsync());
        _tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => ExitApp());

        _pollTimer = new System.Windows.Forms.Timer { Interval = 800 };
        _pollTimer.Tick += async (_, _) => await PollAsync();
        _pollTimer.Start();

        // Auto demo / prove it works shortly after launch
        var boot = new System.Windows.Forms.Timer { Interval = 1200 };
        boot.Tick += async (_, _) =>
        {
            boot.Stop();
            boot.Dispose();
            await ShowNowAsync();
        };
        boot.Start();
    }

    private async Task ShowNowAsync()
    {
        var np = await NowPlaying.GetAsync();
        if (np is null)
        {
            _form.ShowMessage("Nothing playing", "Start a song in Apple Music, then try again.");
            return;
        }
        _lastKey = np.Key;
        _primed = true;
        _form.ShowTrack(np);
    }

    private async Task PollAsync()
    {
        try
        {
            var np = await NowPlaying.GetAsync();
            if (np is null)
            {
                _primed = true;
                return;
            }

            if (!_primed)
            {
                _lastKey = np.Key;
                _primed = true;
                return;
            }

            if (np.Key != _lastKey)
            {
                _lastKey = np.Key;
                _form.ShowTrack(np);
            }
        }
        catch { }
    }

    private void ExitApp()
    {
        _pollTimer.Stop();
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
    public static async Task<NowPlayingInfo?> GetAsync()
    {
        var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = mgr.GetCurrentSession();
        if (session is null) return null;

        var props = await session.TryGetMediaPropertiesAsync();
        var title = (props.Title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title)) return null;

        Image? art = null;
        try
        {
            if (props.Thumbnail is not null)
            {
                using var ras = await props.Thumbnail.OpenReadAsync();
                art = await DecodeImageAsync(ras);
            }
        }
        catch { }

        var artist = (props.Artist ?? "").Trim();
        if (art is null)
            art = await TryItunesArtAsync(title, artist);

        return new NowPlayingInfo
        {
            Title = title,
            Artist = artist,
            Album = (props.AlbumTitle ?? "").Trim(),
            App = NiceApp(session.SourceAppUserModelId ?? ""),
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
        return new Bitmap(img);
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

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            foreach (var q in queries)
            {
                var url = "https://itunes.apple.com/search?term=" + Uri.EscapeDataString(q.Trim()) +
                          "&entity=song&limit=5&media=music";
                var json = await http.GetStringAsync(url);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                    continue;
                var artUrl = results[0].GetProperty("artworkUrl100").GetString();
                if (string.IsNullOrEmpty(artUrl)) continue;
                artUrl = artUrl.Replace("100x100bb", "600x600bb");
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
    private readonly System.Windows.Forms.Timer _anim;
    private readonly System.Windows.Forms.Timer _hold;
    private readonly Random _rng = new();
    private readonly List<Spark> _sparks = new();

    private Image? _art;
    private string _title = "";
    private string _artist = "";
    private string _app = "";
    private bool _isMessage;

    private enum Phase { Hidden, Opening, Holding, Closing }
    private Phase _phase = Phase.Hidden;
    private float _t;     // portal 0..1
    private float _spin;

    private struct Spark
    {
        public float Angle, Dist, Speed, Size, Life;
        public Color Color;
    }

    public ToastForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(440, 140);
        BackColor = Color.FromArgb(12, 10, 18);
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 20, 20));

        _anim = new System.Windows.Forms.Timer { Interval = 15 };
        _anim.Tick += (_, _) => OnAnim();

        _hold = new System.Windows.Forms.Timer { Interval = 4500 };
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
        _isMessage = true;
        _title = title;
        _artist = body;
        _app = "Now Playing Toast";
        _art?.Dispose();
        _art = null;
        BeginShow();
    }

    public void ShowTrack(NowPlayingInfo np)
    {
        _isMessage = false;
        _title = np.Title;
        _artist = string.IsNullOrWhiteSpace(np.Artist) ? np.Album : np.Artist;
        _app = np.App;
        _art?.Dispose();
        _art = np.Art is null ? null : new Bitmap(np.Art);
        BeginShow();
    }

    private void BeginShow()
    {
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Left = wa.Right - Width - 16;
        Top = wa.Bottom - Height - 16;

        _t = 0f;
        _spin = 0f;
        _sparks.Clear();
        _phase = Phase.Opening;
        _hold.Stop();

        if (!Visible) Show();
        SetWindowPos(Handle, HWND_TOPMOST, Left, Top, Width, Height, 0x0040);
        BringToFront();
        Activate();
        _anim.Start();
        Invalidate();
    }

    private void OnAnim()
    {
        _spin += 0.14f;
        SpawnSparks();
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s.Angle += s.Speed;
            s.Life -= 0.05f;
            s.Dist += 0.35f;
            if (s.Life <= 0) _sparks.RemoveAt(i);
            else _sparks[i] = s;
        }

        if (_phase == Phase.Opening)
        {
            _t = Math.Min(1f, _t + 0.055f);
            if (_t >= 1f)
            {
                _phase = Phase.Holding;
                _hold.Stop();
                _hold.Start();
            }
        }
        else if (_phase == Phase.Closing)
        {
            _t = Math.Max(0f, _t - 0.07f);
            if (_t <= 0f)
            {
                _phase = Phase.Hidden;
                _anim.Stop();
                _sparks.Clear();
                Hide();
            }
        }

        Invalidate();
    }

    private void SpawnSparks()
    {
        if (_t < 0.08f) return;
        int n = _phase == Phase.Opening ? 8 : (_phase == Phase.Holding ? 3 : 2);
        for (int i = 0; i < n && _sparks.Count < 140; i++)
        {
            _sparks.Add(new Spark
            {
                Angle = (float)(_rng.NextDouble() * Math.PI * 2),
                Dist = 40 + (float)_rng.NextDouble() * 30,
                Speed = (float)((_rng.NextDouble() - 0.25) * 0.28),
                Size = 1.8f + (float)_rng.NextDouble() * 3.2f,
                Life = 0.45f + (float)_rng.NextDouble() * 0.55f,
                Color = _rng.Next(3) switch
                {
                    0 => Color.FromArgb(255, 110, 10),
                    1 => Color.FromArgb(255, 170, 35),
                    _ => Color.FromArgb(255, 230, 110)
                }
            });
        }
    }

    private static float Ease(float x) => 1f - (float)Math.Pow(1 - x, 3);

    private void Render(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.FromArgb(12, 10, 18));

        float e = Ease(_t);
        float cx = Width / 2f;
        float cy = Height / 2f;

        // Portal rings behind card
        if (_t > 0.01f)
        {
            float rx = 40 + 170 * e;
            float ry = 22 + 55 * e;

            // void glow
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - rx * 0.7f, cy - ry * 0.7f, rx * 1.4f, ry * 1.4f);
                using var brush = new PathGradientBrush(path)
                {
                    CenterColor = Color.FromArgb((int)(180 * e), 40, 10, 60),
                    SurroundColors = new[] { Color.FromArgb(0, 12, 10, 18) }
                };
                g.FillPath(brush, path);
            }

            for (int ring = 0; ring < 5; ring++)
            {
                float local = Math.Max(0f, e - ring * 0.06f);
                if (local <= 0) continue;
                float rot = (_spin * (ring % 2 == 0 ? 1f : -1.2f) + ring * 0.5f) * 180f / (float)Math.PI;
                float rrx = rx * (0.75f + ring * 0.07f);
                float rry = ry * (0.75f + ring * 0.07f);

                var state = g.Save();
                g.TranslateTransform(cx, cy);
                g.RotateTransform(rot);
                using var pen = new Pen(Color.FromArgb((int)(230 * local), 255, 95 + ring * 20, 8), 2.4f - ring * 0.25f);
                g.DrawEllipse(pen, -rrx, -rry, rrx * 2, rry * 2);
                using var penHot = new Pen(Color.FromArgb((int)(140 * local), 255, 220, 90), 1.1f);
                g.DrawEllipse(penHot, -rrx * 0.9f, -rry * 0.9f, rrx * 1.8f, rry * 1.8f);
                g.Restore(state);
            }

            // tick marks
            for (int i = 0; i < 32; i++)
            {
                float a = _spin * 0.8f + i * (float)(Math.PI * 2 / 32);
                float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
                using var pen = new Pen(Color.FromArgb((int)(200 * e), 255, 150, 30), 1.6f);
                g.DrawLine(pen, cx + c * rx * 0.92f, cy + s * ry * 0.92f, cx + c * rx * 1.14f, cy + s * ry * 1.14f);
            }

            foreach (var sp in _sparks)
            {
                float x = cx + (float)Math.Cos(sp.Angle) * (rx * 0.55f + sp.Dist * 0.35f);
                float y = cy + (float)Math.Sin(sp.Angle) * (ry * 0.55f + sp.Dist * 0.2f);
                int a = Math.Clamp((int)(255 * sp.Life), 0, 255);
                using var b = new SolidBrush(Color.FromArgb(a, sp.Color));
                g.FillEllipse(b, x - sp.Size / 2, y - sp.Size / 2, sp.Size, sp.Size);
            }
        }

        // Card (scales in)
        float scale = 0.2f + 0.8f * e;
        int cardW = (int)(400 * scale);
        int cardH = (int)(108 * scale);
        int cardX = (Width - cardW) / 2;
        int cardY = (Height - cardH) / 2;

        using (var path = RoundRect(cardX, cardY, cardW, cardH, 14))
        using (var cardBrush = new SolidBrush(Color.FromArgb((int)(245 * Math.Min(1, e * 1.2f)), 24, 24, 28)))
        using (var border = new Pen(Color.FromArgb((int)(200 * e), 255, 120, 30), 1.5f))
        {
            g.FillPath(cardBrush, path);
            g.DrawPath(border, path);
        }

        if (e < 0.25f) return;

        float alpha = Math.Min(1f, (e - 0.25f) / 0.5f);
        int a255 = (int)(255 * alpha);

        // Art
        int artSize = (int)(82 * Math.Min(1f, scale));
        int artX = cardX + 12;
        int artY = cardY + (cardH - artSize) / 2;
        var artRect = new Rectangle(artX, artY, artSize, artSize);
        using (var artBg = new SolidBrush(Color.FromArgb(a255, 40, 40, 46)))
            g.FillRectangle(artBg, artRect);
        if (_art is not null)
        {
            var ia = new System.Drawing.Imaging.ImageAttributes();
            var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = alpha };
            ia.SetColorMatrix(cm);
            g.DrawImage(_art, artRect, 0, 0, _art.Width, _art.Height, GraphicsUnit.Pixel, ia);
        }

        int textX = artX + artSize + 14;
        int textW = cardX + cardW - textX - 14;

        using var titleFont = new Font("Segoe UI Semibold", Math.Max(8f, 12f * Math.Min(1f, scale)));
        using var artistFont = new Font("Segoe UI", Math.Max(7f, 10f * Math.Min(1f, scale)));
        using var appFont = new Font("Segoe UI", Math.Max(6.5f, 8.5f * Math.Min(1f, scale)));
        using var titleBrush = new SolidBrush(Color.FromArgb(a255, 255, 255, 255));
        using var artistBrush = new SolidBrush(Color.FromArgb(a255, 190, 190, 195));
        using var appBrush = new SolidBrush(Color.FromArgb(a255, 255, 160, 60));

        var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(_title, titleFont, titleBrush, new RectangleF(textX, cardY + 16, textW, 28), sf);
        g.DrawString(_artist, artistFont, artistBrush, new RectangleF(textX, cardY + 46, textW, 22), sf);
        g.DrawString(_app, appFont, appBrush, new RectangleF(textX, cardY + 72, textW, 18), sf);
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
