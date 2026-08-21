namespace HugoMatter.Core.Api;

/// <summary>
/// Result of saving buffered session content to Git.
/// </summary>
public sealed class SaveResult
{
    /// <summary>SHA of the created commit.</summary>
    public required string CommitSha { get; init; }

    /// <summary>Whether unsaved local edits remain after save.</summary>
    public required bool HasUnsavedLocalEdits { get; init; }
}

/// <summary>
/// Result of a publish attempt.
/// </summary>
public sealed class PublishResult
{
    /// <summary>Outcome of the publish operation.</summary>
    public required Models.PublishOutcome Outcome { get; init; }

    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }

    /// <summary>Merge commit SHA when successful.</summary>
    public string? MergeCommitSha { get; init; }
}

/// <summary>
/// Result of a discard attempt.
/// </summary>
public sealed class DiscardResult
{
    /// <summary>Outcome of the discard operation.</summary>
    public required Models.DiscardOutcome Outcome { get; init; }

    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }
}

/// <summary>
/// Request to save buffered content.
/// </summary>
public sealed class SaveRequest
{
    /// <summary>Optional commit message; server supplies a default when omitted.</summary>
    public string? CommitMessage { get; init; }
}

/// <summary>
/// Request to discard a session.
/// </summary>
public sealed class DiscardRequest
{
    /// <summary>Must be true when the session has unsaved local edits.</summary>
    public required bool ConfirmDiscardUnsaved { get; init; }
}

/// <summary>
/// Request to create new content.
/// </summary>
public sealed class ContentCreateRequest
{
    /// <summary>Content type to create.</summary>
    public required Models.ContentTypeKind ContentType { get; init; }

    /// <summary>URL slug for the new item.</summary>
    public required string Slug { get; init; }

    /// <summary>Optional title; defaults may be applied from the theme pack.</summary>
    public string? Title { get; init; }
}

/// <summary>
/// Write payload for upserting content in the session buffer.
/// </summary>
public sealed class ContentItemWrite
{
    /// <summary>Frontmatter fields.</summary>
    public required IReadOnlyDictionary<string, object?> FrontMatter { get; init; }

    /// <summary>Markdown body.</summary>
    public required string Body { get; init; }
}

/// <summary>
/// Request for in-editor HTML preview.
/// </summary>
public sealed class EditorPreviewRequest
{
    /// <summary>Markdown body to preview.</summary>
    public required string Body { get; init; }

    /// <summary>Optional frontmatter (not rendered in v1 preview).</summary>
    public IReadOnlyDictionary<string, object?>? FrontMatter { get; init; }
}

/// <summary>
/// Response containing approximate HTML preview.
/// </summary>
public sealed class EditorPreviewResponse
{
    /// <summary>Rendered HTML.</summary>
    public required string Html { get; init; }
}
