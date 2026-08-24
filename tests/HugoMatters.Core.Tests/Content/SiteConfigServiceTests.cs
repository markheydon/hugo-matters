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
                Arg.Any<string>(),
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns((GitHubFileContent?)null);

        var values = await _service.UpdateAsync(new Dictionary<string, object?>
        {
            ["title"] = "My Site",
            ["params.hero.content"] = "Welcome",
        }, TestContext.Current.CancellationToken);

        Assert.Equal("My Site", values["title"]);
        Assert.Equal("Welcome", values["params.hero.content"]);
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
title = "From Git"
baseURL = "https://example.com/"
locale = "en-GB"

[params.hero]
title = "Hero"
subtitle = "Sub"
content = "Intro text"
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
        Assert.Equal("https://example.com/", values["baseURL"]);
        Assert.Equal("en-GB", values["locale"]);
        Assert.Equal("Hero", values["params.hero.title"]);
        Assert.Equal("Sub", values["params.hero.subtitle"]);
        Assert.Equal("Intro text", values["params.hero.content"]);
    }

    [Fact]
    public async Task GetAsync_LoadsAllowlistedValuesFromHugoYml()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);
        const string config = """
title: From Yaml
baseURL: https://yaml.example/
locale: en-GB
params:
  hero:
    title: Yaml Hero
    subtitle: Yaml Sub
    content: Yaml intro
""";

        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);
        _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                "hugo.toml",
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns((GitHubFileContent?)null);
        _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                "hugo.yaml",
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns((GitHubFileContent?)null);
        _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                "hugo.yml",
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns(new GitHubFileContent
            {
                Path = "hugo.yml",
                Content = config,
                Sha = "yaml-sha",
            });

        var values = await _service.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("From Yaml", values["title"]);
        Assert.Equal("https://yaml.example/", values["baseURL"]);
        Assert.Equal("en-GB", values["locale"]);
        Assert.Equal("Yaml Hero", values["params.hero.title"]);
        Assert.Equal("Yaml Sub", values["params.hero.subtitle"]);
        Assert.Equal("Yaml intro", values["params.hero.content"]);
    }

    [Fact]
    public async Task GetAsync_MapsLegacyLanguageCodeAndIntroAliases()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        var buffer = TestHelpers.CreateEmptyBuffer(session.Id);
        const string config = """
title: Alias Site
languageCode: en-us
params:
  hero:
    intro: Legacy intro
""";

        TestHelpers.SetupActiveContext(_metadataStore, _themePackRegistry, site, session);
        _bufferStore.GetOrCreateBufferAsync(session.Id, Arg.Any<CancellationToken>()).Returns(buffer);
        _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                "hugo.yml",
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns(new GitHubFileContent
            {
                Path = "hugo.yml",
                Content = config,
                Sha = "alias-sha",
            });
        // Other candidates miss
        _gitHubRepository.GetFileContentsAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                Arg.Is<string>(p => p != "hugo.yml"),
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns((GitHubFileContent?)null);

        var values = await _service.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en-us", values["locale"]);
        Assert.Equal("Legacy intro", values["params.hero.content"]);
    }

    [Fact]
    public async Task GetAsync_AppliesPackDefaultsWhenConfigFileMissing()
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
                Arg.Any<string>(),
                session.BranchName,
                Arg.Any<CancellationToken>())
            .Returns((GitHubFileContent?)null);

        var values = await _service.GetAsync(TestContext.Current.CancellationToken);

        Assert.False(values.ContainsKey("locale"));
        Assert.False(values.ContainsKey("languageCode"));
    }
}
