using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using HugoMatters.Core.Sessions;
using NSubstitute;

namespace HugoMatters.Core.Tests.Sessions;

public class SessionServiceTests
{
    private readonly IMetadataStore _metadataStore = Substitute.For<IMetadataStore>();
    private readonly IGitHubRepository _gitHubRepository = Substitute.For<IGitHubRepository>();
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        _service = new SessionService(_metadataStore, _gitHubRepository);
    }

    [Fact]
    public async Task GetActiveSessionAsync_ReturnsNullWhenNoSiteConnected()
    {
        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns((ConnectedSite?)null);

        var session = await _service.GetActiveSessionAsync(TestContext.Current.CancellationToken);

        Assert.Null(session);
    }

    [Fact]
    public async Task StartSessionAsync_ThrowsWhenNoSiteConnected()
    {
        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns((ConnectedSite?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartSessionAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartSessionAsync_ThrowsWhenSiteNotConnected()
    {
        var site = TestHelpers.CreateConnectedSite(status: SiteStatus.AccessLost);
        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartSessionAsync(TestContext.Current.CancellationToken));

        Assert.Contains("not connected", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartSessionAsync_ThrowsWhenActiveSessionAlreadyExists()
    {
        var site = TestHelpers.CreateConnectedSite();
        var existing = TestHelpers.CreateActiveSession(site.Id);
        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        _metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.StartSessionAsync(TestContext.Current.CancellationToken));

        Assert.Contains("already exists", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartSessionAsync_CreatesBranchPullRequestAndSession()
    {
        var site = TestHelpers.CreateConnectedSite();
        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        _metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns((EditingSession?)null);

        _gitHubRepository.GetRepositoryAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryInfo
            {
                OwnerLogin = site.OwnerLogin,
                RepoName = site.RepoName,
                DefaultBranch = "main",
            });

        _gitHubRepository.CreateBranchAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                Arg.Is<string>(b => b.StartsWith(SessionService.BranchPrefix, StringComparison.Ordinal)),
                "main",
                Arg.Any<CancellationToken>())
            .Returns("refs/heads/hugo-matters/session-abc");

        _gitHubRepository.CreatePullRequestAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                Arg.Any<string>(),
                Arg.Is<string>(b => b.StartsWith(SessionService.BranchPrefix, StringComparison.Ordinal)),
                "main",
                Arg.Any<CancellationToken>())
            .Returns(new GitHubPullRequestInfo
            {
                Number = 42,
                HtmlUrl = "https://github.com/owner/hugo-site/pull/42",
            });

        var session = await _service.StartSessionAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SessionState.Active, session.State);
        Assert.Equal(site.Id, session.SiteId);
        Assert.Equal(42, session.PullRequestNumber);
        Assert.StartsWith(SessionService.BranchPrefix, session.BranchName, StringComparison.Ordinal);
        Assert.False(session.HasUnsavedLocalEdits);

        await _metadataStore.Received(1).SaveSessionAsync(
            Arg.Is<EditingSession>(s => s.Id == session.Id),
            Arg.Any<CancellationToken>());
    }
}
