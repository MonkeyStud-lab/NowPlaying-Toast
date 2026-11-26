using System.Security.Cryptography;

namespace NowPlayingToast;

internal sealed class SessionArtwork
{
    private TrackMetadata? _lastTrack;
    private byte[]? _lastHash;

    // The provider can publish the next title before it updates the cover.
    // Two fresh reads must agree. Keep only the digest between notifications.
    public async Task<byte[]?> ReadStableAsync(TrackMetadata track,
        Func<CancellationToken, Task<byte[]?>> readForTrack, CancellationToken token)
    {
        byte[]? previous = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0) await Task.Delay(200, token);
            var bytes = await readForTrack(token);
            token.ThrowIfCancellationRequested();
            var hash = bytes is { Length: > 0 } ? SHA256.HashData(bytes) : null;
            if (hash is not null && hash.AsSpan().SequenceEqual(previous))
            {
                bool sameAlbum = track.Album.Length > 0 && track.Album == _lastTrack?.Album
                    && track.Artist == _lastTrack.Artist;
                if (!hash.AsSpan().SequenceEqual(_lastHash) || track == _lastTrack || sameAlbum)
                {
                    _lastTrack = track;
                    _lastHash = hash;
                    return bytes;
                }
            }
            previous = hash;
        }
        return null;
    }
}
