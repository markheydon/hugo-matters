namespace HugoMatters.Core.Models;

/// <summary>
/// Outcome of a publish (merge) attempt on an editing session.
/// </summary>
public enum PublishOutcome
{
    /// <summary>Pull request merged, branch deleted, session ended.</summary>
    Succeeded,

    /// <summary>Blocked because the session branch has no changes vs the base branch.</summary>
    BlockedNoChanges,

    /// <summary>Blocked because the session has unsaved local buffer edits.</summary>
    BlockedUnsavedEdits,

    /// <summary>Merge failed due to protection rules, conflicts, or other GitHub errors.</summary>
    FailedMerge,

    /// <summary>Failed because installation authorization is no longer valid.</summary>
    FailedAuth,
}
