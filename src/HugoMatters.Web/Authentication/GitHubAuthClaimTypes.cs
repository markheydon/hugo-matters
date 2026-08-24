namespace HugoMatters.Web.Authentication;

/// <summary>
/// Claim types stored in the GitHub sign-in cookie.
/// </summary>
public static class GitHubAuthClaimTypes
{
    public const string OwnerLogin = "hugo-matters.github.owner-login";

    public const string AccessToken = "hugo-matters.github.access-token";

    public const string InstallationId = "hugo-matters.github.installation-id";

    public const string TokenExpiresAt = "hugo-matters.github.token-expires-at";

    public const string RefreshToken = "hugo-matters.github.refresh-token";

    public const string RefreshTokenExpiresAt = "hugo-matters.github.refresh-token-expires-at";
}
