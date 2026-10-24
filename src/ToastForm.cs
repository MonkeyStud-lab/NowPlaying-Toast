using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Media;

namespace NowPlayingToast;

internal sealed class ToastForm : Form
{
    private static readonly Color AppleRed = Color.FromArgb(250, 45, 70);
    private static readonly Color AppleRedSoft = Color.FromArgb(180, 250, 45, 70);

    private readonly System.Windows.Forms.Timer _anim;
    private readonly System.Windows.Forms.Timer _hold;
    private AppSettings _settings;

    private Image? _art;
    private string _title = "";
    private string _artist = "";
    private string _app = "";
    private string? _trackKey;
    private bool _positioning;

    private enum Phase { Hidden, Opening, Holding, Closing }
    private Phase _phase = Phase.Hidden;
    private float _t;

    public ToastForm(AppSettings settings)
    {
        _settings = settings;
        // The card is painted in device pixels, with DPI handled explicitly below.
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        _anim = new System.Windows.Forms.Timer { Interval = 16 };
        _anim.Tick += (_, _) => OnAnim();

        _hold = new System.Windows.Forms.Timer();
        _hold.Tick += (_, _) =>
        {
            _hold.Stop();
            if (_phase == Phase.Holding)
            {
                _phase = Phase.Closing;
                _anim.Start();
            }
        };

        ApplyLayoutFromSettings();

        Paint += (_, e) => Render(e.Graphics);
        Click += (_, _) => NativeMethods.FocusAppleMusic();
        Cursor = Cursors.Hand;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
            return parameters;
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WM_MOUSEACTIVATE)
        {
            // Deliver the click, but never give the notification keyboard focus.
            message.Result = new IntPtr(NativeMethods.MA_NOACTIVATE);
            return;
        }
        base.WndProc(ref message);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyLayoutFromSettings();
        PositionToast();
        Invalidate();
    }

    protected override bool OnGetDpiScaledSize(int deviceDpiOld, int deviceDpiNew, ref Size desiredSize)
    {
        var scale = _settings.SizeScale * deviceDpiNew / 96f;
        desiredSize = new Size((int)(400 * scale), (int)(112 * scale));
        return true;
    }

    private float PixelScale => _settings.SizeScale * DeviceDpi / 96f;

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        ApplyLayoutFromSettings();
        if (_phase == Phase.Holding || _phase == Phase.Opening)
            PositionToast();
        Invalidate();
    }

    private void ApplyLayoutFromSettings()
    {
        float scale = PixelScale;
        Size = new Size((int)(400 * scale), (int)(112 * scale));
        BackColor = CardBackground;
        var hrgn = NativeMethods.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, (int)(18 * scale), (int)(18 * scale));
        if (hrgn != IntPtr.Zero)
        {
            try
            {
                var oldRegion = Region;
                Region = Region.FromHrgn(hrgn);
                oldRegion?.Dispose();
            }
            finally
            {
                // Region.FromHrgn copies the native region; it does not own hrgn.
                NativeMethods.DeleteObject(hrgn);
            }
        }
        if (_hold is not null)
            _hold.Interval = Math.Max(500, (int)(_settings.HoldDurationSeconds * 1000));
    }

    private Color CardBackground => _settings.Theme == ToastTheme.Light
        ? Color.FromArgb(248, 248, 250)
        : Color.FromArgb(28, 28, 30);

    private Color TitleColor => _settings.Theme == ToastTheme.Light ? Color.FromArgb(20, 20, 24) : Color.White;
    private Color ArtistColor => _settings.Theme == ToastTheme.Light
        ? Color.FromArgb(80, 80, 88)
        : Color.FromArgb(190, 190, 195);
    private Color ArtPlaceholder => _settings.Theme == ToastTheme.Light
        ? Color.FromArgb(220, 220, 224)
        : Color.FromArgb(50, 50, 54);

    public void HideImmediate()
    {
        _hold.Stop();
        _anim.Stop();
        _phase = Phase.Hidden;
        _trackKey = null;
        Opacity = 1;
        Hide();
    }

    public void ShowMessage(string title, string body)
    {
        _title = title;
        _artist = body;
        _app = "Now Playing Toast";
        _trackKey = null;
        _art?.Dispose();
        _art = null;
        BeginShow();
    }

    public void ShowTrack(NowPlayingInfo np)
    {
        _title = np.Title;
        _artist = string.IsNullOrWhiteSpace(np.Artist) ? np.Album : np.Artist;
        _app = np.App;
        _trackKey = np.Key;
        ReplaceArtwork(np.Art);
        BeginShow();
    }

    public void UpdateArtwork(string trackKey, Image? artwork)
    {
        // A delayed download must not revive a hidden card or replace a newer song.
        if (!Visible || _phase == Phase.Hidden || !string.Equals(_trackKey, trackKey, StringComparison.Ordinal))
            return;

        ReplaceArtwork(artwork);
        Invalidate();
    }

    private void ReplaceArtwork(Image? artwork)
    {
        // The caller retains its image. The card owns only this independent copy.
        var replacement = artwork is null ? null : new Bitmap(artwork);
        _art?.Dispose();
        _art = replacement;
    }

    private void BeginShow()
    {
        ApplyLayoutFromSettings();
        PositionToast();

        _t = 0f;
        _phase = Phase.Opening;
        _hold.Stop();
        Opacity = 0;

        if (!Visible) Show();
        // Creating the window can change DeviceDpi on the selected monitor.
        ApplyLayoutFromSettings();
        PositionToast();
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, Left, Top, Width, Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        _anim.Start();
        Invalidate();

        if (_settings.PlaySoundOnToast)
        {
            try { SystemSounds.Asterisk.Play(); } catch { }
        }
    }

    private void PositionToast()
    {
        if (_positioning) return;
        _positioning = true;
        try
        {
            // A monitor move can synchronously change DPI and size. Recalculate
            // the corner once more after that move, without recursive positioning.
            for (var pass = 0; pass < 2; pass++)
            {
                var wa = _settings.ResolveScreen().WorkingArea;
                int margin = Math.Max(1, (int)(18 * DeviceDpi / 96f));
                bool left = _settings.Corner is ToastCorner.BottomLeft or ToastCorner.TopLeft;
                bool top = _settings.Corner is ToastCorner.TopLeft or ToastCorner.TopRight;
                var x = left ? wa.Left + margin : wa.Right - Width - margin;
                var y = top ? wa.Top + margin : wa.Bottom - Height - margin;
                Location = new Point(
                    Math.Clamp(x, wa.Left, Math.Max(wa.Left, wa.Right - Width)),
                    Math.Clamp(y, wa.Top, Math.Max(wa.Top, wa.Bottom - Height)));
            }
        }
        finally
        {
            _positioning = false;
        }
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
                _trackKey = null;
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
        float scale = PixelScale;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(CardBackground);

        using (var path = RoundRect(1, 1, Width - 3, Height - 3, (int)(16 * scale)))
        {
            using var glow = new Pen(AppleRedSoft, 4f * scale);
            g.DrawPath(glow, path);
            using var edge = new Pen(AppleRed, 1.8f * scale);
            g.DrawPath(edge, path);
        }

        int artSize = (int)(80 * scale);
        int artX = (int)(14 * scale);
        int artY = (Height - artSize) / 2;
        var artRect = new Rectangle(artX, artY, artSize, artSize);
        using (var artPath = RoundRect(artX, artY, artSize, artSize, (int)(10 * scale)))
        {
            var state = g.Save();
            try
            {
                g.SetClip(artPath);
                using var background = new SolidBrush(ArtPlaceholder);
                g.FillRectangle(background, artRect);
                if (_art is not null)
                {
                    var fit = Math.Min((float)artSize / _art.Width, (float)artSize / _art.Height);
                    var artworkWidth = _art.Width * fit;
                    var artworkHeight = _art.Height * fit;
                    g.DrawImage(_art, new RectangleF(artX + (artSize - artworkWidth) / 2,
                        artY + (artSize - artworkHeight) / 2, artworkWidth, artworkHeight));
                }
            }
            finally
            {
                g.Restore(state);
            }
            using var artBorder = new Pen(Color.FromArgb(60, AppleRed), scale);
            g.DrawPath(artBorder, artPath);
        }

        int textX = artX + artSize + (int)(14 * scale);
        int textW = Width - textX - (int)(16 * scale);

        // Font sizes are points; GDI+ already applies the display DPI to them.
        using var titleFont = new Font("Segoe UI Semibold", 12f * _settings.SizeScale);
        using var artistFont = new Font("Segoe UI", 10f * _settings.SizeScale);
        using var appFont = new Font("Segoe UI", 8.5f * _settings.SizeScale);
        using var titleBrush = new SolidBrush(TitleColor);
        using var artistBrush = new SolidBrush(ArtistColor);
        using var appBrush = new SolidBrush(AppleRed);

        using var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        g.DrawString(_title, titleFont, titleBrush, new RectangleF(textX, 20 * scale, textW, 28 * scale), sf);
        g.DrawString(_artist, artistFont, artistBrush, new RectangleF(textX, 48 * scale, textW, 22 * scale), sf);
        g.DrawString(_app, appFont, appBrush, new RectangleF(textX, 74 * scale, textW, 18 * scale), sf);
    }

    private static GraphicsPath RoundRect(int x, int y, int w, int h, int r)
    {
        var p = new GraphicsPath();
        int d = Math.Max(2, r * 2);
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

