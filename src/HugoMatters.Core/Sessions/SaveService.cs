using HugoMatters.Core.Api;
using HugoMatters.Core.Content;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;

namespace HugoMatters.Core.Sessions;

/// <summary>
/// Persists buffered session content as Git commits on the session branch.
/// </summary>
public sealed class SaveService
{
    private readonly IMetadataStore _metadataStore;
    private readonly IGitHubRepository _gitHubRepository;
    private readonly ContentBufferService _contentBuffer;
    private readonly SiteConfigService _siteConfigService;

    /// <summary>
    /// Creates a new <see cref="SaveService"/>.
    /// </summary>
    public SaveService(
        IMetadataStore metadataStore,
        IGitHubRepository gitHubRepository,
        ContentBufferService contentBuffer,
        SiteConfigService siteConfigService)
    {
        _metadataStore = metadataStore;
        _gitHubRepository = gitHubRepository;
        _contentBuffer = contentBuffer;
        _siteConfigService = siteConfigService;
    }

    /// <summary>
    /// Saves all pending buffered changes as a commit on the session branch.
    /// </summary>
    public async Task<SaveResult> SaveAsync(
        SaveRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
            ?? throw new InvalidOperationException("No site is connected.");

        var session = await _metadataStore.GetActiveSessionAsync(site.Id, cancellationToken)
            ?? throw new InvalidOperationException("No active editing session.");

        if (session.State != SessionState.Active)
        {
            throw new InvalidOperationException($"Cannot save a session in state '{session.State}'.");
        }

        var pending = await _contentBuffer.GetPendingChangesAsync(cancellationToken);
        var changes = new List<GitHubFileChange>();

        foreach (var item in pending)
        {
            if (item.IsDeleted)
            {
                var existing = await _gitHubRepository.GetFileContentsAsync(
                    site.InstallationId,
                    site.OwnerLogin,
                    site.RepoName,
                    item.Path,
                    session.BranchName,
                    cancellationToken);

                if (existing is not null)
                {
                    changes.Add(new GitHubFileChange
                    {
                        Path = item.Path,
                        Content = null,
                        Sha = existing.Sha,
                    });
                }

                continue;
            }

            var document = HugoContentDocument.Create(item.FrontMatter, item.Body);
            var serialized = document.Serialize();

            string? sha = null;
            if (!item.IsNew)
            {
                var existing = await _gitHubRepository.GetFileContentsAsync(
                    site.InstallationId,
                    site.OwnerLogin,
                    site.RepoName,
                    item.Path,
                    session.BranchName,
                    cancellationToken);
                sha = existing?.Sha;
            }

            changes.Add(new GitHubFileChange
            {
                Path = item.Path,
                Content = serialized,
                Sha = sha,
            });
        }

        var message = request?.CommitMessage ?? $"Hugo Matters: save session {session.Id:N}";
        GitHubCommitInfo commit;

        if (changes.Count == 0 && session.HasUnsavedLocalEdits)
        {
            commit = new GitHubCommitInfo
            {
                Sha = await _gitHubRepository.GetBranchTipShaAsync(
                    site.InstallationId,
                    site.OwnerLogin,
                    site.RepoName,
                    session.BranchName,
                    cancellationToken),
            };
        }
        else if (changes.Count == 0)
        {
            throw new InvalidOperationException("No changes to save.");
        }
        else
        {
            commit = await _gitHubRepository.CreateCommitAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.BranchName,
                message,
                changes,
                cancellationToken);
        }

        await _contentBuffer.ClearDirtyFlagsAsync(cancellationToken);

        return new SaveResult
        {
            CommitSha = commit.Sha,
            HasUnsavedLocalEdits = false,
        };
    }
}
