using HugoMatters.Core.Api;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;

namespace HugoMatters.Core.Sessions;

/// <summary>
/// Discards an editing session by closing its pull request without merging.
/// </summary>
public sealed class DiscardService
{
    private readonly IMetadataStore _metadataStore;
    private readonly IGitHubRepository _gitHubRepository;
    private readonly IContentBufferStore _bufferStore;
    private readonly ISitePreviewOrchestrator _previewOrchestrator;

    /// <summary>
    /// Creates a new <see cref="DiscardService"/>.
    /// </summary>
    public DiscardService(
        IMetadataStore metadataStore,
        IGitHubRepository gitHubRepository,
        IContentBufferStore bufferStore,
        ISitePreviewOrchestrator previewOrchestrator)
    {
        _metadataStore = metadataStore;
        _gitHubRepository = gitHubRepository;
        _bufferStore = bufferStore;
        _previewOrchestrator = previewOrchestrator;
    }

    /// <summary>
    /// Closes the session pull request, deletes the branch, and ends the session.
    /// </summary>
    public async Task<DiscardResult> DiscardAsync(
        DiscardRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
            ?? throw new InvalidOperationException("No site is connected.");

        var session = await _metadataStore.GetActiveSessionAsync(site.Id, cancellationToken)
            ?? throw new InvalidOperationException("No active editing session.");

        var guard = SessionLifecycle.EvaluateDiscard(session, request.ConfirmDiscardUnsaved);
        if (!guard.IsAllowed)
        {
            return new DiscardResult
            {
                Outcome = guard.BlockedOutcome!.Value,
                Message = guard.Message!,
            };
        }

        SessionLifecycle.BeginDiscarding(session);
        await _metadataStore.SaveSessionAsync(session, cancellationToken);

        try
        {
            await _gitHubRepository.ClosePullRequestAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.PullRequestNumber,
                cancellationToken);

            await _gitHubRepository.DeleteBranchAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.BranchName,
                cancellationToken);

            await _previewOrchestrator.StopPreviewAsync(session.Id, cancellationToken);
            await _metadataStore.DeleteSitePreviewAsync(session.Id, cancellationToken);
            await _bufferStore.ClearBufferAsync(session.Id, cancellationToken);

            SessionLifecycle.End(session);
            await _metadataStore.SaveSessionAsync(session, cancellationToken);

            return new DiscardResult
            {
                Outcome = DiscardOutcome.Succeeded,
                Message = "Session discarded successfully.",
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SessionLifecycle.RevertToActive(session);
            await _metadataStore.SaveSessionAsync(session, cancellationToken);

            return new DiscardResult
            {
                Outcome = DiscardOutcome.Failed,
                Message = "Discard failed. The session remains active.",
            };
        }
    }
}
