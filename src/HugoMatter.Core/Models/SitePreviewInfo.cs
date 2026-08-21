namespace HugoMatter.Core.Models;

/// <summary>
/// Metadata for an orchestrated real Hugo site preview.
/// </summary>
public sealed class SitePreviewInfo
{
    /// <summary>Preview instance identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Editing session the preview belongs to.</summary>
    public required Guid SessionId { get; init; }

    /// <summary>Current preview runtime state.</summary>
    public SitePreviewState Status { get; set; }

    /// <summary>Localhost URL when the preview is running.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Isolated workspace directory path.</summary>
    public string? WorkspacePath { get; set; }

    /// <summary>Session branch tip commit SHA used for the preview.</summary>
    public string? SourceRef { get; set; }

    /// <summary>Safe failure detail when <see cref="Status"/> is <see cref="SitePreviewState.Failed"/>.</summary>
    public string? ErrorMessage { get; set; }
}
