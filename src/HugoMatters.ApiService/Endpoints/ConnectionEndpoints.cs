using HugoMatters.ApiService.Http;
using HugoMatters.Core.Api;
using HugoMatters.Core.Connection;
using HugoMatters.Infrastructure.GitHub;

namespace HugoMatters.ApiService.Endpoints;

/// <summary>
/// GitHub site connection endpoints.
/// </summary>
public static class ConnectionEndpoints
{
    /// <summary>
    /// Maps connection endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/connection").WithTags("Connection");

        group.MapGet("/", GetConnectionAsync)
            .WithName("GetConnection");

        group.MapDelete("/", DisconnectAsync)
            .WithName("Disconnect");

        group.MapPost("/authorize", AuthorizeAsync)
            .WithName("AuthorizeConnection");

        return app;
    }

    private static async Task<IResult> GetConnectionAsync(
        ConnectionService connectionService,
        CancellationToken cancellationToken)
    {
        var site = await connectionService.GetConnectedSiteAsync(cancellationToken);
        if (site is null || site.Status == HugoMatters.Core.Models.SiteStatus.Disconnected)
        {
            return Results.NotFound();
        }

        return Results.Ok(site);
    }

    private static async Task<IResult> DisconnectAsync(
        ConnectionService connectionService,
        CancellationToken cancellationToken)
    {
        try
        {
            await connectionService.DisconnectAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResults.Error("active_session", ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> AuthorizeAsync(
        AuthorizeRequest request,
        GitHubConnectionHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await handler.AuthorizeAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return ApiResults.Error("invalid_request", ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApiResults.Error("unauthorized", ex.Message, StatusCodes.Status401Unauthorized);
        }
        catch (InvalidOperationException ex) when (IsGitHubAppNotConfigured(ex))
        {
            return ApiResults.Error(
                "github_app_not_configured",
                "GitHub App is not configured. Configure App credentials in ApiService user secrets.",
                StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool IsGitHubAppNotConfigured(InvalidOperationException ex) =>
        ex.Message.Contains("GitHub App", StringComparison.OrdinalIgnoreCase)
            && (
                ex.Message.Contains("not configured", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("private key file was not found", StringComparison.OrdinalIgnoreCase));
}
