using System.Collections.Concurrent;
using HugoMatter.Core.Models;
using HugoMatter.Core.Ports;

namespace HugoMatter.Infrastructure.Persistence;

/// <summary>
/// In-memory metadata store for tests and lightweight scenarios.
/// </summary>
public sealed class InMemoryMetadataStore : IMetadataStore
{
    private ConnectedSite? _site;
    private readonly ConcurrentDictionary<Guid, EditingSession> _sessions = new();
    private readonly ConcurrentDictionary<Guid, SitePreviewInfo> _previews = new();

    /// <inheritdoc />
    public Task<ConnectedSite?> GetConnectedSiteAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_site);

    /// <inheritdoc />
    public Task SaveConnectedSiteAsync(ConnectedSite site, CancellationToken cancellationToken = default)
    {
        _site = site;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteConnectedSiteAsync(CancellationToken cancellationToken = default)
    {
        _site = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<EditingSession?> GetActiveSessionAsync(Guid siteId, CancellationToken cancellationToken = default)
    {
        var session = _sessions.Values.FirstOrDefault(
            s => s.SiteId == siteId && s.State == SessionState.Active);
        return Task.FromResult(session);
    }

    /// <inheritdoc />
    public Task<EditingSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sessions.TryGetValue(sessionId, out var session) ? session : null);

    /// <inheritdoc />
    public Task SaveSessionAsync(EditingSession session, CancellationToken cancellationToken = default)
    {
        _sessions[session.Id] = session;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SitePreviewInfo?> GetSitePreviewAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_previews.TryGetValue(sessionId, out var preview) ? preview : null);

    /// <inheritdoc />
    public Task SaveSitePreviewAsync(SitePreviewInfo preview, CancellationToken cancellationToken = default)
    {
        _previews[preview.SessionId] = preview;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteSitePreviewAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        _previews.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }
}
