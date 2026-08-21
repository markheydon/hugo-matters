namespace HugoMatter.Infrastructure.GitHub;

/// <summary>
/// GitHub App credentials and OAuth settings.
/// </summary>
public sealed class GitHubAppOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "GitHubApp";

    /// <summary>GitHub App identifier.</summary>
    public long AppId { get; set; }

    /// <summary>OAuth client identifier.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// PEM-encoded RSA private key contents, or a path to a PEM file.
    /// </summary>
    public string PrivateKeyPem { get; set; } = string.Empty;

    /// <summary>
    /// Optional OAuth redirect URI used during the browser install flow.
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>Whether required credentials are configured.</summary>
    public bool IsConfigured =>
        AppId > 0
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(PrivateKeyPem);
}
