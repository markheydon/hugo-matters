using System.Collections.Concurrent;
using HugoMatters.Core.Ports;

namespace HugoMatters.Infrastructure.Persistence;

/// <summary>
/// In-memory session content buffer store.
/// </summary>
public sealed class InMemoryContentBufferStore : IContentBufferStore
{
    private readonly ConcurrentDictionary<Guid, SessionContentBuffer> _buffers = new();

    /// <inheritdoc />
    public Task<SessionContentBuffer> GetOrCreateBufferAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var buffer = _buffers.GetOrAdd(
            sessionId,
            id => new SessionContentBuffer { SessionId = id });

        return Task.FromResult(buffer);
    }

    /// <inheritdoc />
    public Task SaveBufferAsync(SessionContentBuffer buffer, CancellationToken cancellationToken = default)
    {
        _buffers[buffer.SessionId] = buffer;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearBufferAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        _buffers.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }
}
