using HugoMatter.Core.Models;
using HugoMatter.Core.Ports;

namespace HugoMatter.Core.Connection;

/// <summary>
/// Connects and disconnects a Hugo site repository via GitHub App installation.
/// </summary>
public sealed class ConnectionService
{
    private readonly IMetadataStore _metadataStore;
    private readonly IGitHubRepository _gitHubRepository;
    private readonly IThemePackRegistry _themePackRegistry;

    /// <summary>
    /// Default theme pack applied on connect when none is specified.
    /// </summary>
    public const string DefaultThemePackId = "hugo-profile";

    /// <summary>
    /// Creates a new <see cref="ConnectionService"/>.
    /// </summary>
    public ConnectionService(
        IMetadataStore metadataStore,
        IGitHubRepository gitHubRepository,
        IThemePackRegistry themePackRegistry)
    {
        _metadataStore = metadataStore;
        _gitHubRepository = gitHubRepository;
        _themePackRegistry = themePackRegistry;
    }

    /// <summary>
    /// Gets the currently connected site, if any.
    /// </summary>
    public Task<ConnectedSite?> GetConnectedSiteAsync(CancellationToken cancellationToken = default) =>
        _metadataStore.GetConnectedSiteAsync(cancellationToken);

    /// <summary>
    /// Connects a GitHub repository using an App installation.
    /// </summary>
    public async Task<ConnectedSite> ConnectAsync(
        long installationId,
        string ownerLogin,
        string repoName,
        string? themePackId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerLogin);
        ArgumentException.ThrowIfNullOrWhiteSpace(repoName);

        var packId = themePackId ?? DefaultThemePackId;
        var pack = _themePackRegistry.GetPackDetail(packId)
            ?? throw new ArgumentException($"Theme pack '{packId}' was not found.", nameof(themePackId));

        var repo = await _gitHubRepository.GetRepositoryAsync(
            installationId,
            ownerLogin,
            repoName,
            cancellationToken);

        var existing = await _metadataStore.GetConnectedSiteAsync(cancellationToken);
        if (existing is not null)
        {
            await _metadataStore.DeleteConnectedSiteAsync(cancellationToken);
        }

        var site = new ConnectedSite
        {
            Id = Guid.NewGuid(),
            InstallationId = installationId,
            OwnerLogin = repo.OwnerLogin,
            RepoName = repo.RepoName,
            DefaultBranch = repo.DefaultBranch,
            HtmlUrl = repo.HtmlUrl,
            ThemePackId = pack.Id,
            ThemePackVersion = pack.Version,
            ConnectedAt = DateTimeOffset.UtcNow,
            Status = SiteStatus.Connected,
        };

        await _metadataStore.SaveConnectedSiteAsync(site, cancellationToken);
        return site;
    }

    /// <summary>
    /// Disconnects the local site binding without uninstalling the GitHub App.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken);
        if (site is null)
        {
            return;
        }

        var session = await _metadataStore.GetActiveSessionAsync(site.Id, cancellationToken);
        if (session is not null && session.State == SessionState.Active)
        {
            throw new InvalidOperationException(
                "Cannot disconnect while an active editing session exists. End the session first.");
        }

        var disconnected = new ConnectedSite
        {
            Id = site.Id,
            InstallationId = site.InstallationId,
            OwnerLogin = site.OwnerLogin,
            RepoName = site.RepoName,
            DefaultBranch = site.DefaultBranch,
            HtmlUrl = site.HtmlUrl,
            ThemePackId = site.ThemePackId,
            ThemePackVersion = site.ThemePackVersion,
            ConnectedAt = site.ConnectedAt,
            Status = SiteStatus.Disconnected,
        };
        await _metadataStore.SaveConnectedSiteAsync(disconnected, cancellationToken);
        await _metadataStore.DeleteConnectedSiteAsync(cancellationToken);
    }

    /// <summary>
    /// Marks the connected site as having lost GitHub App access.
    /// </summary>
    public async Task MarkAccessLostAsync(CancellationToken cancellationToken = default)
    {
        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken);
        if (site is null)
        {
            return;
        }

        var updated = new ConnectedSite
        {
            Id = site.Id,
            InstallationId = site.InstallationId,
            OwnerLogin = site.OwnerLogin,
            RepoName = site.RepoName,
            DefaultBranch = site.DefaultBranch,
            HtmlUrl = site.HtmlUrl,
            ThemePackId = site.ThemePackId,
            ThemePackVersion = site.ThemePackVersion,
            ConnectedAt = site.ConnectedAt,
            Status = SiteStatus.AccessLost,
        };

        await _metadataStore.SaveConnectedSiteAsync(updated, cancellationToken);
    }

    /// <summary>
    /// Refreshes cached repository metadata for the connected site.
    /// </summary>
    public async Task<ConnectedSite> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
            ?? throw new InvalidOperationException("No site is connected.");

        var repo = await _gitHubRepository.GetRepositoryAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            cancellationToken);

        var updated = new ConnectedSite
        {
            Id = site.Id,
            InstallationId = site.InstallationId,
            OwnerLogin = repo.OwnerLogin,
            RepoName = repo.RepoName,
            DefaultBranch = repo.DefaultBranch,
            HtmlUrl = repo.HtmlUrl,
            ThemePackId = site.ThemePackId,
            ThemePackVersion = site.ThemePackVersion,
            ConnectedAt = site.ConnectedAt,
            Status = SiteStatus.Connected,
        };

        await _metadataStore.SaveConnectedSiteAsync(updated, cancellationToken);
        return updated;
    }
}
