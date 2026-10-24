namespace NowPlayingToast;

/// <summary>
/// Coalesces event bursts into a final refresh instead of dropping changes during a read.
/// Start requests on the UI thread to preserve the refresh delegate's synchronization context.
/// </summary>
internal sealed class TrackRefreshQueue : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<bool, CancellationToken, Task> _refresh;
    private readonly Action<Exception> _onError;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _token;
    private TaskCompletionSource? _completion;
    private bool _pending;
    private bool _forceShow;
    private bool _disposed;

    public TrackRefreshQueue(Func<bool, CancellationToken, Task> refresh, Action<Exception> onError)
    {
        _refresh = refresh;
        _onError = onError;
        _token = _lifetime.Token;
    }

    public Task RequestAsync(bool forceShow = false)
    {
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_disposed) return Task.CompletedTask;
            _pending = true;
            _forceShow |= forceShow;
            if (_completion is not null) return _completion.Task;
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion;
        }
        _ = DrainAsync(completion);
        return completion.Task;
    }

    private async Task DrainAsync(TaskCompletionSource completion)
    {
        while (true)
        {
            bool forceShow;
            lock (_gate)
            {
                if (_disposed || !_pending)
                {
                    _completion = null;
                    break;
                }
                forceShow = _forceShow;
                _pending = false;
                _forceShow = false;
            }
            try { await _refresh(forceShow, _token); }
            catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
            catch (Exception ex) { _onError(ex); }
        }
        completion.TrySetResult();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _pending = false;
        }
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
