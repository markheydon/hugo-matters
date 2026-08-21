using System.Collections;
using System.Collections.Specialized;
using HugoMatter.Core.Models;
using HugoMatter.Core.Ports;
using HugoMatter.Core.Security;

namespace HugoMatter.Core.Content;

/// <summary>
/// Manages in-session content buffer operations for posts and pages.
/// </summary>
public sealed class ContentBufferService
{
    private readonly IContentBufferStore _bufferStore;
    private readonly IMetadataStore _metadataStore;
    private readonly IThemePackRegistry _themePackRegistry;
    private readonly IGitHubRepository _gitHubRepository;

    /// <summary>
    /// Creates a new <see cref="ContentBufferService"/>.
    /// </summary>
    public ContentBufferService(
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
    /// Lists content items visible in the active session buffer.
    /// </summary>
    public async Task<IReadOnlyList<ContentItemSummary>> ListAsync(
        ContentTypeKind? contentTypeFilter,
        CancellationToken cancellationToken = default)
    {
        var (session, site, pack) = await GetActiveContextAsync(cancellationToken);
        var buffer = await EnsureBufferLoadedAsync(session, site, pack, cancellationToken);

        var items = buffer.Items.Values
            .Where(i => contentTypeFilter is null || i.ContentType == contentTypeFilter)
            .Where(i => !i.IsDeleted)
            .Select(ToSummary)
            .OrderBy(i => i.Path, StringComparer.Ordinal)
            .ToList();

        return items;
    }

    /// <summary>
    /// Gets a content item from the buffer, loading from Git when not buffered.
    /// </summary>
    public async Task<ContentItem?> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        var (session, site, pack) = await GetActiveContextAsync(cancellationToken);
        var allowed = GetAllowedDirectories(pack);
        var normalized = ContentPathValidator.ValidateAndNormalize(path, allowed);

        var buffer = await EnsureBufferLoadedAsync(session, site, pack, cancellationToken);
        if (buffer.Items.TryGetValue(normalized, out var buffered))
        {
            return buffered;
        }

        return null;
    }

    /// <summary>
    /// Creates a new content item in the session buffer using theme-pack defaults.
    /// </summary>
    public async Task<ContentItem> CreateAsync(
        ContentTypeKind contentType,
        string slug,
        string? title,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var (session, site, pack) = await GetActiveContextAsync(cancellationToken);
        var contentTypeDef = GetContentTypeDefinition(pack, contentType);
        var directory = contentType == ContentTypeKind.Post ? pack.PostsDirectory : pack.PagesDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"Theme pack '{pack.Id}' does not define a directory for {contentType}.");
        }

        var path = ContentPathValidator.ValidateAndNormalize(
            $"{directory.TrimEnd('/')}/{slug}.md",
            GetAllowedDirectories(pack));

        var buffer = await EnsureBufferLoadedAsync(session, site, pack, cancellationToken);
        if (buffer.Items.ContainsKey(path))
        {
            throw new InvalidOperationException($"Content already exists at '{path}'.");
        }

        var frontMatter = new OrderedDictionary(StringComparer.Ordinal);
        if (contentTypeDef.Defaults is not null)
        {
            foreach (var (key, value) in contentTypeDef.Defaults)
            {
                frontMatter[key] = value;
            }
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            frontMatter["title"] = title;
        }
        else if (!frontMatter.Contains("title"))
        {
            frontMatter["title"] = slug;
        }

        var item = new ContentItem
        {
            Path = path,
            ContentType = contentType,
            FrontMatter = frontMatter,
            Body = string.Empty,
            IsNew = true,
            HasUnsavedLocalEdits = true,
            ExistsInSession = false,
        };

        buffer.Items[path] = item;
        await MarkDirtyAsync(buffer, session, cancellationToken);
        return item;
    }

    /// <summary>
    /// Upserts content in the session buffer (not Git until save).
    /// </summary>
    public async Task<ContentItem> UpsertAsync(
        string path,
        IReadOnlyDictionary<string, object?> frontMatter,
        string body,
        CancellationToken cancellationToken = default)
    {
        var (session, site, pack) = await GetActiveContextAsync(cancellationToken);
        var allowed = GetAllowedDirectories(pack);
        var normalized = ContentPathValidator.ValidateAndNormalize(path, allowed);

        var buffer = await EnsureBufferLoadedAsync(session, site, pack, cancellationToken);
        var contentType = InferContentType(normalized, pack);

        var orderedFrontMatter = new OrderedDictionary(StringComparer.Ordinal);
        foreach (var (key, value) in frontMatter)
        {
            orderedFrontMatter[key] = value;
        }

        if (buffer.Items.TryGetValue(normalized, out var existing))
        {
            existing.FrontMatter.Clear();
            foreach (DictionaryEntry entry in orderedFrontMatter)
            {
                existing.FrontMatter[entry.Key] = entry.Value;
            }

            existing.Body = body;
            existing.IsDeleted = false;
            existing.HasUnsavedLocalEdits = true;
            await MarkDirtyAsync(buffer, session, cancellationToken);
            return existing;
        }

        var item = new ContentItem
        {
            Path = normalized,
            ContentType = contentType,
            FrontMatter = orderedFrontMatter,
            Body = body,
            IsNew = true,
            HasUnsavedLocalEdits = true,
            ExistsInSession = false,
        };

        buffer.Items[normalized] = item;
        await MarkDirtyAsync(buffer, session, cancellationToken);
        return item;
    }

    /// <summary>
    /// Marks content as deleted in the session buffer.
    /// </summary>
    public async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var (session, site, pack) = await GetActiveContextAsync(cancellationToken);
        var allowed = GetAllowedDirectories(pack);
        var normalized = ContentPathValidator.ValidateAndNormalize(path, allowed);

        var buffer = await EnsureBufferLoadedAsync(session, site, pack, cancellationToken);
        if (!buffer.Items.TryGetValue(normalized, out var item))
        {
            var loaded = await TryLoadFromGitAsync(session, site, normalized, pack, cancellationToken);
            if (loaded is null)
            {
                return false;
            }

            item = loaded;
            buffer.Items[normalized] = item;
        }

        item.IsDeleted = true;
        item.HasUnsavedLocalEdits = true;
        await MarkDirtyAsync(buffer, session, cancellationToken);
        return true;
    }

    /// <summary>
    /// Returns all buffered items with pending changes for save.
    /// </summary>
    public async Task<IReadOnlyList<ContentItem>> GetPendingChangesAsync(CancellationToken cancellationToken = default)
    {
        var (session, site, pack) = await GetActiveContextAsync(cancellationToken);
        var buffer = await EnsureBufferLoadedAsync(session, site, pack, cancellationToken);
        return buffer.Items.Values.Where(i => i.HasUnsavedLocalEdits).ToList();
    }

    /// <summary>
    /// Clears dirty flags on buffered items after a successful save.
    /// </summary>
    public async Task ClearDirtyFlagsAsync(CancellationToken cancellationToken = default)
    {
        var (session, _, pack) = await GetActiveContextAsync(cancellationToken);
        var buffer = await _bufferStore.GetOrCreateBufferAsync(session.Id, cancellationToken);

        foreach (var item in buffer.Items.Values)
        {
            if (item.IsDeleted)
            {
                buffer.Items.Remove(item.Path);
                continue;
            }

            item.HasUnsavedLocalEdits = false;
            item.IsNew = false;
            item.ExistsInSession = true;
        }

        buffer.HasUnsavedEdits = false;
        await _bufferStore.SaveBufferAsync(buffer, cancellationToken);

        session.HasUnsavedLocalEdits = false;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _metadataStore.SaveSessionAsync(session, cancellationToken);
    }

    private async Task<SessionContentBuffer> EnsureBufferLoadedAsync(
        EditingSession session,
        ConnectedSite site,
        ThemePackDefinition pack,
        CancellationToken cancellationToken)
    {
        var buffer = await _bufferStore.GetOrCreateBufferAsync(session.Id, cancellationToken);
        if (buffer.Items.Count > 0)
        {
            return buffer;
        }

        await LoadTreeFromGitAsync(buffer, session, site, pack, cancellationToken);
        return buffer;
    }

    private async Task LoadTreeFromGitAsync(
        SessionContentBuffer buffer,
        EditingSession session,
        ConnectedSite site,
        ThemePackDefinition pack,
        CancellationToken cancellationToken)
    {
        var tipSha = await _gitHubRepository.GetBranchTipShaAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            session.BranchName,
            cancellationToken);

        var entries = await _gitHubRepository.ListTreeAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            tipSha,
            cancellationToken);

        var allowed = GetAllowedDirectories(pack);
        foreach (var entry in entries.Where(e => e.Type == "blob" && e.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)))
        {
            if (!ContentPathValidator.IsUnderAllowedDirectory(entry.Path, allowed))
            {
                continue;
            }

            var file = await _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                entry.Path,
                session.BranchName,
                cancellationToken);

            if (file is null)
            {
                continue;
            }

            var document = HugoContentDocument.Parse(file.Content);
            buffer.Items[entry.Path] = new ContentItem
            {
                Path = entry.Path,
                ContentType = InferContentType(entry.Path, pack),
                FrontMatter = document.FrontMatter,
                Body = document.Body,
                ExistsInSession = true,
            };
        }

        await _bufferStore.SaveBufferAsync(buffer, cancellationToken);
    }

    private async Task<ContentItem?> TryLoadFromGitAsync(
        EditingSession session,
        ConnectedSite site,
        string path,
        ThemePackDefinition pack,
        CancellationToken cancellationToken)
    {
        var file = await _gitHubRepository.GetFileContentsAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            path,
            session.BranchName,
            cancellationToken);

        if (file is null)
        {
            return null;
        }

        var document = HugoContentDocument.Parse(file.Content);
        return new ContentItem
        {
            Path = path,
            ContentType = InferContentType(path, pack),
            FrontMatter = document.FrontMatter,
            Body = document.Body,
            ExistsInSession = true,
        };
    }

    private async Task MarkDirtyAsync(
        SessionContentBuffer buffer,
        EditingSession session,
        CancellationToken cancellationToken)
    {
        buffer.HasUnsavedEdits = true;
        await _bufferStore.SaveBufferAsync(buffer, cancellationToken);

        if (!session.HasUnsavedLocalEdits)
        {
            session.HasUnsavedLocalEdits = true;
            session.UpdatedAt = DateTimeOffset.UtcNow;
            await _metadataStore.SaveSessionAsync(session, cancellationToken);
        }
    }

    private async Task<(EditingSession Session, ConnectedSite Site, ThemePackDefinition Pack)> GetActiveContextAsync(
        CancellationToken cancellationToken)
    {
        var site = await _metadataStore.GetConnectedSiteAsync(cancellationToken)
            ?? throw new InvalidOperationException("No site is connected.");

        if (site.Status != SiteStatus.Connected)
        {
            throw new InvalidOperationException("Site is not connected.");
        }

        var session = await _metadataStore.GetActiveSessionAsync(site.Id, cancellationToken)
            ?? throw new InvalidOperationException("No active editing session.");

        if (session.State != SessionState.Active)
        {
            throw new InvalidOperationException($"Session is not active (state: {session.State}).");
        }

        var pack = _themePackRegistry.GetPackDetail(site.ThemePackId)
            ?? throw new InvalidOperationException($"Theme pack '{site.ThemePackId}' was not found.");

        return (session, site, pack);
    }

    private static IReadOnlyList<string> GetAllowedDirectories(ThemePackDefinition pack) =>
        ContentPathValidator.BuildAllowedDirectories(pack.PostsDirectory, pack.PagesDirectory);

    private static ContentTypeDefinition GetContentTypeDefinition(ThemePackDefinition pack, ContentTypeKind contentType)
    {
        var id = contentType == ContentTypeKind.Post ? "post" : "page";
        return pack.ContentTypes.FirstOrDefault(ct => ct.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Theme pack does not define content type '{id}'.");
    }

    private static ContentTypeKind InferContentType(string path, ThemePackDefinition pack)
    {
        if (!string.IsNullOrWhiteSpace(pack.PostsDirectory)
            && path.StartsWith(pack.PostsDirectory.TrimEnd('/') + "/", StringComparison.Ordinal))
        {
            return ContentTypeKind.Post;
        }

        return ContentTypeKind.Page;
    }

    private static ContentItemSummary ToSummary(ContentItem item)
    {
        var title = item.FrontMatter.Contains("title")
            ? item.FrontMatter["title"]?.ToString() ?? item.Path
            : item.Path;

        return new ContentItemSummary
        {
            Path = item.Path,
            ContentType = item.ContentType,
            Title = title,
            IsDeleted = item.IsDeleted,
            IsNew = item.IsNew,
        };
    }
}
