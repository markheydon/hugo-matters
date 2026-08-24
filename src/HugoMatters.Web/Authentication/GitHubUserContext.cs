using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace HugoMatters.Web.Authentication;

/// <summary>
/// Reads GitHub sign-in state from cookie claims and the server-side token store.
/// </summary>
public sealed class GitHubUserContext(
    IOptions<GitHubAuthOptions> optionsAccessor,
    GitHubAuthTokenStore tokenStore)
{
    private readonly GitHubAuthOptions _options = optionsAccessor.Value;
    private readonly GitHubAuthTokenStore _tokenStore = tokenStore;

    public string? GetOwnerLogin(ClaimsPrincipal? user) =>
        user?.FindFirst(_options.HostedOwnerLoginClaimType)?.Value
        ?? user?.FindFirst(ClaimTypes.Name)?.Value;

    public string? GetAccessToken(ClaimsPrincipal? user) =>
        TryGetStoredSession(user)?.AccessToken;

    public long? GetInstallationId(ClaimsPrincipal? user)
    {
        var stored = TryGetStoredSession(user);
        if (stored?.InstallationId is { } installationId and > 0)
        {
            return installationId;
        }

        var claim = user?.FindFirst(_options.HostedInstallationIdClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(claim) || !long.TryParse(claim, out var parsedInstallationId))
        {
            return null;
        }

        return parsedInstallationId;
    }

    public bool IsTokenExpired(ClaimsPrincipal? user)
    {
        DateTimeOffset? expiresAtUtc = TryGetStoredSession(user)?.TokenExpiresAtUtc;
        if (expiresAtUtc is null)
        {
            var claim = user?.FindFirst(_options.HostedTokenExpiresAtClaimType)?.Value;
            if (string.IsNullOrWhiteSpace(claim)
                || !DateTimeOffset.TryParse(claim, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedExpiresAt))
            {
                return false;
            }

            expiresAtUtc = parsedExpiresAt;
        }

        return expiresAtUtc <= DateTimeOffset.UtcNow;
    }

    public string? GetSessionKey(ClaimsPrincipal? user) =>
        user?.FindFirst(_options.HostedSessionKeyClaimType)?.Value;

    private GitHubAuthSession? TryGetStoredSession(ClaimsPrincipal? user)
    {
        var sessionKey = GetSessionKey(user);
        return _tokenStore.TryGetSession(sessionKey);
    }
}
