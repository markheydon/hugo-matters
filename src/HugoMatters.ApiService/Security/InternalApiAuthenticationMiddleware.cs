using Microsoft.Extensions.Options;

namespace HugoMatters.ApiService.Security;

/// <summary>
/// Validates a shared secret header on API routes so only the Web frontend can call ApiService.
/// </summary>
public sealed class InternalApiAuthenticationMiddleware(RequestDelegate next, IOptions<InternalApiOptions> optionsAccessor)
{
    public const string SharedSecretHeaderName = "X-Hugo-Matters-Internal-Token";

    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly InternalApiOptions _options = optionsAccessor.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!RequiresAuthentication(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (!_options.IsConfigured)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var provided = context.Request.Headers[SharedSecretHeaderName].FirstOrDefault();
        if (!string.Equals(provided, _options.SharedSecret, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized.").ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool RequiresAuthentication(PathString path)
    {
        if (!path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
