namespace HugoMatters.Infrastructure.GitHub;

/// <summary>
/// GitHub App credentials for installation access tokens on ApiService.
/// </summary>
public sealed class GitHubAppOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "GitHubApp";

    /// <summary>GitHub App identifier.</summary>
    public long AppId { get; set; }

    /// <summary>
    /// PEM-encoded RSA private key contents, or a path to a PEM file.
    /// </summary>
    public string PrivateKeyPem { get; set; } = string.Empty;

    /// <summary>Whether required App JWT credentials are configured.</summary>
    public bool IsConfigured =>
        AppId > 0 && !string.IsNullOrWhiteSpace(PrivateKeyPem);
}
