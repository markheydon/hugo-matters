using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HugoMatters.Web.Authentication;

/// <summary>
/// Updates the signed-in GitHub session (installation binding) without re-running OAuth.
/// </summary>
public sealed class GitHubAuthSessionManager(
    GitHubAuthTokenStore tokenStore,
    GitHubAuthGateway authGateway)
{
    public async Task<bool> TrySetInstallationIdAsync(
        HttpContext context,
        long installationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (installationId <= 0)
        {
            return false;
        }

        var sessionKey = context.User.FindFirst(GitHubAuthClaimTypes.SessionKey)?.Value;
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return false;
        }

        var storedSession = tokenStore.TryGetSession(sessionKey);
        if (storedSession is null)
        {
            return false;
        }

        var updatedSession = storedSession with { InstallationId = installationId };
        tokenStore.UpdateSession(sessionKey, updatedSession);

        var principal = authGateway.CreatePrincipal(updatedSession, sessionKey);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true,
                ExpiresUtc = updatedSession.TokenExpiresAtUtc,
            }).ConfigureAwait(false);

        return true;
    }
}
