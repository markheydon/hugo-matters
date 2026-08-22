using HugoMatters.Core.Content;
using HugoMatters.Core.Ports;
using NSubstitute;

namespace HugoMatters.Core.Tests.Content;

public class SiteConfigServiceTests
{
    private readonly IContentBufferStore _bufferStore = Substitute.For<IContentBufferStore>();
    private readonly IMetadataStore _metadataStore = Substitute.For<IMetadataStore>();
    private readonly IThemePackRegistry _themePackRegistry = Substitute.For<IThemePackRegistry>();
    private readonly IGitHubRepository _gitHubRepository = Substitute.For<IGitHubRepository>();
    private readonly SiteConfigService _service;

    public SiteConfigServiceTests()
    {
        _service = new SiteConfigService(
            _bufferStore,
            _metadataStore,
            _themePackRegistry,
            _gitHubRepository);
    }

    [Fact]
    public async Task UpdateAsync_RejectsKeysOutsideThemePackAllowlist()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);

        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);
        _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                SiteConfigService.DefaultConfigPath,
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns((GitHubFileContent?)null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateAsync(new Dictionary<string, object?> { ["disallowed.key"] = "nope" }, TestContext.Current.CancellationToken));

        Assert.Contains("not allowed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_AllowsAllowlistedKeys()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);

        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);
        _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                SiteConfigService.DefaultConfigPath,
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns((GitHubFileContent?)null);

        var values = await _service.UpdateAsync(new Dictionary<string, object?>
        {
            ["title"] = "My Site",
            ["params.hero.title"] = "Welcome",
        }, TestContext.Current.CancellationToken);

        Assert.Equal("My Site", values["title"]);
        Assert.Equal("Welcome", values["params.hero.title"]);
        Assert.True(buffer.HasUnsavedEdits);
        Assert.True(session.HasUnsavedLocalEdits);
    }

    [Fact]
    public async Task GetAsync_LoadsAllowlistedValuesFromHugoToml()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);
        const string config = """
[params]
title = "From Git"
params.hero.title = "Hero"
ignored = "skip-me"
""";

        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);
        _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                SiteConfigService.DefaultConfigPath,
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns(new GitHubFileContent
            {
                Path = SiteConfigService.DefaultConfigPath,
                Content = config,
                Sha = "config-sha",
            });

        var values = await _service.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("From Git", values["title"]);
        Assert.Equal("Hero", values["params.hero.title"]);
        Assert.False(values.ContainsKey("ignored"));
    }

    [Fact]
    public async Task GetAsync_AppliesPackDefaultsWhenConfigFileMissing()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);
        var pack = TestHelpers.GetHugoProfilePack();

        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);
        _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                SiteConfigService.DefaultConfigPath,
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns((GitHubFileContent?)null);

        var languageDefault = pack.SiteConfigFields.First(f => f.Key == "languageCode").Default;

        var values = await _service.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(languageDefault, values["languageCode"]);
    }
}
