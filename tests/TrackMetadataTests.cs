using Xunit;

namespace NowPlayingToast;

public sealed class TrackMetadataTests
{
    [Theory]
    [InlineData("AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", true)]
    [InlineData("appleinc.applemusicwin_nzyj5cx40ttqa!app", true)]
    [InlineData("Other.AppleMusicPlayer", false)]
    [InlineData("AppleInc.AppleMusicWin_nzyj5cx40ttqa!Other", false)]
    [InlineData(null, false)]
    public void RecognizesOnlyTheRegisteredAppleMusicSource(string? appId, bool expected)
        => Assert.Equal(expected, TrackMetadata.IsAppleMusicSource(appId));

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
