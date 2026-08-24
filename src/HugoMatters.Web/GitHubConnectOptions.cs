namespace HugoMatters.Web;

/// <summary>
/// GitHub App OAuth settings for the Web connect flow (mirrors solo-dev-board hosted sign-in wiring).
/// </summary>
public sealed class GitHubConnectOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "GitHubConnect";

    /// <summary>GitHub App OAuth client identifier.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth callback URI registered on the GitHub App.</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>OAuth scopes requested during user authorization (space-separated).</summary>
    public string OauthScopes { get; set; } = "read:user read:org";

    /// <summary>GitHub user authorization endpoint.</summary>
    public string AuthorizeEndpoint { get; set; } = "https://github.com/login/oauth/authorize";

    /// <summary>Whether client id and redirect URI are configured for hosted sign-in.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(RedirectUri);
}
