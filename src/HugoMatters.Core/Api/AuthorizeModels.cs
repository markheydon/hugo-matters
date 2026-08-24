namespace HugoMatters.Core.Api;

/// <summary>
/// API error payload (no secrets).
/// </summary>
public sealed class ErrorBody
{
    /// <summary>Error code.</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }
}

/// <summary>
/// Site preview response for API consumers.
/// </summary>
public sealed class SitePreviewResponse
{
    /// <summary>Preview instance identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Current preview status (Starting, Running, Failed, Stopped).</summary>
    public required string Status { get; init; }

    /// <summary>Localhost URL when running.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>Commit SHA of saved session content used for preview.</summary>
    public string? SourceRef { get; init; }

    /// <summary>Safe failure detail when status is failed.</summary>
    public string? ErrorMessage { get; init; }
}
