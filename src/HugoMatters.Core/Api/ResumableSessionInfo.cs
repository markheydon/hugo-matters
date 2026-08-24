namespace HugoMatters.Core.Api;

/// <summary>
/// An open Hugo Matters pull request that can be resumed as a local editing session.
/// </summary>
public sealed class ResumableSessionInfo
{
    /// <summary>Pull request number.</summary>
    public required int PullRequestNumber { get; init; }

    /// <summary>Pull request HTML URL.</summary>
    public required string PullRequestUrl { get; init; }

    /// <summary>Pull request title.</summary>
    public string? Title { get; init; }

    /// <summary>Session branch name.</summary>
    public required string BranchName { get; init; }

    /// <summary>Base branch the PR targets.</summary>
    public required string BaseBranch { get; init; }
}
