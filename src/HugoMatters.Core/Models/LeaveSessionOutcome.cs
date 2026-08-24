namespace HugoMatters.Core.Models;

/// <summary>
/// Outcome of leaving (detaching) a local editing session without closing the PR.
/// </summary>
public enum LeaveSessionOutcome
{
    /// <summary>Local session ended; PR remains open on GitHub.</summary>
    Succeeded,

    /// <summary>Caller must confirm discarding unsaved local buffer edits.</summary>
    ConfirmationRequired,

    /// <summary>Leave failed; session remains active.</summary>
    Failed,
}
