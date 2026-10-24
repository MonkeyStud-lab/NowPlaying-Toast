using NowPlayingToast;
using Xunit;

namespace NowPlayingToast.Tests;

public sealed class TrackRefreshQueueTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task EventsDuringBlockedReadCoalesceAndRefreshLatestTrack()
    {
        var entered = NewSignal();
        var release = NewSignal();
        var observed = new List<string>();
        var errors = new List<Exception>();
        var currentTrack = "first";
        using var queue = new TrackRefreshQueue(async (_, token) =>
        {
            observed.Add(Volatile.Read(ref currentTrack));
            if (observed.Count == 1)
            {
                entered.SetResult();
                await release.Task.WaitAsync(token);
            }
        }, errors.Add);

        var first = queue.RequestAsync();
        await entered.Task.WaitAsync(TestTimeout);
        Volatile.Write(ref currentTrack, "second");
        var second = queue.RequestAsync();
        Volatile.Write(ref currentTrack, "third");
        var third = queue.RequestAsync();
        Volatile.Write(ref currentTrack, "latest");
        var latest = queue.RequestAsync();
        release.SetResult();
        await Task.WhenAll(first, second, third, latest).WaitAsync(TestTimeout);

        Assert.Equal(new[] { "first", "latest" }, observed);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task ManualShowRequestSurvivesSubsequentAutomaticEvents()
    {
        var entered = NewSignal();
        var release = NewSignal();
        var forceFlags = new List<bool>();
        using var queue = new TrackRefreshQueue(async (forceShow, token) =>
        {
            forceFlags.Add(forceShow);
            if (forceFlags.Count == 1)
            {
                entered.SetResult();
                await release.Task.WaitAsync(token);
            }
        }, _ => { });

        var first = queue.RequestAsync();
        await entered.Task.WaitAsync(TestTimeout);
        var manual = queue.RequestAsync(forceShow: true);
        var automatic = queue.RequestAsync();
        var anotherAutomatic = queue.RequestAsync();
        release.SetResult();
        await Task.WhenAll(first, manual, automatic, anotherAutomatic).WaitAsync(TestTimeout);

        Assert.Equal(new[] { false, true }, forceFlags);
    }

    [Fact]
    public async Task FailedReadIsReportedAndDoesNotDropPendingOrFutureRefreshes()
    {
        var entered = NewSignal();
        var release = NewSignal();
        var expectedError = new InvalidOperationException("Media session changed during read.");
        var errors = new List<Exception>();
        var callCount = 0;
        using var queue = new TrackRefreshQueue(async (_, token) =>
        {
            if (Interlocked.Increment(ref callCount) == 1)
            {
                entered.SetResult();
                await release.Task.WaitAsync(token);
                throw expectedError;
            }
        }, errors.Add);

        var first = queue.RequestAsync();
        await entered.Task.WaitAsync(TestTimeout);
        var pending = queue.RequestAsync();
        release.SetResult();
        await Task.WhenAll(first, pending).WaitAsync(TestTimeout);
        await queue.RequestAsync().WaitAsync(TestTimeout);

        Assert.Equal(3, callCount);
        Assert.Same(expectedError, Assert.Single(errors));
    }

    [Fact]
    public async Task DisposeCancelsActiveReadAndPreventsPendingAndLaterWork()
    {
        var entered = NewSignal();
        var observedCancellation = false;
        var callCount = 0;
        var errors = new List<Exception>();
        using var queue = new TrackRefreshQueue(async (_, token) =>
        {
            Interlocked.Increment(ref callCount);
            entered.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                observedCancellation = true;
                throw;
            }
        }, errors.Add);

        var active = queue.RequestAsync();
        await entered.Task.WaitAsync(TestTimeout);
        var pending = queue.RequestAsync(forceShow: true);
        queue.Dispose();
        await Task.WhenAll(active, pending).WaitAsync(TestTimeout);
        await queue.RequestAsync().WaitAsync(TestTimeout);

        Assert.True(observedCancellation);
        Assert.Equal(1, callCount);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task NewRequestAfterDrainStartsAnotherRefreshWithoutKeepingForceFlag()
    {
        var forceFlags = new List<bool>();
        using var queue = new TrackRefreshQueue(async (forceShow, _) =>
        {
            forceFlags.Add(forceShow);
            await Task.Yield();
        }, _ => { });

        await queue.RequestAsync(forceShow: true).WaitAsync(TestTimeout);
        await queue.RequestAsync().WaitAsync(TestTimeout);
        await queue.RequestAsync().WaitAsync(TestTimeout);

        Assert.Equal(new[] { true, false, false }, forceFlags);
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
