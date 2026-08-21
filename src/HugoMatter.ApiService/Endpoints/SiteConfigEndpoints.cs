using HugoMatter.Core.Content;
using HugoMatter.ApiService.Http;

namespace HugoMatter.ApiService.Endpoints;

/// <summary>
/// Site configuration endpoints.
/// </summary>
public static class SiteConfigEndpoints
{
    /// <summary>
    /// Maps site configuration endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapSiteConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/site-config").WithTags("Content");

        group.MapGet("/", GetSiteConfigAsync)
            .WithName("GetSiteConfig");

        group.MapPut("/", UpdateSiteConfigAsync)
            .WithName("UpdateSiteConfig");

        return app;
    }

    private static async Task<IResult> GetSiteConfigAsync(
        SiteConfigService siteConfigService,
        CancellationToken cancellationToken)
    {
        try
        {
            var config = await siteConfigService.GetAsync(cancellationToken);
            return Results.Ok(config);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> UpdateSiteConfigAsync(
        Dictionary<string, object?> request,
        SiteConfigService siteConfigService,
        CancellationToken cancellationToken)
    {
        try
        {
            var config = await siteConfigService.UpdateAsync(request, cancellationToken);
            return Results.Ok(config);
        }
        catch (ArgumentException ex)
        {
            return ApiResults.Error("invalid_request", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("invalid_state", ex.Message, StatusCodes.Status400BadRequest);
        }
    }
}
