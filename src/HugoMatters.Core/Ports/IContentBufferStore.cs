using System.Collections.Specialized;
using HugoMatters.Core.Models;

namespace HugoMatters.Core.Ports;

/// <summary>
/// Summary of a content item for list views.
/// </summary>
public sealed class ContentItemSummary
{
    /// <summary>Repo-relative path.</summary>
    public required string Path { get; init; }

    /// <summary>Content type.</summary>
    public required ContentTypeKind ContentType { get; init; }

    /// <summary>Display title from frontmatter.</summary>
    public required string Title { get; init; }

    /// <summary>Whether marked deleted in the buffer.</summary>
    public bool IsDeleted { get; init; }

    /// <summary>Whether not yet saved to the session branch.</summary>
    public bool IsNew { get; init; }

    /// <summary>Whether the buffer has local edits not yet saved to the session branch.</summary>
    public bool HasUnsavedLocalEdits { get; init; }
}

/// <summary>
/// In-memory buffer state for an editing session.
/// </summary>
public sealed class SessionContentBuffer
{
    /// <summary>Session this buffer belongs to.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>Buffered content items keyed by normalized path.</summary>
    public Dictionary<string, ContentItem> Items { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Buffered site configuration values (allowlisted keys only).</summary>
    public OrderedDictionary SiteConfig { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Whether any buffered item or site config has unsaved edits.</summary>
    public bool HasUnsavedEdits { get; set; }
}

/// <summary>
/// Port for in-session content buffer storage.
/// </summary>
public interface IContentBufferStore
{
    /// <summary>
    /// Loads or creates the buffer for a session.
    /// </summary>
    Task<SessionContentBuffer> GetOrCreateBufferAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists buffer state.
    /// </summary>
    Task SaveBufferAsync(SessionContentBuffer buffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all buffered state for a session.
    /// </summary>
    Task ClearBufferAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
