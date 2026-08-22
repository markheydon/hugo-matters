using System.Text.Json;
using System.Text.Json.Serialization;

namespace HugoMatters.ThemePacks.Models;

/// <summary>
/// JSON document matching <c>contracts/theme-pack-schema.json</c>.
/// </summary>
public sealed class ThemePackDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("compatibility")]
    public CompatibilityDocument? Compatibility { get; set; }

    [JsonPropertyName("contentTypes")]
    public List<ContentTypeDocument> ContentTypes { get; set; } = [];

    [JsonPropertyName("siteConfigFields")]
    public List<FieldDocument>? SiteConfigFields { get; set; }

    [JsonPropertyName("pathConventions")]
    public PathConventionsDocument? PathConventions { get; set; }
}

/// <summary>
/// Compatibility markers used to detect whether a repository matches a pack.
/// </summary>
public sealed class CompatibilityDocument
{
    [JsonPropertyName("themeNames")]
    public List<string>? ThemeNames { get; set; }

    [JsonPropertyName("requiredFiles")]
    public List<string>? RequiredFiles { get; set; }
}

/// <summary>
/// Content type section in a theme pack document.
/// </summary>
public sealed class ContentTypeDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public List<FieldDocument> Fields { get; set; } = [];

    [JsonPropertyName("defaults")]
    public Dictionary<string, JsonElement>? Defaults { get; set; }
}

/// <summary>
/// Field definition in a theme pack document.
/// </summary>
public sealed class FieldDocument
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("default")]
    public JsonElement? DefaultValue { get; set; }

    [JsonPropertyName("options")]
    public List<string>? Options { get; set; }

    [JsonPropertyName("editorWidget")]
    public string? EditorWidget { get; set; }
}

/// <summary>
/// Path conventions for posts and pages within a theme pack document.
/// </summary>
public sealed class PathConventionsDocument
{
    [JsonPropertyName("postsDirectory")]
    public string? PostsDirectory { get; set; }

    [JsonPropertyName("pagesDirectory")]
    public string? PagesDirectory { get; set; }
}
