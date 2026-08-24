namespace HugoMatters.Core.Models;

/// <summary>
/// Outcome of a discard attempt on an editing session.
/// </summary>
public enum DiscardOutcome
{
    /// <summary>Pull request closed, branch deleted, session ended.</summary>
    Succeeded,

    /// <summary>Blocked pending explicit confirmation for unsaved local edits.</summary>
    ConfirmationRequired,

    /// <summary>Discard failed due to an API or infrastructure error.</summary>
    Failed,
}
