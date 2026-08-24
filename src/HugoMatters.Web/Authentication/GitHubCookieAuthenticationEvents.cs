using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HugoMatters.Web.Authentication;

/// <summary>
/// Refreshes GitHub access tokens on cookie validation when near expiry.
/// </summary>
internal sealed class GitHubCookieAuthenticationEvents : CookieAuthenticationEvents
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(5);

    private readonly GitHubAuthOptions _authOptions;

    private GitHubCookieAuthenticationEvents(GitHubAuthOptions authOptions)
    {
        _authOptions = authOptions;
    }

    internal static GitHubCookieAuthenticationEvents Create(GitHubAuthOptions authOptions) =>
        new(authOptions);

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var expiresAtClaim = context.Principal.FindFirst(_authOptions.HostedTokenExpiresAtClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(expiresAtClaim)
            || !DateTimeOffset.TryParse(expiresAtClaim, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAtUtc))
        {
            return;
        }

        if (expiresAtUtc > DateTimeOffset.UtcNow.Add(RefreshSkew))
        {
            return;
        }

        var refreshToken = context.Principal.FindFirst(_authOptions.HostedRefreshTokenClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            MarkSessionExpired(context);
            return;
        }

        if (context.HttpContext.RequestServices.GetService<GitHubAuthGateway>() is not GitHubAuthGateway gateway)
        {
            MarkSessionExpired(context);
            return;
        }

        var ownerLogin = context.Principal.FindFirst(_authOptions.HostedOwnerLoginClaimType)?.Value ?? string.Empty;
        long? installationId = null;
        var installationClaim = context.Principal.FindFirst(_authOptions.HostedInstallationIdClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(installationClaim) && long.TryParse(installationClaim, out var parsedInstallationId))
        {
            installationId = parsedInstallationId;
        }

        DateTimeOffset? refreshExpiresAtUtc = null;
        var refreshExpiresClaim = context.Principal.FindFirst(_authOptions.HostedRefreshTokenExpiresAtClaimType)?.Value;
        if (!string.IsNullOrWhiteSpace(refreshExpiresClaim)
            && DateTimeOffset.TryParse(refreshExpiresClaim, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedRefreshExpires))
        {
            refreshExpiresAtUtc = parsedRefreshExpires;
        }

        var currentSession = new GitHubAuthSession(
            ownerLogin,
            context.Principal.FindFirst(_authOptions.HostedAccessTokenClaimType)?.Value ?? string.Empty,
            installationId,
            expiresAtUtc,
            refreshToken,
            refreshExpiresAtUtc);

        try
        {
            var refreshedSession = await gateway.RefreshSessionAsync(currentSession, context.HttpContext.RequestAborted).ConfigureAwait(false);
            var principal = gateway.CreatePrincipal(refreshedSession);
            context.ReplacePrincipal(principal);
            context.ShouldRenew = true;
        }
        catch
        {
            MarkSessionExpired(context);
        }
    }

    private static void MarkSessionExpired(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        context.ShouldRenew = false;
    }
}
