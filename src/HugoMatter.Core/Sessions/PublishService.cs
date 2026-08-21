using HugoMatter.Core.Api;
using HugoMatter.Core.Models;
using HugoMatter.Core.Ports;

namespace HugoMatter.Core.Sessions;

/// <summary>
/// Publishes an editing session by merging its pull request.
/// </summary>
public sealed class PublishService
{
    private readonly IMetadataStore _metadataStore;
    private readonly IGitHubRepository _gitHubRepository;
    private readonly IContentBufferStore _bufferStore;
    private readonly ISitePreviewOrchestrator _previewOrchestrator;

    /// <summary>
    /// Creates a new <see cref="PublishService"/>.
    /// </summary>
    public PublishService(
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
    /// Merges the session pull request, deletes the branch, and ends the session.
    /// </summary>
    public async Task<PublishResult> PublishAsync(CancellationToken cancellationToken = default)
    {
        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
            ?? throw new InvalidOperationException("No site is connected.");

        var session = await _metadataStore.GetActiveSessionAsync(site.Id, cancellationToken)
            ?? throw new InvalidOperationException("No active editing session.");

        var installationAuthorized = site.Status == SiteStatus.Connected;
        var hasChanges = false;

        if (installationAuthorized)
        {
            try
            {
                hasChanges = await _gitHubRepository.BranchHasChangesAsync(
                    site.InstallationId,
                    site.OwnerLogin,
                    site.RepoName,
                    session.BranchName,
                    session.BaseBranch,
                    cancellationToken);
            }
            catch
            {
                installationAuthorized = false;
            }
        }

        var guard = SessionLifecycle.EvaluatePublish(session, hasChanges, installationAuthorized);
        if (!guard.IsAllowed)
        {
            return new PublishResult
            {
                Outcome = guard.BlockedOutcome!.Value,
                Message = guard.Message!,
            };
        }

        SessionLifecycle.BeginPublishing(session);
        await _metadataStore.SaveSessionAsync(session, cancellationToken);

        try
        {
            var mergeResult = await _gitHubRepository.MergePullRequestAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.PullRequestNumber,
                cancellationToken);

            if (!mergeResult.Succeeded)
            {
                SessionLifecycle.RevertToActive(session);
                await _metadataStore.SaveSessionAsync(session, cancellationToken);

                return new PublishResult
                {
                    Outcome = PublishOutcome.FailedMerge,
                    Message = mergeResult.FailureReason ?? "Merge failed.",
                };
            }

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

            return new PublishResult
            {
                Outcome = PublishOutcome.Succeeded,
                Message = "Session published successfully.",
                MergeCommitSha = mergeResult.MergeCommitSha,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SessionLifecycle.RevertToActive(session);
            await _metadataStore.SaveSessionAsync(session, cancellationToken);

            var outcome = site.Status == SiteStatus.AccessLost
                ? PublishOutcome.FailedAuth
                : PublishOutcome.FailedMerge;

            return new PublishResult
            {
                Outcome = outcome,
                Message = "Publish failed. The session remains active.",
            };
        }
    }
}
