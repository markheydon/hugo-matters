namespace HugoMatters.Web.Authentication;

/// <summary>
/// GitHub App user sign-in configuration for the Web app.
/// </summary>
public sealed class GitHubAuthOptions
{
    public const string SectionName = "GitHubAuth";

    public string HostedGitHubAppClientId { get; set; } = string.Empty;

    public string HostedGitHubAppClientSecret { get; set; } = string.Empty;

    public string HostedSignInCallbackPath { get; set; } = "/auth/callback";

    public string HostedSignInCallbackBaseUri { get; set; } = string.Empty;

    public string HostedGitHubAuthoriseEndpoint { get; set; } = "https://github.com/login/oauth/authorize";

    public string HostedGitHubAccessTokenEndpoint { get; set; } = "https://github.com/login/oauth/access_token";

    public string HostedSignInScopes { get; set; } = "read:user";

    /// <summary>Optional GitHub App slug for the install URL (e.g. <c>hugo-matters-local</c>).</summary>
    public string GitHubAppSlug { get; set; } = string.Empty;

    public string HostedOwnerLoginClaimType { get; set; } = GitHubAuthClaimTypes.OwnerLogin;

    public string HostedAccessTokenClaimType { get; set; } = GitHubAuthClaimTypes.AccessToken;

    public string HostedInstallationIdClaimType { get; set; } = GitHubAuthClaimTypes.InstallationId;

    public string HostedTokenExpiresAtClaimType { get; set; } = GitHubAuthClaimTypes.TokenExpiresAt;

    public string HostedRefreshTokenClaimType { get; set; } = GitHubAuthClaimTypes.RefreshToken;

    public string HostedRefreshTokenExpiresAtClaimType { get; set; } = GitHubAuthClaimTypes.RefreshTokenExpiresAt;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(HostedGitHubAppClientId)
        && !string.IsNullOrWhiteSpace(HostedGitHubAppClientSecret);
}
