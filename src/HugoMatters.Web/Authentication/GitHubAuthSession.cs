namespace HugoMatters.Web.Authentication;

/// <summary>
/// GitHub user session established after OAuth callback.
/// </summary>
public sealed record GitHubAuthSession(
    string OwnerLogin,
    string AccessToken,
    long? InstallationId,
    DateTimeOffset? TokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset? RefreshTokenExpiresAtUtc);
