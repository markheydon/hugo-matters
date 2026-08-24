namespace HugoMatters.Web.Authentication;

/// <summary>
/// Static HTML error pages for auth boundary failures.
/// </summary>
internal static class GitHubAuthErrorPageRenderer
{
    internal static StaticErrorPageResult Render(HttpContext context, string reason)
    {
        var (title, message, statusCode) = GetPresentation(reason);
        var returnUrl = GitHubAuthReturnUrl.GetSafeReturnUrl(context.Request.Query["returnUrl"].FirstOrDefault());
        var signInUrl = $"/auth/sign-in?returnUrl={Uri.EscapeDataString(returnUrl)}";

        var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>{title} — Hugo Matters</title>
  <style>
    body {{ font-family: system-ui, sans-serif; margin: 0; background: #f8fafc; color: #0f172a; }}
    main {{ max-width: 32rem; margin: 4rem auto; padding: 2rem; background: #fff; border: 1px solid #e2e8f0; border-radius: 0.75rem; }}
    h1 {{ font-size: 1.25rem; margin: 0 0 0.75rem; }}
    p {{ margin: 0 0 1rem; color: #475569; line-height: 1.5; }}
    a {{ display: inline-block; padding: 0.5rem 1rem; background: #0284c7; color: #fff; text-decoration: none; border-radius: 0.5rem; font-weight: 500; }}
  </style>
</head>
<body>
  <main>
    <h1>{title}</h1>
    <p>{message}</p>
    <a href=""{signInUrl}"">Sign in with GitHub</a>
  </main>
</body>
</html>";

        return new StaticErrorPageResult(html, statusCode);
    }

    private static (string Title, string Message, int StatusCode) GetPresentation(string reason)
    {
        return reason switch
        {
            GitHubAuthErrorRoutes.SessionExpired => (
                "Session expired",
                "Your GitHub session expired. Sign in again to continue.",
                StatusCodes.Status401Unauthorized),
            GitHubAuthErrorRoutes.SignInDenied => (
                "Sign-in denied",
                "GitHub sign-in was denied or cancelled.",
                StatusCodes.Status400BadRequest),
            GitHubAuthErrorRoutes.SignInIncomplete => (
                "Sign-in incomplete",
                "GitHub did not return a complete authorization response.",
                StatusCodes.Status400BadRequest),
            GitHubAuthErrorRoutes.SignInUnavailable => (
                "Sign-in unavailable",
                "GitHub sign-in could not be completed. Try again later.",
                StatusCodes.Status503ServiceUnavailable),
            _ => (
                "Sign-in failed",
                "GitHub sign-in could not be completed. Check App credentials and try again.",
                StatusCodes.Status400BadRequest),
        };
    }
}

internal sealed record StaticErrorPageResult(string Html, int StatusCode);
