namespace HugoMatters.Web.Authentication;

/// <summary>
/// A GitHub App installation visible to the signed-in user.
/// </summary>
public sealed record GitHubUserInstallation(long Id, string AccountLogin);
