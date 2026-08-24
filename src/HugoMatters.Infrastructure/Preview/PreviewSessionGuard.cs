using System.Collections.Concurrent;

namespace HugoMatters.Infrastructure.Preview;

/// <summary>
/// Tracks per-session preview startup generations so stop/restart cancels in-flight work
/// and stale background tasks cannot overwrite newer state.
/// </summary>
internal sealed class PreviewSessionGuard
{
    private readonly ConcurrentDictionary<Guid, SessionState> _sessions = new();

    /// <summary>
    /// Begins a new startup operation for <paramref name="sessionId"/>, cancelling any prior one.
    /// </summary>
    public (long Generation, CancellationToken CancellationToken) BeginOperation(Guid sessionId)
    {
        var state = _sessions.GetOrAdd(sessionId, static _ => new SessionState());
        return state.Begin();
    }

    /// <summary>
    /// Invalidates the current operation without starting a replacement (for example on stop).
    /// </summary>
    public void Invalidate(Guid sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var state))
        {
            state.Invalidate();
        }
    }

    /// <summary>
    /// Returns whether <paramref name="generation"/> is still the active generation for the session.
    /// </summary>
    public bool IsCurrent(Guid sessionId, long generation) =>
        _sessions.TryGetValue(sessionId, out var state) && state.Generation == generation;

    private sealed class SessionState
    {
        private readonly object _lock = new();
        private CancellationTokenSource _cts = new();

        public long Generation { get; private set; }

        public (long Generation, CancellationToken CancellationToken) Begin()
        {
            lock (_lock)
            {
                InvalidateLocked();
                Generation++;
                _cts = new CancellationTokenSource();
                return (Generation, _cts.Token);
            }
        }

        public void Invalidate()
        {
            lock (_lock)
            {
                InvalidateLocked();
            }
        }

        private void InvalidateLocked()
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            _cts.Dispose();
            Generation++;
            _cts = new CancellationTokenSource();
        }
    }
}
