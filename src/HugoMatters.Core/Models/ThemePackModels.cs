namespace HugoMatters.Core.Models;

/// <summary>
/// Definition of a single frontmatter or site-config field in a theme pack.
/// </summary>
public sealed class FieldDefinition
{
    /// <summary>Frontmatter or config key.</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable editor label.</summary>
    public required string Label { get; init; }

    /// <summary>Data type (string, markdown, boolean, datetime, stringList, select).</summary>
    public required string DataType { get; init; }

    /// <summary>Whether the field is required.</summary>
    public bool Required { get; init; }

    /// <summary>Default value when creating new content.</summary>
    public object? Default { get; init; }

    /// <summary>
    /// Alternate config keys that map into <see cref="Key"/> when reading Hugo config
    /// (e.g. <c>languageCode</c> → <c>locale</c>).
    /// </summary>
    public IReadOnlyList<string> SourceKeys { get; init; } = [];

    /// <summary>
    /// Field origin for UI grouping: <c>hugo</c> (core site config) or <c>theme</c> (theme-pack params).
    /// </summary>
    public string Scope { get; init; } = "theme";

    /// <summary>UI primitive hint (text, textarea, toggle, date, tags, select).</summary>
    public string? EditorWidget { get; init; }

    /// <summary>Allowed values for select fields.</summary>
    public IReadOnlyList<string>? Options { get; init; }
}

/// <summary>
/// Theme-pack definition for a content type (post or page).
/// </summary>
public sealed class ContentTypeDefinition
{
    /// <summary>Content type identifier (<c>post</c> or <c>page</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable label.</summary>
    public required string Label { get; init; }

    /// <summary>Field definitions for this content type.</summary>
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }

    /// <summary>Default frontmatter values applied on create.</summary>
    public IReadOnlyDictionary<string, object?>? Defaults { get; init; }
}

/// <summary>
/// Summary metadata for a theme pack.
/// </summary>
public class ThemePackSummary
{
    /// <summary>Stable pack identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Pack definition version (semver).</summary>
    public required string Version { get; init; }

    /// <summary>Display name for the UI.</summary>
    public required string DisplayName { get; init; }
}

/// <summary>
/// Full theme pack definition including content types and site config fields.
/// </summary>
public sealed class ThemePackDefinition : ThemePackSummary
{
    /// <summary>Supported content types and their field schemas.</summary>
    public required IReadOnlyList<ContentTypeDefinition> ContentTypes { get; init; }

    /// <summary>Minimal site configuration fields exposed in the editor.</summary>
    public IReadOnlyList<FieldDefinition> SiteConfigFields { get; init; } = [];

    /// <summary>Repo-relative directory for posts.</summary>
    public string? PostsDirectory { get; init; }

    /// <summary>Repo-relative directory for pages.</summary>
    public string? PagesDirectory { get; init; }

    /// <summary>Hugo theme names that identify this pack.</summary>
    public IReadOnlyList<string>? ThemeNames { get; init; }

    /// <summary>Repo-relative paths that suggest compatibility with this pack.</summary>
    public IReadOnlyList<string>? RequiredFiles { get; init; }
}
