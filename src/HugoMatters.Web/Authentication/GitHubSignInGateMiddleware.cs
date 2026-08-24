using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HugoMatters.Web.Authentication;

/// <summary>
/// Challenges unauthenticated browser requests while allowing static assets and auth routes.
/// </summary>
public sealed class GitHubSignInGateMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> StaticFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css",
        ".js",
        ".map",
        ".woff",
        ".woff2",
        ".ttf",
        ".otf",
        ".ico",
        ".svg",
        ".png",
        ".gif",
        ".webp",
        ".json",
    };

    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsBypassedPath(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await context.ChallengeAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool IsBypassedPath(PathString path)
    {
        if (path.StartsWithSegments("/auth") || path.StartsWithSegments("/welcome"))
        {
            return true;
        }

        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/alive"))
        {
            return true;
        }

        if (path.StartsWithSegments("/_framework")
            || path.StartsWithSegments("/_content")
            || path.StartsWithSegments("/_blazor"))
        {
            return true;
        }

        var value = path.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var extension = Path.GetExtension(value);
        return !string.IsNullOrWhiteSpace(extension) && StaticFileExtensions.Contains(extension);
    }
}
