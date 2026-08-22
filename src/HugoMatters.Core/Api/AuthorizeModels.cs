using HugoMatters.Core.Models;

namespace HugoMatters.Core.Api;

/// <summary>
/// Request to start or complete GitHub App authorization.
/// </summary>
public sealed class AuthorizeRequest
{
    /// <summary>GitHub App installation id when completing binding.</summary>
    public long? InstallationId { get; init; }

    /// <summary>Repository owner login.</summary>
    public string? Owner { get; init; }

    /// <summary>Repository name.</summary>
    public string? Repo { get; init; }

    /// <summary>OAuth callback code from the browser install flow.</summary>
    public string? CallbackCode { get; init; }
}

/// <summary>
/// Response from the authorize endpoint.
/// </summary>
public sealed class AuthorizeResponse
{
    /// <summary>Outcome status: <c>connected</c> or <c>redirect</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Redirect URL when status is <c>redirect</c>.</summary>
    public string? RedirectUrl { get; init; }

    /// <summary>Connected site when status is <c>connected</c>.</summary>
    public ConnectedSite? Site { get; init; }
}

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

    /// <summary>Current preview status.</summary>
    public required SitePreviewState Status { get; init; }

    /// <summary>Localhost URL when running.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>Commit SHA of saved session content used for preview.</summary>
    public string? SourceRef { get; init; }

    /// <summary>Safe failure detail when status is failed.</summary>
    public string? ErrorMessage { get; init; }
}
