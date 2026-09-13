using System.Drawing.Drawing2D;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace NowPlayingToast;

internal sealed class NowPlayingInfo
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string App { get; set; } = "";
    public string SourceAppUserModelId { get; set; } = "";
    public Image? Art { get; set; }
    public string Key => Title + "|" + Artist + "|" + Album;
}

internal static class NowPlaying
{
    public static bool IsAppleMusicSession(string? sourceAppUserModelId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppUserModelId))
            return false;
        return sourceAppUserModelId.Contains("AppleMusic", StringComparison.OrdinalIgnoreCase)
            || sourceAppUserModelId.Contains("AppleInc.AppleMusic", StringComparison.OrdinalIgnoreCase);
    }

    public static GlobalSystemMediaTransportControlsSession? FindAppleMusicSession(
        GlobalSystemMediaTransportControlsSessionManager mgr)
    {
        // Prefer current session only if it is Apple Music; otherwise scan all sessions.
        var current = mgr.GetCurrentSession();
        if (current is not null && IsAppleMusicSession(current.SourceAppUserModelId))
            return current;

        foreach (var session in mgr.GetSessions())
        {
            if (IsAppleMusicSession(session.SourceAppUserModelId))
                return session;
        }

        return null;
    }

    public static async Task<NowPlayingInfo?> GetAsync(
        GlobalSystemMediaTransportControlsSessionManager? existingMgr,
        bool includeArt)
    {
        var mgr = existingMgr ?? await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        var session = FindAppleMusicSession(mgr);
        if (session is null) return null;

        var props = await session.TryGetMediaPropertiesAsync();
        var title = (props.Title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title)) return null;

        var artist = (props.Artist ?? "").Trim();
        var album = (props.AlbumTitle ?? "").Trim();
        var aumid = session.SourceAppUserModelId ?? "";

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
            App = "Apple Music",
            SourceAppUserModelId = aumid,
            Art = art
        };
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
            var artistClean = artist.Contains(" - ", StringComparison.Ordinal)
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
