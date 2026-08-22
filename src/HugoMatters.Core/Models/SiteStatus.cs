namespace HugoMatters.Core.Models;

/// <summary>
/// Connection status of a <see cref="ConnectedSite"/> to GitHub.
/// </summary>
public enum SiteStatus
{
    /// <summary>The site is connected and authorized.</summary>
    Connected,

    /// <summary>GitHub App installation access has been revoked or lost.</summary>
    AccessLost,

    /// <summary>The site has been disconnected locally.</summary>
    Disconnected,
}
