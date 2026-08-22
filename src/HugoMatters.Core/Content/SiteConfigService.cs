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

        if (buffer.SiteConfig.Count == 0)
        {
            await LoadFromGitAsync(buffer, session, site, pack, cancellationToken);
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
        var file = await _gitHubRepository.GetFileContentsAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            DefaultConfigPath,
            session.BranchName,
            cancellationToken);

        if (file is null)
        {
            foreach (var field in pack.SiteConfigFields)
            {
                if (field.Default is not null)
                {
                    buffer.SiteConfig[field.Key] = field.Default;
                }
            }

            return;
        }

        var parsed = ParseSimpleTomlParams(file.Content, pack.SiteConfigFields.Select(f => f.Key));
        foreach (DictionaryEntry entry in parsed)
        {
            buffer.SiteConfig[entry.Key] = entry.Value;
        }
    }

    private static OrderedDictionary ParseSimpleTomlParams(string content, IEnumerable<string> allowedKeys)
    {
        var result = new OrderedDictionary(StringComparer.Ordinal);
        var allowed = allowedKeys.ToHashSet(StringComparer.Ordinal);
        var inParams = false;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line == "[params]")
            {
                inParams = true;
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inParams = false;
                continue;
            }

            if (!inParams || !line.Contains('='))
            {
                continue;
            }

            var eqIndex = line.IndexOf('=');
            var key = line[..eqIndex].Trim().Trim('"');
            if (!allowed.Contains(key))
            {
                continue;
            }

            var value = line[(eqIndex + 1)..].Trim().Trim('"');
            result[key] = value;
        }

        return result;
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
