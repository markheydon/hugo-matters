namespace HugoMatters.Core.Ports;

/// <summary>
/// Information about a GitHub repository.
/// </summary>
public sealed class GitHubRepositoryInfo
{
    /// <summary>Repository owner login.</summary>
    public required string OwnerLogin { get; init; }

    /// <summary>Repository name.</summary>
    public required string RepoName { get; init; }

    /// <summary>Default branch name.</summary>
    public required string DefaultBranch { get; init; }

    /// <summary>HTML URL of the repository.</summary>
    public string? HtmlUrl { get; init; }
}

/// <summary>
/// Information about an open pull request.
/// </summary>
public sealed class GitHubPullRequestInfo
{
    /// <summary>Pull request number.</summary>
    public required int Number { get; init; }

    /// <summary>Pull request HTML URL.</summary>
    public required string HtmlUrl { get; init; }
}

/// <summary>
/// Result of merging a pull request.
/// </summary>
public sealed class GitHubMergeResult
{
    /// <summary>Whether the merge succeeded.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>SHA of the merge commit when successful.</summary>
    public string? MergeCommitSha { get; init; }

    /// <summary>Failure reason when not successful.</summary>
    public string? FailureReason { get; init; }
}

/// <summary>
/// File content retrieved from a GitHub repository.
/// </summary>
public sealed class GitHubFileContent
{
    /// <summary>Repo-relative path.</summary>
    public required string Path { get; init; }

    /// <summary>File content (decoded text).</summary>
    public required string Content { get; init; }

    /// <summary>Blob SHA required for updates and deletes.</summary>
    public required string Sha { get; init; }
}

/// <summary>
/// Entry in a Git tree listing.
/// </summary>
public sealed class GitHubTreeEntry
{
    /// <summary>Repo-relative path.</summary>
    public required string Path { get; init; }

    /// <summary>Entry type (blob, tree).</summary>
    public required string Type { get; init; }

    /// <summary>Object SHA.</summary>
    public required string Sha { get; init; }
}

/// <summary>
/// A file change to include in a commit.
/// </summary>
public sealed class GitHubFileChange
{
    /// <summary>Repo-relative path.</summary>
    public required string Path { get; init; }

    /// <summary>New file content, or <see langword="null"/> to delete.</summary>
    public string? Content { get; init; }

    /// <summary>Existing blob SHA when updating or deleting.</summary>
    public string? Sha { get; init; }
}

/// <summary>
/// Result of creating a commit.
/// </summary>
public sealed class GitHubCommitInfo
{
    /// <summary>Commit SHA.</summary>
    public required string Sha { get; init; }
}

/// <summary>
/// Port for GitHub App repository operations.
/// </summary>
public interface IGitHubRepository
{
    /// <summary>
    /// Obtains a short-lived installation access token.
    /// </summary>
    Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves repository metadata.
    /// </summary>
    Task<GitHubRepositoryInfo> GetRepositoryAsync(
        long installationId,
        string owner,
        string repo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a branch from an existing ref.
    /// </summary>
    Task<string> CreateBranchAsync(
        long installationId,
        string owner,
        string repo,
        string branchName,
        string fromRef,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a pull request.
    /// </summary>
    Task<GitHubPullRequestInfo> CreatePullRequestAsync(
        long installationId,
        string owner,
        string repo,
        string title,
        string head,
        string baseBranch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges an open pull request.
    /// </summary>
    Task<GitHubMergeResult> MergePullRequestAsync(
        long installationId,
        string owner,
        string repo,
        int pullRequestNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes a pull request without merging.
    /// </summary>
    Task ClosePullRequestAsync(
        long installationId,
        string owner,
        string repo,
        int pullRequestNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a branch ref.
    /// </summary>
    Task DeleteBranchAsync(
        long installationId,
        string owner,
        string repo,
        string branchName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads file contents at an optional ref (branch or SHA).
    /// </summary>
    Task<GitHubFileContent?> GetFileContentsAsync(
        long installationId,
        string owner,
        string repo,
        string path,
        string? @ref = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a file on a branch.
    /// </summary>
    Task<GitHubFileContent> PutFileContentsAsync(
        long installationId,
        string owner,
        string repo,
        string path,
        string content,
        string message,
        string branch,
        string? sha = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file on a branch.
    /// </summary>
    Task DeleteFileContentsAsync(
        long installationId,
        string owner,
        string repo,
        string path,
        string message,
        string branch,
        string sha,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists entries in a Git tree.
    /// </summary>
    Task<IReadOnlyList<GitHubTreeEntry>> ListTreeAsync(
        long installationId,
        string owner,
        string repo,
        string treeSha,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a commit with one or more file changes on a branch.
    /// </summary>
    Task<GitHubCommitInfo> CreateCommitAsync(
        long installationId,
        string owner,
        string repo,
        string branch,
        string message,
        IReadOnlyList<GitHubFileChange> changes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the tip commit SHA for a branch.
    /// </summary>
    Task<string> GetBranchTipShaAsync(
        long installationId,
        string owner,
        string repo,
        string branch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a branch has file changes compared to a base branch.
    /// </summary>
    Task<bool> BranchHasChangesAsync(
        long installationId,
        string owner,
        string repo,
        string branch,
        string baseBranch,
        CancellationToken cancellationToken = default);
}
