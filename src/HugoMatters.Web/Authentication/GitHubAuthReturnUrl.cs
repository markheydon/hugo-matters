using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace HugoMatters.Web.Authentication;

/// <summary>
/// Safe return URL handling for sign-in flows.
/// </summary>
internal static class GitHubAuthReturnUrl
{
    internal static string GetRequestedReturnUrl(Uri absoluteUri)
    {
        var query = QueryHelpers.ParseQuery(absoluteUri.Query);

        if (TryGetQueryValue(query, "returnUrl", out var returnUrl))
        {
            return GetSafeReturnUrl(returnUrl);
        }

        if (TryGetQueryValue(query, "ReturnUrl", out var alternateReturnUrl))
        {
            return GetSafeReturnUrl(alternateReturnUrl);
        }

        return "/";
    }

    internal static string GetSafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (!returnUrl.StartsWith("/", StringComparison.Ordinal) || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }

    private static bool TryGetQueryValue(
        Dictionary<string, StringValues> query,
        string key,
        out string? value)
    {
        if (!query.TryGetValue(key, out var values) || StringValues.IsNullOrEmpty(values))
        {
            value = null;
            return false;
        }

        value = values.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(value);
    }
}
