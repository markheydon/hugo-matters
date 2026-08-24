namespace HugoMatters.Core.Connection;

/// <summary>
/// Whether the connected repository is ready for editing sessions.
/// </summary>
public sealed record RepositoryReadiness(bool Ready, string? Code, string? Message);
