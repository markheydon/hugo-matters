using HugoMatters.Core.Api;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using HugoMatters.Core.Sessions;
using NSubstitute;

namespace HugoMatters.Core.Tests.Sessions;

public class DiscardServiceTests
{
    private readonly IMetadataStore _metadataStore = Substitute.For<IMetadataStore>();
    private readonly IGitHubRepository _gitHubRepository = Substitute.For<IGitHubRepository>();
    private readonly IContentBufferStore _bufferStore = Substitute.For<IContentBufferStore>();
    private readonly ISitePreviewOrchestrator _previewOrchestrator = Substitute.For<ISitePreviewOrchestrator>();
    private readonly DiscardService _service;

    public DiscardServiceTests()
    {
        _service = new DiscardService(
            _metadataStore,
            _gitHubRepository,
            _bufferStore,
            _previewOrchestrator);
    }

    [Fact]
    public async Task DiscardAsync_RequiresConfirmationWhenUnsavedEditsExist()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id, hasUnsavedLocalEdits: true);
        SetupContext(site, session);

        var result = await _service.DiscardAsync(new DiscardRequest { ConfirmDiscardUnsaved = false }, TestContext.Current.CancellationToken);

        Assert.Equal(DiscardOutcome.ConfirmationRequired, result.Outcome);
        await _gitHubRepository.DidNotReceive().ClosePullRequestAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscardAsync_SucceedsWhenUnsavedEditsConfirmed()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id, hasUnsavedLocalEdits: true);
        SetupContext(site, session);

        var result = await _service.DiscardAsync(new DiscardRequest { ConfirmDiscardUnsaved = true }, TestContext.Current.CancellationToken);

        Assert.Equal(DiscardOutcome.Succeeded, result.Outcome);
        Assert.Equal(SessionState.Ended, session.State);
        await _gitHubRepository.Received(1).ClosePullRequestAsync(
            site.InstallationId,
            site.OwnerLogin,
            site.RepoName,
            session.PullRequestNumber,
            Arg.Any<CancellationToken>());
        await _bufferStore.Received(1).ClearBufferAsync(session.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscardAsync_SucceedsWithoutConfirmationWhenNoUnsavedEdits()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        SetupContext(site, session);

        var result = await _service.DiscardAsync(new DiscardRequest { ConfirmDiscardUnsaved = false }, TestContext.Current.CancellationToken);

        Assert.Equal(DiscardOutcome.Succeeded, result.Outcome);
    }

    [Fact]
    public async Task DiscardAsync_RevertsToActiveWhenGitHubOperationFails()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        SetupContext(site, session);
        _gitHubRepository.ClosePullRequestAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.PullRequestNumber,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("GitHub unavailable")));

        var result = await _service.DiscardAsync(new DiscardRequest { ConfirmDiscardUnsaved = false }, TestContext.Current.CancellationToken);

        Assert.Equal(DiscardOutcome.Failed, result.Outcome);
        Assert.Equal(SessionState.Active, session.State);
    }

    private void SetupContext(ConnectedSite site, EditingSession session)
    {
        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        _metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns(session);
    }
}
