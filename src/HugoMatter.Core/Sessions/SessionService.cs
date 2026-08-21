using HugoMatter.Core.Models;
using HugoMatter.Core.Ports;

namespace HugoMatter.Core.Sessions;

/// <summary>
/// Creates editing sessions mapped to a branch and open pull request.
/// </summary>
public sealed class SessionService
{
    private readonly IMetadataStore _metadataStore;
    private readonly IGitHubRepository _gitHubRepository;

    /// <summary>
    /// Branch name prefix for session branches.
    /// </summary>
    public const string BranchPrefix = "hugo-matter/session-";

    /// <summary>
    /// Creates a new <see cref="SessionService"/>.
    /// </summary>
    public SessionService(IMetadataStore metadataStore, IGitHubRepository gitHubRepository)
    {
        _metadataStore = metadataStore;
        _gitHubRepository = gitHubRepository;
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

        var shortId = Guid.NewGuid().ToString("N")[..8];
        var branchName = $"{BranchPrefix}{shortId}";

        await _gitHubRepository.CreateBranchAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            branchName,
            repo.DefaultBranch,
            cancellationToken);

        var prTitle = $"Hugo Matter editing session ({shortId})";
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
}
