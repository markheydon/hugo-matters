using HugoMatters.Core.Ports;

namespace HugoMatters.Core.GitHub;

/// <summary>
/// Shared validation for GitHub repository capabilities Hugo Matters requires.
/// </summary>
public static class GitHubRepositoryRequirements
{
    /// <summary>
    /// User-facing explanation when pull requests are disabled on the repository.
    /// </summary>
    public static string PullRequestsRequiredMessage(string ownerLogin, string repoName) =>
        $"Pull requests are disabled for '{ownerLogin}/{repoName}'. " +
        "Hugo Matters starts each editing session as a branch and pull request, so that feature must be on. " +
        $"Enable it under https://github.com/{ownerLogin}/{repoName}/settings " +
        "(General → Features → Pull requests), then try again.";

    /// <summary>
    /// Ensures the repository can host pull requests.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when pull requests are disabled.</exception>
    public static void EnsurePullRequestsAvailable(GitHubRepositoryInfo repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        if (!repository.HasPullRequests)
        {
            throw new InvalidOperationException(PullRequestsRequiredMessage(repository.OwnerLogin, repository.RepoName));
        }
    }
}
