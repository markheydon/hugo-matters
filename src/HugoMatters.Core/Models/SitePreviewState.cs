namespace HugoMatters.Core.Models;

/// <summary>
/// Runtime state of an orchestrated Hugo site preview.
/// </summary>
public enum SitePreviewState
{
    /// <summary>Preview container is starting.</summary>
    Starting,

    /// <summary>Preview is running and reachable.</summary>
    Running,

    /// <summary>Preview failed to start or crashed.</summary>
    Failed,

    /// <summary>Preview has been stopped.</summary>
    Stopped,
}
