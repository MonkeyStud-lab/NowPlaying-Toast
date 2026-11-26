using Xunit;

namespace NowPlayingToast;

public sealed class SessionArtworkTests
{
    private readonly SessionArtwork _art = new();
    private readonly TrackMetadata _song = new("Song", "Artist", "Album");

    private Task<byte[]?> Sample(TrackMetadata track, params byte[]?[] samples)
    {
        var queue = new Queue<byte[]?>(samples);
        return _art.ReadStableAsync(track, _ => Task.FromResult(queue.Dequeue()), CancellationToken.None);
    }

    [Fact]
    public async Task AcceptsLocalCoverAfterMissingOrChangingFirstSample()
    {
        Assert.Equal(new byte[] { 2 }, await Sample(_song, null, [2], [2]));
        Assert.Equal(new byte[] { 3 }, await Sample(_song with { Album = "Next" }, [2], [3], [3]));
    }

    [Fact]
    public async Task RejectsPriorAlbumsCoverEvenWhenWindowsRepeatsIt()
    {
        await Sample(_song, [1], [1]);
        Assert.Null(await Sample(_song with { Album = "Next" }, [1], [1], [1]));
        Assert.Equal(new byte[] { 2 }, await Sample(_song with { Album = "Next" }, [1], [2], [2]));
    }

    [Fact]
    public async Task AllowsSameCoverAcrossSongsOnOneAlbum()
    {
        await Sample(_song, [1], [1]);
        Assert.Equal(new byte[] { 1 }, await Sample(_song with { Title = "Track two" }, [1], [1]));
    }

    [Fact]
    public async Task RequiresConsecutiveMatchingSamples()
        => Assert.Null(await Sample(_song, [1], null, [1]));

    [Fact]
    public async Task CancelledSkipDoesNotChangeHistory()
    {
        using var cancel = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _art.ReadStableAsync(_song, token =>
        {
            cancel.Cancel();
            return Task.FromResult<byte[]?>([1]);
        }, cancel.Token));
        Assert.Equal(new byte[] { 1 }, await Sample(_song with { Album = "Next" }, [1], [1]));
    }
}
