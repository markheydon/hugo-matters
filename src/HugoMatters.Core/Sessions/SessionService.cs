using HugoMatters.Core.Api;
using HugoMatters.Core.Connection;
using HugoMatters.Core.GitHub;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;

namespace HugoMatters.Core.Sessions;

/// <summary>
/// Creates editing sessions mapped to a branch and open pull request.
/// </summary>
public sealed class SessionService
{
    private readonly IMetadataStore _metadataStore;
    private readonly IGitHubRepository _gitHubRepository;
    private readonly ConnectionService _connectionService;

    /// <summary>
    /// Branch name prefix for session branches.
    /// </summary>
    public const string BranchPrefix = "hugo-matters/session-";

    /// <summary>
    /// Creates a new <see cref="SessionService"/>.
    /// </summary>
    public SessionService(
        IMetadataStore metadataStore,
        IGitHubRepository gitHubRepository,
        ConnectionService connectionService)
    {
        _metadataStore = metadataStore;
        _gitHubRepository = gitHubRepository;
        _connectionService = connectionService;
    }

    /// <summary>
    /// Gets the active session for the connected site, if any.
    /// </summary>
    public async Task<EditingSession?> GetActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken);
        if (site is null)
        {
            return null;
        }

        return await _metadataStore.GetActiveSessionAsync(site.Id, cancellationToken);
    }

    /// <summary>
    /// Starts a new editing session with a branch and pull request.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when prerequisites are not met.</exception>
    public async Task<EditingSession> StartSessionAsync(CancellationToken cancellationToken = default)
    {
        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
            ?? throw new InvalidOperationException("No site is connected.");

        if (site.Status == SiteStatus.Disconnected)
        {
            throw new InvalidOperationException($"Site is not connected (status: {site.Status}).");
        }

        // AccessLost may be a stale flag from a permission error; verify GitHub access and restore.
        if (site.Status == SiteStatus.AccessLost)
        {
            await _gitHubRepository.GetRepositoryAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                cancellationToken);
            await _connectionService.RestoreConnectedStatusAsync(cancellationToken);
            site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
                ?? throw new InvalidOperationException("No site is connected.");
        }

        if (site.Status != SiteStatus.Connected)
        {
            throw new InvalidOperationException($"Site is not connected (status: {site.Status}).");
        }

        var existing = await _metadataStore.GetActiveSessionAsync(site.Id, cancellationToken);
        if (existing is not null && existing.State == SessionState.Active)
        {
            throw new InvalidOperationException("An active editing session already exists.");
        }

        var repo = await _gitHubRepository.GetRepositoryAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            cancellationToken);

        GitHubRepositoryRequirements.EnsurePullRequestsAvailable(repo);

        var shortId = Guid.NewGuid().ToString("N")[..8];
        var branchName = $"{BranchPrefix}{shortId}";

        await _gitHubRepository.CreateBranchAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            branchName,
            repo.DefaultBranch,
            cancellationToken);

        // GitHub rejects pull requests when head and base point at the same commit.
        // A small marker file under .hugo-matters/ creates a non-empty diff; it appears in the session PR.
        await _gitHubRepository.CreateCommitAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            branchName,
            $"Start Hugo Matters editing session ({shortId})",
            [
                new GitHubFileChange
                {
                    Path = ".hugo-matters/session",
                    Content = $"session={shortId}\ncreated={DateTimeOffset.UtcNow:O}\n",
                },
            ],
            cancellationToken);

        var prTitle = $"Hugo Matters editing session ({shortId})";
        var pr = await _gitHubRepository.CreatePullRequestAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            prTitle,
            branchName,
            repo.DefaultBranch,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var session = new EditingSession
        {
            Id = Guid.NewGuid(),
            SiteId = site.Id,
            BranchName = branchName,
            PullRequestNumber = pr.Number,
            PullRequestUrl = pr.HtmlUrl,
            BaseBranch = repo.DefaultBranch,
            State = SessionState.Active,
            HasUnsavedLocalEdits = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _metadataStore.SaveSessionAsync(session, cancellationToken);
        return session;
    }

    /// <summary>
    /// Lists open Hugo Matters pull requests that can be resumed after reconnect or local session loss.
    /// </summary>
    public async Task<IReadOnlyList<ResumableSessionInfo>> ListResumableSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
            ?? throw new InvalidOperationException("No site is connected.");

        if (site.Status != SiteStatus.Connected && site.Status != SiteStatus.AccessLost)
        {
            return [];
        }

        var pullRequests = await _gitHubRepository.ListOpenPullRequestsAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            cancellationToken);

        return pullRequests
            .Where(pr =>
                !string.IsNullOrWhiteSpace(pr.HeadRef)
                && pr.HeadRef.StartsWith(BranchPrefix, StringComparison.Ordinal))
            .Select(pr => new ResumableSessionInfo
            {
                PullRequestNumber = pr.Number,
                PullRequestUrl = pr.HtmlUrl,
                Title = pr.Title,
                BranchName = pr.HeadRef!,
                BaseBranch = string.IsNullOrWhiteSpace(pr.BaseRef) ? "main" : pr.BaseRef,
            })
            .ToList();
    }

    /// <summary>
    /// Resumes a local editing session from an existing open Hugo Matters pull request.
    /// </summary>
    public async Task<EditingSession> ResumeSessionAsync(
        int pullRequestNumber,
        CancellationToken cancellationToken = default)
    {
        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
            ?? throw new InvalidOperationException("No site is connected.");

        if (site.Status == SiteStatus.AccessLost)
        {
            await _gitHubRepository.GetRepositoryAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                cancellationToken);
            await _connectionService.RestoreConnectedStatusAsync(cancellationToken);
            site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
                ?? throw new InvalidOperationException("No site is connected.");
        }

        if (site.Status != SiteStatus.Connected)
        {
            throw new InvalidOperationException($"Site is not connected (status: {site.Status}).");
        }

        var existing = await _metadataStore.GetActiveSessionAsync(site.Id, cancellationToken);
        if (existing is not null && existing.State == SessionState.Active)
        {
            throw new InvalidOperationException("An active editing session already exists.");
        }

        var resumable = (await ListResumableSessionsAsync(cancellationToken))
            .FirstOrDefault(item => item.PullRequestNumber == pullRequestNumber)
            ?? throw new InvalidOperationException(
                $"No open Hugo Matters pull request #{pullRequestNumber} was found to resume.");

        // Ensure the branch still exists before binding the local session.
        _ = await _gitHubRepository.GetBranchTipShaAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            resumable.BranchName,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var session = new EditingSession
        {
            Id = Guid.NewGuid(),
            SiteId = site.Id,
            BranchName = resumable.BranchName,
            PullRequestNumber = resumable.PullRequestNumber,
            PullRequestUrl = resumable.PullRequestUrl,
            BaseBranch = resumable.BaseBranch,
            State = SessionState.Active,
            HasUnsavedLocalEdits = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _metadataStore.SaveSessionAsync(session, cancellationToken);
        return session;
    }
}
