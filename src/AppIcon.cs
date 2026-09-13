using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

namespace NowPlayingToast;

internal static class AppIcon
{
    public static Icon Create()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("NowPlayingToast.app.ico");
            if (stream is not null)
                return new Icon(stream);
        }
        catch { }

        // Fallback: generate at runtime into a memory ICO
        try
        {
            using var ms = new MemoryStream();
            WriteMultiSizeIco(ms, new[] { RenderBitmap(16), RenderBitmap(32) });
            ms.Position = 0;
            return new Icon(ms);
        }
        catch
        {
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    public static void WriteIcoFile(string path)
    {
        using var fs = File.Create(path);
        WriteMultiSizeIco(fs, new[] { RenderBitmap(16), RenderBitmap(32), RenderBitmap(48), RenderBitmap(256) });
    }

    private static Bitmap RenderBitmap(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        float s = size / 64f;

        using (var path = RoundRect(4 * s, 8 * s, 56 * s, 48 * s, 10 * s))
        using (var brush = new SolidBrush(Color.FromArgb(32, 32, 36)))
            g.FillPath(brush, path);

        using (var path = RoundRect(4 * s, 8 * s, 56 * s, 48 * s, 10 * s))
        using (var pen = new Pen(Color.FromArgb(250, 45, 70), Math.Max(1.5f, 3f * s)))
            g.DrawPath(pen, path);

        using (var noteBrush = new SolidBrush(Color.FromArgb(250, 45, 70)))
        {
            g.FillEllipse(noteBrush, 18 * s, 38 * s, 12 * s, 10 * s);
            g.FillEllipse(noteBrush, 34 * s, 34 * s, 12 * s, 10 * s);
            using var stem = new Pen(Color.FromArgb(250, 45, 70), Math.Max(1.5f, 3.5f * s));
            g.DrawLine(stem, 29 * s, 42 * s, 29 * s, 18 * s);
            g.DrawLine(stem, 45 * s, 38 * s, 45 * s, 14 * s);
            using var beam = new Pen(Color.FromArgb(250, 45, 70), Math.Max(2f, 4f * s));
            g.DrawLine(beam, 29 * s, 18 * s, 45 * s, 14 * s);
        }

        return bmp;
    }

    private static void WriteMultiSizeIco(Stream stream, Bitmap[] bitmaps)
    {
        using var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        bw.Write((short)0);
        bw.Write((short)1);
        bw.Write((short)bitmaps.Length);

        var imageData = new List<byte[]>();
        int offset = 6 + (16 * bitmaps.Length);

        foreach (var bmp in bitmaps)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            var png = ms.ToArray();
            imageData.Add(png);

            int w = bmp.Width >= 256 ? 0 : bmp.Width;
            int h = bmp.Height >= 256 ? 0 : bmp.Height;
            bw.Write((byte)w);
            bw.Write((byte)h);
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(png.Length);
            bw.Write(offset);
            offset += png.Length;
        }

        foreach (var data in imageData)
            bw.Write(data);

        foreach (var bmp in bitmaps)
            bmp.Dispose();
    }

    private static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
