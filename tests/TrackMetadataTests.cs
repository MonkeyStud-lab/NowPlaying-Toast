using Xunit;

namespace NowPlayingToast;

public sealed class TrackMetadataTests
{
    [Theory]
    [InlineData("Kanye West — Graduation", "", "Kanye West", "Graduation")]
    [InlineData("Kanye West — Graduation", "Graduation", "Kanye West", "Graduation")]
    [InlineData("Artist — Duo", "Album", "Artist — Duo", "Album")]
    [InlineData("Artist — Album — Deluxe", "", "Artist", "Album — Deluxe")]
    [InlineData(" Artist ", " Album ", "Artist", "Album")]
    [InlineData("Artist — ", "", "Artist —", "")]
    public void SeparatesOnlyTheAlbumPresentationSuffix(string artist, string album, string expectedArtist, string expectedAlbum)
    {
        var metadata = TrackMetadata.FromMediaFields(" Song ", artist, album);
        Assert.Equal(new TrackMetadata("Song", expectedArtist, expectedAlbum), metadata);
    }

    [Fact]
    public void MissingFieldsRemainEmptyWithoutInventingAnArtworkIdentity()
        => Assert.Equal(new TrackMetadata("", "", ""), TrackMetadata.FromMediaFields(null, null, null));
}
