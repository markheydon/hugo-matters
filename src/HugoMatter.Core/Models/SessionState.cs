namespace HugoMatter.Core.Models;

/// <summary>
/// Lifecycle state of an <see cref="EditingSession"/>.
/// </summary>
public enum SessionState
{
    /// <summary>Session is active and accepting edits.</summary>
    Active,

    /// <summary>Session is merging its pull request.</summary>
    Publishing,

    /// <summary>Session is closing its pull request without merge.</summary>
    Discarding,

    /// <summary>Session has ended (published or discarded).</summary>
    Ended,
}
