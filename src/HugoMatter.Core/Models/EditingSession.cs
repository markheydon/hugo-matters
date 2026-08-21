namespace HugoMatter.Core.Models;

/// <summary>
/// Unit of editorial work mapped to one branch and one open pull request.
/// </summary>
public sealed class EditingSession
{
    /// <summary>Local primary key.</summary>
    public required Guid Id { get; init; }

    /// <summary>Connected site this session belongs to.</summary>
    public required Guid SiteId { get; init; }

    /// <summary>Session branch name (e.g. <c>hugo-matter/session-abc123</c>).</summary>
    public required string BranchName { get; init; }

    /// <summary>Open pull request number against the base branch.</summary>
    public required int PullRequestNumber { get; init; }

    /// <summary>URL of the open pull request.</summary>
    public string? PullRequestUrl { get; init; }

    /// <summary>Repository default branch at session start.</summary>
    public required string BaseBranch { get; init; }

    /// <summary>Current session lifecycle state.</summary>
    public SessionState State { get; set; } = SessionState.Active;

    /// <summary>Whether the local content buffer has unsaved edits.</summary>
    public bool HasUnsavedLocalEdits { get; set; }

    /// <summary>When the session was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the session was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
