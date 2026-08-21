using HugoMatter.Core.Models;
using HugoMatter.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace HugoMatter.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IMetadataStore"/>.
/// </summary>
public sealed class EfMetadataStore : IMetadataStore
{
    private readonly HugoMatterDbContext _dbContext;
    private readonly SessionRepository _sessionRepository;

    /// <summary>
    /// Creates a new <see cref="EfMetadataStore"/>.
    /// </summary>
    public EfMetadataStore(HugoMatterDbContext dbContext, SessionRepository sessionRepository)
    {
        _dbContext = dbContext;
        _sessionRepository = sessionRepository;
    }

    /// <inheritdoc />
    public async Task<ConnectedSite?> GetConnectedSiteAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sites
            .AsNoTracking()
            .OrderByDescending(site => site.ConnectedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveConnectedSiteAsync(ConnectedSite site, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(site);

        var existing = await _dbContext.Sites.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            _dbContext.Sites.Add(site);
        }
        else
        {
            _dbContext.Sites.Remove(existing);
            _dbContext.Sites.Add(site);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteConnectedSiteAsync(CancellationToken cancellationToken = default)
    {
        var sites = await _dbContext.Sites.ToListAsync(cancellationToken);
        if (sites.Count == 0)
        {
            return;
        }

        _dbContext.Sites.RemoveRange(sites);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<EditingSession?> GetActiveSessionAsync(Guid siteId, CancellationToken cancellationToken = default) =>
        _sessionRepository.GetActiveSessionAsync(siteId, cancellationToken);

    /// <inheritdoc />
    public Task<EditingSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _sessionRepository.GetSessionAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task SaveSessionAsync(EditingSession session, CancellationToken cancellationToken = default) =>
        _sessionRepository.SaveSessionAsync(session, cancellationToken);

    /// <inheritdoc />
    public Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _sessionRepository.DeleteSessionAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public async Task<SitePreviewInfo?> GetSitePreviewAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Previews
            .AsNoTracking()
            .FirstOrDefaultAsync(preview => preview.SessionId == sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveSitePreviewAsync(SitePreviewInfo preview, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var tracked = await _dbContext.Previews
            .FirstOrDefaultAsync(existing => existing.SessionId == preview.SessionId, cancellationToken);

        if (tracked is null)
        {
            _dbContext.Previews.Add(preview);
        }
        else
        {
            tracked.Status = preview.Status;
            tracked.BaseUrl = preview.BaseUrl;
            tracked.WorkspacePath = preview.WorkspacePath;
            tracked.SourceRef = preview.SourceRef;
            tracked.ErrorMessage = preview.ErrorMessage;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteSitePreviewAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var tracked = await _dbContext.Previews
            .FirstOrDefaultAsync(preview => preview.SessionId == sessionId, cancellationToken);

        if (tracked is null)
        {
            return;
        }

        _dbContext.Previews.Remove(tracked);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
