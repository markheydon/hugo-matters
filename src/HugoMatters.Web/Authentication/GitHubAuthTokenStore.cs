using System.Collections.Concurrent;

namespace HugoMatters.Web.Authentication;

/// <summary>
/// Server-side store for GitHub OAuth tokens keyed by an opaque session id (not placed in the auth cookie).
/// </summary>
public sealed class GitHubAuthTokenStore
{
    private readonly ConcurrentDictionary<string, GitHubAuthSession> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new(StringComparer.Ordinal);

    public string StoreSession(GitHubAuthSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var sessionKey = Guid.NewGuid().ToString("N");
        _sessions[sessionKey] = session;
        return sessionKey;
    }

    public GitHubAuthSession? TryGetSession(string? sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return null;
        }

        return _sessions.TryGetValue(sessionKey, out var session) ? session : null;
    }

    public void UpdateSession(string sessionKey, GitHubAuthSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionKey);
        ArgumentNullException.ThrowIfNull(session);
        _sessions[sessionKey] = session;
    }

    public void RemoveSession(string? sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return;
        }

        _sessions.TryRemove(sessionKey, out _);
        _refreshLocks.TryRemove(sessionKey, out var lockObj);
        lockObj?.Dispose();
    }

    internal SemaphoreSlim GetRefreshLock(string sessionKey) =>
        _refreshLocks.GetOrAdd(sessionKey, _ => new SemaphoreSlim(1, 1));
}
