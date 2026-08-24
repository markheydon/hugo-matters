namespace HugoMatters.Web.Authentication;

/// <summary>
/// Claim types stored in the GitHub sign-in cookie.
/// OAuth tokens are stored server-side; the cookie only references a session key.
/// </summary>
public static class GitHubAuthClaimTypes
{
    public const string OwnerLogin = "hugo-matters.github.owner-login";

    public const string InstallationId = "hugo-matters.github.installation-id";

    public const string TokenExpiresAt = "hugo-matters.github.token-expires-at";

    public const string SessionKey = "hugo-matters.github.session-key";

    /// <summary>Legacy claim type; tokens are no longer stored in the cookie.</summary>
    public const string AccessToken = "hugo-matters.github.access-token";

    /// <summary>Legacy claim type; tokens are no longer stored in the cookie.</summary>
    public const string RefreshToken = "hugo-matters.github.refresh-token";

    /// <summary>Legacy claim type; tokens are no longer stored in the cookie.</summary>
    public const string RefreshTokenExpiresAt = "hugo-matters.github.refresh-token-expires-at";
}
