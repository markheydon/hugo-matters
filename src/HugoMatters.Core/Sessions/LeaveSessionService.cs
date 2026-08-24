using HugoMatters.Core.Api;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;

namespace HugoMatters.Core.Sessions;

/// <summary>
/// Leaves the active local editing session without closing the GitHub pull request.
/// </summary>
public sealed class LeaveSessionService
{
    private readonly IMetadataStore _metadataStore;
    private readonly IContentBufferStore _bufferStore;
    private readonly ISitePreviewOrchestrator _previewOrchestrator;

    /// <summary>
    /// Creates a new <see cref="LeaveSessionService"/>.
    /// </summary>
    public LeaveSessionService(
        IMetadataStore metadataStore,
        IContentBufferStore bufferStore,
        ISitePreviewOrchestrator previewOrchestrator)
    {
        _metadataStore = metadataStore;
        _bufferStore = bufferStore;
        _previewOrchestrator = previewOrchestrator;
    }

    /// <summary>
    /// Ends the local session binding so another session can be resumed or started.
    /// The pull request and branch remain on GitHub.
    /// </summary>
    public async Task<LeaveSessionResult> LeaveAsync(
        LeaveSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
            ?? throw new InvalidOperationException("No site is connected.");

        var session = await _metadataStore.GetActiveSessionAsync(site.Id, cancellationToken)
            ?? throw new InvalidOperationException("No active editing session.");

        if (session.State != SessionState.Active)
        {
            return new LeaveSessionResult
            {
                Outcome = LeaveSessionOutcome.Failed,
                Message = $"Cannot leave a session in state '{session.State}'.",
            };
        }

        if (session.HasUnsavedLocalEdits && !request.ConfirmLeaveUnsaved)
        {
            return new LeaveSessionResult
            {
                Outcome = LeaveSessionOutcome.ConfirmationRequired,
                Message = "Unsaved local edits will be lost from this device. Confirm leave to continue — the pull request stays open on GitHub.",
            };
        }

        try
        {
            try
            {
                await _previewOrchestrator.StopPreviewAsync(session.Id, cancellationToken);
            }
            catch (Exception previewEx) when (previewEx is not OperationCanceledException)
            {
                // Preview cleanup must not block leaving the session.
            }

            await _metadataStore.DeleteSitePreviewAsync(session.Id, cancellationToken);
            await _bufferStore.ClearBufferAsync(session.Id, cancellationToken);

            SessionLifecycle.End(session);
            await _metadataStore.SaveSessionAsync(session, cancellationToken);

            return new LeaveSessionResult
            {
                Outcome = LeaveSessionOutcome.Succeeded,
                Message = "Left the session. The pull request remains open and can be resumed later.",
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new LeaveSessionResult
            {
                Outcome = LeaveSessionOutcome.Failed,
                Message = string.IsNullOrWhiteSpace(ex.Message)
                    ? "Could not leave the session."
                    : $"Could not leave the session. {ex.Message}",
            };
        }
    }
}
