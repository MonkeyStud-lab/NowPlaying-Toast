using NowPlayingToast;
using Xunit;

namespace NowPlayingToast.Tests;

public sealed class TrackNotificationStateTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FirstPopulatedSessionHonorsActivationSetting(bool showOnActivate)
    {
        var state = new TrackNotificationState();

        Assert.Equal(showOnActivate, state.Observe("first", showOnActivate));
        Assert.Equal("first", state.LastKey);
        Assert.False(state.Observe("first", showOnActivate));
    }

    [Fact]
    public void PlaybackAndMetadataEventsForSameTrackDoNotRepeatToast()
    {
        var state = new TrackNotificationState();

        Assert.True(state.Observe("same-track", showOnActivate: true));
        Assert.False(state.Observe("same-track", showOnActivate: true));
        Assert.False(state.Observe("same-track", showOnActivate: true));
    }

    [Fact]
    public void ManualShowCanRepeatCurrentTrackAndDoesNotDisableDeduplication()
    {
        var state = new TrackNotificationState();

        Assert.False(state.Observe("same-track", showOnActivate: false));
        Assert.True(state.Observe("same-track", showOnActivate: false, forceShow: true));
        Assert.False(state.Observe("same-track", showOnActivate: false));
    }

    [Fact]
    public void ChangedTrackShowsEvenWithActivationNotificationDisabled()
    {
        var state = new TrackNotificationState();

        Assert.False(state.Observe("first", showOnActivate: false));
        Assert.True(state.Observe("second", showOnActivate: false));
        Assert.Equal("second", state.LastKey);
        Assert.False(state.Observe("second", showOnActivate: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FirstRealSongAfterAnEmptySessionStillShows(string? emptyKey)
    {
        var state = new TrackNotificationState();

        Assert.False(state.Observe(emptyKey, showOnActivate: false));
        Assert.True(state.Observe("first-real-song", showOnActivate: false));
    }

    [Fact]
    public void EmptyMetadataGapKeepsKnownTrackAndDoesNotCauseDuplicateToast()
    {
        var state = new TrackNotificationState();

        Assert.True(state.Observe("known-track", showOnActivate: true));
        Assert.False(state.Observe(null, showOnActivate: true));
        Assert.False(state.Observe("", showOnActivate: true));
        Assert.Equal("known-track", state.LastKey);
        Assert.False(state.Observe("known-track", showOnActivate: true));
        Assert.True(state.Observe("next-track", showOnActivate: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReopeningSameTrackAfterResetHonorsActivationSetting(bool showOnActivate)
    {
        var state = new TrackNotificationState();
        state.Observe("same-track", showOnActivate: true);

        state.Reset();

        Assert.Null(state.LastKey);
        Assert.Equal(showOnActivate, state.Observe("same-track", showOnActivate));
        Assert.False(state.Observe("same-track", showOnActivate));
    }

    [Fact]
    public void ManualShowDoesNotProduceAnEmptyTrack()
    {
        var state = new TrackNotificationState();

        Assert.False(state.Observe(null, showOnActivate: true, forceShow: true));
        Assert.Null(state.LastKey);
        Assert.True(state.Observe("first", showOnActivate: false, forceShow: true));
    }
}
