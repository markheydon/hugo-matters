using System.Collections.Specialized;

namespace HugoMatter.Core.Models;

/// <summary>
/// Post or page representation within an editing session buffer.
/// </summary>
public sealed class ContentItem
{
    /// <summary>Repo-relative path to the content file.</summary>
    public required string Path { get; init; }

    /// <summary>Content type (post or page).</summary>
    public required ContentTypeKind ContentType { get; init; }

    /// <summary>YAML frontmatter fields; unknown keys are preserved.</summary>
    public required OrderedDictionary FrontMatter { get; init; }

    /// <summary>Markdown body after frontmatter.</summary>
    public required string Body { get; set; }

    /// <summary>Whether the item exists on the session branch (false when pending delete).</summary>
    public bool ExistsInSession { get; set; } = true;

    /// <summary>Whether the item is marked deleted in the local buffer.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Whether the item has not yet been saved to the session branch.</summary>
    public bool IsNew { get; set; }

    /// <summary>Whether the item has unsaved local edits in the buffer.</summary>
    public bool HasUnsavedLocalEdits { get; set; }
}
