using System.Collections.Specialized;
using HugoMatter.Core.Api;
using HugoMatter.Core.Content;
using HugoMatter.Core.Models;
using HugoMatter.Core.Ports;
using HugoMatter.Core.Sessions;
using NSubstitute;

namespace HugoMatter.Core.Tests.Sessions;

public class SaveServiceTests
{
    private readonly IMetadataStore _metadataStore = Substitute.For<IMetadataStore>();
    private readonly IGitHubRepository _gitHubRepository = Substitute.For<IGitHubRepository>();
    private readonly IContentBufferStore _bufferStore = Substitute.For<IContentBufferStore>();
    private readonly IThemePackRegistry _themePackRegistry = Substitute.For<IThemePackRegistry>();
    private readonly SaveService _service;

    public SaveServiceTests()
    {
        var contentBuffer = new ContentBufferService(
            _bufferStore,
            _metadataStore,
            _themePackRegistry,
            _gitHubRepository);
        var siteConfigService = new SiteConfigService(
            _bufferStore,
            _metadataStore,
            _themePackRegistry,
            _gitHubRepository);

        _service = new SaveService(
            _metadataStore,
            _gitHubRepository,
            contentBuffer,
            siteConfigService);
    }

    [Fact]
    public async Task SaveAsync_CreatesCommitForPendingChanges()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        const string path = "content/posts/save-me.md";
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);
        buffer.Items[path] = new ContentItem
        {
            Path = path,
            ContentType = ContentTypeKind.Post,
            FrontMatter = new OrderedDictionary(StringComparer.Ordinal) { ["title"] = "Save me" },
            Body = "Saved body",
            IsNew = true,
            HasUnsavedLocalEdits = true,
        };
        buffer.HasUnsavedEdits = true;

        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        _metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns(session);
        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);

        _gitHubRepository.CreateCommitAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.BranchName,
                Arg.Any<string>(),
                Arg.Is<IReadOnlyList<GitHubFileChange>>(changes =>
                    changes.Count == 1
                    && changes[0].Path == path
                    && changes[0].Content!.Contains("title", StringComparison.Ordinal)),
                Arg.Any<CancellationToken>())
            .Returns(new GitHubCommitInfo { Sha = "commit-sha" });

        var result = await _service.SaveAsync(new SaveRequest { CommitMessage = "Test save" });

        Assert.Equal("commit-sha", result.CommitSha);
        Assert.False(result.HasUnsavedLocalEdits);
        await _gitHubRepository.Received(1).CreateCommitAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            session.BranchName,
            "Test save",
            Arg.Any<IReadOnlyList<GitHubFileChange>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_IncludesDeletionChangesWhenItemMarkedDeleted()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        const string path = "content/posts/delete-me.md";
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);
        buffer.Items[path] = new ContentItem
        {
            Path = path,
            ContentType = ContentTypeKind.Post,
            FrontMatter = new OrderedDictionary(StringComparer.Ordinal) { ["title"] = "Delete me" },
            Body = "Body",
            IsDeleted = true,
            HasUnsavedLocalEdits = true,
            ExistsInSession = true,
        };

        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        _metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns(session);
        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);

        _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                path,
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns(new GitHubFileContent { Path = path, Content = "existing", Sha = "existing-sha" });

        _gitHubRepository.CreateCommitAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.BranchName,
                Arg.Any<string>(),
                Arg.Is<IReadOnlyList<GitHubFileChange>>(changes =>
                    changes.Count == 1
                    && changes[0].Path == path
                    && changes[0].Content == null
                    && changes[0].Sha == "existing-sha"),
                Arg.Any<CancellationToken>())
            .Returns(new GitHubCommitInfo { Sha = "delete-commit" });

        var result = await _service.SaveAsync();

        Assert.Equal("delete-commit", result.CommitSha);
    }

    [Fact]
    public async Task SaveAsync_ThrowsWhenNoPendingChanges()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);

        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        _metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns(session);
        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);
        _gitHubRepository.GetBranchTipShaAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns("tip-sha");
        _gitHubRepository.ListTreeAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                "tip-sha",
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<GitHubTreeEntry>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SaveAsync());
    }
}
