using System.Collections;
using System.Collections.Specialized;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;

namespace HugoMatters.Core.Content;

/// <summary>
/// Reads and writes minimal site configuration in the session buffer with theme-pack allowlists.
/// </summary>
public sealed class SiteConfigService
{
    private readonly IContentBufferStore _bufferStore;
    private readonly IMetadataStore _metadataStore;
    private readonly IThemePackRegistry _themePackRegistry;
    private readonly IGitHubRepository _gitHubRepository;

    /// <summary>
    /// Default Hugo config file path used when the pack does not specify otherwise.
    /// </summary>
    public const string DefaultConfigPath = "hugo.toml";

    private static readonly string[] ConfigCandidatePaths =
    [
        "hugo.toml",
        "hugo.yaml",
        "hugo.yml",
        "config.toml",
        "config.yaml",
        "config.yml",
    ];

    /// <summary>
    /// Creates a new <see cref="SiteConfigService"/>.
    /// </summary>
    public SiteConfigService(
        IContentBufferStore bufferStore,
        IMetadataStore metadataStore,
        IThemePackRegistry themePackRegistry,
        IGitHubRepository gitHubRepository)
    {
        _bufferStore = bufferStore;
        _metadataStore = metadataStore;
        _themePackRegistry = themePackRegistry;
        _gitHubRepository = gitHubRepository;
    }

    /// <summary>
    /// Gets allowlisted site configuration values for the active session.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, object?>> GetAsync(CancellationToken cancellationToken = default)
    {
        var (session, site, pack) = await GetActiveContextAsync(cancellationToken);
        var buffer = await _bufferStore.GetOrCreateBufferAsync(session.Id, cancellationToken);

        // Refresh when empty, missing title, or buffered under obsolete pack keys.
        if (!buffer.HasUnsavedEdits && NeedsConfigReload(buffer))
        {
            buffer.SiteConfig.Clear();
            await LoadFromGitAsync(buffer, session, site, pack, cancellationToken);
            await _bufferStore.SaveBufferAsync(buffer, cancellationToken);
        }

        return buffer.SiteConfig
            .Cast<DictionaryEntry>()
            .ToDictionary(e => e.Key.ToString()!, e => e.Value);
    }

    /// <summary>
    /// Updates allowlisted site configuration values in the session buffer.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, object?>> UpdateAsync(
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default)
    {
        var (session, site, pack) = await GetActiveContextAsync(cancellationToken);
        var allowedKeys = pack.SiteConfigFields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var key in values.Keys)
        {
            if (!allowedKeys.Contains(key))
            {
                throw new ArgumentException($"Site config key '{key}' is not allowed by theme pack '{pack.Id}'.", nameof(values));
            }
        }

        var buffer = await _bufferStore.GetOrCreateBufferAsync(session.Id, cancellationToken);
        if (buffer.SiteConfig.Count == 0)
        {
            await LoadFromGitAsync(buffer, session, site, pack, cancellationToken);
        }

        foreach (var (key, value) in values)
        {
            buffer.SiteConfig[key] = value;
        }

        buffer.HasUnsavedEdits = true;
        await _bufferStore.SaveBufferAsync(buffer, cancellationToken);

        session.HasUnsavedLocalEdits = true;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _metadataStore.SaveSessionAsync(session, cancellationToken);

        return buffer.SiteConfig
            .Cast<DictionaryEntry>()
            .ToDictionary(e => e.Key.ToString()!, e => e.Value);
    }

    /// <summary>
    /// Returns buffered site config entries that differ from the saved branch state.
    /// </summary>
    public async Task<OrderedDictionary> GetBufferedConfigAsync(CancellationToken cancellationToken = default)
    {
        var (session, _, pack) = await GetActiveContextAsync(cancellationToken);
        var buffer = await _bufferStore.GetOrCreateBufferAsync(session.Id, cancellationToken);
        if (buffer.SiteConfig.Count == 0)
        {
            var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
                ?? throw new InvalidOperationException("No site is connected.");
            await LoadFromGitAsync(buffer, session, site, pack, cancellationToken);
        }

        return buffer.SiteConfig;
    }

    private async Task LoadFromGitAsync(
        SessionContentBuffer buffer,
        EditingSession session,
        ConnectedSite site,
        ThemePackDefinition pack,
        CancellationToken cancellationToken)
    {
        GitHubFileContent? file = null;
        foreach (var candidate in ConfigCandidatePaths)
        {
            file = await _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                candidate,
                session.BranchName,
                cancellationToken);

            if (file is not null)
            {
                break;
            }
        }

        if (file is null)
        {
            ApplyPackDefaults(buffer, pack);
            return;
        }

        var keyMap = BuildSourceKeyMap(pack.SiteConfigFields);
        var parsed = file.Path.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)
            ? ParseSimpleToml(file.Content, keyMap)
            : ParseSimpleYaml(file.Content, keyMap);

        foreach (DictionaryEntry entry in parsed)
        {
            buffer.SiteConfig[entry.Key] = entry.Value;
        }

        foreach (var field in pack.SiteConfigFields)
        {
            if (field.Default is not null && !buffer.SiteConfig.Contains(field.Key))
            {
                buffer.SiteConfig[field.Key] = field.Default;
            }
        }
    }

    private static bool NeedsConfigReload(SessionContentBuffer buffer) =>
        buffer.SiteConfig.Count == 0
        || !buffer.SiteConfig.Contains("title")
        || (buffer.SiteConfig.Contains("languageCode") && !buffer.SiteConfig.Contains("locale"))
        || (buffer.SiteConfig.Contains("params.hero.intro")
            && !buffer.SiteConfig.Contains("params.hero.content"));

    /// <summary>
    /// Maps config source keys (including aliases) to the theme-pack canonical field key.
    /// </summary>
    internal static Dictionary<string, string> BuildSourceKeyMap(IEnumerable<FieldDefinition> fields)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            map[field.Key] = field.Key;
            foreach (var sourceKey in field.SourceKeys)
            {
                map[sourceKey] = field.Key;
            }
        }

        return map;
    }

    private static void ApplyPackDefaults(SessionContentBuffer buffer, ThemePackDefinition pack)
    {
        foreach (var field in pack.SiteConfigFields)
        {
            if (field.Default is not null)
            {
                buffer.SiteConfig[field.Key] = field.Default;
            }
        }
    }

    /// <summary>
    /// Parses allowlisted keys from Hugo TOML, including root keys and nested tables
    /// such as <c>[params.hero]</c> → <c>params.hero.title</c>.
    /// </summary>
    internal static OrderedDictionary ParseSimpleToml(string content, IReadOnlyDictionary<string, string> sourceKeyMap)
    {
        var result = new OrderedDictionary(StringComparer.Ordinal);
        string? tablePath = "";

        foreach (var rawLine in content.Split('\n'))
        {
            var line = StripTomlComment(rawLine.Trim());
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var inner = line[1..^1].Trim();
                if (inner.StartsWith('['))
                {
                    tablePath = null;
                    continue;
                }

                tablePath = inner.Trim('"');
                continue;
            }

            if (tablePath is null || !line.Contains('='))
            {
                continue;
            }

            var eqIndex = line.IndexOf('=');
            var key = line[..eqIndex].Trim().Trim('"');
            var fullKey = string.IsNullOrEmpty(tablePath) ? key : $"{tablePath}.{key}";

            if (!TryResolveCanonicalKey(sourceKeyMap, fullKey, tablePath, key, out var canonicalKey))
            {
                continue;
            }

            result[canonicalKey] = UnquoteTomlValue(line[(eqIndex + 1)..].Trim());
        }

        return result;
    }

    /// <summary>
    /// Parses allowlisted dotted keys from a simple Hugo YAML config (maps + scalars only).
    /// </summary>
    internal static OrderedDictionary ParseSimpleYaml(string content, IReadOnlyDictionary<string, string> sourceKeyMap)
    {
        var result = new OrderedDictionary(StringComparer.Ordinal);
        var stack = new List<(int Indent, string Path)>();

        foreach (var rawLine in content.Split('\n'))
        {
            var line = StripYamlComment(rawLine);
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('-'))
            {
                continue;
            }

            var indent = CountLeadingSpaces(line);
            var trimmed = line.Trim();
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..colonIndex].Trim();
            var valuePart = trimmed[(colonIndex + 1)..].Trim();

            while (stack.Count > 0 && stack[^1].Indent >= indent)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            var parentPath = stack.Count == 0 ? "" : stack[^1].Path;
            var fullPath = string.IsNullOrEmpty(parentPath) ? key : $"{parentPath}.{key}";

            if (string.IsNullOrEmpty(valuePart) || valuePart is "{" or "[")
            {
                stack.Add((indent, fullPath));
                continue;
            }

            if (!sourceKeyMap.TryGetValue(fullPath, out var canonicalKey))
            {
                continue;
            }

            result[canonicalKey] = UnquoteYamlValue(valuePart);
        }

        return result;
    }

    private static bool TryResolveCanonicalKey(
        IReadOnlyDictionary<string, string> sourceKeyMap,
        string fullKey,
        string? tablePath,
        string key,
        out string canonicalKey)
    {
        if (sourceKeyMap.TryGetValue(fullKey, out canonicalKey!))
        {
            return true;
        }

        // Legacy flat keys under [params], e.g. params.hero.title = "..."
        if (string.Equals(tablePath, "params", StringComparison.Ordinal)
            && sourceKeyMap.TryGetValue(key, out canonicalKey!))
        {
            return true;
        }

        canonicalKey = "";
        return false;
    }

    private static string StripTomlComment(string line)
    {
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }
            else if (c == '#' && !inQuotes)
            {
                return line[..i].TrimEnd();
            }
        }

        return line;
    }

    private static string StripYamlComment(string line)
    {
        var inQuotes = false;
        char? quote = null;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote is null && (c is '"' or '\''))
            {
                quote = c;
                inQuotes = true;
            }
            else if (inQuotes && c == quote)
            {
                inQuotes = false;
                quote = null;
            }
            else if (c == '#' && !inQuotes)
            {
                return line[..i].TrimEnd();
            }
        }

        return line;
    }

    private static string UnquoteTomlValue(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
        {
            return value[1..^1];
        }

        if (value.Length >= 2 && value.StartsWith('\'') && value.EndsWith('\''))
        {
            return value[1..^1];
        }

        return value;
    }

    private static string UnquoteYamlValue(string value)
    {
        value = value.Trim();
        if (value.Equals("~", StringComparison.Ordinal) || value.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return UnquoteTomlValue(value);
    }

    private static int CountLeadingSpaces(string line)
    {
        var count = 0;
        foreach (var c in line)
        {
            if (c == ' ')
            {
                count++;
            }
            else if (c == '\t')
            {
                count += 2;
            }
            else
            {
                break;
            }
        }

        return count;
    }

    private async Task<(EditingSession Session, ConnectedSite Site, ThemePackDefinition Pack)> GetActiveContextAsync(
        CancellationToken cancellationToken)
    {
        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
            ?? throw new InvalidOperationException("No site is connected.");

        var session = await _metadataStore.GetActiveSessionAsync(site.Id, cancellationToken)
            ?? throw new InvalidOperationException("No active editing session.");

        var pack = _themePackRegistry.GetPackDetail(site.ThemePackId)
            ?? throw new InvalidOperationException($"Theme pack '{site.ThemePackId}' was not found.");

        return (session, site, pack);
    }
}
