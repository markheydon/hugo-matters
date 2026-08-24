using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using HugoMatters.Core.Sessions;
using NSubstitute;

namespace HugoMatters.Core.Tests.Sessions;

public class PublishServiceTests
{
    private readonly IMetadataStore _metadataStore = Substitute.For<IMetadataStore>();
    private readonly IGitHubRepository _gitHubRepository = Substitute.For<IGitHubRepository>();
    private readonly IContentBufferStore _bufferStore = Substitute.For<IContentBufferStore>();
    private readonly ISitePreviewOrchestrator _previewOrchestrator = Substitute.For<ISitePreviewOrchestrator>();
    private readonly PublishService _service;

    public PublishServiceTests()
    {
        _service = new PublishService(
            _metadataStore,
            _gitHubRepository,
            _bufferStore,
            _previewOrchestrator);
    }

    [Fact]
    public async Task PublishAsync_BlocksWhenNoBranchChanges()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        SetupContext(site, session, hasChanges: false);

        var result = await _service.PublishAsync(TestContext.Current.CancellationToken);

        Assert.Equal(PublishOutcome.BlockedNoChanges, result.Outcome);
        await _gitHubRepository.DidNotReceive().MergePullRequestAsync(
            Arg.Any<long>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_BlocksWhenSessionHasUnsavedLocalEdits()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id, hasUnsavedLocalEdits: true);
        SetupContext(site, session, hasChanges: true);

        var result = await _service.PublishAsync(TestContext.Current.CancellationToken);

        Assert.Equal(PublishOutcome.BlockedUnsavedEdits, result.Outcome);
    }

    [Fact]
    public async Task PublishAsync_ReturnsFailedMergeWhenGitHubMergeFails()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        SetupContext(site, session, hasChanges: true);
        _gitHubRepository.MergePullRequestAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.PullRequestNumber,
                Arg.Any<CancellationToken>())
            .Returns(new GitHubMergeResult
            {
                Succeeded = false,
                FailureReason = "Branch protection rules blocked merge.",
            });

        var result = await _service.PublishAsync(TestContext.Current.CancellationToken);

        Assert.Equal(PublishOutcome.FailedMerge, result.Outcome);
        Assert.Equal(SessionState.Active, session.State);
    }

    [Fact]
    public async Task PublishAsync_SucceedsAndEndsSessionWhenMergeSucceeds()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        SetupContext(site, session, hasChanges: true);
        _gitHubRepository.MergePullRequestAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.PullRequestNumber,
                Arg.Any<CancellationToken>())
            .Returns(new GitHubMergeResult
            {
                Succeeded = true,
                MergeCommitSha = "merge-sha",
            });

        var result = await _service.PublishAsync(TestContext.Current.CancellationToken);

        Assert.Equal(PublishOutcome.Succeeded, result.Outcome);
        Assert.Equal("merge-sha", result.MergeCommitSha);
        Assert.Equal(SessionState.Ended, session.State);
        await _bufferStore.Received(1).ClearBufferAsync(session.Id, Arg.Any<CancellationToken>());
    }

    private void SetupContext(ConnectedSite site, EditingSession session, bool hasChanges)
    {
        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        _metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns(session);
        _gitHubRepository.BranchHasChangesAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.BranchName,
                session.BaseBranch,
                Arg.Any<CancellationToken>())
            .Returns(hasChanges);
    }
}
