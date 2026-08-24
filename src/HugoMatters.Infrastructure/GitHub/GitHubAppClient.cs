using System.Collections.Concurrent;
using System.Net;
using HugoMatters.Core.Connection;
using HugoMatters.Core.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace HugoMatters.Infrastructure.GitHub;

/// <summary>
/// GitHub App repository operations via Octokit.
/// </summary>
public sealed class GitHubAppClient : IGitHubRepository
{
    private static readonly ProductHeaderValue ProductHeader = new("HugoMatters", "1.0");

    private readonly GitHubAppJwtFactory _jwtFactory;
    private readonly GitHubAppOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GitHubAppClient> _logger;
    private readonly ConcurrentDictionary<long, CachedInstallationToken> _installationTokens = new();

    /// <summary>
    /// Creates a new <see cref="GitHubAppClient"/>.
    /// </summary>
    public GitHubAppClient(
        GitHubAppJwtFactory jwtFactory,
        IOptions<GitHubAppOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<GitHubAppClient> logger)
    {
        _jwtFactory = jwtFactory;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default) =>
        GetInstallationTokenInternalAsync(installationId, cancellationToken);

    /// <inheritdoc />
    public Task<GitHubRepositoryInfo> GetRepositoryAsync(
        long installationId,
        string owner,
        string repo,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            async client =>
            {
                // Custom payload so we can read has_pull_requests (not on Octokit.Repository yet).
                var response = await client.Connection
                    .Get<GitHubRepositoryApiPayload>(ApiUrls.Repository(owner, repo), null)
                    .ConfigureAwait(false);
                var payload = response.Body
                    ?? throw new InvalidOperationException("GitHub returned an empty repository response.");

                if (string.IsNullOrWhiteSpace(payload.Owner?.Login) || string.IsNullOrWhiteSpace(payload.Name))
                {
                    throw new InvalidOperationException("GitHub repository response was missing owner or name.");
                }

                var hasPullRequests = payload.HasPullRequests
                    ?? payload.HasIssues
                    ?? true;

                return new GitHubRepositoryInfo
                {
                    OwnerLogin = payload.Owner.Login,
                    RepoName = payload.Name,
                    DefaultBranch = string.IsNullOrWhiteSpace(payload.DefaultBranch) ? "main" : payload.DefaultBranch,
                    HtmlUrl = payload.HtmlUrl,
                    HasPullRequests = hasPullRequests,
                };
            },
            cancellationToken);

    private sealed class GitHubRepositoryApiPayload
    {
        public GitHubRepositoryOwnerApiPayload? Owner { get; set; }

        public string? Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("default_branch")]
        public string? DefaultBranch { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("has_issues")]
        public bool? HasIssues { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("has_pull_requests")]
        public bool? HasPullRequests { get; set; }
    }

    private sealed class GitHubRepositoryOwnerApiPayload
    {
        public string? Login { get; set; }
    }

    /// <inheritdoc />
    public Task<string> CreateBranchAsync(
        long installationId,
        string owner,
        string repo,
        string branchName,
        string fromRef,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            async client =>
            {
                var reference = await client.Git.Reference.Get(owner, repo, $"heads/{fromRef}");
                var newRef = new NewReference($"refs/heads/{branchName}", reference.Object.Sha);
                var created = await client.Git.Reference.Create(owner, repo, newRef);
                return created.Object.Sha;
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<GitHubPullRequestInfo> CreatePullRequestAsync(
        long installationId,
        string owner,
        string repo,
        string title,
        string head,
        string baseBranch,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            async client =>
            {
                try
                {
                    var pr = await client.PullRequest.Create(
                        owner,
                        repo,
                        new NewPullRequest(title, head, baseBranch));

                    return new GitHubPullRequestInfo
                    {
                        Number = pr.Number,
                        HtmlUrl = pr.HtmlUrl,
                        Title = pr.Title,
                        HeadRef = pr.Head?.Ref,
                        BaseRef = pr.Base?.Ref,
                    };
                }
                catch (ApiValidationException ex)
                {
                    var detail = ex.ApiError?.Message ?? ex.Message;
                    if (ex.ApiError?.Errors is { Count: > 0 })
                    {
                        detail = string.Join(
                            "; ",
                            ex.ApiError.Errors.Select(error => error.Message).Where(static message => !string.IsNullOrWhiteSpace(message)));
                    }

                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(detail)
                            ? "GitHub rejected the pull request (validation failed)."
                            : $"GitHub rejected the pull request: {detail}",
                        ex);
                }
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<GitHubPullRequestInfo>> ListOpenPullRequestsAsync(
        long installationId,
        string owner,
        string repo,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            async client =>
            {
                var pullRequests = await client.PullRequest.GetAllForRepository(
                    owner,
                    repo,
                    new PullRequestRequest
                    {
                        State = ItemStateFilter.Open,
                        SortProperty = PullRequestSort.Updated,
                        SortDirection = SortDirection.Descending,
                    });

                return (IReadOnlyList<GitHubPullRequestInfo>)pullRequests
                    .Select(pr => new GitHubPullRequestInfo
                    {
                        Number = pr.Number,
                        HtmlUrl = pr.HtmlUrl,
                        Title = pr.Title,
                        HeadRef = NormalizeHeadRef(pr.Head?.Ref, owner),
                        BaseRef = pr.Base?.Ref,
                    })
                    .ToList();
            },
            cancellationToken);

    private static string? NormalizeHeadRef(string? headRef, string owner)
    {
        if (string.IsNullOrWhiteSpace(headRef))
        {
            return headRef;
        }

        var prefix = owner + ":";
        return headRef.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? headRef[prefix.Length..]
            : headRef;
    }

    /// <inheritdoc />
    public Task<GitHubMergeResult> MergePullRequestAsync(
        long installationId,
        string owner,
        string repo,
        int pullRequestNumber,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            async client =>
            {
                try
                {
                    var merge = await client.PullRequest.Merge(
                        owner,
                        repo,
                        pullRequestNumber,
                        new MergePullRequest());

                    return new GitHubMergeResult
                    {
                        Succeeded = merge.Merged,
                        MergeCommitSha = merge.Sha,
                        FailureReason = merge.Merged ? null : "Merge was not completed.",
                    };
                }
                catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.MethodNotAllowed
                    or HttpStatusCode.Conflict
                    or HttpStatusCode.UnprocessableEntity)
                {
                    return new GitHubMergeResult
                    {
                        Succeeded = false,
                        FailureReason = "Pull request merge was blocked or conflicted.",
                    };
                }
            },
            cancellationToken);

    /// <inheritdoc />
    public Task ClosePullRequestAsync(
        long installationId,
        string owner,
        string repo,
        int pullRequestNumber,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            client => client.PullRequest.Update(
                owner,
                repo,
                pullRequestNumber,
                new PullRequestUpdate { State = ItemState.Closed }),
            cancellationToken);

    /// <inheritdoc />
    public Task DeleteBranchAsync(
        long installationId,
        string owner,
        string repo,
        string branchName,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            async client =>
            {
                try
                {
                    await client.Git.Reference.Delete(owner, repo, $"heads/{branchName}");
                }
                catch (NotFoundException)
                {
                    // Branch may already be deleted.
                }
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<GitHubFileContent?> GetFileContentsAsync(
        long installationId,
        string owner,
        string repo,
        string path,
        string? @ref = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            async client =>
            {
                try
                {
                    var contents = @ref is null
                        ? await client.Repository.Content.GetAllContents(owner, repo, path)
                        : await client.Repository.Content.GetAllContentsByRef(owner, repo, path, @ref);

                    var file = contents.FirstOrDefault();
                    if (file is null || file.Type != ContentType.File)
                    {
                        return null;
                    }

                    return new GitHubFileContent
                    {
                        Path = file.Path,
                        Content = file.Content,
                        Sha = file.Sha,
                    };
                }
                catch (NotFoundException)
                {
                    return null;
                }
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<GitHubFileContent> PutFileContentsAsync(
        long installationId,
        string owner,
        string repo,
        string path,
        string content,
        string message,
        string branch,
        string? sha = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            async client =>
            {
                RepositoryContentChangeSet result;
                if (string.IsNullOrWhiteSpace(sha))
                {
                    result = await client.Repository.Content.CreateFile(
                        owner,
                        repo,
                        path,
                        new CreateFileRequest(message, content, branch));
                }
                else
                {
                    result = await client.Repository.Content.UpdateFile(
                        owner,
                        repo,
                        path,
                        new UpdateFileRequest(message, content, sha) { Branch = branch });
                }

                return new GitHubFileContent
                {
                    Path = result.Content.Path,
                    Content = content,
                    Sha = result.Content.Sha,
                };
            },
            cancellationToken);

    /// <inheritdoc />
    public Task DeleteFileContentsAsync(
        long installationId,
        string owner,
        string repo,
        string path,
        string message,
        string branch,
        string sha,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            client => client.Repository.Content.DeleteFile(
                owner,
                repo,
                path,
                new DeleteFileRequest(message, sha) { Branch = branch }),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<GitHubTreeEntry>> ListTreeAsync(
        long installationId,
        string owner,
        string repo,
        string treeSha,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<GitHubTreeEntry>>(
            installationId,
            async client =>
            {
                var tree = await client.Git.Tree.GetRecursive(owner, repo, treeSha);
                return tree.Tree.Select(entry => new GitHubTreeEntry
                {
                    Path = entry.Path,
                    Type = entry.Type.StringValue,
                    Sha = entry.Sha,
                }).ToList();
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<GitHubCommitInfo> CreateCommitAsync(
        long installationId,
        string owner,
        string repo,
        string branch,
        string message,
        IReadOnlyList<GitHubFileChange> changes,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            async client =>
            {
                var reference = await client.Git.Reference.Get(owner, repo, $"heads/{branch}");
                var parentSha = reference.Object.Sha;
                var parentCommit = await client.Git.Commit.Get(owner, repo, parentSha);
                var baseTreeSha = parentCommit.Tree.Sha;

                var newTreeRequest = new NewTree { BaseTree = baseTreeSha };
                foreach (var change in changes)
                {
                    if (change.Content is null)
                    {
                        newTreeRequest.Tree.Add(new NewTreeItem
                        {
                            Path = change.Path,
                            Mode = "100644",
                            Type = TreeType.Blob,
                            Sha = null,
                        });
                        continue;
                    }

                    var blob = await client.Git.Blob.Create(
                        owner,
                        repo,
                        new NewBlob { Content = change.Content, Encoding = EncodingType.Utf8 });

                    newTreeRequest.Tree.Add(new NewTreeItem
                    {
                        Path = change.Path,
                        Mode = "100644",
                        Type = TreeType.Blob,
                        Sha = blob.Sha,
                    });
                }

                var newTree = await client.Git.Tree.Create(owner, repo, newTreeRequest);
                var newCommit = await client.Git.Commit.Create(
                    owner,
                    repo,
                    new NewCommit(message, newTree.Sha, parentSha));

                await client.Git.Reference.Update(
                    owner,
                    repo,
                    $"heads/{branch}",
                    new ReferenceUpdate(newCommit.Sha));

                return new GitHubCommitInfo { Sha = newCommit.Sha };
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<string> GetBranchTipShaAsync(
        long installationId,
        string owner,
        string repo,
        string branch,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            async client =>
            {
                var reference = await client.Git.Reference.Get(owner, repo, $"heads/{branch}");
                return reference.Object.Sha;
            },
            cancellationToken);

    /// <inheritdoc />
    public Task<bool> BranchHasChangesAsync(
        long installationId,
        string owner,
        string repo,
        string branch,
        string baseBranch,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            installationId,
            async client =>
            {
                var comparison = await client.Repository.Commit.Compare(owner, repo, baseBranch, branch);
                return comparison.AheadBy > 0;
            },
            cancellationToken);

    private async Task<TResult> ExecuteAsync<TResult>(
        long installationId,
        Func<GitHubClient, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var client = await CreateInstallationClientAsync(installationId, cancellationToken);
            return await action(client);
        }
        catch (ForbiddenException ex)
        {
            // Missing App permission (e.g. Pull requests: write) — not the same as a revoked installation.
            _logger.LogWarning(
                ex,
                "GitHub App is forbidden from performing an operation for installation {InstallationId}: {Message}",
                installationId,
                ex.Message);
            throw new InvalidOperationException(
                "GitHub refused to create a pull request. " +
                "Enable Pull requests on the repository (Settings → General → Features → Pull requests) " +
                "and confirm the App has Pull requests: Read and write.",
                ex);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            _installationTokens.TryRemove(installationId, out _);
            _logger.LogWarning(
                "GitHub API authorization failed for installation {InstallationId} with status {StatusCode}.",
                installationId,
                (int)ex.StatusCode);
            await MarkAccessLostAsync(cancellationToken);
            throw;
        }
    }

    private async Task ExecuteAsync(
        long installationId,
        Func<GitHubClient, Task> action,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            installationId,
            async client =>
            {
                await action(client);
                return true;
            },
            cancellationToken);
    }

    private async Task<GitHubClient> CreateInstallationClientAsync(
        long installationId,
        CancellationToken cancellationToken)
    {
        var token = await GetInstallationTokenInternalAsync(installationId, cancellationToken);
        return new GitHubClient(ProductHeader)
        {
            Credentials = new Credentials(token),
        };
    }

    private async Task<string> GetInstallationTokenInternalAsync(
        long installationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_installationTokens.TryGetValue(installationId, out var cached)
            && cached.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return cached.Token;
        }

        try
        {
            var appClient = CreateAppClient();
            var response = await appClient.GitHubApps.CreateInstallationToken(installationId);
            var expiresAt = response.ExpiresAt == default
                ? DateTimeOffset.UtcNow.AddMinutes(55)
                : response.ExpiresAt;

            _installationTokens[installationId] = new CachedInstallationToken(response.Token, expiresAt);
            return response.Token;
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "GitHub App installation {InstallationId} was not found for the configured App ID. App ID and private key must belong to the same GitHub App as the Web client credentials.",
                installationId);
            throw new InvalidOperationException(
                "GitHub App credentials do not match this installation. Confirm Parameters:github-app-id and Parameters:github-app-private-key-pem are from the same GitHub App as the Client ID used for sign-in (GitHub returns “Integration not found” when they disagree).",
                ex);
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            _logger.LogWarning(
                "GitHub installation token request failed for installation {InstallationId} with status {StatusCode}.",
                installationId,
                (int)ex.StatusCode);
            await MarkAccessLostAsync(cancellationToken);
            throw;
        }
    }

    private GitHubClient CreateAppClient()
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("GitHub App credentials are not configured.");
        }

        var jwt = _jwtFactory.CreateJwt();
        return new GitHubClient(ProductHeader)
        {
            Credentials = new Credentials(jwt, AuthenticationType.Bearer),
        };
    }

    private async Task MarkAccessLostAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var connectionService = scope.ServiceProvider.GetRequiredService<ConnectionService>();
        await connectionService.MarkAccessLostAsync(cancellationToken);
    }

    private sealed record CachedInstallationToken(string Token, DateTimeOffset ExpiresAt);
}
