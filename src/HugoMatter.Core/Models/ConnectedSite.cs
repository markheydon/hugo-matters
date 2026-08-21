namespace HugoMatter.Core.Models;

/// <summary>
/// Association between the local app and one GitHub repository.
/// </summary>
public sealed class ConnectedSite
{
    /// <summary>Local primary key.</summary>
    public required Guid Id { get; init; }

    /// <summary>GitHub App installation identifier.</summary>
    public required long InstallationId { get; init; }

    /// <summary>Repository owner login.</summary>
    public required string OwnerLogin { get; init; }

    /// <summary>Repository name.</summary>
    public required string RepoName { get; init; }

    /// <summary>Cached default branch from GitHub (publish target).</summary>
    public required string DefaultBranch { get; init; }

    /// <summary>Convenience URL to the repository on GitHub.</summary>
    public string? HtmlUrl { get; init; }

    /// <summary>Applied theme pack identifier (e.g. <c>hugo-profile</c>).</summary>
    public required string ThemePackId { get; init; }

    /// <summary>Applied theme pack version.</summary>
    public string? ThemePackVersion { get; init; }

    /// <summary>When the site was connected.</summary>
    public DateTimeOffset ConnectedAt { get; init; }

    /// <summary>Current connection status.</summary>
    public SiteStatus Status { get; init; } = SiteStatus.Connected;
}
