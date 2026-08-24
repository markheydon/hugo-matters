using System.Collections.Specialized;
using HugoMatters.Core.Content;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using NSubstitute;

namespace HugoMatters.Core.Tests.Content;

public class ContentBufferServiceTests
{
    private readonly IContentBufferStore _bufferStore = Substitute.For<IContentBufferStore>();
    private readonly IMetadataStore _metadataStore = Substitute.For<IMetadataStore>();
    private readonly IThemePackRegistry _themePackRegistry = Substitute.For<IThemePackRegistry>();
    private readonly IGitHubRepository _gitHubRepository = Substitute.For<IGitHubRepository>();
    private readonly ContentBufferService _service;

    public ContentBufferServiceTests()
    {
        _service = new ContentBufferService(
            _bufferStore,
            _metadataStore,
            _themePackRegistry,
            _gitHubRepository);
    }

    [Fact]
    public async Task CreateAsync_AppliesThemePackDefaults()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        var pack = TestHelpers.GetHugoProfilePack();
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);

        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);
        _gitHubRepository.GetBranchTipShaAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns("abc123");
        _gitHubRepository.ListTreeAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                "abc123",
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<GitHubTreeEntry>());

        var item = await _service.CreateAsync(ContentTypeKind.Post, "hello-world", title: "Hello", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("content/posts/hello-world.md", item.Path);
        Assert.Equal(ContentTypeKind.Post, item.ContentType);
        Assert.Equal("Hello", item.FrontMatter["title"]);
        Assert.Equal(true, item.FrontMatter["draft"]);
        Assert.True(item.IsNew);
        Assert.True(item.HasUnsavedLocalEdits);
    }

    [Fact]
    public async Task LoadFromGit_PreservesUnknownFrontmatterKeys()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);
        const string path = "content/posts/existing.md";
        const string fileContent = """
---
title: Existing
legacy_custom: keep-me
---
Body
""";

        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);
        _gitHubRepository.GetBranchTipShaAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns("abc123");
        _gitHubRepository.ListTreeAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                "abc123",
                Arg.Any<CancellationToken>())
            .Returns([new GitHubTreeEntry { Path = path, Type = "blob", Sha = "blob-sha" }]);
        _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                path,
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns(new GitHubFileContent { Path = path, Content = fileContent, Sha = "blob-sha" });

        var item = await _service.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.NotNull(item);
        Assert.Equal("keep-me", item!.FrontMatter["legacy_custom"]);

        var serialized = HugoContentDocument.Create(item.FrontMatter, item.Body).Serialize();
        Assert.Contains("legacy_custom", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpsertAsync_ReplacesFrontmatterWhilePreservingUnknownKeysWhenIncluded()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);
        const string path = "content/posts/update-me.md";

        buffer.Items[path] = new ContentItem
        {
            Path = path,
            ContentType = ContentTypeKind.Post,
            FrontMatter = new OrderedDictionary(StringComparer.Ordinal)
            {
                ["title"] = "Old",
                ["legacy_custom"] = "still-here",
            },
            Body = "Old body",
            ExistsInSession = true,
        };

        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);

        var updated = await _service.UpsertAsync(path, new Dictionary<string, object?>
        {
            ["title"] = "New",
            ["legacy_custom"] = "still-here",
        }, "New body", TestContext.Current.CancellationToken);

        Assert.Equal("New", updated.FrontMatter["title"]);
        Assert.Equal("still-here", updated.FrontMatter["legacy_custom"]);
        Assert.Equal("New body", updated.Body);
        Assert.True(updated.HasUnsavedLocalEdits);
    }
}
