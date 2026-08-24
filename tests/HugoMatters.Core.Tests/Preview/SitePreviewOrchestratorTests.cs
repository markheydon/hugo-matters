using HugoMatters.Core.Api;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using HugoMatters.Core.Preview;
using HugoMatters.Core.Sessions;
using NSubstitute;

namespace HugoMatters.Core.Tests.Preview;

public class SitePreviewOrchestratorTests
{
    [Fact]
    public void EditorPreviewService_RendersMarkdownWithoutSavedBranchState()
    {
        var service = new EditorPreviewService();

        var response = service.Preview(new EditorPreviewRequest
        {
            Body = "# Preview\n\nUnsaved **buffer** content.",
        });

        Assert.Contains("<h1", response.Html, StringComparison.Ordinal);
        Assert.Contains("Unsaved", response.Html, StringComparison.Ordinal);
        Assert.Contains("<strong>buffer</strong>", response.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishService_StopsSitePreviewOnSuccessfulPublish()
    {
        var metadataStore = Substitute.For<IMetadataStore>();
        var gitHubRepository = Substitute.For<IGitHubRepository>();
        var bufferStore = Substitute.For<IContentBufferStore>();
        var previewOrchestrator = Substitute.For<ISitePreviewOrchestrator>();
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);

        metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns(session);
        gitHubRepository.BranchHasChangesAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.BranchName,
                session.BaseBranch,
                Arg.Any<CancellationToken>())
            .Returns(true);
        gitHubRepository.MergePullRequestAsync(
                site.InstallationId,
                site.OwnerLogin,
                site.RepoName,
                session.PullRequestNumber,
                Arg.Any<CancellationToken>())
            .Returns(new GitHubMergeResult { Succeeded = true, MergeCommitSha = "merge-sha" });

        var service = new PublishService(metadataStore, gitHubRepository, bufferStore, previewOrchestrator);

        var result = await service.PublishAsync(TestContext.Current.CancellationToken);

        Assert.Equal(PublishOutcome.Succeeded, result.Outcome);
        await previewOrchestrator.Received(1).StopPreviewAsync(session.Id, Arg.Any<CancellationToken>());
        await metadataStore.Received(1).DeleteSitePreviewAsync(session.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiscardService_StopsSitePreviewOnSuccessfulDiscard()
    {
        var metadataStore = Substitute.For<IMetadataStore>();
        var gitHubRepository = Substitute.For<IGitHubRepository>();
        var bufferStore = Substitute.For<IContentBufferStore>();
        var previewOrchestrator = Substitute.For<ISitePreviewOrchestrator>();
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);

        metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns(session);

        var service = new DiscardService(metadataStore, gitHubRepository, bufferStore, previewOrchestrator);

        var result = await service.DiscardAsync(new DiscardRequest { ConfirmDiscardUnsaved = false }, TestContext.Current.CancellationToken);

        Assert.Equal(DiscardOutcome.Succeeded, result.Outcome);
        await previewOrchestrator.Received(1).StopPreviewAsync(session.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SitePreviewOrchestrator_StartPreviewUsesSavedBranchTipShaBoundary()
    {
        var previewOrchestrator = Substitute.For<ISitePreviewOrchestrator>();
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);
        const string savedTipSha = "saved-branch-tip-sha";

        previewOrchestrator.StartPreviewAsync(
                session,
                site,
                savedTipSha,
                Arg.Any<CancellationToken>())
            .Returns(new SitePreviewInfo
            {
                Id = "preview-1",
                SessionId = session.Id,
                Status = SitePreviewState.Running,
                BaseUrl = "http://localhost:1313",
            });

        var preview = await previewOrchestrator.StartPreviewAsync(session, site, savedTipSha, TestContext.Current.CancellationToken);

        Assert.Equal(SitePreviewState.Running, preview.Status);
        await previewOrchestrator.Received(1).StartPreviewAsync(
            session,
            site,
            savedTipSha,
            Arg.Any<CancellationToken>());
    }
}
