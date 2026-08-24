using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

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

        var sessionKey = context.Principal.FindFirst(_authOptions.HostedSessionKeyClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            MarkSessionExpired(context);
            return;
        }

        var tokenStore = context.HttpContext.RequestServices.GetService<GitHubAuthTokenStore>();
        if (tokenStore is null)
        {
            MarkSessionExpired(context);
            return;
        }

        var storedSession = tokenStore.TryGetSession(sessionKey);
        if (storedSession is null)
        {
            MarkSessionExpired(context);
            return;
        }

        var expiresAtUtc = storedSession.TokenExpiresAtUtc;
        if (expiresAtUtc is null
            || expiresAtUtc > DateTimeOffset.UtcNow.Add(RefreshSkew))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(storedSession.RefreshToken))
        {
            MarkSessionExpired(context);
            return;
        }

        if (context.HttpContext.RequestServices.GetService<GitHubAuthGateway>() is not GitHubAuthGateway gateway)
        {
            MarkSessionExpired(context);
            return;
        }

        var refreshLock = tokenStore.GetRefreshLock(sessionKey);
        await refreshLock.WaitAsync(context.HttpContext.RequestAborted).ConfigureAwait(false);
        try
        {
            storedSession = tokenStore.TryGetSession(sessionKey);
            if (storedSession is null)
            {
                MarkSessionExpired(context);
                return;
            }

            expiresAtUtc = storedSession.TokenExpiresAtUtc;
            if (expiresAtUtc is not null && expiresAtUtc > DateTimeOffset.UtcNow.Add(RefreshSkew))
            {
                context.ReplacePrincipal(gateway.CreatePrincipal(storedSession, sessionKey));
                return;
            }

            try
            {
                var refreshedSession = await gateway.RefreshSessionAsync(storedSession, context.HttpContext.RequestAborted)
                    .ConfigureAwait(false);
                tokenStore.UpdateSession(sessionKey, refreshedSession);
                context.ReplacePrincipal(gateway.CreatePrincipal(refreshedSession, sessionKey));
                context.ShouldRenew = true;
            }
            catch
            {
                MarkSessionExpired(context);
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private static void MarkSessionExpired(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        context.ShouldRenew = false;
    }
}
