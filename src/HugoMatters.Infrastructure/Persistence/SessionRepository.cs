using HugoMatters.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HugoMatters.Infrastructure.Persistence;

/// <summary>
/// Persists editing sessions with one-active-session-per-site enforcement.
/// </summary>
public sealed class SessionRepository
{
    private readonly HugoMattersDbContext _dbContext;

    /// <summary>
    /// Creates a new <see cref="SessionRepository"/>.
    /// </summary>
    public SessionRepository(HugoMattersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Gets the active session for a site, if any.
    /// </summary>
    public async Task<EditingSession?> GetActiveSessionAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                session => session.SiteId == siteId && session.State == SessionState.Active,
                cancellationToken);
    }

    /// <summary>
    /// Gets a session by id.
    /// </summary>
    public async Task<EditingSession?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);
    }

    /// <summary>
    /// Saves or updates a session, enforcing the single-active-session rule.
    /// </summary>
    public async Task SaveSessionAsync(EditingSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.State == SessionState.Active)
        {
            var conflicting = await _dbContext.Sessions
                .FirstOrDefaultAsync(
                    existing => existing.SiteId == session.SiteId
                        && existing.State == SessionState.Active
                        && existing.Id != session.Id,
                    cancellationToken);

            if (conflicting is not null)
            {
                throw new InvalidOperationException(
                    "An active editing session already exists for this site.");
            }
        }

        var tracked = await _dbContext.Sessions
            .FirstOrDefaultAsync(existing => existing.Id == session.Id, cancellationToken);

        if (tracked is null)
        {
            _dbContext.Sessions.Add(session);
        }
        else
        {
            tracked.State = session.State;
            tracked.HasUnsavedLocalEdits = session.HasUnsavedLocalEdits;
            tracked.UpdatedAt = session.UpdatedAt;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueActiveSessionViolation(ex))
        {
            throw new InvalidOperationException(
                "An active editing session already exists for this site.",
                ex);
        }
    }

    /// <summary>
    /// Deletes session metadata.
    /// </summary>
    public async Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var tracked = await _dbContext.Sessions
            .FirstOrDefaultAsync(session => session.Id == sessionId, cancellationToken);

        if (tracked is null)
        {
            return;
        }

        _dbContext.Sessions.Remove(tracked);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueActiveSessionViolation(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase);
    }
}
