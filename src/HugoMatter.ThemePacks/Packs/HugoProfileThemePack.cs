using System.Text.Json;
using System.Text.RegularExpressions;
using HugoMatter.Core.Models;
using HugoMatter.ThemePacks.Models;

namespace HugoMatter.ThemePacks.Packs;

/// <summary>
/// Binder and compatibility probe for the Hugo Profile theme pack.
/// </summary>
public static class HugoProfileThemePack
{
    /// <summary>Stable pack identifier.</summary>
    public const string PackId = "hugo-profile";

    /// <summary>Embedded resource name for the pack JSON definition.</summary>
    public const string EmbeddedResourceName = "HugoMatter.ThemePacks.Packs.hugo-profile.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Regex ThemeLinePattern = new(
        @"^\s*theme\s*[:=]\s*['""]?([^'""\s#]+)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>
    /// Deserializes a theme pack JSON document.
    /// </summary>
    public static ThemePackDocument DeserializeDocument(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var document = JsonSerializer.Deserialize<ThemePackDocument>(json, JsonOptions);
        if (document is null)
        {
            throw new InvalidOperationException("Theme pack JSON did not deserialize to a document.");
        }

        return document;
    }

    /// <summary>
    /// Binds a JSON document to a <see cref="ThemePackDefinition"/>.
    /// </summary>
    public static ThemePackDefinition Bind(ThemePackDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new ThemePackDefinition
        {
            Id = document.Id,
            Version = document.Version,
            DisplayName = document.DisplayName,
            ContentTypes = document.ContentTypes.Select(BindContentType).ToList(),
            SiteConfigFields = document.SiteConfigFields?.Select(BindField).ToList() ?? [],
            PostsDirectory = document.PathConventions?.PostsDirectory,
            PagesDirectory = document.PathConventions?.PagesDirectory,
            ThemeNames = document.Compatibility?.ThemeNames,
            RequiredFiles = document.Compatibility?.RequiredFiles,
        };
    }

    /// <summary>
    /// Binds embedded or raw JSON to a <see cref="ThemePackDefinition"/>.
    /// </summary>
    public static ThemePackDefinition BindFromJson(string json) => Bind(DeserializeDocument(json));

    /// <summary>
    /// Probes whether repository signals suggest compatibility with the Hugo Profile pack.
    /// </summary>
    public static ThemePackCompatibilityResult ProbeCompatibility(
        ThemePackDefinition pack,
        ThemePackProbeContext context)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(context);

        var themeNames = pack.ThemeNames ?? [];
        var requiredFiles = pack.RequiredFiles ?? [];

        if (!string.IsNullOrWhiteSpace(context.HugoConfigContent))
        {
            var theme = ExtractThemeName(context.HugoConfigContent);
            if (theme is not null &&
                themeNames.Any(name => string.Equals(name, theme, StringComparison.OrdinalIgnoreCase)))
            {
                return new ThemePackCompatibilityResult
                {
                    IsCompatible = true,
                    Reason = $"Hugo theme '{theme}' matches the Hugo Profile pack.",
                };
            }
        }

        foreach (var marker in requiredFiles)
        {
            if (HasRepositoryMarker(context.RepositoryFilePaths, marker))
            {
                return new ThemePackCompatibilityResult
                {
                    IsCompatible = true,
                    Reason = $"Repository contains compatibility marker '{marker}'.",
                };
            }
        }

        return new ThemePackCompatibilityResult
        {
            IsCompatible = false,
            Reason = "No Hugo Profile theme markers were found in the repository.",
        };
    }

    private static ContentTypeDefinition BindContentType(ContentTypeDocument contentType)
    {
        var defaults = contentType.Defaults is null
            ? null
            : contentType.Defaults.ToDictionary(
                pair => pair.Key,
                pair => ConvertJsonElement(pair.Value),
                StringComparer.Ordinal);

        return new ContentTypeDefinition
        {
            Id = contentType.Id,
            Label = contentType.Label,
            Fields = contentType.Fields.Select(BindField).ToList(),
            Defaults = defaults,
        };
    }

    private static FieldDefinition BindField(FieldDocument field) => new()
    {
        Key = field.Key,
        Label = field.Label,
        DataType = field.DataType,
        Required = field.Required,
        Default = field.DefaultValue is { } value ? ConvertJsonElement(value) : null,
        Options = field.Options,
        EditorWidget = field.EditorWidget,
    };

    private static object? ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                {
                    return longValue;
                }

                return element.GetDouble();
            case JsonValueKind.Array:
                return element.EnumerateArray()
                    .Select(ConvertJsonElement)
                    .ToList();
            case JsonValueKind.Object:
                return element.EnumerateObject()
                    .ToDictionary(
                        property => property.Name,
                        property => ConvertJsonElement(property.Value),
                        StringComparer.Ordinal);
            default:
                return element.GetRawText();
        }
    }

    private static string? ExtractThemeName(string hugoConfigContent)
    {
        var match = ThemeLinePattern.Match(hugoConfigContent);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static bool HasRepositoryMarker(IReadOnlyList<string> repositoryFilePaths, string marker)
    {
        foreach (var path in repositoryFilePaths)
        {
            if (path.Equals(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (path.StartsWith(marker + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Input for theme pack compatibility probing.
/// </summary>
public sealed class ThemePackProbeContext
{
    /// <summary>Repo-relative file paths discovered in the repository.</summary>
    public IReadOnlyList<string> RepositoryFilePaths { get; init; } = [];

    /// <summary>Contents of <c>hugo.toml</c> or <c>hugo.yaml</c> when available.</summary>
    public string? HugoConfigContent { get; init; }
}

/// <summary>
/// Outcome of a theme pack compatibility probe.
/// </summary>
public sealed class ThemePackCompatibilityResult
{
    /// <summary>Whether the repository appears compatible with the pack.</summary>
    public required bool IsCompatible { get; init; }

    /// <summary>Human-readable explanation of the probe outcome.</summary>
    public string? Reason { get; init; }
}
