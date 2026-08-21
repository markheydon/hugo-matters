using HugoMatter.Core.Models;
using HugoMatter.Core.Sessions;

namespace HugoMatter.Core.Tests.Sessions;

public class SessionLifecycleTests
{
    private static EditingSession CreateSession(
        SessionState state = SessionState.Active,
        bool hasUnsavedLocalEdits = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            BranchName = "hugo-matter/session-abc",
            PullRequestNumber = 1,
            BaseBranch = "main",
            State = state,
            HasUnsavedLocalEdits = hasUnsavedLocalEdits,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    [Fact]
    public void EvaluatePublish_AllowsWhenActiveWithChangesAndAuthorized()
    {
        var session = CreateSession();

        var result = SessionLifecycle.EvaluatePublish(session, hasBranchChanges: true, installationAuthorized: true);

        Assert.True(result.IsAllowed);
        Assert.Null(result.BlockedOutcome);
    }

    [Fact]
    public void EvaluatePublish_BlocksWhenNotActive()
    {
        var session = CreateSession(SessionState.Publishing);

        var result = SessionLifecycle.EvaluatePublish(session, hasBranchChanges: true, installationAuthorized: true);

        Assert.False(result.IsAllowed);
        Assert.Equal(PublishOutcome.FailedMerge, result.BlockedOutcome);
    }

    [Fact]
    public void EvaluatePublish_BlocksWhenUnsavedLocalEdits()
    {
        var session = CreateSession(hasUnsavedLocalEdits: true);

        var result = SessionLifecycle.EvaluatePublish(session, hasBranchChanges: true, installationAuthorized: true);

        Assert.False(result.IsAllowed);
        Assert.Equal(PublishOutcome.BlockedUnsavedEdits, result.BlockedOutcome);
    }

    [Fact]
    public void EvaluatePublish_BlocksWhenNoBranchChanges()
    {
        var session = CreateSession();

        var result = SessionLifecycle.EvaluatePublish(session, hasBranchChanges: false, installationAuthorized: true);

        Assert.False(result.IsAllowed);
        Assert.Equal(PublishOutcome.BlockedNoChanges, result.BlockedOutcome);
    }

    [Fact]
    public void EvaluatePublish_BlocksWhenInstallationNotAuthorized()
    {
        var session = CreateSession();

        var result = SessionLifecycle.EvaluatePublish(session, hasBranchChanges: true, installationAuthorized: false);

        Assert.False(result.IsAllowed);
        Assert.Equal(PublishOutcome.FailedAuth, result.BlockedOutcome);
    }

    [Fact]
    public void EvaluateDiscard_AllowsWhenActiveWithoutUnsavedEdits()
    {
        var session = CreateSession();

        var result = SessionLifecycle.EvaluateDiscard(session, confirmDiscardUnsaved: false);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void EvaluateDiscard_BlocksWhenUnsavedEditsWithoutConfirmation()
    {
        var session = CreateSession(hasUnsavedLocalEdits: true);

        var result = SessionLifecycle.EvaluateDiscard(session, confirmDiscardUnsaved: false);

        Assert.False(result.IsAllowed);
        Assert.Equal(DiscardOutcome.ConfirmationRequired, result.BlockedOutcome);
    }

    [Fact]
    public void EvaluateDiscard_AllowsWhenUnsavedEditsWithConfirmation()
    {
        var session = CreateSession(hasUnsavedLocalEdits: true);

        var result = SessionLifecycle.EvaluateDiscard(session, confirmDiscardUnsaved: true);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void EvaluateDiscard_BlocksWhenNotActive()
    {
        var session = CreateSession(SessionState.Ended);

        var result = SessionLifecycle.EvaluateDiscard(session, confirmDiscardUnsaved: true);

        Assert.False(result.IsAllowed);
        Assert.Equal(DiscardOutcome.Failed, result.BlockedOutcome);
    }

    [Fact]
    public void BeginPublishing_TransitionsActiveSessionToPublishing()
    {
        var session = CreateSession();
        var before = session.UpdatedAt;

        SessionLifecycle.BeginPublishing(session);

        Assert.Equal(SessionState.Publishing, session.State);
        Assert.True(session.UpdatedAt >= before);
    }

    [Fact]
    public void BeginDiscarding_TransitionsActiveSessionToDiscarding()
    {
        var session = CreateSession();

        SessionLifecycle.BeginDiscarding(session);

        Assert.Equal(SessionState.Discarding, session.State);
    }

    [Fact]
    public void End_TransitionsSessionToEndedAndClearsUnsavedEdits()
    {
        var session = CreateSession(hasUnsavedLocalEdits: true);

        SessionLifecycle.End(session);

        Assert.Equal(SessionState.Ended, session.State);
        Assert.False(session.HasUnsavedLocalEdits);
    }

    [Fact]
    public void RevertToActive_RestoresActiveState()
    {
        var session = CreateSession(SessionState.Publishing);

        SessionLifecycle.RevertToActive(session);

        Assert.Equal(SessionState.Active, session.State);
    }

    [Fact]
    public void BeginPublishing_ThrowsWhenSessionNotActive()
    {
        var session = CreateSession(SessionState.Ended);

        Assert.Throws<InvalidOperationException>(() => SessionLifecycle.BeginPublishing(session));
    }
}
