using HugoMatter.Core.Models;
using HugoMatter.Core.Ports;
using HugoMatter.ThemePacks.Packs;

namespace HugoMatter.ThemePacks;

/// <summary>
/// Registry of product-owned theme packs shipped as embedded resources.
/// </summary>
public sealed class ThemePackRegistry : IThemePackRegistry
{
    private readonly Dictionary<string, ThemePackDefinition> _packsById;

    /// <summary>
    /// Creates a registry with the built-in Hugo Profile pack.
    /// </summary>
    public ThemePackRegistry()
    {
        var profileJson = LoadEmbeddedResource(HugoProfileThemePack.EmbeddedResourceName);
        var profileDocument = HugoProfileThemePack.DeserializeDocument(profileJson);
        var profileDefinition = HugoProfileThemePack.Bind(profileDocument);

        _packsById = new Dictionary<string, ThemePackDefinition>(StringComparer.Ordinal)
        {
            [profileDefinition.Id] = profileDefinition,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<ThemePackSummary> ListPacks() =>
        _packsById.Values
            .Select(ToSummary)
            .OrderBy(pack => pack.Id, StringComparer.Ordinal)
            .ToList();

    /// <inheritdoc />
    public ThemePackSummary? GetPack(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var pack = _packsById.GetValueOrDefault(id);
        return pack is null ? null : ToSummary(pack);
    }

    /// <inheritdoc />
    public ThemePackDefinition? GetPackDetail(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _packsById.GetValueOrDefault(id);
    }

    /// <summary>
    /// Probes repository signals against a registered pack.
    /// </summary>
    public ThemePackCompatibilityResult? ProbeCompatibility(string id, ThemePackProbeContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(context);

        var pack = GetPackDetail(id);
        if (pack is null)
        {
            return null;
        }

        return HugoProfileThemePack.ProbeCompatibility(pack, context);
    }

    private static ThemePackSummary ToSummary(ThemePackDefinition pack) => new()
    {
        Id = pack.Id,
        Version = pack.Version,
        DisplayName = pack.DisplayName,
    };

    private static string LoadEmbeddedResource(string resourceName)
    {
        var assembly = typeof(ThemePackRegistry).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded theme pack resource '{resourceName}' was not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
