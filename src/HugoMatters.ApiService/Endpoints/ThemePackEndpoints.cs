using HugoMatters.Core.Ports;

namespace HugoMatters.ApiService.Endpoints;

/// <summary>
/// Theme pack discovery endpoints.
/// </summary>
public static class ThemePackEndpoints
{
    /// <summary>
    /// Maps theme pack endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapThemePackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/theme-packs").WithTags("ThemePacks");

        group.MapGet("/", ListThemePacks)
            .WithName("ListThemePacks");

        group.MapGet("/{id}", GetThemePack)
            .WithName("GetThemePack");

        return app;
    }

    private static IResult ListThemePacks(IThemePackRegistry registry) =>
        Results.Ok(registry.ListPacks());

    private static IResult GetThemePack(string id, IThemePackRegistry registry)
    {
        var pack = registry.GetPackDetail(id);
        return pack is null
            ? Results.NotFound()
            : Results.Ok(pack);
    }
}
