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

    private enum Phase { Hidden, Opening, Holding, Closing }
    private Phase _phase = Phase.Hidden;
    private float _t;

    public ToastForm(AppSettings settings)
    {
        _settings = settings;
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
        float scale = _settings.SizeScale;
        Size = new Size((int)(400 * scale), (int)(112 * scale));
        BackColor = CardBackground;
        var hrgn = NativeMethods.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, (int)(18 * scale), (int)(18 * scale));
        if (hrgn != IntPtr.Zero)
        {
            Region?.Dispose();
            Region = Region.FromHrgn(hrgn);
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
        Opacity = 1;
        Hide();
    }

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
        ApplyLayoutFromSettings();
        PositionToast();

        _t = 0f;
        _phase = Phase.Opening;
        _hold.Stop();
        Opacity = 0;

        if (!Visible) Show();
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, Left, Top, Width, Height, 0x0040);
        BringToFront();
        _anim.Start();
        Invalidate();

        if (_settings.PlaySoundOnToast)
        {
            try { SystemSounds.Asterisk.Play(); } catch { }
        }
    }

    private void PositionToast()
    {
        var wa = _settings.ResolveScreen().WorkingArea;
        int margin = 18;
        switch (_settings.Corner)
        {
            case ToastCorner.BottomLeft:
                Left = wa.Left + margin;
                Top = wa.Bottom - Height - margin;
                break;
            case ToastCorner.TopRight:
                Left = wa.Right - Width - margin;
                Top = wa.Top + margin;
                break;
            case ToastCorner.TopLeft:
                Left = wa.Left + margin;
                Top = wa.Top + margin;
                break;
            default:
                Left = wa.Right - Width - margin;
                Top = wa.Bottom - Height - margin;
                break;
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
        float scale = _settings.SizeScale;
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
            var old = g.Clip;
            g.SetClip(artPath);
            if (_art is not null)
                g.DrawImage(_art, artRect);
            else
            {
                using var bg = new SolidBrush(ArtPlaceholder);
                g.FillRectangle(bg, artRect);
            }
            g.Clip = old;
            using var artBorder = new Pen(Color.FromArgb(60, AppleRed), 1f);
            g.DrawPath(artBorder, artPath);
        }

        int textX = artX + artSize + (int)(14 * scale);
        int textW = Width - textX - (int)(16 * scale);

        using var titleFont = new Font("Segoe UI Semibold", 12f * scale);
        using var artistFont = new Font("Segoe UI", 10f * scale);
        using var appFont = new Font("Segoe UI", 8.5f * scale);
        using var titleBrush = new SolidBrush(TitleColor);
        using var artistBrush = new SolidBrush(ArtistColor);
        using var appBrush = new SolidBrush(AppleRed);

        var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
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

