using HugoMatters.Core.Models;

namespace HugoMatters.Core.Sessions;

/// <summary>
/// Guard rules and state transitions for editing session lifecycle.
/// </summary>
public static class SessionLifecycle
{
    /// <summary>
    /// Evaluates whether a session can transition to publishing.
    /// </summary>
    public static PublishGuardResult EvaluatePublish(
        EditingSession session,
        bool hasBranchChanges,
        bool installationAuthorized)
    {
        if (session.State != SessionState.Active)
        {
            return PublishGuardResult.Blocked(
                PublishOutcome.FailedMerge,
                $"Cannot publish a session in state '{session.State}'.");
        }

        if (session.HasUnsavedLocalEdits)
        {
            return PublishGuardResult.Blocked(
                PublishOutcome.BlockedUnsavedEdits,
                "Save your changes before publishing.");
        }

        if (!hasBranchChanges)
        {
            return PublishGuardResult.Blocked(
                PublishOutcome.BlockedNoChanges,
                "There are no changes to publish.");
        }

        if (!installationAuthorized)
        {
            return PublishGuardResult.Blocked(
                PublishOutcome.FailedAuth,
                "GitHub App authorization is no longer valid.");
        }

        return PublishGuardResult.Allowed();
    }

    /// <summary>
    /// Evaluates whether a session can transition to discarding.
    /// </summary>
    public static DiscardGuardResult EvaluateDiscard(EditingSession session, bool confirmDiscardUnsaved)
    {
        if (session.State != SessionState.Active)
        {
            return DiscardGuardResult.Blocked(
                DiscardOutcome.Failed,
                $"Cannot discard a session in state '{session.State}'.");
        }

        if (session.HasUnsavedLocalEdits && !confirmDiscardUnsaved)
        {
            return DiscardGuardResult.Blocked(
                DiscardOutcome.ConfirmationRequired,
                "Unsaved local edits will be lost. Confirm discard to continue.");
        }

        return DiscardGuardResult.Allowed();
    }

    /// <summary>
    /// Transitions a session to the publishing state.
    /// </summary>
    public static void BeginPublishing(EditingSession session)
    {
        EnsureActive(session);
        session.State = SessionState.Publishing;
        session.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Transitions a session to the discarding state.
    /// </summary>
    public static void BeginDiscarding(EditingSession session)
    {
        EnsureActive(session);
        session.State = SessionState.Discarding;
        session.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks a session as ended after successful publish or discard.
    /// </summary>
    public static void End(EditingSession session)
    {
        session.State = SessionState.Ended;
        session.HasUnsavedLocalEdits = false;
        session.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Reverts a failed publish back to active state.
    /// </summary>
    public static void RevertToActive(EditingSession session)
    {
        session.State = SessionState.Active;
        session.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void EnsureActive(EditingSession session)
    {
        if (session.State != SessionState.Active)
        {
            throw new InvalidOperationException($"Session must be Active (current: {session.State}).");
        }
    }
}

/// <summary>
/// Result of evaluating publish guards.
/// </summary>
public sealed class PublishGuardResult
{
    private PublishGuardResult(bool isAllowed, PublishOutcome? blockedOutcome, string? message)
    {
        IsAllowed = isAllowed;
        BlockedOutcome = blockedOutcome;
        Message = message;
    }

    /// <summary>Whether publishing is allowed.</summary>
    public bool IsAllowed { get; }

    /// <summary>Blocked outcome when not allowed.</summary>
    public PublishOutcome? BlockedOutcome { get; }

    /// <summary>Human-readable message.</summary>
    public string? Message { get; }

    /// <summary>Creates an allowed result.</summary>
    public static PublishGuardResult Allowed() => new(true, null, null);

    /// <summary>Creates a blocked result.</summary>
    public static PublishGuardResult Blocked(PublishOutcome outcome, string message) =>
        new(false, outcome, message);
}

/// <summary>
/// Result of evaluating discard guards.
/// </summary>
public sealed class DiscardGuardResult
{
    private DiscardGuardResult(bool isAllowed, DiscardOutcome? blockedOutcome, string? message)
    {
        IsAllowed = isAllowed;
        BlockedOutcome = blockedOutcome;
        Message = message;
    }

    /// <summary>Whether discarding is allowed.</summary>
    public bool IsAllowed { get; }

    /// <summary>Blocked outcome when not allowed.</summary>
    public DiscardOutcome? BlockedOutcome { get; }

    /// <summary>Human-readable message.</summary>
    public string? Message { get; }

    /// <summary>Creates an allowed result.</summary>
    public static DiscardGuardResult Allowed() => new(true, null, null);

    /// <summary>Creates a blocked result.</summary>
    public static DiscardGuardResult Blocked(DiscardOutcome outcome, string message) =>
        new(false, outcome, message);
}
