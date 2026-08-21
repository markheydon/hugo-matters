using HugoMatter.Core.Models;

namespace HugoMatter.Core.Ports;

/// <summary>
/// Port for orchestrating real Hugo site previews in an isolated workspace.
/// </summary>
public interface ISitePreviewOrchestrator
{
    /// <summary>
    /// Starts a site preview from the saved session branch tip.
    /// </summary>
    Task<SitePreviewInfo> StartPreviewAsync(
        EditingSession session,
        ConnectedSite site,
        string branchTipSha,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current preview status for a session.
    /// </summary>
    Task<SitePreviewInfo?> GetStatusAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the preview and cleans up the workspace.
    /// </summary>
    Task StopPreviewAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
