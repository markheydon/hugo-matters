namespace HugoMatters.Core.Api;

/// <summary>
/// Request to bind a Hugo site repository via GitHub App installation.
/// </summary>
public sealed class ConnectRequest
{
    /// <summary>GitHub App installation id from the signed-in user's session.</summary>
    public long InstallationId { get; init; }

    /// <summary>Repository owner login.</summary>
    public required string Owner { get; init; }

    /// <summary>Repository name.</summary>
    public required string Repo { get; init; }
}
