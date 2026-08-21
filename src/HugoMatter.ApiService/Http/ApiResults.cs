using HugoMatter.Core.Api;

namespace HugoMatter.ApiService.Http;

/// <summary>
/// Common API result helpers.
/// </summary>
public static class ApiResults
{
    /// <summary>
    /// Returns a JSON error payload.
    /// </summary>
    public static IResult Error(string code, string message, int statusCode = 400) =>
        Results.Json(new ErrorBody { Code = code, Message = message }, statusCode: statusCode);
}
