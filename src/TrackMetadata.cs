namespace NowPlayingToast;

internal sealed record TrackMetadata(string Title, string Artist, string Album)
{
    public static TrackMetadata FromMediaFields(string? title, string? artist, string? album)
    {
        var trackTitle = (title ?? "").Trim();
        var trackArtist = (artist ?? "").Trim();
        var trackAlbum = (album ?? "").Trim();

        // Apple Music for Windows can publish "Artist — Album" in Artist while
        // leaving AlbumTitle empty. Keep that presentation text out of lookups.
        const string separator = " — ";
        if (trackAlbum.Length == 0)
        {
            var split = trackArtist.IndexOf(separator, StringComparison.Ordinal);
            if (split > 0 && split + separator.Length < trackArtist.Length)
            {
                trackAlbum = trackArtist[(split + separator.Length)..].Trim();
                trackArtist = trackArtist[..split].Trim();
            }
        }
        else
        {
            var suffix = separator + trackAlbum;
            if (trackArtist.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                trackArtist = trackArtist[..^suffix.Length].Trim();
        }

        return new TrackMetadata(trackTitle, trackArtist, trackAlbum);
    }
}
