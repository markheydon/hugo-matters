using HugoMatter.Core.Models;

namespace HugoMatter.Core.Ports;

/// <summary>
/// Local metadata persistence for connected sites, sessions, and previews.
/// </summary>
public interface IMetadataStore
{
    /// <summary>
    /// Gets the single connected site (v1 supports one site).
    /// </summary>
    Task<ConnectedSite?> GetConnectedSiteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates the connected site.
    /// </summary>
    Task SaveConnectedSiteAsync(ConnectedSite site, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the connected site binding.
    /// </summary>
    Task DeleteConnectedSiteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active editing session for a site, if any.
    /// </summary>
    Task<EditingSession?> GetActiveSessionAsync(Guid siteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session by id.
    /// </summary>
    Task<EditingSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates an editing session.
    /// </summary>
    Task SaveSessionAsync(EditingSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes session metadata.
    /// </summary>
    Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets site preview metadata for a session.
    /// </summary>
    Task<SitePreviewInfo?> GetSitePreviewAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates site preview metadata.
    /// </summary>
    Task SaveSitePreviewAsync(SitePreviewInfo preview, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes site preview metadata for a session.
    /// </summary>
    Task DeleteSitePreviewAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
