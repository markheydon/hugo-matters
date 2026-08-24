using Microsoft.AspNetCore.WebUtilities;

namespace HugoMatters.Web.Authentication;

/// <summary>
/// Routes and reason codes for GitHub auth error pages.
/// </summary>
internal static class GitHubAuthErrorRoutes
{
    internal const string SignInStateInvalid = "sign-in-state-invalid";
    internal const string SignInDenied = "sign-in-denied";
    internal const string SignInIncomplete = "sign-in-incomplete";
    internal const string SignInFailed = "sign-in-failed";
    internal const string SignInUnavailable = "sign-in-unavailable";
    internal const string SessionExpired = "session-expired";

    internal static string BuildErrorUrl(string reason, string? returnUrl = null)
    {
        var query = new Dictionary<string, string?> { ["reason"] = reason };
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            query["returnUrl"] = returnUrl;
        }

        return QueryHelpers.AddQueryString("/auth/error", query);
    }
}
