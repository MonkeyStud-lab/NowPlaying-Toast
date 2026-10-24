using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace NowPlayingToast;

internal sealed class NowPlayingInfo
{
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";
    public string App { get; init; } = "";
    public string SourceAppUserModelId { get; init; } = "";
    public IRandomAccessStreamReference? Thumbnail { get; init; }
    public Image? Art { get; set; }
    public string Key => JsonSerializer.Serialize(new[] { SourceAppUserModelId, Title, Artist, Album });
}

internal static class NowPlaying
{
    private const int MaxArtworkBytes = 8_000_000;
    private const int MaxSearchBytes = 256_000;
    private const int MaxImageDimension = 4096;
    private static readonly HttpClient ArtworkHttp = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        // One linked deadline covers the entire thumbnail/search/download operation.
        Timeout = Timeout.InfiniteTimeSpan
    };

    public static bool IsAppleMusicSession(string? sourceAppUserModelId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppUserModelId))
            return false;
        return sourceAppUserModelId.Contains("AppleMusic", StringComparison.OrdinalIgnoreCase);
    }

    public static GlobalSystemMediaTransportControlsSession? FindAppleMusicSession(
        GlobalSystemMediaTransportControlsSessionManager manager)
    {
        var current = manager.GetCurrentSession();
        if (current is not null && IsAppleMusicSession(current.SourceAppUserModelId))
            return current;

        foreach (var session in manager.GetSessions())
        {
            if (IsAppleMusicSession(session.SourceAppUserModelId))
                return session;
        }

        return null;
    }

    public static async Task<NowPlayingInfo?> ReadAsync(
        GlobalSystemMediaTransportControlsSession session,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var aumid = session.SourceAppUserModelId ?? "";
        if (!IsAppleMusicSession(aumid)) return null;

        var properties = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var title = (properties.Title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title)) return null;

        // Keep the artwork reference from this exact metadata snapshot. Re-reading
        // the session later could pair the old title with the next song's artwork.
        return new NowPlayingInfo
        {
            Title = title,
            Artist = (properties.Artist ?? "").Trim(),
            Album = (properties.AlbumTitle ?? "").Trim(),
            App = "Apple Music",
            SourceAppUserModelId = aumid,
            Thumbnail = properties.Thumbnail
        };
    }

    public static async Task<Image?> GetArtworkAsync(NowPlayingInfo info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var token = deadline.Token;

        try
        {
            if (info.Thumbnail is not null)
            {
                try
                {
                    using var stream = await info.Thumbnail.OpenReadAsync().AsTask(token);
                    var bytes = await ReadThumbnailAsync(stream, token);
                    if (bytes is not null)
                    {
                        var thumbnail = await DecodeImageAsync(bytes, token);
                        if (thumbnail is not null) return thumbnail;
                    }
                }
                catch (Exception ex) when (IsArtworkError(ex))
                {
                    token.ThrowIfCancellationRequested();
                    // A missing or malformed local image may still have a catalog match.
                }
            }

            return await TryItunesArtworkAsync(info, token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // An artwork timeout is optional; cancellation by the caller propagates.
            return null;
        }
        catch (Exception ex) when (IsArtworkError(ex))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    private static bool IsArtworkError(Exception error) => error is
        IOException or HttpRequestException or JsonException or ArgumentException or
        ExternalException or InvalidOperationException or NotSupportedException;

    private static async Task<byte[]?> ReadThumbnailAsync(
        IRandomAccessStreamWithContentType stream, CancellationToken cancellationToken)
    {
        if (stream.Size == 0 || stream.Size > MaxArtworkBytes) return null;
        var size = (uint)stream.Size;
        using var reader = new DataReader(stream);
        try
        {
            if (await reader.LoadAsync(size).AsTask(cancellationToken) != size) return null;
            var bytes = new byte[(int)size];
            reader.ReadBytes(bytes);
            return bytes;
        }
        finally
        {
            // The caller owns and disposes the WinRT stream.
            reader.DetachStream();
        }
    }

    private static async Task<Image?> DecodeImageAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        // GDI+ decoding is synchronous; keep it off the tray app's UI thread.
        var image = await Task.Run(() => DecodeImage(bytes), cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            image?.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        return image;
    }

    private static Image? DecodeImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        if (image.Width <= 0 || image.Height <= 0 || image.Width > MaxImageDimension || image.Height > MaxImageDimension)
            return null;

        var scale = Math.Min(1d, 300d / Math.Max(image.Width, image.Height));
        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(image, new Rectangle(0, 0, width, height), 0, 0,
                image.Width, image.Height, GraphicsUnit.Pixel);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static async Task<Image?> TryItunesArtworkAsync(NowPlayingInfo info, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(info.Title) || string.IsNullOrWhiteSpace(info.Artist)) return null;
        var query = Uri.EscapeDataString(info.Artist + " " + info.Title);
        var searchUri = new Uri("https://itunes.apple.com/search?term=" + query + "&entity=song&limit=10&media=music");
        var searchBytes = await GetBoundedBytesAsync(searchUri, MaxSearchBytes, cancellationToken);
        if (searchBytes is null) return null;

        using var document = JsonDocument.Parse(searchBytes);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("results", out var results)) return null;

        var url = ArtworkMatcher.FindArtworkUrl(results, info.Title, info.Artist, info.Album);
        if (url is null) return null;
        var artworkUri = new Uri(url.Replace("100x100bb", "300x300bb", StringComparison.Ordinal));
        var artworkBytes = await GetBoundedBytesAsync(artworkUri, MaxArtworkBytes, cancellationToken);
        return artworkBytes is null ? null : await DecodeImageAsync(artworkBytes, cancellationToken);
    }

    private static async Task<byte[]?> GetBoundedBytesAsync(Uri uri, int limit, CancellationToken cancellationToken)
    {
        // Validate every redirect as well as the original URL before sending it.
        for (var redirect = 0; redirect <= 3; redirect++)
        {
            if (!ArtworkMatcher.IsAllowedArtworkUrl(uri.AbsoluteUri)) return null;
            using var response = await ArtworkHttp.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
                HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                var location = response.Headers.Location;
                if (location is null) return null;
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                continue;
            }
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > limit) return null;

            using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[16_384];
            int count;
            while ((count = await body.ReadAsync(chunk.AsMemory(), cancellationToken)) != 0)
            {
                if (buffer.Length + count > limit) return null;
                buffer.Write(chunk, 0, count);
            }
            return buffer.Length == 0 ? null : buffer.ToArray();
        }
        return null;
    }
}
