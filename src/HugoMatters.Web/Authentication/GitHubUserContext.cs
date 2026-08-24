using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace HugoMatters.Web.Authentication;

/// <summary>
/// Reads GitHub sign-in claims from the current user principal.
/// </summary>
public sealed class GitHubUserContext(IOptions<GitHubAuthOptions> optionsAccessor)
{
    private readonly GitHubAuthOptions _options = optionsAccessor.Value;

    public string? GetOwnerLogin(ClaimsPrincipal? user) =>
        user?.FindFirst(_options.HostedOwnerLoginClaimType)?.Value
        ?? user?.FindFirst(ClaimTypes.Name)?.Value;

    public string? GetAccessToken(ClaimsPrincipal? user) =>
        user?.FindFirst(_options.HostedAccessTokenClaimType)?.Value;

    public long? GetInstallationId(ClaimsPrincipal? user)
    {
        var claim = user?.FindFirst(_options.HostedInstallationIdClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(claim) || !long.TryParse(claim, out var installationId))
        {
            return null;
        }

        return installationId;
    }

    public bool IsTokenExpired(ClaimsPrincipal? user)
    {
        var claim = user?.FindFirst(_options.HostedTokenExpiresAtClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(claim)
            || !DateTimeOffset.TryParse(claim, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAtUtc))
        {
            return false;
        }

        return expiresAtUtc <= DateTimeOffset.UtcNow;
    }
}
