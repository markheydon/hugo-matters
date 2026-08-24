using HugoMatters.Core.GitHub;
using HugoMatters.Core.Ports;

namespace HugoMatters.Core.Tests.GitHub;

public sealed class GitHubRepositoryRequirementsTests
{
    [Fact]
    public void EnsurePullRequestsAvailable_AllowsWhenPullRequestsEnabled()
    {
        var repo = new GitHubRepositoryInfo
        {
            OwnerLogin = "owner",
            RepoName = "site",
            DefaultBranch = "main",
            HasPullRequests = true,
        };

        GitHubRepositoryRequirements.EnsurePullRequestsAvailable(repo);
    }

    [Fact]
    public void EnsurePullRequestsAvailable_ThrowsClearMessageWhenPullRequestsDisabled()
    {
        var repo = new GitHubRepositoryInfo
        {
            OwnerLogin = "markheydon",
            RepoName = "turpinverse-richard-turpin",
            DefaultBranch = "main",
            HasPullRequests = false,
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GitHubRepositoryRequirements.EnsurePullRequestsAvailable(repo));

        Assert.Contains("Pull requests are disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Features → Pull requests", ex.Message, StringComparison.Ordinal);
        Assert.Contains("settings", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
