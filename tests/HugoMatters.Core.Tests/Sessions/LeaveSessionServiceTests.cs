using HugoMatters.Core.Api;
using HugoMatters.Core.Models;
using HugoMatters.Core.Ports;
using HugoMatters.Core.Sessions;
using NSubstitute;

namespace HugoMatters.Core.Tests.Sessions;

public sealed class LeaveSessionServiceTests
{
  private readonly IMetadataStore _metadataStore = Substitute.For<IMetadataStore>();
    private readonly IContentBufferStore _bufferStore = Substitute.For<IContentBufferStore>();
    private readonly ISitePreviewOrchestrator _previewOrchestrator = Substitute.For<ISitePreviewOrchestrator>();
    private readonly LeaveSessionService _service;

    public LeaveSessionServiceTests()
    {
        _service = new LeaveSessionService(_metadataStore, _bufferStore, _previewOrchestrator);
    }

    [Fact]
    public async Task LeaveAsync_requires_confirmation_when_unsaved_edits_exist()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id, hasUnsavedLocalEdits: true);

        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        _metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns(session);

        var result = await _service.LeaveAsync(
            new LeaveSessionRequest { ConfirmLeaveUnsaved = false },
            TestContext.Current.CancellationToken);

        Assert.Equal(LeaveSessionOutcome.ConfirmationRequired, result.Outcome);
    }

    [Fact]
    public async Task LeaveAsync_succeeds_and_clears_local_state()
    {
        var site = TestHelpers.CreateConnectedSite();
        var session = TestHelpers.CreateActiveSession(site.Id);

        _metadataStore.GetConnectedSiteAsync(Arg.Any<CancellationToken>()).Returns(site);
        _metadataStore.GetActiveSessionAsync(site.Id, Arg.Any<CancellationToken>()).Returns(session);

        var result = await _service.LeaveAsync(
            new LeaveSessionRequest { ConfirmLeaveUnsaved = false },
            TestContext.Current.CancellationToken);

        Assert.Equal(LeaveSessionOutcome.Succeeded, result.Outcome);
        await _bufferStore.Received(1).ClearBufferAsync(session.Id, Arg.Any<CancellationToken>());
        await _metadataStore.Received().SaveSessionAsync(
            Arg.Is<EditingSession>(s => s.State == SessionState.Ended),
            Arg.Any<CancellationToken>());
    }
}
